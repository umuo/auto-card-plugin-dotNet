using System;
using System.Drawing;
using System.Windows.Forms;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Autodesk.AutoCAD.EditorInput;

namespace XiaoLiPV
{
    public class SidebarControl : UserControl
    {
        private readonly TabControl _tabs;
        private readonly ListBox _nav;
        private readonly TextBox _log;

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

            _tabs.TabPages.Add(CreateShadowPage());
            _tabs.TabPages.Add(CreateLayoutPage());
            _tabs.TabPages.Add(CreateCablePage());
            _tabs.TabPages.Add(CreateNamePage());
            _tabs.TabPages.Add(CreateBridgePage());
            _tabs.TabPages.Add(CreatePlinePage());
            _tabs.TabPages.Add(CreateTextPage());

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

        private TabPage CreateShadowPage()
        {
            var page = CreatePage("光伏阴影分析");
            var panel = CreateFlowPanel();
            panel.Controls.Add(CreateLabel("屋面类型"));
            panel.Controls.Add(CreateCombo(new[] { "彩钢瓦坡屋面", "混凝土平屋面" }));
            panel.Controls.Add(CreateLabel("命令入口"));
            panel.Controls.Add(CreateActionButton("执行 XL_SHADOW", "XL_SHADOW"));
            panel.Controls.Add(CreateHint("这里先做侧边栏入口。后续再把真实参数表单和算法接进去。"));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreateLayoutPage()
        {
            var page = CreatePage("光伏组件排布");
            var panel = CreateFlowPanel();
            panel.Controls.Add(CreateLabel("排布模式"));
            panel.Controls.Add(CreateCombo(new[] { "常规", "BIPV" }));
            panel.Controls.Add(CreateActionButton("执行 XL_LAYOUT", "XL_LAYOUT"));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreateCablePage()
        {
            var page = CreatePage("组串穿线");
            var panel = CreateFlowPanel();
            panel.Controls.Add(CreateActionButton("执行 XL_CABLE", "XL_CABLE"));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreateNamePage()
        {
            var page = CreatePage("组串命名");
            var panel = CreateFlowPanel();
            panel.Controls.Add(CreateTextInput("前缀", "NB01"));
            panel.Controls.Add(CreateActionButton("执行 XL_NAME", "XL_NAME"));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreateBridgePage()
        {
            var page = CreatePage("桥架统计");
            var panel = CreateFlowPanel();
            panel.Controls.Add(CreateActionButton("执行 XL_BRIDGE", "XL_BRIDGE"));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreatePlinePage()
        {
            var page = CreatePage("多段线统计");
            var panel = CreateFlowPanel();
            panel.Controls.Add(CreateActionButton("执行 XL_PLINE", "XL_PLINE"));
            page.Controls.Add(panel);
            return page;
        }

        private TabPage CreateTextPage()
        {
            var page = CreatePage("文字数字递增");
            var panel = CreateFlowPanel();
            panel.Controls.Add(CreateActionButton("执行 XL_TEXT", "XL_TEXT"));
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
                Height = 60,
                ForeColor = Color.DimGray
            };
        }

        private Control CreateCombo(string[] items)
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

        private Control CreateTextInput(string label, string value)
        {
            var panel = new Panel { Width = 190, Height = 52 };
            var lbl = new Label { Text = label, Left = 0, Top = 0, Width = 180 };
            var tb = new TextBox { Left = 0, Top = 22, Width = 180, Text = value };
            panel.Controls.Add(lbl);
            panel.Controls.Add(tb);
            return panel;
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

            try
            {
                doc.SendStringToExecute(command + " ", true, false, false);
            }
            catch (System.Exception ex)
            {
                var ed = doc.Editor;
                ed.WriteMessage("\n[小栗光伏] 执行失败: " + ex.Message + "\n");
            }
        }
    }
}
