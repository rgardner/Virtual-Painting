using System.Windows.Controls;
using System.Windows.Media;
using KinectRecorder;

namespace KinectDrawing.PaintAlgorithm
{
    public interface IPaintAlgorithm
    {
        void Paint(SensorBody body, Brush brush, Canvas canvas, bool startNewSubSession = false);
    }
}
