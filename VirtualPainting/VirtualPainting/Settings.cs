using System.Windows.Media;
using VirtualPainting.PaintAlgorithm;

namespace VirtualPainting
{
    static class Settings
    {
        public static readonly System.Type PaintAlgorithm = typeof(HandRightLinePaintAlgorithm);

        public static readonly SolidColorBrush MyBlue = new SolidColorBrush(Color.FromRgb(39, 96, 163));
        public static readonly SolidColorBrush MyBurntOrange = new SolidColorBrush(Color.FromRgb(242, 108, 96));
        public static readonly SolidColorBrush MyGray = new SolidColorBrush(Color.FromRgb(153, 155, 154));

        public static readonly SolidColorBrush[] PaintBrushes = new SolidColorBrush[]
        {
            MyBlue,
            MyBurntOrange,
            new SolidColorBrush(Color.FromRgb(153, 86, 152)),
            new SolidColorBrush(Color.FromRgb(0, 90, 100)),
            new SolidColorBrush(Color.FromRgb(236, 0, 140)),
            new SolidColorBrush(Color.FromRgb(129, 203, 235)),
            new SolidColorBrush(Color.FromRgb(223, 130, 182)),
        };

        public const double BodyDistanceFromCameraThresholdInMeters = 2.0;
        public const double BodyDistanceVariationThresholdInMeters = 0.5;

        /// <summary>
        /// Virtual Painting was designed for an environment where a person's lower body may or may
        /// not be visible. Because of this, the human ratio heuristic has fewer ratios to compare
        /// and can return more false positives. Also, in low light conditions, the false positive
        /// rate was observed to be higher, which is why this configuration exists.
        /// </summary>
        public static readonly bool UseHumanRatioHeuristic = false;

        /// <summary>
        /// Expected max difference between actual human ratio and expected human ratio.
        /// Liam McInroy used 0.2f for his value. This is too small for this app because people
        /// will stand close to the sensor, so only a person's upper body is fully visible.
        /// </summary>
        public const double HumanRatioTolerance = 1.0f;

        public const string SavedImagesDirectoryPathEnvironmentVariableName = "VirtualPainting_SavedImagesDirectoryPath";
        public const string SavedBackgroundImagesDirectoryPathEnvironmentVariableName = "VirtualPainting_SavedBackgroundImagesDirectoryPath";

        public static bool IsDebugViewEnabled { get; } = false;
        public static bool GenerateStateMachineGraph { get; } = false;

        public static readonly bool IsTestModeEnabled = false;
    }
}
