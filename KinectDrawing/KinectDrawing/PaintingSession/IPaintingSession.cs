using System.Windows.Controls;
using System.Windows.Media;
using KinectRecorder;
using Microsoft.Kinect;

namespace KinectDrawing.PaintingSession
{
    interface IPaintingSession
    {
        void Paint(SensorBody body, Brush brush, Canvas canvas, SensorBodyFrame bodyFrame = null);
        void SavePainting(Image background, Canvas canvas, int width, int height, string directoryPath, string backgroundDirectoryPath);
        void ClearCanvas(Canvas canvas);
    }
}
