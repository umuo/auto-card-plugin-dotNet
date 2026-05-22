namespace XiaoLiPV
{
    public enum CableRouteMode
    {
        UShape = 0,
        OneLine = 1
    }

    public class CableSettings
    {
        public int ModulesPerString { get; set; } = 20;
        public CableRouteMode RouteMode { get; set; } = CableRouteMode.UShape;
    }
}
