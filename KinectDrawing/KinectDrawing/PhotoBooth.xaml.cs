using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Kinect;
using Stateless;

namespace KinectDrawing
{
    /// <summary>
    /// Interaction logic for PhotoBooth.xaml
    /// </summary>
    public partial class PhotoBooth : UserControl
    {
        private enum Trigger
        {
            PersonEnters,
            PersonLeaves,
            TimerTick,
        };

        private enum State
        {
            WaitingForPresence,
            ConfirmingPresence,
            Countdown3,
            Countdown2,
            Countdown1,
            Flash,
            Done,
        };

        private readonly StateMachine<State, Trigger> stateMachine;
        private State currentState = State.WaitingForPresence;

        private readonly DispatcherTimer timer = new DispatcherTimer();

        private KinectSensor sensor = null;
        private ColorFrameReader colorReader = null;
        private BodyFrameReader bodyReader = null;
        private IList<Body> bodies = null;

        private int width = 0;
        private int height = 0;
        private byte[] pixels = null;
        private WriteableBitmap bitmap = null;

        private Rect bodyPresenceArea;

        private static readonly IDictionary<State, BitmapImage> overlayImages = new Dictionary<State, BitmapImage>
        {
            [State.WaitingForPresence] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_smile.PNG")),
            [State.ConfirmingPresence] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_smile.PNG")),
            [State.Countdown3] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_countdown3.PNG")),
            [State.Countdown2] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_countdown2.PNG")),
            [State.Countdown1] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_countdown1.PNG")),
            [State.Flash] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_flash.PNG"))
        };

        public PhotoBooth()
        {
            InitializeComponent();

            Unloaded += (s, e) =>
                {
                    colorReader?.Dispose();
                    bodyReader?.Dispose();
                    sensor?.Close();
                };

            this.timer.Tick += (s, e) =>
                {
                    this.stateMachine.Fire(Trigger.TimerTick);
                };

            this.stateMachine = new StateMachine<State, Trigger>(() => this.currentState, s => this.currentState = s);
            ConfigureStateMachine();

            this.sensor = KinectSensor.GetDefault();

            if (sensor != null)
            {
                this.sensor.Open();

                this.width = sensor.ColorFrameSource.FrameDescription.Width;
                this.height = sensor.ColorFrameSource.FrameDescription.Height;

                this.colorReader = sensor.ColorFrameSource.OpenReader();
                this.colorReader.FrameArrived += ColorReader_FrameArrived;

                this.bodyReader = sensor.BodyFrameSource.OpenReader();
                this.bodyReader.FrameArrived += BodyReader_FrameArrived;

                this.pixels = new byte[width * height * 4];
                this.bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Bgra32, null);

                this.bodies = new Body[sensor.BodyFrameSource.BodyCount];

                this.camera.Source = bitmap;

                double frameX1 = ConfigurationConstants.BodyPresenceAreaLeftWidthRatio * this.width;
                double frameY1 = ConfigurationConstants.BodyPresenceAreaTopHeightRatio * this.height;
                double frameX2 = ConfigurationConstants.BodyPresenceAreaRightWidthRatio * this.width;
                double frameY2 = ConfigurationConstants.BodyPresenceAreaBottomHeightRatio * this.height;
                this.bodyPresenceArea = new Rect(frameX1, frameY1, frameX2 - frameX1, frameY2 - frameY1);
                if (ConfigurationConstants.ShouldDisplayBodyPresenceAreas)
                {
                    DrawRect(this.bodyPresenceArea, this.hitTestingFrame);
                }
            }
        }

