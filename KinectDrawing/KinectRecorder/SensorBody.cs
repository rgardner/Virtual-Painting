using Microsoft.Kinect;
using Newtonsoft.Json;

namespace KinectRecorder
{
    [JsonObject(MemberSerialization.OptOut)]
    public class SensorBody
    {
        public SensorBody()
        {
        }

        public SensorBody(Body body, KinectSensor kinectSensor)
        {
            this.HandTipRight = new SensorJoint(body.Joints[JointType.HandTipRight], kinectSensor);
            this.HandRight = new SensorJoint(body.Joints[JointType.HandRight], kinectSensor);
            this.WristRight = new SensorJoint(body.Joints[JointType.WristRight], kinectSensor);
        }

        public SensorJoint HandTipRight { get; set; }

        public SensorJoint HandRight { get; set; }

        public SensorJoint WristRight { get; set; }

        public static string GetCsvHeader()
        {
            return $"{SensorJoint.GetCsvHeader(JointType.HandTipRight)}"
                + $",{SensorJoint.GetCsvHeader(JointType.HandRight)}"
                + $",{SensorJoint.GetCsvHeader(JointType.WristRight)}";
        }

        public string SerializeToCsv()
        {
            return $"{this.HandTipRight.SerializeToCsv()}"
                + $",{this.HandRight.SerializeToCsv()}"
                + $",{this.WristRight.SerializeToCsv()}";
        }
   }
}