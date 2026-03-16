using System;

namespace LazyBootstrap.Services.Windowing
{
    internal enum AspectRatioResizeAction
    {
        None,
        InitializeTracking,
        ApplyResize
    }

    internal readonly record struct AspectRatioResizeDecision(AspectRatioResizeAction Action, double Width, double Height);

    internal static class AspectRatioResizeCalculator
    {
        public static AspectRatioResizeDecision Calculate(
            double width,
            double height,
            double previousWidth,
            double previousHeight,
            double minWidth,
            double minHeight,
            double aspectRatio,
            double changeThreshold = 0.5)
        {
            if (width <= 0 || height <= 0 || aspectRatio <= 0)
            {
                return default;
            }

            if (previousWidth <= 0 || previousHeight <= 0)
            {
                return new AspectRatioResizeDecision(AspectRatioResizeAction.InitializeTracking, width, height);
            }

            var deltaWidth = Math.Abs(width - previousWidth);
            var deltaHeight = Math.Abs(height - previousHeight);
            if (deltaWidth < changeThreshold && deltaHeight < changeThreshold)
            {
                return default;
            }

            double targetWidth;
            double targetHeight;
            if (deltaWidth >= deltaHeight)
            {
                targetWidth = width;
                targetHeight = targetWidth / aspectRatio;
            }
            else
            {
                targetHeight = height;
                targetWidth = targetHeight * aspectRatio;
            }

            if (targetWidth < minWidth)
            {
                targetWidth = minWidth;
                targetHeight = targetWidth / aspectRatio;
            }

            if (targetHeight < minHeight)
            {
                targetHeight = minHeight;
                targetWidth = targetHeight * aspectRatio;
            }

            return new AspectRatioResizeDecision(AspectRatioResizeAction.ApplyResize, targetWidth, targetHeight);
        }
    }
}
