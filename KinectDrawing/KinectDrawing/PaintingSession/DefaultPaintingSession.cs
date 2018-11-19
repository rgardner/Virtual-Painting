using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KinectDrawing.PaintAlgorithm;
using Microsoft.Kinect;

namespace KinectDrawing.PaintingSession
{
    class DefaultPaintingSession : IPaintingSession
    {
        private readonly KinectSensor sensor;
        private readonly IPaintAlgorithm paintingAlgorithm;
        private readonly ImageSaverBackgroundWorker imageSaver = new ImageSaverBackgroundWorker();

        public DefaultPaintingSession(KinectSensor sensor, IPaintAlgorithm paintingAlgorithm)
        {
            this.sensor = sensor;
            this.paintingAlgorithm = paintingAlgorithm;
        }

        public void Paint(Body body, Brush brush, Canvas canvas, bool startNewSubSession)
        {
            this.paintingAlgorithm.Paint(body, brush, canvas, startNewSubSession);
        }

        public void SavePainting(Image background, Canvas canvas, int width, int height, string directoryPath)
        {
            var rtb = new RenderTargetBitmap(width, height, 96d, 96d, PixelFormats.Default);
            rtb.Render(background);
            rtb.Render(canvas);
            // TODO: add saved image filter to RenderTargetBitmap
            rtb.Freeze(); // necessary for the backgroundWorker to access it
            this.imageSaver.SaveImageAsync(rtb, directoryPath);
        }

        public void ClearCanvas(Canvas canvas)
        {
            Debug.Assert(canvas.Children.Count > 1);
            var elementCountToRemove = canvas.Children.Count - 1;
            canvas.Children.RemoveRange(1, elementCountToRemove);
        }
    }
}
