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
        private KinectSensor _sensor = null;
        private ColorFrameReader _colorReader = null;
        private BodyFrameReader _bodyReader = null;
        private IList<Body> _bodies = null;

        private int _width = 0;
        private int _height = 0;
        private byte[] _pixels = null;
        private WriteableBitmap _bitmap = null;

        public PhotoBooth()
        {
            InitializeComponent();

            Unloaded += (s, e) =>
                {
                    _colorReader?.Dispose();
                    _bodyReader?.Dispose();
                    _sensor?.Close();
                };

            _sensor = KinectSensor.GetDefault();

            if (_sensor != null)
            {
                _sensor.Open();

                _width = _sensor.ColorFrameSource.FrameDescription.Width;
                _height = _sensor.ColorFrameSource.FrameDescription.Height;

                _colorReader = _sensor.ColorFrameSource.OpenReader();
                _colorReader.FrameArrived += ColorReader_FrameArrived;

                _bodyReader = _sensor.BodyFrameSource.OpenReader();
                _bodyReader.FrameArrived += BodyReader_FrameArrived;

                _pixels = new byte[_width * _height * 4];
                _bitmap = new WriteableBitmap(_width, _height, 96.0, 96.0, PixelFormats.Bgra32, null);

                _bodies = new Body[_sensor.BodyFrameSource.BodyCount];

                camera.Source = _bitmap;
            }
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.CopyConvertedFrameDataToArray(_pixels, ColorImageFormat.Bgra);

                    _bitmap.Lock();
                    Marshal.Copy(_pixels, 0, _bitmap.BackBuffer, _pixels.Length);
                    _bitmap.AddDirtyRect(new Int32Rect(0, 0, _width, _height));
                    _bitmap.Unlock();
                }
            }
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.GetAndRefreshBodyData(_bodies);

                    Body body = _bodies.Where(b => b.IsTracked).FirstOrDefault();

                    if (body != null)
                    {
                        Joint handRight = body.Joints[JointType.HandRight];

                        if (handRight.TrackingState != TrackingState.NotTracked)
                        {
                            CameraSpacePoint handRightPosition = handRight.Position;
                            ColorSpacePoint handRightPoint = _sensor.CoordinateMapper.MapCameraPointToColorSpace(handRightPosition);

                            float x = handRightPoint.X;
                            float y = handRightPoint.Y;

                            if (!float.IsInfinity(x) && ! float.IsInfinity(y))
                            {
                                // DRAW!
                                trail.Points.Add(new Point { X = x, Y = y });

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
