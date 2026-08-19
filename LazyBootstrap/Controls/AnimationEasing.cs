using System;

namespace LazyBootstrap.Controls
{
    internal static class AnimationEasing
    {
        public static double EaseInOutCubic(double progress)
        {
            return progress < 0.5d
                ? 4d * progress * progress * progress
                : 1d - Math.Pow(-2d * progress + 2d, 3d) / 2d;
        }
    }
}
