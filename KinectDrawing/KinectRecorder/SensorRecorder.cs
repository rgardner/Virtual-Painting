using Microsoft.Kinect;

namespace KinectRecorder
{
    public class SensorRecorder
    {
        public SensorRecorder()
        {
        }

        public SensorData SensorData { get; set; } = new SensorData();

        public void LogBodyFrame(BodyFrame bodyFrame, KinectSensor kinectSensor)
        {
            this.SensorData.BodyFrames.Add(new SensorBodyFrame(bodyFrame, kinectSensor));
        }
    }
}
