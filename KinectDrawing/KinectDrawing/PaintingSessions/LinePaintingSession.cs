﻿using System.Diagnostics.Contracts;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace KinectDrawing
{
    class LinePaintingSession : IPaintingSession
    {
        private readonly KinectSensor sensor;
        private readonly ImageSaverBackgroundWorker imageSaver = new ImageSaverBackgroundWorker();
        private Point lastPoint;

        public LinePaintingSession(KinectSensor sensor)
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

                var x = handPoint.X;
                var y = handPoint.Y;

                if (!float.IsInfinity(x) && !float.IsInfinity(y))
                {
                    var newPoint = new Point { X = x, Y = y };
                    if (this.lastPoint != null)
                    {
                        canvas.Children.Add(new Line
                        {
                            X1 = this.lastPoint.X,
                            Y1 = this.lastPoint.Y,
                            X2 = newPoint.X,
                            Y2 = newPoint.Y,
                            StrokeThickness = 20,
                            StrokeDashCap = PenLineCap.Round,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round,
                            Effect = new BlurEffect
                            {
                                Radius = 2
                            },
                        });
                    }

                    this.lastPoint = newPoint;
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
            Contract.Requires(canvas.Children.Count > 1);
            var elementCountToRemove = canvas.Children.Count - 1;
            canvas.Children.RemoveRange(1, elementCountToRemove);
        }
    }
}
