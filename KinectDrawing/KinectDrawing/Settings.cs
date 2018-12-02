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

        public const double BodyDistanceFromCameraThresholdInMeters = 1.5;
        public const double BodyDistanceVariationThresholdInMeters = 0.5;

        /// <summary>
        /// Expected max difference between actual human ratio and expected human ratio.
        /// Liam McInroy used 0.2f for his value. This is too small for this app because people
        /// will stand close to the sensor, so there's only really the upper body that is visible.
        /// </summary>
        public const double HumanRatioTolerance = 1.0f;

        public const string SavedImagesDirectoryPathEnvironmentVariableName = "VirtualPainting_SavedImagesDirectoryPath";
        public const string SavedBackgroundImagesDirectoryPathEnvironmentVariableName = "VirtualPainting_SavedBackgroundImagesDirectoryPath";

        public static bool IsDebugViewEnabled { get; } = true;
        public static bool GenerateStateMachineGraph { get; } = true;

        public static readonly bool IsTestModeEnabled = true;
    }
}
