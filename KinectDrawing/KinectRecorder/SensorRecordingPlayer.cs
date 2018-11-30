using System;
using System.ComponentModel;
using System.Threading;
using Newtonsoft.Json;

namespace KinectRecorder
{
    public class SensorRecordingPlayer : INotifyPropertyChanged
    {
        private readonly SensorData sensorReadings;
        private readonly BackgroundWorker bodyFrameBackgroundWorker = new BackgroundWorker();

        public SensorRecordingPlayer(string jsonSerializedSensorRecording)
        {
            this.sensorReadings = JsonConvert.DeserializeObject<SensorData>(jsonSerializedSensorRecording);

            this.bodyFrameBackgroundWorker.DoWork += (s, e) =>
                {
                    for (int i = 0; i < this.sensorReadings.BodyFrames.Count; i++)
                    {
                        SensorBodyFrame sensorReading = this.sensorReadings.BodyFrames[i];

                        if (i != (this.sensorReadings.BodyFrames.Count - 1))
                        {
                            TimeSpan timeUntilNextReading = this.sensorReadings.BodyFrames[i + 1].RelativeTime - sensorReading.RelativeTime;
                            Thread.Sleep(timeUntilNextReading);
                        }
                    }
                };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Start()
        {
            this.bodyFrameBackgroundWorker.RunWorkerAsync();
        }
    }
}
