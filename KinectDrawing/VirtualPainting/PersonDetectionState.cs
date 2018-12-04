using System.ComponentModel;
using System.Linq;
using System.Windows;
using LightBuzz.Vitruvius;
using Microsoft.Kinect;

namespace VirtualPainting
{
    public class PersonDetectionState : INotifyPropertyChanged
    {
        public PersonDetectionState(int bodyIndex, bool? isPrimary = null, Body body = null, Rect? bodyPresenceArea = null, Rect? newUserButtonArea = null)
        {
            this.BodyIndex = bodyIndex;
            if (isPrimary.HasValue && body != null && bodyPresenceArea.HasValue)
            {
                Refresh(isPrimary.Value, body, bodyPresenceArea.Value, newUserButtonArea.Value);
            }
        }

        public int BodyIndex { get; set; }

        public bool IsPrimary { get; set; }

        public bool IsHuman { get; set; }

        public bool IsInFrame { get; set; }

        public string DistanceFromSensor { get; set; } = string.Empty;

        public int TrackedJointCount { get; set; }

        public int SelectingNewUserButtonFrameCount { get; set; }

#pragma warning disable CS0067 // PropertyChanged is used by Fody-generated property setters
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        public void Refresh(bool isPrimary, Body body, Rect bodyPresenceArea, Rect newUserButtonArea)
        {
            this.IsPrimary = isPrimary;
            this.IsHuman = body.IsHuman();
            this.IsInFrame = PersonDetector.IsInFrame(body, bodyPresenceArea);
            this.DistanceFromSensor = body.DistanceFromSensor().ToString("0.00");
            this.TrackedJointCount = body.TrackedJoints(includeInferred: false).Count();

            var handPoint = body.Joints[JointType.HandRight].Position.ToPoint(Visualization.Color);
            if (newUserButtonArea.Contains(handPoint))
            {
                this.SelectingNewUserButtonFrameCount++;
            }
            else
            {
                this.SelectingNewUserButtonFrameCount = 0;
            }

        }

        public static bool operator==(PersonDetectionState first, PersonDetectionState second)
        {
            if (first is null && second is null)
            {
                return true;
            }
            else if (first is null || second is null)
            {
                return false;
            }

            return (first.IsHuman == second.IsHuman)
                && (first.IsHuman == second.IsHuman)
                && (first.IsInFrame == second.IsInFrame)
                && (first.DistanceFromSensor == second.DistanceFromSensor);
        }

        public static bool operator!=(PersonDetectionState first, PersonDetectionState second)
        {
            return !(first == second);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hashCode = -1494063407;
            hashCode = hashCode * -1521134295 + this.IsHuman.GetHashCode();
            hashCode = hashCode * -1521134295 + this.IsInFrame.GetHashCode();
            hashCode = hashCode * -1521134295 + this.DistanceFromSensor.GetHashCode();
            return hashCode;
        }
    }
}
