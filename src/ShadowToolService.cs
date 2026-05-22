using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace XiaoLiPV
{
    public static class ShadowToolService
    {
        public static void Run(Document doc, ShadowSettings settings)
        {
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var opts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择需要生成阴影的障碍物轮廓（支持多段线）: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE")
            });

            var sel = ed.GetSelection(opts, filter);
            if (sel.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[小栗光伏] 未选择有效障碍物，阴影分析已取消。\n");
                return;
            }

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                var winterLayerId = EnsureLayer(tr, db, "0阴影", 1);
                var summerLayerId = EnsureLayer(tr, db, "0夏至阴影", 2);

                var winterShift = settings.RoofType == ShadowRoofType.Slope
                    ? new Vector2d(1200.0, 800.0)
                    : new Vector2d(900.0, 500.0);
                var summerShift = settings.RoofType == ShadowRoofType.Slope
                    ? new Vector2d(650.0, 350.0)
                    : new Vector2d(420.0, 220.0);

                int count = 0;
                foreach (SelectedObject so in sel.Value)
                {
                    if (so == null) continue;
                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    Extents3d ext;
                    try
                    {
                        ext = ent.GeometricExtents;
                    }
                    catch
                    {
                        continue;
                    }

                    CreateShadowRect(btr, tr, ext, winterShift, winterLayerId);
                    CreateShadowRect(btr, tr, ext, summerShift, summerLayerId);
                    count++;
                }

                tr.Commit();
                ed.WriteMessage($"\n[小栗光伏] 阴影分析完成，已为 {count} 个障碍物生成冬至/夏至阴影轮廓。\n");
            }
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

        private static void CreateShadowRect(BlockTableRecord btr, Transaction tr, Extents3d ext, Vector2d shift, ObjectId layerId)
        {
            var min = ext.MinPoint;
            var max = ext.MaxPoint;

            var pl = new Polyline();
            pl.SetDatabaseDefaults();
            pl.LayerId = layerId;

            pl.AddVertexAt(0, new Point2d(min.X + shift.X, min.Y + shift.Y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(max.X + shift.X, min.Y + shift.Y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(max.X + shift.X, max.Y + shift.Y), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(min.X + shift.X, max.Y + shift.Y), 0, 0, 0);
            pl.Closed = true;

            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
        }
    }
}
