using LazyBootstrap.Services;

namespace LazyBootstrap.Models
{
    public sealed class DisplayChoiceOption
    {
        internal DisplayChoiceOption(DisplayInfo info, string displayName)
        {
            Info = info;
            DisplayName = displayName ?? string.Empty;
        }

        internal DisplayInfo Info { get; }

        public string DisplayName { get; }

        public override string ToString() => DisplayName;
    }
}
