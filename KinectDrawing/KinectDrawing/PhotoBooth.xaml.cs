using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace KinectDrawing
{
    /// <summary>
    /// Interaction logic for PhotoBooth.xaml
    /// </summary>
    public partial class PhotoBooth : UserControl
    {
        private KinectSensor sensor = null;
        private ColorFrameReader colorReader = null;
        private BodyFrameReader bodyReader = null;
        private IList<Body> bodies = null;

        private int width = 0;
        private int height = 0;
        private byte[] pixels = null;
        private WriteableBitmap bitmap = null;

        public PhotoBooth()
        {
            InitializeComponent();

            Unloaded += (s, e) =>
                {
                    colorReader?.Dispose();
                    bodyReader?.Dispose();
                    sensor?.Close();
                };

            sensor = KinectSensor.GetDefault();

            if (sensor != null)
            {
                sensor.Open();

                width = sensor.ColorFrameSource.FrameDescription.Width;
                height = sensor.ColorFrameSource.FrameDescription.Height;

                colorReader = sensor.ColorFrameSource.OpenReader();
                colorReader.FrameArrived += ColorReader_FrameArrived;

                bodyReader = sensor.BodyFrameSource.OpenReader();
                bodyReader.FrameArrived += BodyReader_FrameArrived;

                pixels = new byte[width * height * 4];
                bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);

                bodies = new Body[sensor.BodyFrameSource.BodyCount];

                camera.Source = bitmap;
            }
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);

                    bitmap.Lock();
                    Marshal.Copy(pixels, 0, bitmap.BackBuffer, pixels.Length);
                    bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
                    bitmap.Unlock();
                }
            }
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.GetAndRefreshBodyData(bodies);

                    Body body = bodies.Where(b => b.IsTracked).FirstOrDefault();

                    if (body != null)
                    {
                        Joint handRight = body.Joints[JointType.HandRight];

                        if (handRight.TrackingState != TrackingState.NotTracked)
                        {
                            CameraSpacePoint handRightPosition = handRight.Position;
                            ColorSpacePoint handRightPoint = sensor.CoordinateMapper.MapCameraPointToColorSpace(handRightPosition);

                            float x = handRightPoint.X;
                            float y = handRightPoint.Y;

                            if (!float.IsInfinity(x) && ! float.IsInfinity(y))
                            {
                                Canvas.SetLeft(brush, x - brush.Width / 2.0);
                                Canvas.SetTop(brush, y - brush.Height);
                            }
                        }
                    }
                }
            }
        }
    }
}
