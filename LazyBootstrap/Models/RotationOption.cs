namespace LazyBootstrap.Models
{
    public sealed class RotationOption
    {
        public RotationOption(int angle)
        {
            Angle = angle;
            DisplayName = GetDisplayName(angle);
        }

        public int Angle { get; }

        public string DisplayName { get; }

        public static string GetDisplayName(int angle)
        {
            int normalizedAngle = ((angle % 360) + 360) % 360;
            return normalizedAngle switch
            {
                0 => "横向",
                90 => "纵向",
                180 => "横向（翻转）",
                270 => "纵向（翻转）",
                _ => $"{normalizedAngle}°"
            };
        }

        public override string ToString() => DisplayName;
    }
}
