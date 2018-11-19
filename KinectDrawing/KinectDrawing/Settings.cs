using KinectDrawing.PaintAlgorithm;

namespace KinectDrawing
{
    static class Settings
    {
        public static System.Type PaintAlgorithm = typeof(HandRightLinePaintAlgorithm);

        public static double BodyPresenceAreaLeftWidthRatio = 0.28;
        public static double BodyPresenceAreaTopHeightRatio = 0.20;
        public static double BodyPresenceAreaRightWidthRatio = 0.70;
        public static double BodyPresenceAreaBottomHeightRatio = 0.97;

        public static string SavedImagesDirectoryPathEnvironmentVariableName = "VirtualPainting_SavedImagesDirectoryPath";

        public static bool IsBodyPresenceDebugModeEnabled = true;
        public static bool IsTestModeEnabled = true;

    }
}
