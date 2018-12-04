using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using KinectRecorder;

namespace VirtualPainting.PaintAlgorithm
{
    public class HandRightPolylinePaintAlgorithm : IPaintAlgorithm
    {
        public void Paint(SensorBody body, Brush brush, Canvas canvas, bool startNewSubSession)
        {
            var hand = body.HandRight;
            if (hand.TrackingState != SensorTrackingState.NotTracked)
            {
                var handPoint = new FloatPoint(hand.X, hand.Y);
                if (!handPoint.IsInfinity())
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
