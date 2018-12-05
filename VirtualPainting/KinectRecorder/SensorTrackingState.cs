using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace KinectRecorder
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum SensorTrackingState
    {
        NotTracked = 0,
        Inferred = 1,
        Tracked = 2,
    }
}
