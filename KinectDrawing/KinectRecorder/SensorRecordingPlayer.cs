using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace KinectRecorder
{
    public class SensorRecordingPlayer : INotifyPropertyChanged
    {
        private readonly SensorData sensorReadings;
        private readonly BackgroundWorker colorFrameBackgroundWorker = new BackgroundWorker();
        private readonly BackgroundWorker bodyFrameBackgroundWorker = new BackgroundWorker();

        public SensorRecordingPlayer(string jsonSerializedSensorRecording)
        {
            this.sensorReadings = JsonConvert.DeserializeObject<SensorData>(jsonSerializedSensorRecording);
            this.Camera = new WriteableBitmap(this.sensorReadings.ColorFrameDescriptionWidth,
                this.sensorReadings.ColorFrameDescriptionHeight, 96.0, 96.0, PixelFormats.Bgra32, null);

            this.colorFrameBackgroundWorker.DoWork += (s, e) =>
                {
                    for (int i = 0; i < this.sensorReadings.ColorFrames.Count; i++)
                    {
                        SensorColorFrame sensorReading = this.sensorReadings.ColorFrames[i];

                        this.Camera.Lock();
                        Marshal.Copy(sensorReading.Image, 0, this.Camera.BackBuffer, sensorReading.Image.Length);
                        this.Camera.AddDirtyRect(new Int32Rect(0, 0, sensorReading.Width, sensorReading.Height));
                        this.Camera.Unlock();

                        if (i != (this.sensorReadings.ColorFrames.Count - 1))
                        {
                            TimeSpan timeUntilNextReading = this.sensorReadings.ColorFrames[i + 1].RelativeTime - sensorReading.RelativeTime;
                            Thread.Sleep(timeUntilNextReading);
                        }
                    }
                };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public WriteableBitmap Camera { get; }
        public bool CameraFeedIsPaused { get; set; } = false;

        public void Start()
        {
            this.colorFrameBackgroundWorker.RunWorkerAsync();
        }
    }
}
