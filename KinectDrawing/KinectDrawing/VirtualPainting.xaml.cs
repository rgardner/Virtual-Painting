using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
            Snapshot,
            Painting,
            ConfirmingLeaving,
            SavingImage,
        };

        private class Person
        {
            public Person(int bodyIndex, ulong trackingId)
            {
                this.BodyIndex = bodyIndex;
                this.TrackingId = trackingId;
            }

            public int BodyIndex { get; private set; }
            public ulong TrackingId { get; private set; }
        }

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

        private SolidColorBrush currentBrush;
        private IPaintingSession paintingSession = null;
        private Person primaryPerson = null;

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

                this.colorReader = this.sensor.ColorFrameSource.OpenReader();
                this.colorReader.FrameArrived += ColorReader_FrameArrived;

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
                        this.header.Text = "Smile!";
                        this.subHeader.Text = "to capture a base layer image";
                        this.personOutline.Visibility = Visibility.Visible;
                        this.colorReader.IsPaused = false;
                        this.primaryPerson = null;
                    })
                .Permit(Trigger.PersonEnters, State.ConfirmingPresence);

            this.stateMachine.Configure(State.ConfirmingPresence)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Confirming presence...");
                        this.timer.Interval = new TimeSpan(0, 0, 1);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Countdown3)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.stateMachine.Configure(State.Countdown3)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("3...");
                        this.countdownValue.Text = "3";
                        this.countdownValue.Visibility = Visibility.Visible;
                        this.timer.Interval = new TimeSpan(0, 0, 1);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        if (t.Destination == State.WaitingForPresence)
                        {
                            this.countdownValue.Visibility = Visibility.Collapsed;
                        }
                    })
                .Permit(Trigger.TimerTick, State.Countdown2)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.stateMachine.Configure(State.Countdown2)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("2...");
                        this.countdownValue.Text = "2";
                        this.timer.Interval = new TimeSpan(0, 0, 1);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        if (t.Destination == State.WaitingForPresence)
                        {
                            this.countdownValue.Visibility = Visibility.Collapsed;
                        }
                    })
                .Permit(Trigger.TimerTick, State.Countdown1)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.stateMachine.Configure(State.Countdown1)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("1...");
                        this.countdownValue.Text = "1";
                        this.timer.Interval = new TimeSpan(0, 0, 1);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.countdownValue.Visibility = Visibility.Collapsed;
                    })
                .Permit(Trigger.TimerTick, State.Snapshot)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.stateMachine.Configure(State.Snapshot)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Snapshot!");
                        this.header.Text = "Snapshot!";
                        this.personOutline.Visibility = Visibility.Collapsed;
                        FlashWindow();

                        this.colorReader.IsPaused = true;

                        this.timer.Interval = new TimeSpan(0, 0, 2);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Painting)
                .Ignore(Trigger.PersonLeaves);

            this.stateMachine.Configure(State.Painting)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Painting...");
                        this.header.Text = "Construct";
                        this.subHeader.Text = "a new identity with paint";

                        this.currentBrush = GetRandomBrush();
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
                        this.paintingSession.SavePainting(this.camera, this.canvas, this.width, this.height, GetSavedImagesDirectoryPath());
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
                        this.header.Text = "Saved";
                        this.subHeader.Text = "to the iPad for future reference";

                        FlashWindow();
                        this.paintingSession.SavePainting(this.camera, this.canvas, this.width, this.height, GetSavedImagesDirectoryPath());

                        this.timer.Interval = new TimeSpan(0, 0, 7);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.paintingSession.ClearCanvas(this.canvas);
                        this.paintingSession = null;
                    })
                .Permit(Trigger.TimerTick, State.Painting)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);
        }

        private void FlashWindow()
        {
            var sb = (Storyboard)FindResource("FlashAnimation");
            Storyboard.SetTarget(sb, this.flashOverlay);
            sb.Begin();
        }

        private static SolidColorBrush GetRandomBrush()
        {
            var random = new Random();
            return Settings.PaintBrushes[random.Next(Settings.PaintBrushes.Length)];
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

                    if (this.primaryPerson == null)
                    {
                        // If we are not tracking anyone, make the first tracked person in the
                        // frame the primary body.
                        for (int i = 0; i < this.bodies.Count; i++)
                        {
                            var body = this.bodies[i];
                            if (body != null && body.IsTracked && IsBodyInFrame(body))
                            {
                                this.primaryPerson = new Person(i, body.TrackingId);
                                this.stateMachine.Fire(Trigger.PersonEnters);
                                break;
                            }
                        }
                    }
                    else
                    {
                        // If we are tracking someone, check if they are still present and still in
                        // the frame.
                        var primaryBody = this.bodies[this.primaryPerson.BodyIndex];
                        if (primaryBody != null && primaryBody.TrackingId == this.primaryPerson.TrackingId && primaryBody.IsTracked)
                        {
                            if (this.stateMachine.IsInState(State.Painting))
                            {
                                DrawUserPointerIfNeeded(primaryBody.Joints[JointType.HandRight]);
                                this.paintingSession.Paint(primaryBody, this.currentBrush, this.canvas);
                            }

                            var isPrimaryBodyInFrame = IsBodyInFrame(primaryBody);
                            if (this.stateMachine.IsInState(State.ConfirmingLeaving))
                            {
                                if (isPrimaryBodyInFrame)
                                {
                                    this.stateMachine.Fire(Trigger.PersonEnters);
                                }
                            }
                            else if (!isPrimaryBodyInFrame)
                            {
                                this.stateMachine.Fire(Trigger.PersonLeaves);
                            }
                        }
                        else
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
        private bool IsBodyInFrame(Body body)
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

            if (Settings.IsBodyPresenceDebugModeEnabled)
            {
                var bodyRect = new Rect(bodyX1, bodyY1, Math.Abs(bodyX2 - bodyX1), Math.Abs(bodyY2 - bodyY1));
                DrawRect(bodyRect, this.hitTestingBody);
            }

            return this.bodyPresenceArea.Contains(bodyX1, bodyY1) && this.bodyPresenceArea.Contains(bodyX2, bodyY2);
        }

        private static void DrawRect(Rect rect, StackPanel stackPanel)
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
