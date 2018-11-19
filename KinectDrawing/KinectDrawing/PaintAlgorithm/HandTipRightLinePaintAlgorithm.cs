using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace KinectDrawing.PaintAlgorithm
{
    class HandTipRightLinePaintAlgorithm : IPaintAlgorithm
    {
        private readonly KinectSensor sensor;
        private Point? lastPoint = null;

        public HandTipRightLinePaintAlgorithm(KinectSensor sensor)
        {
            this.sensor = sensor;
        }

        public void Paint(Body body, Brush brush, Canvas canvas, bool _startNewSubSession)
        {
            var hand = body.Joints[JointType.HandTipRight];
            if (hand.TrackingState != TrackingState.NotTracked)
            {
                CameraSpacePoint handPosition = hand.Position;
                ColorSpacePoint handPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(handPosition);

                if (!float.IsInfinity(handPoint.X) && !float.IsInfinity(handPoint.Y))
                {
                    var newPoint = new Point { X = handPoint.X, Y = handPoint.Y };
                    if (this.lastPoint != null)
                    {
                        canvas.Children.Add(new Line
                        {
                            X1 = this.lastPoint.Value.X,
                            Y1 = this.lastPoint.Value.Y,
                            X2 = newPoint.X,
                            Y2 = newPoint.Y,
                            Stroke = brush,
                            StrokeThickness = 20,
                            StrokeDashCap = PenLineCap.Round,
                            StrokeStartLineCap = PenLineCap.Round,
                            StrokeEndLineCap = PenLineCap.Round,
                        });
                    }

                    this.lastPoint = newPoint;
                }
            }
        }
    }
}
