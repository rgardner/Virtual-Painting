using System.Windows.Media;

namespace VirtualRepainter.PaintAlgorithm
{
    public class PaintLine
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public Brush Brush { get; set; }
    }
}
