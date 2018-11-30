using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Kinect;

namespace KinectDrawing.PaintingSession
{
    interface IPaintingSession
    {
        void Paint(Body body, Brush brush, Canvas canvas, BodyFrame bodyFrame = null);
        void SavePainting(Image background, Canvas canvas, int width, int height, string directoryPath, string backgroundDirectoryPath);
        void ClearCanvas(Canvas canvas);
    }
}
