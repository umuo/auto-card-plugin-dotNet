namespace XiaoLiPV
{
    public enum LayoutOrientation
    {
        Horizontal = 0,
        Vertical = 1
    }

    public class LayoutSettings
    {
        public double ModuleWidth { get; set; } = 1134.0;
        public double ModuleHeight { get; set; } = 2278.0;
        public double Gap { get; set; } = 20.0;
        public LayoutOrientation Orientation { get; set; } = LayoutOrientation.Horizontal;
    }
}
