using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace XiaoLiPV
{
    public static class NameToolService
    {
        private sealed class StringInfo
        {
            public Polyline Polyline { get; set; }
            public Point3d Anchor { get; set; }
            public int ModuleCount { get; set; }
        }

        public static void Run(Document doc, NameSettings settings)
        {
            if (doc == null) return;

            settings = settings ?? new NameSettings();
            var prefix = NormalizePrefix(settings.Prefix);
            var ed = doc.Editor;
            var db = doc.Database;

            var opts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择需要命名的组串线（图层需为 0组串穿线 或 0 组串穿线）: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });

            var sel = ed.GetSelection(opts, filter);
            if (sel.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[小栗光伏] 未选择有效组串线，组串命名已取消。\n");
                return;
            }

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var nameLayerId = EnsureLayer(tr, db, "0组串命名", 2);
                var strings = CollectStrings(tr, sel.Value);

                if (strings.Count == 0)
                {
                    ed.WriteMessage("\n[小栗光伏] 所选对象中没有 0组串穿线 图层的有效组串线，未生成命名。\n");
                    tr.Commit();
                    return;
                }

                strings.Sort((a, b) =>
                {
                    var yCompare = b.Anchor.Y.CompareTo(a.Anchor.Y);
                    if (Math.Abs(a.Anchor.Y - b.Anchor.Y) > 1.0) return yCompare;
                    return a.Anchor.X.CompareTo(b.Anchor.X);
                });

                int created = 0;
                for (int i = 0; i < strings.Count; i++)
                {
                    var info = strings[i];
                    var label = $"{prefix}-A{(i + 1):00}({Math.Max(1, info.ModuleCount)})";
                    CreateNameText(btr, tr, info, label, nameLayerId);
                    created++;
                }

                tr.Commit();
                ed.WriteMessage($"\n[小栗光伏] 组串命名完成，处理组串 {strings.Count} 条，生成命名 {created} 个。\n");
            }
        }

        private static string NormalizePrefix(string prefix)
        {
            prefix = (prefix ?? string.Empty).Trim();
            return string.IsNullOrEmpty(prefix) ? "NB01" : prefix;
        }

        private static List<StringInfo> CollectStrings(Transaction tr, SelectionSet selection)
        {
            var strings = new List<StringInfo>();
            if (selection == null) return strings;

            foreach (SelectedObject so in selection)
            {
                if (so == null) continue;
                var pl = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null) continue;
                if (!IsCableLayer(pl.Layer)) continue;
                if (pl.NumberOfVertices < 2) continue;

                var anchor = TryGetAnchorPoint(pl);
                if (!anchor.HasValue) continue;

                strings.Add(new StringInfo
                {
                    Polyline = pl,
                    Anchor = anchor.Value,
                    ModuleCount = GetModuleCount(pl)
                });
            }

            return strings;
        }

        private static bool IsCableLayer(string layerName)
        {
            return string.Equals(layerName, "0组串穿线", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(layerName, "0 组串穿线", StringComparison.OrdinalIgnoreCase);
        }

        private static Point3d? TryGetAnchorPoint(Polyline pl)
        {
            try
            {
                var ext = pl.GeometricExtents;
                return new Point3d((ext.MinPoint.X + ext.MaxPoint.X) / 2.0, (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0, ext.MinPoint.Z);
            }
            catch
            {
                return null;
            }
        }

        private static int GetModuleCount(Polyline pl)
        {
            var xdataCount = TryGetModuleCountFromXData(pl);
            if (xdataCount > 0)
            {
                return xdataCount;
            }

            return Math.Max(1, pl.NumberOfVertices);
        }

        private static int TryGetModuleCountFromXData(Polyline pl)
        {
            ResultBuffer buffer = null;
            try
            {
                buffer = pl.GetXDataForApplication(CableToolService.RegAppName);
                if (buffer == null) return 0;

                foreach (TypedValue value in buffer)
                {
                    if (value.TypeCode != (int)DxfCode.ExtendedDataInteger32) continue;
                    if (value.Value is int intValue && intValue > 0)
                    {
                        return intValue;
                    }

                    if (value.Value != null && int.TryParse(value.Value.ToString(), out var parsed) && parsed > 0)
                    {
                        return parsed;
                    }
                }
            }
            catch
            {
                return 0;
            }
            finally
            {
                buffer?.Dispose();
            }

            return 0;
        }

        private static void CreateNameText(
            BlockTableRecord btr,
            Transaction tr,
            StringInfo info,
            string label,
            ObjectId layerId)
        {
            var height = GetTextHeight(info.Polyline);
            var text = new DBText
            {
                TextString = label,
                Height = height,
                Position = new Point3d(info.Anchor.X, info.Anchor.Y + height, info.Anchor.Z),
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = new Point3d(info.Anchor.X, info.Anchor.Y + height, info.Anchor.Z),
                LayerId = layerId
            };
            text.SetDatabaseDefaults();

            btr.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
        }

        private static double GetTextHeight(Polyline pl)
        {
            try
            {
                var ext = pl.GeometricExtents;
                var width = Math.Abs(ext.MaxPoint.X - ext.MinPoint.X);
                var height = Math.Abs(ext.MaxPoint.Y - ext.MinPoint.Y);
                var baseSize = Math.Max(width, height);
                return Math.Max(120.0, baseSize * 0.25);
            }
            catch
            {
                return 120.0;
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
    }
}
