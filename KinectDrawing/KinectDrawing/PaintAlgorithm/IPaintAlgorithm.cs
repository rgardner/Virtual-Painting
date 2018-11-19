using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Kinect;

namespace KinectDrawing.PaintAlgorithm
{
    interface IPaintAlgorithm
    {
        void Paint(Body body, Brush brush, Canvas canvas, bool startNewSubSession = false);
    }
}
