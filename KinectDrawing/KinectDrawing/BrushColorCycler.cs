using System.Windows.Media;

namespace KinectDrawing
{
    internal class BrushColorCycler
    {
        private static readonly SolidColorBrush[] brushes = new SolidColorBrush[]
        {
            new SolidColorBrush(Color.FromRgb(39, 96, 163)),
            new SolidColorBrush(Color.FromRgb(242, 108, 96)),
            new SolidColorBrush(Color.FromRgb(153, 86, 152)),
            new SolidColorBrush(Color.FromRgb(0, 90, 100)),
            new SolidColorBrush(Color.FromRgb(236, 0, 140)),
            new SolidColorBrush(Color.FromRgb(129, 203, 235)),
            new SolidColorBrush(Color.FromRgb(223, 130, 182)),
        };

        private int currentBrushIndex = 0;

        public SolidColorBrush CurrentBrush => brushes[this.currentBrushIndex];

        public SolidColorBrush Next()
        {
            this.currentBrushIndex++;
            if (this.currentBrushIndex == brushes.Length)
            {
                this.currentBrushIndex = 0;
            }

            return this.CurrentBrush;
        }
    }
}
