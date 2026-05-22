using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace XiaoLiPV
{
    public static class TextToolService
    {
        private static readonly Regex TrailingNumberRegex = new Regex(@"^(.*?)(\d+)(\s*)$", RegexOptions.Compiled);

        private sealed class TextTarget
        {
            public ObjectId Id { get; set; }
            public string OriginalText { get; set; }
            public string Prefix { get; set; }
            public string NumberText { get; set; }
            public string Suffix { get; set; }
            public double SortX { get; set; }
            public double SortY { get; set; }
        }

        public static void Run(Document doc, TextSettings settings)
        {
            if (doc == null) return;

            settings = settings ?? new TextSettings();
            var ed = doc.Editor;
            var db = doc.Database;

            var opts = new PromptSelectionOptions
            {
                MessageForAdding = settings.Mode == TextIncrementMode.Single
                    ? "\n请选择需要递增的单个文字对象: "
                    : "\n请选择需要批量递增的文字对象: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "TEXT,MTEXT,ATTRIB")
            });

            ObjectId[] selectedIds;
            if (settings.Mode == TextIncrementMode.Single)
            {
                var entityOptions = new PromptEntityOptions("\n请选择需要递增的单个文字对象: ");
                entityOptions.SetRejectMessage("\n请选择文字对象。");
                entityOptions.AddAllowedClass(typeof(DBText), true);
                entityOptions.AddAllowedClass(typeof(MText), true);
                entityOptions.AddAllowedClass(typeof(AttributeReference), true);

                var entityResult = ed.GetEntity(entityOptions);
                if (entityResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n[小栗光伏] 未选择有效文字对象，文字递增已取消。\n");
                    return;
                }

                selectedIds = new[] { entityResult.ObjectId };
            }
            else
            {
                var sel = ed.GetSelection(opts, filter);
                if (sel.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n[小栗光伏] 未选择有效文字对象，文字递增已取消。\n");
                    return;
                }

                selectedIds = sel.Value.GetObjectIds();
            }

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var targets = CollectTargets(tr, selectedIds);
                if (targets.Count == 0)
                {
                    ed.WriteMessage("\n[小栗光伏] 选中对象中没有可递增的尾部数字文字。\n");
                    tr.Commit();
                    return;
                }

                targets.Sort((a, b) =>
                {
                    var yCompare = b.SortY.CompareTo(a.SortY);
                    if (Math.Abs(a.SortY - b.SortY) > 1.0) return yCompare;
                    return a.SortX.CompareTo(b.SortX);
                });

                int step = settings.Step;
                int changed = 0;
                int currentValue = 0;
                bool hasCurrent = false;

                for (int i = 0; i < targets.Count; i++)
                {
                    var target = targets[i];
                    if (!int.TryParse(target.NumberText, out var parsed))
                    {
                        continue;
                    }

                    int nextValue;
                    if (settings.Mode == TextIncrementMode.Single)
                    {
                        nextValue = parsed + step;
                    }
                    else
                    {
                        if (!hasCurrent)
                        {
                            currentValue = parsed + step;
                            hasCurrent = true;
                        }
                        else
                        {
                            currentValue += step;
                        }

                        nextValue = currentValue;
                    }

                    var newNumber = FormatNumber(nextValue, target.NumberText.Length);
                    var updatedText = target.Prefix + newNumber + target.Suffix;
                    if (TrySetText(tr, target.Id, updatedText))
                    {
                        changed++;
                    }
                }

                tr.Commit();
                ed.WriteMessage($"\n[小栗光伏] 文字递增完成，共更新 {changed} 个文字对象。\n");
            }
        }

        private static List<TextTarget> CollectTargets(Transaction tr, ObjectId[] objectIds)
        {
            var targets = new List<TextTarget>();
            if (objectIds == null || objectIds.Length == 0) return targets;

            for (int index = 0; index < objectIds.Length; index++)
            {
                var id = objectIds[index];
                if (id.IsNull) continue;
                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null) continue;

                if (!TryGetText(entity, out var text)) continue;
                if (string.IsNullOrWhiteSpace(text)) continue;

                var match = TrailingNumberRegex.Match(text);
                if (!match.Success) continue;

                if (!TryGetSortPoint(entity, out var sortX, out var sortY))
                {
                    sortX = 0.0;
                    sortY = 0.0;
                }

                targets.Add(new TextTarget
                {
                    Id = id,
                    OriginalText = text,
                    Prefix = match.Groups[1].Value,
                    NumberText = match.Groups[2].Value,
                    Suffix = match.Groups[3].Value,
                    SortX = sortX,
                    SortY = sortY
                });
            }

            return targets;
        }

        private static bool TryGetText(Entity entity, out string text)
        {
            text = null;
            switch (entity)
            {
                case DBText dbText:
                    text = dbText.TextString;
                    return true;
                case MText mText:
                    text = NormalizeMTextText(mText.Contents);
                    return true;
                case AttributeReference attrib:
                    text = attrib.TextString;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TrySetText(Transaction tr, ObjectId id, string text)
        {
            var entity = tr.GetObject(id, OpenMode.ForWrite) as Entity;
            if (entity == null) return false;

            switch (entity)
            {
                case DBText dbText:
                    dbText.TextString = text;
                    return true;
                case MText mText:
                    mText.Contents = text;
                    return true;
                case AttributeReference attrib:
                    attrib.TextString = text;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetSortPoint(Entity entity, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            try
            {
                switch (entity)
                {
                    case DBText dbText:
                        x = dbText.Position.X;
                        y = dbText.Position.Y;
                        return true;
                    case MText mText:
                        x = mText.Location.X;
                        y = mText.Location.Y;
                        return true;
                    case AttributeReference attrib:
                        x = attrib.Position.X;
                        y = attrib.Position.Y;
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeMTextText(string contents)
        {
            if (string.IsNullOrEmpty(contents)) return contents;

            return contents
                .Replace("\\P", " ")
                .Replace("\\~", " ")
                .Replace("{", string.Empty)
                .Replace("}", string.Empty);
        }

        private static string FormatNumber(int value, int width)
        {
            if (width <= 1)
            {
                return value.ToString();
            }

            var absValue = Math.Abs(value).ToString().PadLeft(width, '0');
            return value < 0 ? "-" + absValue : absValue;
        }
    }
}