        void ConfigureStateMachine()
        {
            this.stateMachine.OnTransitioned(t =>
                {
                    this.timer.Stop();
                });

            this.stateMachine.Configure(State.WaitingForPresence)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Waiting for presence...");
                        this.overlay.Source = overlayImages[State.WaitingForPresence];
                    })
                .Permit(Trigger.PersonEnters, State.ConfirmingPresence);

            this.stateMachine.Configure(State.ConfirmingPresence)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Confirming presence...");
                        this.overlay.Source = overlayImages[State.ConfirmingPresence];
                        this.timer.Interval = new TimeSpan(0, 0, 2);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Countdown3)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.stateMachine.Configure(State.Countdown3)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("3...");
                        this.overlay.Source = overlayImages[State.Countdown3];
                        this.timer.Interval = new TimeSpan(0, 0, 1);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Countdown2)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.stateMachine.Configure(State.Countdown2)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("2...");
                        this.overlay.Source = overlayImages[State.Countdown2];
                        this.timer.Interval = new TimeSpan(0, 0, 1);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Countdown1)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.stateMachine.Configure(State.Countdown1)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("1...");
                        this.overlay.Source = overlayImages[State.Countdown1];
                        this.timer.Interval = new TimeSpan(0, 0, 1);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Flash)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.stateMachine.Configure(State.Flash)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Flash!");
                        this.overlay.Source = overlayImages[State.Flash];
                        this.timer.Interval = new TimeSpan(0, 0, 1);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Done)
                .Ignore(Trigger.PersonLeaves);

            this.stateMachine.Configure(State.Done)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Navigating to painting page...");
                        var mainWindow = (MainWindow)Window.GetWindow(this);
                        mainWindow.currentPage.Children.Clear();
                        mainWindow.currentPage.Children.Add(new VirtualPainting(this.bitmap));
                    })
                .Ignore(Trigger.PersonLeaves);
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.CopyConvertedFrameDataToArray(pixels, ColorImageFormat.Bgra);

                    this.bitmap.Lock();
                    Marshal.Copy(pixels, 0, this.bitmap.BackBuffer, this.pixels.Length);
                    this.bitmap.AddDirtyRect(new Int32Rect(0, 0, this.width, this.height));
                    this.bitmap.Unlock();
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

                    Body body = this.bodies.Where(b => b.IsTracked).FirstOrDefault();

                    if (body != null)
                    {
                        Joint handRight = body.Joints[JointType.HandRight];
                        DrawCursorIfNeeded(handRight);

                        bool bodyIsInFrame = BodyIsInFrame(body);
                        if (this.currentState == State.WaitingForPresence)
                        {
                            if (bodyIsInFrame)
                            {
                                this.stateMachine.Fire(Trigger.PersonEnters);
                            }
                        }
                        else if (!bodyIsInFrame)
                        {
                            this.stateMachine.Fire(Trigger.PersonLeaves);
                        }
                    }
                }
            }
        }

        private void DrawCursorIfNeeded(Joint hand)
        {
            if (hand.TrackingState != TrackingState.NotTracked)
            {
                CameraSpacePoint handPosition = hand.Position;
                ColorSpacePoint handPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(handPosition);

                float x = handPoint.X;
                float y = handPoint.Y;

                if (!float.IsInfinity(x) && ! float.IsInfinity(y))
                {
                    Canvas.SetLeft(this.brush, x - this.brush.Width / 2.0);
                    Canvas.SetTop(this.brush, y - this.brush.Height);
                }
            }
        }

        /// <summary>
        /// Returns true if both shoulders are in the frame.
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        private bool BodyIsInFrame(Body body)
        {
            Joint shoulderLeft = body.Joints[JointType.ShoulderLeft];
            Joint shoulderRight = body.Joints[JointType.ShoulderRight];

            if ((shoulderLeft.TrackingState == TrackingState.NotTracked) || (shoulderRight.TrackingState == TrackingState.NotTracked))
            {
                return false;
            }

            ColorSpacePoint shoulderLeftPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(shoulderLeft.Position);
            ColorSpacePoint shoulderRightPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(shoulderRight.Position);

            float bodyX1 = shoulderLeftPoint.X;
            float bodyY1 = shoulderLeftPoint.Y;
            float bodyX2 = shoulderRightPoint.X;
            float bodyY2 = shoulderRightPoint.Y;
            if (float.IsInfinity(bodyX1) || float.IsInfinity(bodyY1) || float.IsInfinity(bodyX2) || float.IsInfinity(bodyY2))
            {
                return false;
            }

            var bodyRect = new Rect(bodyX1, bodyY1, Math.Abs(bodyX2 - bodyX1), Math.Abs(bodyY2 - bodyY1));

            if (ConfigurationConstants.ShouldDisplayBodyPresenceAreas)
            {
                DrawRect(bodyRect, this.hitTestingBody);
            }

            return bodyRect.IntersectsWith(this.bodyPresenceArea);
        }

        private void DrawRect(Rect rect, StackPanel stackPanel)
        {
            stackPanel.Children.Clear();

            var sp = new StackPanel()
            {
                Margin = new Thickness(-5, -5, 0, 0),
                Orientation = Orientation.Horizontal
            };
            sp.Children.Add(new Rectangle()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Height = rect.Height,
                Width = rect.Width,
                Margin = new Thickness(20),
                Stroke = Brushes.Yellow,
            });
            stackPanel.Children.Add(sp);

            Canvas.SetLeft(stackPanel, rect.Left);
            Canvas.SetTop(stackPanel, rect.Top);
        }
    }
}
