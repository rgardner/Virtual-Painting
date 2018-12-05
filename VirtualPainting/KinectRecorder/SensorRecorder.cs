namespace KinectRecorder
{
    public class SensorRecorder
    {
        public SensorRecorder()
        {
        }

        public SensorData SensorData { get; set; } = new SensorData();

        public void LogBodyFrame(SensorBodyFrame bodyFrame)
        {
            this.SensorData.BodyFrames.Add(bodyFrame);
        }
    }
}
