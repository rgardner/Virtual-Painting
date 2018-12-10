using System.ComponentModel;
using System.Linq;
using System.Windows;
using LightBuzz.Vitruvius;
using Microsoft.Kinect;

namespace VirtualPainting
{
    public class PersonDetectionState : INotifyPropertyChanged
    {
        public PersonDetectionState(int bodyIndex, bool isPrimary, Body body, Rect bodyPresenceArea)
        {
            this.BodyIndex = bodyIndex;
            Refresh(isPrimary, body, bodyPresenceArea);
        }

        public int BodyIndex { get; set; }

        public bool IsPrimary { get; set; }

        public bool IsHuman { get; set; }

        public bool IsInFrame { get; set; }

        public string DistanceFromSensor { get; set; } = string.Empty;

        public int TrackedJointCount { get; set; }

#pragma warning disable CS0067 // PropertyChanged is used by Fody-generated property setters
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        public void Refresh(bool isPrimary, Body body, Rect bodyPresenceArea)
        {
            this.IsPrimary = isPrimary;
            this.IsHuman = body.IsHuman();
            this.IsInFrame = PersonDetector.IsInFrame(body, bodyPresenceArea);
            this.DistanceFromSensor = body.DistanceFromSensor().ToString("0.00");
            this.TrackedJointCount = body.TrackedJoints(includeInferred: false).Count();
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PersonDetectionState);
        }

        public bool Equals(PersonDetectionState other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return (this.IsPrimary == other.IsPrimary)
                && (this.IsHuman == other.IsHuman)
                && (this.IsInFrame == other.IsInFrame)
                && (this.DistanceFromSensor == other.DistanceFromSensor)
                && (this.TrackedJointCount == other.TrackedJointCount);
        }

        public static bool operator==(PersonDetectionState lhs, PersonDetectionState rhs)
        {
            if (lhs is null)
            {
                if (rhs is null)
                {
                    return true;
                }

                return false;
            }

            return lhs.Equals(rhs);
        }

        public static bool operator!=(PersonDetectionState first, PersonDetectionState second)
        {
            return !(first == second);
        }

        public override int GetHashCode()
        {
            var hashCode = -1494063407;
            hashCode = hashCode * -1521134295 + this.IsPrimary.GetHashCode();
            hashCode = hashCode * -1521134295 + this.IsHuman.GetHashCode();
            hashCode = hashCode * -1521134295 + this.IsInFrame.GetHashCode();
            hashCode = hashCode * -1521134295 + this.DistanceFromSensor.GetHashCode();
            hashCode = hashCode * -1521134295 + this.TrackedJointCount.GetHashCode();
            return hashCode;
        }
    }
}
