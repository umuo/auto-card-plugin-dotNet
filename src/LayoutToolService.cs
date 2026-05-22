using System;
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

                int boundaryCount = 0;
                int moduleCount = 0;

                foreach (SelectedObject so in sel.Value)
                {
                    if (so == null) continue;
                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent == null || !IsBoundaryLayer(ent.Layer)) continue;

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
                    moduleCount += CreateModulesInExtents(btr, tr, ext, moduleWidth, moduleHeight, gap, moduleLayerId);
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

        private static int CreateModulesInExtents(
            BlockTableRecord btr,
            Transaction tr,
            Extents3d ext,
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
                    CreateModuleRect(btr, tr, x, y, moduleWidth, moduleHeight, layerId);
                    count++;
                }
            }

            return count;
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
