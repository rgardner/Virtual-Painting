using System.Windows.Media;
using KinectDrawing.PaintAlgorithm;

namespace KinectDrawing
{
    static class Settings
    {
        public static readonly System.Type PaintAlgorithm = typeof(HandRightLinePaintAlgorithm);

        public static readonly SolidColorBrush[] PaintBrushes = new SolidColorBrush[]
        {
            new SolidColorBrush(Color.FromRgb(39, 96, 163)),
            new SolidColorBrush(Color.FromRgb(242, 108, 96)),
            new SolidColorBrush(Color.FromRgb(153, 86, 152)),
            new SolidColorBrush(Color.FromRgb(0, 90, 100)),
            new SolidColorBrush(Color.FromRgb(236, 0, 140)),
            new SolidColorBrush(Color.FromRgb(129, 203, 235)),
            new SolidColorBrush(Color.FromRgb(223, 130, 182)),
        };

        public const double BodyPresenceAreaLeftWidthRatio = 0.27;
        public const double BodyPresenceAreaTopHeightRatio = 0.17;
        public const double BodyPresenceAreaRightWidthRatio = 0.71;
        public const double BodyPresenceAreaBottomHeightRatio = 0.97;

        public const double BodyDistanceToCameraThresholdInMeters = 2.0;

        public const string SavedImagesDirectoryPathEnvironmentVariableName = "VirtualPainting_SavedImagesDirectoryPath";
        public const string SavedBackgroundImagesDirectoryPathEnvironmentVariableName = "VirtualPainting_SavedBackgroundImagesDirectoryPath";

        public static readonly bool IsBodyPresenceDebugModeEnabled = false;
        public static readonly bool IsTestModeEnabled = false;
        public static readonly bool IsBodyDistanceDebugModeEnabled = false;
    }
}
