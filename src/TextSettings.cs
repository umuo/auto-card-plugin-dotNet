namespace XiaoLiPV
{
    public enum TextIncrementMode
    {
        Single = 0,
        Batch = 1
    }

    public class TextSettings
    {
        public int Step { get; set; } = 1;
        public TextIncrementMode Mode { get; set; } = TextIncrementMode.Batch;
    }
}
