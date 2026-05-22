using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace XiaoLiPV
{
    public static class CableToolService
    {
        public const string RegAppName = "XLPV";

        private sealed class ModuleInfo
        {
            public Point3d Center { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        public static void Run(Document doc, CableSettings settings)
        {
            if (doc == null) return;

            settings = settings ?? new CableSettings();
            var modulesPerString = Math.Max(1, settings.ModulesPerString);
            var ed = doc.Editor;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var cableLayerId = EnsureLayer(tr, db, "0组串穿线", 4);
                var endpointLayerId = EnsureLayer(tr, db, "0正负极端点", 1);
                var modules = CollectModules(btr, tr);

                if (modules.Count == 0)
                {
                    ed.WriteMessage("\n[小栗光伏] 未找到 0组件 图层上的组件矩形/多段线，组串穿线已取消。\n");
                    tr.Commit();
                    return;
                }

                var ordered = OrderModules(modules, settings.RouteMode);
                int stringCount = 0;
                int cableCount = 0;

                for (int i = 0; i < ordered.Count; i += modulesPerString)
                {
                    var take = Math.Min(modulesPerString, ordered.Count - i);
                    if (take <= 0) continue;

                    var stringModules = ordered.GetRange(i, take);
                    if (stringModules.Count >= 2)
                    {
                        CreateCablePolyline(btr, tr, db, stringModules, cableLayerId);
                        cableCount++;
                    }

                    CreateEndpointMarkers(btr, tr, stringModules[0], stringModules[stringModules.Count - 1], endpointLayerId);
                    stringCount++;
                }

                tr.Commit();
                ed.WriteMessage($"\n[小栗光伏] 组串穿线完成，组件 {ordered.Count} 块，组串 {stringCount} 串，连线 {cableCount} 条。\n");
            }
        }

        private static List<ModuleInfo> CollectModules(BlockTableRecord btr, Transaction tr)
        {
            var modules = new List<ModuleInfo>();

            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (!string.Equals(ent.Layer, "0组件", StringComparison.OrdinalIgnoreCase)) continue;
                if (!(ent is Polyline) && !(ent is Polyline2d)) continue;

                Extents3d ext;
                try
                {
                    ext = ent.GeometricExtents;
                }
                catch
                {
                    continue;
                }

                var min = ext.MinPoint;
                var max = ext.MaxPoint;
                modules.Add(new ModuleInfo
                {
                    Center = new Point3d((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, min.Z),
                    Width = Math.Abs(max.X - min.X),
                    Height = Math.Abs(max.Y - min.Y)
                });
            }

            return modules;
        }

        private static List<ModuleInfo> OrderModules(List<ModuleInfo> modules, CableRouteMode routeMode)
        {
            var rowTolerance = GetRowTolerance(modules);
            modules.Sort((a, b) =>
            {
                var yCompare = b.Center.Y.CompareTo(a.Center.Y);
                if (Math.Abs(b.Center.Y - a.Center.Y) > rowTolerance) return yCompare;
                return a.Center.X.CompareTo(b.Center.X);
            });

            if (routeMode == CableRouteMode.OneLine)
            {
                return new List<ModuleInfo>(modules);
            }

            var ordered = new List<ModuleInfo>();
            var rows = new List<List<ModuleInfo>>();

            for (int i = 0; i < modules.Count; i++)
            {
                if (rows.Count == 0 ||
                    Math.Abs(rows[rows.Count - 1][0].Center.Y - modules[i].Center.Y) > rowTolerance)
                {
                    rows.Add(new List<ModuleInfo>());
                }

                rows[rows.Count - 1].Add(modules[i]);
            }

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                rows[rowIndex].Sort((a, b) => a.Center.X.CompareTo(b.Center.X));
                if (rowIndex % 2 == 1)
                {
                    rows[rowIndex].Reverse();
                }

                ordered.AddRange(rows[rowIndex]);
            }

            return ordered;
        }

        private static double GetRowTolerance(IList<ModuleInfo> modules)
        {
            double totalHeight = 0.0;
            int count = 0;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].Height <= 0.0) continue;
                totalHeight += modules[i].Height;
                count++;
            }

            if (count == 0) return 1.0;
            return Math.Max(1.0, totalHeight / count * 0.5);
        }

        private static void CreateCablePolyline(
            BlockTableRecord btr,
            Transaction tr,
            Database db,
            IList<ModuleInfo> modules,
            ObjectId layerId)
        {
            var pl = new Polyline();
            pl.SetDatabaseDefaults();
            pl.LayerId = layerId;

            for (int i = 0; i < modules.Count; i++)
            {
                pl.AddVertexAt(i, new Point2d(modules[i].Center.X, modules[i].Center.Y), 0, 0, 0);
            }

            AttachModuleCountData(tr, db, pl, modules.Count);
            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
        }

        private static void CreateEndpointMarkers(
            BlockTableRecord btr,
            Transaction tr,
            ModuleInfo start,
            ModuleInfo end,
            ObjectId layerId)
        {
            var radius = Math.Max(20.0, Math.Min(GetMarkerBaseSize(start), GetMarkerBaseSize(end)) * 0.12);
            CreateMarker(btr, tr, start.Center, radius, "+", layerId);
            CreateMarker(btr, tr, end.Center, radius, "-", layerId);
        }

        private static void AttachModuleCountData(Transaction tr, Database db, Entity entity, int moduleCount)
        {
            if (entity == null || moduleCount <= 0) return;

            EnsureRegApp(tr, db, RegAppName);
            entity.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
                new TypedValue((int)DxfCode.ExtendedDataInteger32, moduleCount));
        }

        private static void EnsureRegApp(Transaction tr, Database db, string appName)
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (rat.Has(appName))
            {
                return;
            }

            rat.UpgradeOpen();
            var record = new RegAppTableRecord { Name = appName };
            rat.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        private static double GetMarkerBaseSize(ModuleInfo module)
        {
            var width = module.Width > 0.0 ? module.Width : 100.0;
            var height = module.Height > 0.0 ? module.Height : width;
            return Math.Min(width, height);
        }

        private static void CreateMarker(
            BlockTableRecord btr,
            Transaction tr,
            Point3d center,
            double radius,
            string label,
            ObjectId layerId)
        {
            var circle = new Circle(center, Vector3d.ZAxis, radius);
            circle.SetDatabaseDefaults();
            circle.LayerId = layerId;
            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);

            var text = new DBText();
            text.SetDatabaseDefaults();
            text.LayerId = layerId;
            text.TextString = label;
            text.Height = radius * 1.2;
            text.Position = new Point3d(center.X - radius * 0.35, center.Y - radius * 0.45, center.Z);
            btr.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
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
    }
}
