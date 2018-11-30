using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Kinect;
using Newtonsoft.Json;

namespace KinectRecorder
{
    [JsonObject(MemberSerialization.OptOut)]
    public class SensorBodyFrame
    {
        public SensorBodyFrame()
        {
        }

        public SensorBodyFrame(BodyFrame bodyFrame, KinectSensor kinectSensor)
        {
            this.RelativeTime = bodyFrame.RelativeTime;

            var bodies = new Body[bodyFrame.BodyCount];
            bodyFrame.GetAndRefreshBodyData(bodies);
            this.Bodies = bodies.Select(b => new SensorBody(b, kinectSensor)).ToList();
        }

        public TimeSpan RelativeTime { get; set; }

        public IList<SensorBody> Bodies { get; set; }
    }
}
