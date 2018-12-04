using System.Windows;
using Microsoft.Kinect;

namespace KinectDrawing
{
    static class PersonDetector
    {
        public static bool IsPersonPresent(Body body, Rect frame, double maxExpectedDistance = Settings.BodyDistanceFromCameraThresholdInMeters)
        {
            return body.IsHuman() && IsInFrame(body, frame) && IsWithinValidDistance(body, maxExpectedDistance);
        }

        /// <summary>
        /// Detects if both shoulders are in the frame.
        /// </summary>
        public static bool IsInFrame(Body body, Rect frame)
        {
            Joint shoulderLeft = body.Joints[JointType.ShoulderLeft];
            Joint shoulderRight = body.Joints[JointType.ShoulderRight];

            if ((shoulderLeft.TrackingState == TrackingState.NotTracked) || (shoulderRight.TrackingState == TrackingState.NotTracked))
            {
                return false;
            }

            var sensor = KinectSensor.GetDefault();
            ColorSpacePoint shoulderLeftPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(shoulderLeft.Position);
            ColorSpacePoint shoulderRightPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(shoulderRight.Position);

            var bodyX1 = shoulderLeftPoint.X;
            var bodyY1 = shoulderLeftPoint.Y;
            var bodyX2 = shoulderRightPoint.X;
            var bodyY2 = shoulderRightPoint.Y;
            if (float.IsInfinity(bodyX1) || float.IsInfinity(bodyY1) || float.IsInfinity(bodyX2) || float.IsInfinity(bodyY2))
            {
                return false;
            }

            return frame.Contains(bodyX1, bodyY1) && frame.Contains(bodyX2, bodyY2);
        }

        public static bool IsWithinValidDistance(Body body, double maxExpectedDistance)
        {
            return body.DistanceFromSensor() < maxExpectedDistance;
        }
    }
}
