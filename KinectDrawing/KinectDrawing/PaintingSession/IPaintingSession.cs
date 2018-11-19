using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Kinect;

namespace KinectDrawing.PaintingSession
{
    interface IPaintingSession
    {
        void Paint(Body body, Brush brush, Canvas canvas, bool startNewSubSession = false);
        void SavePainting(Image background, Canvas canvas, int width, int height, string directoryPath);
        void ClearCanvas(Canvas canvas);
    }
}
