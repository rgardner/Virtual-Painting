using System;
using System.Linq;
using LightBuzz.Vitruvius;
using Microsoft.Kinect;

namespace VirtualPainting
{
    public static class BodyExtensions
    {
        /// <summary>
        /// https://stackoverflow.com/a/27883660
        /// </summary>
        private static class HumanRatios
        {
            public const double LegLength = 4.0f;   // foot to knee to hip
            public const double BodyWidth = 2.33f;  // shoulder to spine shoulder to shoulder
            public const double TorsoLength = 3.0f; // hips to shoulder
        }

        /// <summary>
        /// Detects if a body is human, based on Liam McInroy's Human detection algorithm.
        ///
        /// Compares the proportions of the human body relative to the head. Because this app is
        /// expected to be used in scenarios where the person's legs aren't visible, this algorithm
        /// only checks if at least one ratio is correct, instead of two like in the original.
        /// https://stackoverflow.com/a/27883660
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        public static bool IsHuman(this Body body)
        {
            return true;
            //double headSize = body.Length(JointType.SpineShoulder, JointType.Head);

            //double legLengthRatio = body.Length(JointType.FootLeft, JointType.KneeLeft, JointType.HipLeft) / headSize;
            //double bodyWidthRatio = body.Length(JointType.ShoulderLeft, JointType.SpineShoulder, JointType.ShoulderRight) / headSize;
            //double torsoLengthRatio = body.Length(JointType.SpineBase, JointType.SpineShoulder) / headSize;

            //// (actual, expected)
            //var ratios = new Tuple<double, double>[] {
            //    new Tuple<double, double>(legLengthRatio, HumanRatios.LegLength),
            //    new Tuple<double, double>(bodyWidthRatio, HumanRatios.BodyWidth),
            //    new Tuple<double, double>(torsoLengthRatio, HumanRatios.TorsoLength),
            //};

            //foreach (var actualExpectedRatio in ratios)
            //{
            //    double diff = actualExpectedRatio.Item1 - actualExpectedRatio.Item2;
            //    if (Math.Abs(diff) <= Settings.HumanRatioTolerance)
            //    {
            //        return true;
            //    }
            //}

            //return false;
        }

        public static double DistanceFromSensor(this Body body)
        {
            Joint joint = body.Joints[JointType.SpineMid];
            return joint.Position.Length();
        }

        /// <summary>
        /// Sums the distances between each of the joints.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="jointTypes"></param>
        /// <returns></returns>
        public static double Length(this Body body, params JointType[] jointTypes)
        {
            CameraSpacePoint[] jointPoints = jointTypes.Select(jointType => body.Joints[jointType].Position).ToArray();
            return MathExtensions.Length(jointPoints);
        }
    }
}
