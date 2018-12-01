using Microsoft.Kinect;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KinectRecorder
{
    [JsonObject(MemberSerialization.OptOut)]
    public class SensorJoint
    {
        public SensorJoint()
        {
        }

        public SensorJoint(Joint joint, KinectSensor kinectSensor)
        {
            CameraSpacePoint cameraSpacePoint = joint.Position;
            ColorSpacePoint colorSpacePoint = kinectSensor.CoordinateMapper.MapCameraPointToColorSpace(cameraSpacePoint);

            this.JointType = joint.JointType;
            this.X = colorSpacePoint.X;
            this.Y = colorSpacePoint.Y;
            this.CameraX = cameraSpacePoint.X;
            this.CameraY = cameraSpacePoint.Y;
            this.CameraZ = cameraSpacePoint.Z;
            this.TrackingState = (SensorTrackingState)joint.TrackingState;
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public JointType JointType { get; set; }

        public float X { get; set; }
        public float Y { get; set; }

        public float CameraX { get; set; }
        public float CameraY { get; set; }
        public float CameraZ { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public SensorTrackingState TrackingState { get; set; }

        public string SerializeToCsv()
        {
            return $"{this.X}"
                + $",{this.Y}"
                + $",{this.CameraX}"
                + $",{this.CameraY}"
                + $",{this.CameraZ}"
                + $",{this.TrackingState}";
        }

        public static string GetCsvHeader(JointType jointType)
        {
            return $"{jointType}_X"
                + $",{jointType}_Y"
                + $",{jointType}_CameraX"
                + $",{jointType}_CameraY"
                + $",{jointType}_CameraZ"
                + $",{jointType}_TrackingState";
        }
    }
}
