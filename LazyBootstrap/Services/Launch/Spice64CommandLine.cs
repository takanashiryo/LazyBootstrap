namespace LazyBootstrap.Services.Launch
{
    internal static class Spice64CommandLine
    {
        private const string LazyCfgRelative = "lazy/spicetools.xml";

        public static string BuildGameLaunchArguments(bool useSystemConfig)
        {
            if (useSystemConfig)
            {
                return string.Empty;
            }

            return $"-cmdoverride -cfgpath {LazyCfgRelative}";
        }

        public static string BuildConfigEditorArguments(bool useSystemConfig)
        {
            if (useSystemConfig)
            {
                return "-cfg";
            }

            return $"-cfg -cmdoverride -cfgpath {LazyCfgRelative}";
        }
    }
}
