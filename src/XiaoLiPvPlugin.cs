using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

namespace XiaoLiPV
{
    public class XiaoLiPvPlugin : IExtensionApplication
    {
        private static PaletteSet _palette;
        private static SidebarControl _control;

        public void Initialize()
        {
            Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                "\n[小栗光伏] DLL 已加载。命令：XLPV、XL_SHADOW、XL_LAYOUT、XL_CABLE、XL_NAME、XL_BRIDGE、XL_PLINE、XL_TEXT\n");
        }

        public void Terminate()
        {
        }

        [CommandMethod("XLPV")]
        [CommandMethod("XL_PANEL")]
        public void ShowSidebar()
        {
            EnsurePalette();
            _palette.Visible = true;
            _palette.Activate(0);
        }

        [CommandMethod("XL_SHADOW")]
        public void RunShadowTool()
        {
            EnsurePalette();
            _palette.Visible = true;
            _control.SelectTab(ToolTab.Shadow);
            var settings = _control.GetShadowSettings();
            ShadowToolService.Run(Application.DocumentManager.MdiActiveDocument, settings);
        }

        [CommandMethod("XL_LAYOUT")]
        public void ShowLayoutTool() => ShowSidebarTab(ToolTab.Layout);

        [CommandMethod("XL_CABLE")]
        public void ShowCableTool() => ShowSidebarTab(ToolTab.Cable);

        [CommandMethod("XL_NAME")]
        public void ShowNameTool() => ShowSidebarTab(ToolTab.Name);

        [CommandMethod("XL_BRIDGE")]
        public void ShowBridgeTool() => ShowSidebarTab(ToolTab.Bridge);

        [CommandMethod("XL_PLINE")]
        public void ShowPolylineTool() => ShowSidebarTab(ToolTab.Pline);

        [CommandMethod("XL_TEXT")]
        public void ShowTextTool() => ShowSidebarTab(ToolTab.Text);

        private static void ShowSidebarTab(ToolTab tab)
        {
            EnsurePalette();
            _palette.Visible = true;
            _control.SelectTab(tab);
        }

        private static void EnsurePalette()
        {
            if (_palette != null) return;

            _control = new SidebarControl();
            _palette = new PaletteSet("小栗光伏")
            {
                Style = PaletteSetStyles.NameEditable |
                        PaletteSetStyles.ShowAutoHideButton |
                        PaletteSetStyles.ShowCloseButton |
                        PaletteSetStyles.Snappable,
                MinimumSize = new System.Drawing.Size(240, 500),
                Size = new System.Drawing.Size(300, 640),
                DockEnabled = DockSides.Left | DockSides.Right
            };

            _palette.Add("功能面板", _control);
            _palette.Visible = true;
        }
    }
}
