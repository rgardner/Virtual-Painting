using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace KinectDrawing
{
    class BasicPaintingSession : IPaintingSession
    {
        private readonly KinectSensor sensor;
        private readonly ImageSaverBackgroundWorker imageSaver = new ImageSaverBackgroundWorker();

        public BasicPaintingSession(KinectSensor sensor)
        {
            this.sensor = sensor;
        }

        public void Paint(Body body, Brush brush, Canvas canvas, bool startNewSubSession)
        {
            var hand = body.Joints[JointType.HandRight];
            if (hand.TrackingState != TrackingState.NotTracked)
            {
                CameraSpacePoint handPosition = hand.Position;
                ColorSpacePoint handPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(handPosition);

                if (!float.IsInfinity(handPoint.X) && !float.IsInfinity(handPoint.Y))
                {
                    if (startNewSubSession || (canvas.Children.Count == 1))
                    {
                        canvas.Children.Add(CreateDrawingLine(brush));
                    }

                    var trail = canvas.Children[canvas.Children.Count - 1] as Polyline;
                    trail.Points.Add(new Point { X = handPoint.X, Y = handPoint.Y });
                }
            }
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

        private Polyline CreateDrawingLine(Brush brush)
        {
            return new Polyline
            {
                Stroke = brush,
                StrokeThickness = 20,
                Effect = new BlurEffect
                {
                    Radius = 2
                }
            };
        }
    }
}
