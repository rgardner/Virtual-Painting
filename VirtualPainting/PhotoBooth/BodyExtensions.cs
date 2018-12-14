using LightBuzz.Vitruvius;
using Microsoft.Kinect;

namespace PhotoBooth
{
    static class BodyExtensions
    {
        public static double DistanceFromSensor(this Body body)
        {
            Joint joint = body.Joints[JointType.SpineMid];
            return joint.Position.Length();
        }
    }
}
