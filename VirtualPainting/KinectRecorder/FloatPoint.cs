namespace KinectRecorder
{
    public class FloatPoint
    {
        public FloatPoint(float x, float y)
        {
            this.X = x;
            this.Y = y;
        }

        public float X { get; set; }
        public float Y { get; set; }

        public bool IsInfinity()
        {
            return float.IsInfinity(this.X) || float.IsInfinity(this.Y);
        }
    }
}
