using System.Collections.Generic;
using Newtonsoft.Json;

namespace KinectRecorder
{
    [JsonObject()]
    public class SensorData
    {
        [JsonProperty]
        public List<SensorColorFrame> ColorFrames { get; set; }

        [JsonProperty]
        public List<SensorBodyFrame> BodyFrames { get; set; }

        [JsonProperty]
        public int ColorFrameDescriptionWidth { get; set; }

        [JsonProperty]
        public int ColorFrameDescriptionHeight { get; set; }
    }
}
