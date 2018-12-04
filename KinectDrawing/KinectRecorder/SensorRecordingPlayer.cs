using System;
using System.ComponentModel;
using System.Threading;
using Newtonsoft.Json;

namespace KinectRecorder
{
    public class SensorRecordingPlayer
    {
        private readonly SensorData sensorReadings;
        private readonly BackgroundWorker bodyFrameBackgroundWorker = new BackgroundWorker();

        public SensorRecordingPlayer(string jsonSerializedSensorRecording)
        {
            this.sensorReadings = JsonConvert.DeserializeObject<SensorData>(jsonSerializedSensorRecording);

            this.bodyFrameBackgroundWorker.DoWork += (s, e) =>
                {
                    for (var i = 0; i < this.sensorReadings.BodyFrames.Count; i++)
                    {
                        SensorBodyFrame sensorReading = this.sensorReadings.BodyFrames[i];
                        SensorBodyFrameCaptured?.Invoke(this, sensorReading);
                        if (i != (this.sensorReadings.BodyFrames.Count - 1))
                        {
                            TimeSpan timeUntilNextReading = this.sensorReadings.BodyFrames[i + 1].RelativeTime - sensorReading.RelativeTime;
                            Thread.Sleep(timeUntilNextReading);
                        }
                    }
                };
        }

        public event EventHandler<SensorBodyFrame> SensorBodyFrameCaptured;

        public void Start()
        {
            this.bodyFrameBackgroundWorker.RunWorkerAsync();
        }
    }
}
