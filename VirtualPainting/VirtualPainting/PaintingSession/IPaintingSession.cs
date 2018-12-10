using System.Windows.Controls;
using System.Windows.Media;
using KinectRecorder;

namespace VirtualPainting.PaintingSession
{
    interface IPaintingSession
    {
        void Paint(SensorBody body, Brush brush, Canvas canvas, SensorBodyFrame bodyFrame);
        void SavePainting(Image background, Canvas canvas, int width, int height, string directoryPath, string backgroundDirectoryPath);
    }
}
