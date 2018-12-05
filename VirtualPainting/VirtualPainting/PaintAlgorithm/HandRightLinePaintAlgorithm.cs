using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using KinectRecorder;

namespace VirtualPainting.PaintAlgorithm
{
    public class HandRightLinePaintAlgorithm : IPaintAlgorithm
    {
        private Point? lastPoint = null;

        public void Paint(SensorBody body, Brush brush, Canvas canvas, bool _startNewSubSession)
        {
            var hand = body.HandRight;
            if (hand.TrackingState == SensorTrackingState.Tracked)
            {
                var handPoint = new FloatPoint(hand.X, hand.Y);
                if (!handPoint.IsInfinity())
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
