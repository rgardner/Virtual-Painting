using System;
using System.IO;
using System.Text;
using Microsoft.Kinect;
using Newtonsoft.Json;

namespace KinectRecorder
{
    [JsonObject]
    public class SensorColorFrame
    {
        public SensorColorFrame()
        {
        }

        public SensorColorFrame(ColorFrame colorFrame)
        {
            this.RelativeTime = colorFrame.RelativeTime;
            this.Width = colorFrame.FrameDescription.Width;
            this.Height = colorFrame.FrameDescription.Height;

            this.Image = new byte[this.Width * this.Height * 4];
            colorFrame.CopyConvertedFrameDataToArray(this.Image, ColorImageFormat.Bgra);
        }

        [JsonProperty]
        public TimeSpan RelativeTime { get; set; }

        [JsonProperty]
        public byte[] Image { get; set; }

        [JsonProperty]
        public int Width { get; set; }

        [JsonProperty]
        public int Height { get; set; }

        public static T Deserialize<T>(byte[] data) where T : class
        {
            using (var stream = new MemoryStream(data))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                return JsonSerializer.Create().Deserialize(reader, typeof(T)) as T;
            }
        }
    }
}
