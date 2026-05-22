using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace XiaoLiPV
{
    public static class PlineToolService
    {
        private sealed class PlineInfo
        {
            public string Label { get; set; }
            public double Length { get; set; }
            public bool IsClosed { get; set; }
            public double Area { get; set; }
        }

        public static void Run(Document doc, PlineSettings settings)
        {
            if (doc == null) return;

            settings = settings ?? new PlineSettings();
            var lengthPrecision = Math.Max(0, settings.LengthDecimalPlaces);
            var areaPrecision = Math.Max(0, settings.AreaDecimalPlaces);
            var ed = doc.Editor;
            var db = doc.Database;

            var opts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择需要统计的多段线: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE")
            });

            var sel = ed.GetSelection(opts, filter);
            if (sel.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[小栗光伏] 未选择有效多段线，多段线统计已取消。\n");
                return;
            }

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var items = CollectPolylines(tr, sel.Value);
                tr.Commit();

                if (items.Count == 0)
                {
                    ed.WriteMessage("\n[小栗光伏] 框选范围内未检测到有效多段线。\n");
                    ShowSummaryDialog("多段线统计", "未检测到有效多段线。\n\n请重新框选多段线对象。");
                    return;
                }

                var summary = BuildSummary(items, lengthPrecision, areaPrecision);
                ed.WriteMessage($"\n[小栗光伏] 多段线统计完成，总条数 {items.Count}，总长度 {FormatNumber(GetTotalLength(items), lengthPrecision)}，总面积 {FormatNumber(GetTotalArea(items), areaPrecision)}。\n");
                ShowSummaryDialog("多段线统计", summary);
            }
        }

        private static List<PlineInfo> CollectPolylines(Transaction tr, SelectionSet selection)
        {
            var items = new List<PlineInfo>();
            if (selection == null) return items;

            int index = 1;
            foreach (SelectedObject so in selection)
            {
                if (so == null) continue;
                var entity = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                if (entity == null) continue;

                if (TryGetPolylineInfo(entity, out var info))
                {
                    info.Label = $"第{index}条[{entity.GetType().Name}]";
                    items.Add(info);
                    index++;
                }
            }

            return items;
        }

        private static bool TryGetPolylineInfo(Entity entity, out PlineInfo info)
        {
            info = null;
            switch (entity)
            {
                case Polyline polyline:
                    var polylineArea = 0.0;
                    if (polyline.Closed)
                    {
                        try
                        {
                            polylineArea = SafeGetArea(polyline.Area);
                        }
                        catch
                        {
                            polylineArea = 0.0;
                        }
                    }

                    info = new PlineInfo
                    {
                        Length = polyline.Length,
                        IsClosed = polyline.Closed,
                        Area = polylineArea
                    };
                    return true;
                case Polyline2d polyline2d:
                    var length = GetPolyline2dLength(polyline2d);
                    var isClosed = polyline2d.Closed;
                    info = new PlineInfo
                    {
                        Length = length,
                        IsClosed = isClosed,
                        Area = isClosed ? GetPolyline2dArea(polyline2d) : 0.0
                    };
                    return true;
                default:
                    return false;
            }
        }

        private static double SafeGetArea(double area)
        {
            return double.IsNaN(area) || double.IsInfinity(area) ? 0.0 : Math.Abs(area);
        }

        private static double GetPolyline2dLength(Polyline2d polyline)
        {
            try
            {
                double total = 0.0;
                Point3d? previous = null;
                Point3d? first = null;

                foreach (ObjectId vertexId in polyline)
                {
                    if (vertexId.IsNull) continue;
                    var vertex = polyline.Database.TransactionManager.TopTransaction.GetObject(vertexId, OpenMode.ForRead) as Vertex2d;
                    if (vertex == null) continue;

                    var point = vertex.Position;
                    if (!first.HasValue) first = point;
                    if (previous.HasValue)
                    {
                        total += previous.Value.DistanceTo(point);
                    }

                    previous = point;
                }

                if (polyline.Closed && previous.HasValue && first.HasValue)
                {
                    total += previous.Value.DistanceTo(first.Value);
                }

                return total;
            }
            catch
            {
                return 0.0;
            }
        }

        private static double GetPolyline2dArea(Polyline2d polyline)
        {
            try
            {
                var points = new List<Point2d>();
                foreach (ObjectId vertexId in polyline)
                {
                    if (vertexId.IsNull) continue;
                    var vertex = polyline.Database.TransactionManager.TopTransaction.GetObject(vertexId, OpenMode.ForRead) as Vertex2d;
                    if (vertex == null) continue;
                    points.Add(new Point2d(vertex.Position.X, vertex.Position.Y));
                }

                if (points.Count < 3) return 0.0;

                double area = 0.0;
                for (int i = 0; i < points.Count; i++)
                {
                    var current = points[i];
                    var next = points[(i + 1) % points.Count];
                    area += current.X * next.Y - next.X * current.Y;
                }

                return Math.Abs(area) * 0.5;
            }
            catch
            {
                return 0.0;
            }
        }

        private static string BuildSummary(IList<PlineInfo> items, int lengthPrecision, int areaPrecision)
        {
            var sb = new StringBuilder();
            sb.AppendLine("多段线统计结果");
            sb.AppendLine(new string('=', 24));

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var closedText = item.IsClosed ? "闭合" : "未闭合";
                var areaText = item.IsClosed
                    ? $"，面积 {FormatNumber(item.Area, areaPrecision)} ㎡"
                    : string.Empty;
                sb.AppendLine($"{item.Label}：长度 {FormatNumber(item.Length, lengthPrecision)} m，{closedText}{areaText}");
            }

            sb.AppendLine(new string('-', 24));
            sb.AppendLine($"总条数：{items.Count}");
            sb.AppendLine($"总长度：{FormatNumber(GetTotalLength(items), lengthPrecision)} m");
            sb.AppendLine($"闭合多段线总面积：{FormatNumber(GetTotalArea(items), areaPrecision)} ㎡");
            return sb.ToString();
        }

        private static double GetTotalLength(IList<PlineInfo> items)
        {
            double total = 0.0;
            for (int i = 0; i < items.Count; i++)
            {
                total += items[i].Length;
            }

            return total;
        }

        private static double GetTotalArea(IList<PlineInfo> items)
        {
            double total = 0.0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].IsClosed)
                {
                    total += items[i].Area;
                }
            }

            return total;
        }

        private static string FormatNumber(double value, int precision)
        {
            return Math.Round(value, precision, MidpointRounding.AwayFromZero).ToString($"F{precision}");
        }

        private static void ShowSummaryDialog(string title, string content)
        {
            try
            {
                Clipboard.SetText(content);
            }
            catch
            {
                // Clipboard may be unavailable in some host contexts; ignore copy failures.
            }

            MessageBox.Show(
                content + "\n\n结果已尝试复制到剪贴板。",
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1,
                MessageBoxOptions.DefaultDesktopOnly);
        }
    }
}
