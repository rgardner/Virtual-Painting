using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace KinectDrawing.PaintAlgorithm
{
    class HandRightPolylinePaintAlgorithm : IPaintAlgorithm
    {
        private readonly KinectSensor sensor;

        public HandRightPolylinePaintAlgorithm(KinectSensor sensor)
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
                        canvas.Children.Add(new Polyline
                        {
                            Stroke = brush,
                            StrokeThickness = 20,
                            Effect = new BlurEffect
                            {
                                Radius = 2
                            },
                        });
                    }

                    var trail = canvas.Children[canvas.Children.Count - 1] as Polyline;
                    trail.Points.Add(new Point { X = handPoint.X, Y = handPoint.Y });
                }
            }
        }

    }
}
