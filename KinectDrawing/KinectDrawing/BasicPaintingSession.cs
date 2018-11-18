using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        private readonly Brush brush;
        private readonly BackgroundWorker backgroundWorker = new BackgroundWorker();

        public BasicPaintingSession(KinectSensor sensor, Brush brush)
        {
            this.sensor = sensor;
            this.brush = brush;

            this.backgroundWorker.DoWork += (s, e) =>
                {
                    var arguments = e.Argument as IList<object>;
                    var image = arguments[0] as RenderTargetBitmap;
                    var directoryPath = arguments[1] as string;

                    BitmapEncoder pngEncoder = new PngBitmapEncoder();
                    pngEncoder.Frames.Add(BitmapFrame.Create(image));
                    using (var ms = new MemoryStream())
                    {
                        pngEncoder.Save(ms);

                        string fileName = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss") + ".png";
                        string fullPath = System.IO.Path.Combine(directoryPath, fileName);
                        Directory.CreateDirectory(directoryPath);
                        File.WriteAllBytes(fullPath, ms.ToArray());
                    }
                };
        }

        public void Paint(Body body, Canvas canvas)
        {
            var hand = body.Joints[JointType.HandRight];
            if (hand.TrackingState != TrackingState.NotTracked)
            {
                CameraSpacePoint handPosition = hand.Position;
                ColorSpacePoint handPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(handPosition);

                var x = handPoint.X;
                var y = handPoint.Y;

                if (!float.IsInfinity(x) && !float.IsInfinity(y))
                {
                    if (canvas.Children.Count == 1)
                    {
                        canvas.Children.Add(CreateDrawingLine());
                    }

                    var trail = canvas.Children[canvas.Children.Count - 1] as Polyline;
                    trail.Points.Add(new Point { X = x, Y = y });
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
            this.backgroundWorker.RunWorkerAsync(new List<object> { rtb, directoryPath });

        }

        public void ClearCanvas(Canvas canvas)
        {
            Debug.Assert(canvas.Children.Count > 1);
            var elementCountToRemove = canvas.Children.Count - 1;
            canvas.Children.RemoveRange(1, elementCountToRemove);
        }

        private Polyline CreateDrawingLine()
        {
            return new Polyline
            {
                Stroke = this.brush,
                StrokeThickness = 20,
                Effect = new BlurEffect
                {
                    Radius = 2
                }
            };
        }
    }
}
