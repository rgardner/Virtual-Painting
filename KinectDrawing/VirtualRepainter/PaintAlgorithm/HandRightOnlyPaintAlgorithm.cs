using System.Windows;
using System.Windows.Media;
using KinectRecorder;

namespace VirtualRepainter.PaintAlgorithm
{
    public class HandRightOnlyPaintAlgorithm
    {
        private Point? lastPoint = null;

        public PaintLine Paint(SensorBody body, Brush brush)
        {
            PaintLine result = null;

            SensorJoint hand = body.HandRight;
            if (hand.TrackingState != SensorTrackingState.NotTracked)
            {
                var handPoint = new FloatPoint(hand.X, hand.Y);
                if (!handPoint.IsInfinity())
                {
                    var newPoint = new Point { X = handPoint.X, Y = handPoint.Y };
                    if (this.lastPoint != null)
                    {
                        result = new PaintLine
                        {
                            X1 = this.lastPoint.Value.X,
                            Y1 = this.lastPoint.Value.Y,
                            X2 = newPoint.X,
                            Y2 = newPoint.Y,
                            Brush = brush,
                        };
                    }

                    this.lastPoint = newPoint;
                }
            }

            return result;
        }
    }
}
