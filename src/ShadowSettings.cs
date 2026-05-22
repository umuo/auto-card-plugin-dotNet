namespace XiaoLiPV
{
    public enum ShadowRoofType
    {
        Slope = 0,
        Flat = 1
    }

    public class ShadowSettings
    {
        public ShadowRoofType RoofType { get; set; } = ShadowRoofType.Slope;
    }
}
