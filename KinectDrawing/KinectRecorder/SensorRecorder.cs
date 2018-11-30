using Microsoft.Kinect;

namespace KinectRecorder
{
    public class SensorRecorder
    {
        public SensorRecorder()
        {
        }

        public SensorData SensorData { get; set; } = new SensorData();

        public void LogColorFrame(ColorFrame colorFrame)
        {
            this.SensorData.ColorFrames.Add(new SensorColorFrame(colorFrame));
        }

        public void LogBodyFrame(BodyFrame bodyFrame)
        {
            // TODO
        }
    }
}
