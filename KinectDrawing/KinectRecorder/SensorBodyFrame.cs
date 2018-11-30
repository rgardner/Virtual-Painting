using System;
using Newtonsoft.Json;

namespace KinectRecorder
{
    [JsonObject]
    public class SensorBodyFrame
    {
        [JsonProperty]
        public TimeSpan RelativeTime { get; set; }
    }
}
