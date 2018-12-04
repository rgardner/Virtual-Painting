using System.Windows.Media;

namespace VirtualPainting
{
    internal class BrushColorCycler
    {
        private int? currentBrushIndex = null;

        public SolidColorBrush Next()
        {
            if ((this.currentBrushIndex == null) ||
                (this.currentBrushIndex == (Settings.PaintBrushes.Length - 1)))
            {
                this.currentBrushIndex = 0;
            }
            else
            {
                this.currentBrushIndex++;
            }

            return Settings.PaintBrushes[this.currentBrushIndex.Value];
        }
    }
}
