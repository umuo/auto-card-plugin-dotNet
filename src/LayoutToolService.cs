using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace XiaoLiPV
{
    public static class LayoutToolService
    {
        public static void Run(Document doc, LayoutSettings settings)
        {
            if (doc == null) return;

            settings = settings ?? new LayoutSettings();
            var moduleWidth = Math.Abs(settings.ModuleWidth);
            var moduleHeight = Math.Abs(settings.ModuleHeight);
            var gap = Math.Max(0.0, settings.Gap);

            if (moduleWidth <= 0.0 || moduleHeight <= 0.0)
            {
                doc.Editor.WriteMessage("\n[小栗光伏] 组件宽度和高度必须大于 0，组件排布已取消。\n");
                return;
            }

            if (settings.Orientation == LayoutOrientation.Vertical)
            {
                var temp = moduleWidth;
                moduleWidth = moduleHeight;
                moduleHeight = temp;
            }

            var ed = doc.Editor;
            var db = doc.Database;

            var opts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择边界多段线（图层需为 0层 或 0 层）: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE")
            });

            var sel = ed.GetSelection(opts, filter);
            if (sel.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[小栗光伏] 未选择有效边界，组件排布已取消。\n");
                return;
            }

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var moduleLayerId = EnsureLayer(tr, db, "0组件", 3);
                var obstacles = CollectObstacleExtents(btr, tr);

                int boundaryCount = 0;
                int moduleCount = 0;

                foreach (SelectedObject so in sel.Value)
                {
                    if (so == null) continue;
                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent == null || !IsBoundaryLayer(ent.Layer)) continue;

                    var vertices = GetPolylineVertices(ent, tr);
                    if (vertices.Count < 3) continue;

                    Extents3d ext;
                    try
                    {
                        ext = ent.GeometricExtents;
                    }
                    catch
                    {
                        continue;
                    }

                    boundaryCount++;
                    moduleCount += CreateModulesInBoundary(btr, tr, ext, vertices, obstacles, moduleWidth, moduleHeight, gap, moduleLayerId);
                }

                tr.Commit();
                ed.WriteMessage($"\n[小栗光伏] 组件排布完成，处理边界 {boundaryCount} 个，生成组件 {moduleCount} 个。\n");
            }
        }

        private static bool IsBoundaryLayer(string layerName)
        {
            return string.Equals(layerName, "0层", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(layerName, "0 层", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsObstacleLayer(string layerName)
        {
            return string.Equals(layerName, "0阴影", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(layerName, "0 阴影", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsObstacleCandidateLayer(string layerName)
        {
            return IsObstacleLayer(layerName) || !IsBoundaryLayer(layerName);
        }

        private static ObjectId EnsureLayer(Transaction tr, Database db, string layerName, short colorIndex)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName))
            {
                return lt[layerName];
            }

            lt.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name = layerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
            };
            var id = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return id;
        }

        private static int CreateModulesInBoundary(
            BlockTableRecord btr,
            Transaction tr,
            Extents3d ext,
            IList<Point2d> boundary,
            IList<Extents3d> obstacles,
            double moduleWidth,
            double moduleHeight,
            double gap,
            ObjectId layerId)
        {
            var min = ext.MinPoint;
            var max = ext.MaxPoint;
            var stepX = moduleWidth + gap;
            var stepY = moduleHeight + gap;
            int count = 0;

            for (double y = min.Y; y + moduleHeight <= max.Y + Tolerance.Global.EqualPoint; y += stepY)
            {
                for (double x = min.X; x + moduleWidth <= max.X + Tolerance.Global.EqualPoint; x += stepX)
                {
                    var moduleExt = new Extents3d(
                        new Point3d(x, y, min.Z),
                        new Point3d(x + moduleWidth, y + moduleHeight, max.Z));

                    if (!AreModuleCornersInsideBoundary(x, y, moduleWidth, moduleHeight, boundary)) continue;
                    if (OverlapsAnyObstacle(moduleExt, obstacles)) continue;

                    CreateModuleRect(btr, tr, x, y, moduleWidth, moduleHeight, layerId);
                    count++;
                }
            }

            return count;
        }

        private static List<Extents3d> CollectObstacleExtents(BlockTableRecord btr, Transaction tr)
        {
            var obstacles = new List<Extents3d>();

            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (!IsObstacleCandidateLayer(ent.Layer)) continue;
                if (!IsClosedPolyline(ent)) continue;

                try
                {
                    obstacles.Add(ent.GeometricExtents);
                }
                catch
                {
                    // Some entities do not report extents until regenerated; ignore them for layout.
                }
            }

            return obstacles;
        }

        private static bool IsClosedPolyline(Entity ent)
        {
            var pl = ent as Polyline;
            if (pl != null) return pl.Closed;

            var pl2d = ent as Polyline2d;
            return pl2d != null && pl2d.Closed;
        }

        private static List<Point2d> GetPolylineVertices(Entity ent, Transaction tr)
        {
            var vertices = new List<Point2d>();

            var pl = ent as Polyline;
            if (pl != null)
            {
                for (int i = 0; i < pl.NumberOfVertices; i++)
                {
                    vertices.Add(pl.GetPoint2dAt(i));
                }

                return vertices;
            }

            var pl2d = ent as Polyline2d;
            if (pl2d != null)
            {
                foreach (ObjectId vertexId in pl2d)
                {
                    var vertex = tr.GetObject(vertexId, OpenMode.ForRead) as Vertex2d;
                    if (vertex != null)
                    {
                        vertices.Add(new Point2d(vertex.Position.X, vertex.Position.Y));
                    }
                }
            }

            return vertices;
        }

        private static bool AreModuleCornersInsideBoundary(
            double x,
            double y,
            double width,
            double height,
            IList<Point2d> boundary)
        {
            return IsPointInsideOrOnBoundary(new Point2d(x, y), boundary) &&
                   IsPointInsideOrOnBoundary(new Point2d(x + width, y), boundary) &&
                   IsPointInsideOrOnBoundary(new Point2d(x + width, y + height), boundary) &&
                   IsPointInsideOrOnBoundary(new Point2d(x, y + height), boundary);
        }

        private static bool IsPointInsideOrOnBoundary(Point2d point, IList<Point2d> polygon)
        {
            bool inside = false;
            int count = polygon.Count;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];

                if (IsPointOnSegment(point, a, b)) return true;

                bool crosses = ((a.Y > point.Y) != (b.Y > point.Y)) &&
                               (point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X);
                if (crosses) inside = !inside;
            }

            return inside;
        }

        private static bool IsPointOnSegment(Point2d point, Point2d a, Point2d b)
        {
            const double tolerance = 1e-8;
            var lengthSquared = (b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y);
            if (lengthSquared <= tolerance)
            {
                var dx = point.X - a.X;
                var dy = point.Y - a.Y;
                return dx * dx + dy * dy <= tolerance;
            }

            var cross = (point.Y - a.Y) * (b.X - a.X) - (point.X - a.X) * (b.Y - a.Y);
            if (Math.Abs(cross) > tolerance) return false;

            var dot = (point.X - a.X) * (b.X - a.X) + (point.Y - a.Y) * (b.Y - a.Y);
            if (dot < -tolerance) return false;

            return dot <= lengthSquared + tolerance;
        }

        private static bool OverlapsAnyObstacle(Extents3d candidate, IList<Extents3d> obstacles)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                if (ExtentsOverlap(candidate, obstacles[i])) return true;
            }

            return false;
        }

        private static bool ExtentsOverlap(Extents3d a, Extents3d b)
        {
            return a.MinPoint.X < b.MaxPoint.X &&
                   a.MaxPoint.X > b.MinPoint.X &&
                   a.MinPoint.Y < b.MaxPoint.Y &&
                   a.MaxPoint.Y > b.MinPoint.Y;
        }

        private static void CreateModuleRect(
            BlockTableRecord btr,
            Transaction tr,
            double x,
            double y,
            double width,
            double height,
            ObjectId layerId)
        {
            var pl = new Polyline();
            pl.SetDatabaseDefaults();
            pl.LayerId = layerId;

            pl.AddVertexAt(0, new Point2d(x, y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(x + width, y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(x + width, y + height), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(x, y + height), 0, 0, 0);
            pl.Closed = true;

            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
        }
    }
}
