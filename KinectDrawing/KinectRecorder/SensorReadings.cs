using System.Collections.Generic;
using Newtonsoft.Json;

namespace KinectRecorder
{
    [JsonObject()]
    public class SensorData
    {
        [JsonProperty]
        public List<SensorBodyFrame> BodyFrames { get; set; } = new List<SensorBodyFrame>();
    }
}
