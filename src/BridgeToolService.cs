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
    public static class BridgeToolService
    {
        private sealed class SegmentInfo
        {
            public string Label { get; set; }
            public double Length { get; set; }
        }

        public static void Run(Document doc, BridgeSettings settings)
        {
            if (doc == null) return;

            settings = settings ?? new BridgeSettings();
            var precision = Math.Max(0, settings.DecimalPlaces);
            var ed = doc.Editor;
            var db = doc.Database;

            var opts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择需要统计的桥架线条（直线/多段线）: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LINE,LWPOLYLINE,POLYLINE")
            });

            var sel = ed.GetSelection(opts, filter);
            if (sel.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[小栗光伏] 未选择有效桥架线条，桥架统计已取消。\n");
                return;
            }

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var segments = CollectSegments(tr, sel.Value);
                tr.Commit();

                if (segments.Count == 0)
                {
                    ed.WriteMessage("\n[小栗光伏] 框选范围内未检测到有效桥架线条。\n");
                    ShowSummaryDialog("桥架工程量统计", "未检测到有效桥架线条。\n\n请重新框选直线或多段线对象。");
                    return;
                }

                var summary = BuildSummary(segments, precision);
                ed.WriteMessage($"\n[小栗光伏] 桥架统计完成，有效线条 {segments.Count} 条，总长度 {FormatNumber(GetTotalLength(segments), precision)}。\n");
                ShowSummaryDialog("桥架工程量统计", summary);
            }
        }

        private static List<SegmentInfo> CollectSegments(Transaction tr, SelectionSet selection)
        {
            var segments = new List<SegmentInfo>();
            if (selection == null) return segments;

            int index = 1;
            foreach (SelectedObject so in selection)
            {
                if (so == null) continue;
                var entity = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                if (entity == null) continue;

                if (TryGetLength(entity, out var length))
                {
                    segments.Add(new SegmentInfo
                    {
                        Label = $"第{index}段[{entity.GetType().Name}]",
                        Length = length
                    });
                    index++;
                }
            }

            return segments;
        }

        private static bool TryGetLength(Entity entity, out double length)
        {
            length = 0.0;
            switch (entity)
            {
                case Line line:
                    length = line.Length;
                    return length >= 0.0;
                case Polyline polyline:
                    length = polyline.Length;
                    return length >= 0.0;
                case Polyline2d polyline2d:
                    length = GetPolyline2dLength(polyline2d);
                    return length >= 0.0;
                default:
                    return false;
            }
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

        private static string BuildSummary(IList<SegmentInfo> segments, int precision)
        {
            var sb = new StringBuilder();
            sb.AppendLine("桥架工程量统计结果");
            sb.AppendLine(new string('=', 24));

            for (int i = 0; i < segments.Count; i++)
            {
                var item = segments[i];
                sb.AppendLine($"{item.Label}：{FormatNumber(item.Length, precision)} m");
            }

            sb.AppendLine(new string('-', 24));
            sb.AppendLine($"总条数：{segments.Count}");
            sb.AppendLine($"总长度：{FormatNumber(GetTotalLength(segments), precision)} m");
            return sb.ToString();
        }

        private static double GetTotalLength(IList<SegmentInfo> segments)
        {
            double total = 0.0;
            for (int i = 0; i < segments.Count; i++)
            {
                total += segments[i].Length;
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
