using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace PhotoBooth
{
    class CameraSensor
    {
        private readonly KinectSensor sensor;
        private readonly int width;
        private readonly int height;
        private readonly ColorFrameReader colorReader;
        private readonly BodyFrameReader bodyReader;
        private readonly byte[] pixels;
        private readonly Body[] bodies;
        private readonly bool[] bodyPresences;

        public CameraSensor(KinectSensor sensor)
        {
            this.sensor = sensor ?? throw new ArgumentNullException("sensor");

            this.sensor.Open();

            this.width = this.sensor.ColorFrameSource.FrameDescription.Width;
            this.height = this.sensor.ColorFrameSource.FrameDescription.Height;

            this.pixels = new byte[this.width * this.height * 4];
            this.Camera = new WriteableBitmap(this.width, this.height, 96.0, 96.0, PixelFormats.Bgra32, null);

            this.colorReader = this.sensor.ColorFrameSource.OpenReader();
            this.colorReader.FrameArrived += (s, e) =>
                {
                    using (ColorFrame frame = e.FrameReference.AcquireFrame())
                    {
                        if (frame != null)
                        {
                            UpdateCamera(frame);
                        }
                    }
                };

            this.bodies = new Body[this.sensor.BodyFrameSource.BodyCount];
            this.bodyPresences = new bool[this.bodies.Length];

            this.bodyReader = this.sensor.BodyFrameSource.OpenReader();
            this.bodyReader.FrameArrived += (s, e) =>
                {
                    using (BodyFrame frame = e.FrameReference.AcquireFrame())
                    {
                        if (frame != null)
                        {
                            UpdateBodies(frame);
                        }
                    }
                };
        }

        public WriteableBitmap Camera { get; }

        public bool IsCameraPaused
        {
            get => this.colorReader.IsPaused;
            set => this.colorReader.IsPaused = value;
        }

        public event EventHandler PersonEnters;
        public event EventHandler PersonLeaves;

        private void UpdateCamera(ColorFrame frame)
        {
            frame.CopyConvertedFrameDataToArray(this.pixels, ColorImageFormat.Bgra);

            this.Camera.Lock();
            Marshal.Copy(this.pixels, 0, this.Camera.BackBuffer, this.pixels.Length);
            this.Camera.AddDirtyRect(new Int32Rect(0, 0, this.width, this.height));
            this.Camera.Unlock();
        }

        private void UpdateBodies(BodyFrame frame)
        {
            frame.GetAndRefreshBodyData(this.bodies);

            for (int i = 0; i < this.bodies.Length; i++)
            {
                bool isBodyPresent = this.bodies[i] != null && IsBodyPresent(this.bodies[i]);
                if (!this.bodyPresences[i] && isBodyPresent)
                {
                    PersonEnters?.Invoke(this, null);
                }
                else if (this.bodyPresences[i] && !isBodyPresent)
                {
                    PersonLeaves?.Invoke(this, null);
                }

                this.bodyPresences[i] = isBodyPresent;
            }
        }

        private static bool IsBodyPresent(Body body)
        {
            return body.DistanceFromSensor() < Properties.Settings.Default.MaxBodySensorDistance;
        }
    }
}
