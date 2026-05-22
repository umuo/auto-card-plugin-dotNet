using System;
using System.Globalization;
using System.Drawing;
using System.Windows.Forms;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace XiaoLiPV
{
    public class SidebarControl : UserControl
    {
        private readonly TabControl _tabs;
        private readonly ListBox _nav;
        private readonly ComboBox _shadowRoofType;
        private readonly TextBox _layoutModuleWidth;
        private readonly TextBox _layoutModuleHeight;
        private readonly TextBox _layoutGap;
        private readonly ComboBox _layoutOrientation;

        public SidebarControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _nav = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Microsoft YaHei UI", 9F),
                IntegralHeight = false
            };
            _nav.Items.AddRange(new object[]
            {
                "阴影分析", "组件排布", "组串穿线", "组串命名", "桥架统计", "多段线统计", "文字递增"
            });
            _nav.SelectedIndexChanged += (_, __) => _tabs.SelectedIndex = Math.Max(0, _nav.SelectedIndex);

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Appearance = TabAppearance.FlatButtons,
                ItemSize = new Size(0, 1),
                SizeMode = TabSizeMode.Fixed
            };

            _tabs.TabPages.Add(CreateShadowPage(out _shadowRoofType));
            _tabs.TabPages.Add(CreateLayoutPage(
                out _layoutModuleWidth,
                out _layoutModuleHeight,
                out _layoutGap,
                out _layoutOrientation));
            _tabs.TabPages.Add(CreateSimplePage("组串穿线", "执行 XL_CABLE", "XL_CABLE"));
            _tabs.TabPages.Add(CreateSimplePage("组串命名", "执行 XL_NAME", "XL_NAME"));
            _tabs.TabPages.Add(CreateSimplePage("桥架统计", "执行 XL_BRIDGE", "XL_BRIDGE"));
            _tabs.TabPages.Add(CreateSimplePage("多段线统计", "执行 XL_PLINE", "XL_PLINE"));
            _tabs.TabPages.Add(CreateSimplePage("文字数字递增", "执行 XL_TEXT", "XL_TEXT"));

            root.Controls.Add(_nav, 0, 0);
            root.Controls.Add(_tabs, 1, 0);
            Controls.Add(root);

            _nav.SelectedIndex = 0;
        }

        public void SelectTab(ToolTab tab)
        {
            var idx = (int)tab;
            if (idx >= 0 && idx < _tabs.TabPages.Count)
            {
                _tabs.SelectedIndex = idx;
                _nav.SelectedIndex = idx;
            }
        }

        public ShadowSettings GetShadowSettings()
        {
            return new ShadowSettings
            {
                RoofType = _shadowRoofType.SelectedIndex == 1 ? ShadowRoofType.Flat : ShadowRoofType.Slope
            };
        }

        public LayoutSettings GetLayoutSettings()
        {
            return new LayoutSettings
            {
                ModuleWidth = ReadDouble(_layoutModuleWidth, 1134.0),
                ModuleHeight = ReadDouble(_layoutModuleHeight, 2278.0),
                Gap = ReadDouble(_layoutGap, 20.0),
                Orientation = _layoutOrientation.SelectedIndex == 1
                    ? LayoutOrientation.Vertical
                    : LayoutOrientation.Horizontal
            };
        }

        private TabPage CreateShadowPage(out ComboBox roofType)
        {
            var page = CreatePage("光伏阴影分析");
            var panel = CreateFlowPanel();
            panel.Controls.Add(CreateLabel("屋面类型"));
            roofType = CreateCombo(new[] { "彩钢瓦坡屋面", "混凝土平屋面" });
            panel.Controls.Add(roofType);
            panel.Controls.Add(CreateActionButton("执行 XL_SHADOW", "XL_SHADOW"));
            panel.Controls.Add(CreateHint("当前已实现第一版真实执行：选择障碍物后，会在 CAD 中生成冬至/夏至阴影示意轮廓。"));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreateLayoutPage(
            out TextBox moduleWidth,
            out TextBox moduleHeight,
            out TextBox gap,
            out ComboBox orientation)
        {
            var page = CreatePage("光伏组件排布");
            var panel = CreateFlowPanel();

            panel.Controls.Add(CreateLabel("组件宽度"));
            moduleWidth = CreateTextBox("1134");
            panel.Controls.Add(moduleWidth);

            panel.Controls.Add(CreateLabel("组件高度"));
            moduleHeight = CreateTextBox("2278");
            panel.Controls.Add(moduleHeight);

            panel.Controls.Add(CreateLabel("组件间距"));
            gap = CreateTextBox("20");
            panel.Controls.Add(gap);

            panel.Controls.Add(CreateLabel("排布方向"));
            orientation = CreateCombo(new[] { "横向", "竖向" });
            panel.Controls.Add(orientation);

            panel.Controls.Add(CreateActionButton("执行 XL_LAYOUT", "XL_LAYOUT"));
            panel.Controls.Add(CreateHint("选择 0层 或 0 层上的边界多段线后，在边界外包范围内生成 0组件 矩形组件。"));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreateSimplePage(string title, string buttonText, string command)
        {
            var page = CreatePage(title);
            var panel = CreateFlowPanel();
            panel.Controls.Add(CreateActionButton(buttonText, command));
            panel.Controls.Add(CreateHint("该功能页目前还是占位骨架，后续继续补真实逻辑。"));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreatePage(string title)
        {
            return new TabPage(title)
            {
                BackColor = Color.White,
                Padding = new Padding(10)
            };
        }

        private FlowLayoutPanel CreateFlowPanel()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true
            };
        }

        private Control CreateLabel(string text)
        {
            return new Label
            {
                Text = text,
                Width = 160,
                Height = 22,
                Margin = new Padding(3, 6, 3, 0),
                Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold)
            };
        }

        private Control CreateHint(string text)
        {
            return new Label
            {
                Text = text,
                Width = 180,
                Height = 70,
                ForeColor = Color.DimGray
            };
        }

        private ComboBox CreateCombo(string[] items)
        {
            var box = new ComboBox
            {
                Width = 180,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            box.Items.AddRange(items);
            if (box.Items.Count > 0) box.SelectedIndex = 0;
            return box;
        }

        private TextBox CreateTextBox(string text)
        {
            return new TextBox
            {
                Text = text,
                Width = 180
            };
        }

        private static double ReadDouble(TextBox box, double fallback)
        {
            double value;
            if (box != null &&
                double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            if (box != null &&
                double.TryParse(box.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            return fallback;
        }

        private Control CreateActionButton(string caption, string command)
        {
            var btn = new Button
            {
                Text = caption,
                Width = 180,
                Height = 32,
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btn.Click += (_, __) => ExecuteCommand(command);
            return btn;
        }

        private void ExecuteCommand(string command)
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            doc.SendStringToExecute(command + " ", true, false, false);
        }
    }
}
