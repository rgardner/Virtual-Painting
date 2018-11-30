using System;
using Microsoft.Kinect;
using Newtonsoft.Json;

namespace KinectRecorder
{
    [JsonObject]
    public class SensorBodyFrame
    {
        public SensorBodyFrame()
        {
        }

        public SensorBodyFrame(BodyFrame bodyFrame)
        {
            this.RelativeTime = bodyFrame.RelativeTime;
        }

        [JsonProperty]
        public TimeSpan RelativeTime { get; set; }
    }
}
