using System.Collections.Generic;
using Newtonsoft.Json;

namespace KinectRecorder
{
    [JsonObject(MemberSerialization.OptOut)]
    public class SensorData
    {
        public List<SensorBodyFrame> BodyFrames { get; set; } = new List<SensorBodyFrame>();
    }
}
