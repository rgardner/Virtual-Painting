using System.Windows.Controls;
using Microsoft.Kinect;

namespace KinectDrawing
{
    interface IPaintingSession
    {
        void Paint(Body body, Canvas canvas);
        void SavePainting(Image background, Canvas canvas, int width, int height, string directoryPath);
        void ClearCanvas(Canvas canvas);
    }
}
