using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using KinectDrawing.PaintAlgorithm;
using KinectDrawing.PaintingSession;
using Microsoft.Kinect;
using Stateless;

namespace KinectDrawing
{
    /// <summary>
    /// Interaction logic for VirtualPainting.xaml
    /// </summary>
    public partial class VirtualPainting : UserControl
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
            Painting,
            ConfirmingLeaving,
            SavingImage,
        };

        private readonly StateMachine<State, Trigger> stateMachine = new StateMachine<State, Trigger>(State.WaitingForPresence);

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

        private BrushColorCycler brushColorCycler = new BrushColorCycler();
        private SolidColorBrush currentBrush;
        private IPaintingSession paintingSession = null;
        private static readonly IDictionary<State, BitmapImage> overlayImages = new Dictionary<State, BitmapImage>
        {
            [State.WaitingForPresence] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_smile.PNG")),
            [State.ConfirmingPresence] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_smile.PNG")),
            [State.Countdown3] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_countdown3.PNG")),
            [State.Countdown2] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_countdown2.PNG")),
            [State.Countdown1] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_countdown1.PNG")),
            [State.Flash] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_flash.PNG")),
            [State.Painting] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_paint.PNG")),
            [State.SavingImage] = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_saved.PNG"))
        };

        public VirtualPainting()
        {
            InitializeComponent();

            Unloaded += (s, e) =>
                {
                    this.colorReader?.Dispose();
                    this.bodyReader?.Dispose();
                    this.sensor?.Close();
                };

            this.timer.Tick += (s, e) =>
                {
                    this.stateMachine.Fire(Trigger.TimerTick);
                };

            ConfigureStateMachine();

            this.sensor = KinectSensor.GetDefault();

            if (this.sensor != null)
            {
                this.sensor.Open();

                this.width = this.sensor.ColorFrameSource.FrameDescription.Width;
                this.height = this.sensor.ColorFrameSource.FrameDescription.Height;

                EnsureStartedColorReader();

                this.bodyReader = this.sensor.BodyFrameSource.OpenReader();
                this.bodyReader.FrameArrived += BodyReader_FrameArrived;

                this.pixels = new byte[this.width * this.height * 4];
                this.bitmap = new WriteableBitmap(this.width, this.height, 96.0, 96.0, PixelFormats.Bgra32, null);

                this.bodies = new Body[this.sensor.BodyFrameSource.BodyCount];

                this.camera.Source = this.bitmap;

                var frameX1 = Settings.BodyPresenceAreaLeftWidthRatio * this.width;
                var frameY1 = Settings.BodyPresenceAreaTopHeightRatio * this.height;
                var frameX2 = Settings.BodyPresenceAreaRightWidthRatio * this.width;
                var frameY2 = Settings.BodyPresenceAreaBottomHeightRatio * this.height;
                this.bodyPresenceArea = new Rect(frameX1, frameY1, frameX2 - frameX1, frameY2 - frameY1);
                if (Settings.IsBodyPresenceDebugModeEnabled)
                {
                    DrawRect(this.bodyPresenceArea, this.hitTestingFrame);
                }
            }

            this.DataContext = this;
        }

        private string GetSavedImagesDirectoryPath()
        {
            return Environment.GetEnvironmentVariable(Settings.SavedImagesDirectoryPathEnvironmentVariableName)
                ?? Environment.CurrentDirectory;
        }

        private void ConfigureStateMachine()
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
                        EnsureStartedColorReader();
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

                        StopColorReader();

                        this.timer.Interval = new TimeSpan(0, 0, 2);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Painting)
                .Ignore(Trigger.PersonLeaves);

            this.stateMachine.Configure(State.Painting)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Painting...");
                        this.overlay.Source = overlayImages[State.Painting];

                        this.currentBrush = this.brushColorCycler.Next();
                        this.paintingSession = CreatePaintingSession();
                        this.userPointer.Visibility = Visibility.Visible;

                        this.timer.Interval = new TimeSpan(0, 0, 15);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.userPointer.Visibility = Visibility.Collapsed;
                    })
                .Permit(Trigger.TimerTick, State.SavingImage)
                .Permit(Trigger.PersonLeaves, State.ConfirmingLeaving);

            this.stateMachine.Configure(State.ConfirmingLeaving)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Confirming leaving...");
                        this.timer.Interval = new TimeSpan(0, 0, 2);
                        this.timer.Start();
                    })
                .OnExit(t =>
                {
                    if (t.Destination == State.WaitingForPresence)
                    {
                        this.paintingSession.ClearCanvas(this.canvas);
                        this.paintingSession = null;
                    }
                })
                .Permit(Trigger.PersonEnters, State.Painting)
                .Permit(Trigger.TimerTick, State.WaitingForPresence)
                .Ignore(Trigger.PersonLeaves);

            this.stateMachine.Configure(State.SavingImage)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Saving image...");
                        this.userPointer.Visibility = Visibility.Collapsed;
                        this.overlay.Source = overlayImages[State.SavingImage];

                        this.paintingSession.SavePainting(this.camera, this.canvas, this.width, this.height, GetSavedImagesDirectoryPath());

                        this.timer.Interval = new TimeSpan(0, 0, 7);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.userPointer.Visibility = Visibility.Visible;
                        this.paintingSession.ClearCanvas(this.canvas);
                        this.paintingSession = null;
                    })
                .Permit(Trigger.TimerTick, State.Painting)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);
        }

        private void EnsureStartedColorReader()
        {
            if (this.colorReader == null)
            {
                this.colorReader = this.sensor.ColorFrameSource.OpenReader();
                this.colorReader.FrameArrived += ColorReader_FrameArrived;
            }

            Contract.Ensures(this.colorReader != null);
        }

        private void StopColorReader()
        {
            this.colorReader?.Dispose();
            this.colorReader = null;
            Contract.Ensures(this.colorReader == null);
        }

        private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            using (ColorFrame frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.CopyConvertedFrameDataToArray(this.pixels, ColorImageFormat.Bgra);

                    this.bitmap.Lock();
                    Marshal.Copy(this.pixels, 0, this.bitmap.BackBuffer, this.pixels.Length);
                    this.bitmap.AddDirtyRect(new Int32Rect(0, 0, this.width, this.height));
                    this.bitmap.Unlock();
                }
            }
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (BodyFrame frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    frame.GetAndRefreshBodyData(this.bodies);

                    Body body = this.bodies.Where(b => b.IsTracked).FirstOrDefault();

                    if (body != null)
                    {
                        if (this.stateMachine.IsInState(State.Painting))
                        {
                            DrawUserPointerIfNeeded(body.Joints[JointType.HandRight]);
                            this.paintingSession.Paint(body, this.currentBrush, this.canvas);
                        }

                        var bodyIsInFrame = BodyIsInFrame(body);
                        if (this.stateMachine.IsInState(State.WaitingForPresence) || this.stateMachine.IsInState(State.ConfirmingLeaving))
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

        private void DrawUserPointerIfNeeded(Joint hand)
        {
            if (hand.TrackingState != TrackingState.NotTracked)
            {
                CameraSpacePoint handPosition = hand.Position;
                ColorSpacePoint handPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(handPosition);

                var x = handPoint.X;
                var y = handPoint.Y;

                if (!float.IsInfinity(x) && !float.IsInfinity(y))
                {
                    Canvas.SetLeft(this.userPointer, x);
                    Canvas.SetTop(this.userPointer, y);
                }
            }
        }

        private IPaintingSession CreatePaintingSession()
        {
            var paintAlgorithm = (IPaintAlgorithm)Activator.CreateInstance(Settings.PaintAlgorithm, this.sensor);
            if (Settings.IsTestModeEnabled)
            {
                return new TestModePaintingSession(this.sensor, paintAlgorithm);
            }
            else
            {
                return new DefaultPaintingSession(this.sensor, paintAlgorithm);
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

            var bodyX1 = shoulderLeftPoint.X;
            var bodyY1 = shoulderLeftPoint.Y;
            var bodyX2 = shoulderRightPoint.X;
            var bodyY2 = shoulderRightPoint.Y;
            if (float.IsInfinity(bodyX1) || float.IsInfinity(bodyY1) || float.IsInfinity(bodyX2) || float.IsInfinity(bodyY2))
            {
                return false;
            }

            var bodyRect = new Rect(bodyX1, bodyY1, Math.Abs(bodyX2 - bodyX1), Math.Abs(bodyY2 - bodyY1));

            if (Settings.IsBodyPresenceDebugModeEnabled)
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
