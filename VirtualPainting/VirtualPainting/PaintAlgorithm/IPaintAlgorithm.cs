using System.Windows.Controls;
using System.Windows.Media;
using KinectRecorder;

namespace VirtualPainting.PaintAlgorithm
{
    public interface IPaintAlgorithm
    {
        void Paint(SensorBody body, Brush brush, Canvas canvas, bool startNewSubSession = false);
    }
}
