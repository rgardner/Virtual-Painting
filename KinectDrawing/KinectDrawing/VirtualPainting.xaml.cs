using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    public partial class VirtualPainting : UserControl, INotifyPropertyChanged
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
            Countdown,
            Snapshot,
            HandPickup,
            ConfirmingLeavingHandPickup,
            Painting,
            ConfirmingLeavingPainting,
            SavingImage,
            ConfirmingLeavingSavingImage,
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

        private CountdownTimer countdownTimer = null;
        private string countdownValue = string.Empty;

        private SolidColorBrush currentBrush;
        private IPaintingSession paintingSession = null;
        private TimeSpan? paintingSessionTimeRemaining;
        private DateTime? paintingSessionStartTime;
        private Person primaryPerson = null;

        public VirtualPainting()
        {
            InitializeComponent();

            Unloaded += (s, e) =>
                {
                    this.timer?.Stop();
                    this.countdownTimer?.Stop();

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

        public string CountdownValue
        {
            get => this.countdownValue;

            private set
            {
                if (value != this.countdownValue)
                {
                    this.countdownValue = value;
                    NotifyPropertyChanged();
                }
            }
        }

        /// <summary>
        /// INotifyPropertyChanged event to allow window controls to bind to changeable data.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string GetSavedImagesDirectoryPath()
        {
            return Environment.GetEnvironmentVariable(Settings.SavedImagesDirectoryPathEnvironmentVariableName)
                ?? Environment.CurrentDirectory;
        }

        private string GetSavedBackgroundImagesDirectoryPath()
        {
            return Environment.GetEnvironmentVariable(Settings.SavedBackgroundImagesDirectoryPathEnvironmentVariableName)
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
                        this.timer.Interval = TimeSpan.FromSeconds(1);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Countdown)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.stateMachine.Configure(State.Countdown)
                .OnEntry(t =>
                    {
                        const int initialCountdownValue = 3;
                        Debug.WriteLine($"{initialCountdownValue}...");
                        this.countdownTimer = new CountdownTimer(initialCountdownValue);
                        this.countdownTimer.PropertyChanged += (s, e) =>
                            {
                                if (string.Equals(e.PropertyName, "Value"))
                                {
                                    if (this.countdownTimer.Value == 0)
                                    {
                                        this.stateMachine.Fire(Trigger.TimerTick);
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"{this.countdownTimer.Value}...");
                                        this.CountdownValue = countdownTimer.Value.ToString();
                                    }
                                }
                            };
                        this.countdownTimer.Start();
                        this.CountdownValue = initialCountdownValue.ToString();
                    })
                .OnExit(t =>
                    {
                        this.countdownTimer.Stop();
                        this.countdownTimer = null;
                        this.CountdownValue = string.Empty;
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

                        this.timer.Interval = TimeSpan.FromSeconds(0.75);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.HandPickup)
                .Ignore(Trigger.PersonLeaves);

            this.stateMachine.Configure(State.HandPickup)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Waiting for hand to enter frame...");
                        this.header.Text = "Construct";
                        this.subHeader.Text = "a new identity with paint";
                        this.userPointer.Visibility = Visibility.Visible;

                        // Do not start the timer, this will be started in BodyReader_FrameArrived
                        // when the user pointer has entered the canvas.
                        this.timer.Interval = TimeSpan.FromSeconds(1);
                    })
                .OnExit(t =>
                    {
                        if (t.Destination != State.Painting)
                        {
                            this.userPointer.Visibility = Visibility.Collapsed;
                        }
                    })
                .Permit(Trigger.TimerTick, State.Painting)
                .Permit(Trigger.PersonLeaves, State.ConfirmingLeavingHandPickup);

            this.stateMachine.Configure(State.ConfirmingLeavingHandPickup)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Confirming leaving hand pickup...");
                        this.timer.Interval = TimeSpan.FromSeconds(3);
                        this.timer.Start();
                    })
                .Permit(Trigger.PersonEnters, State.HandPickup)
                .Permit(Trigger.TimerTick, State.WaitingForPresence)
                .Ignore(Trigger.PersonLeaves);

            this.stateMachine.Configure(State.Painting)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Painting...");

                        if (t.Source == State.ConfirmingLeavingPainting)
                        {
                            this.timer.Interval = this.paintingSessionTimeRemaining.Value;
                        }
                        else
                        {
                            this.header.Text = "Construct";
                            this.subHeader.Text = "a new identity with paint";

                            this.currentBrush = GetRandomBrush();
                            this.paintingSession = CreatePaintingSession();

                            // Save start time so we can resume the timer if the person leaves and
                            // re-enters the frame.
                            this.paintingSessionTimeRemaining = null;
                            this.paintingSessionStartTime = DateTime.Now;
                            this.timer.Interval = TimeSpan.FromSeconds(15);
                        }

                        this.userPointer.Visibility = Visibility.Visible;
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.userPointer.Visibility = Visibility.Collapsed;
                        if (t.Destination == State.ConfirmingLeavingPainting)
                        {
                            TimeSpan elapsedPaintingSessionTime = DateTime.Now - this.paintingSessionStartTime.Value;
                            this.paintingSessionTimeRemaining = TimeSpan.FromSeconds(15) - elapsedPaintingSessionTime;
                        }
                    })
                .Permit(Trigger.TimerTick, State.SavingImage)
                .Permit(Trigger.PersonLeaves, State.ConfirmingLeavingPainting);

            this.stateMachine.Configure(State.ConfirmingLeavingPainting)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Confirming leaving...");
                        this.timer.Interval = TimeSpan.FromSeconds(3);
                        this.timer.Start();
                    })
                .OnExit(t =>
                {
                    // Save the painting session if one exists
                    if ((t.Destination == State.WaitingForPresence) && (this.paintingSession != null))
                    {
                        this.paintingSession.SavePainting(this.camera, this.canvas, this.width, this.height,
                            GetSavedImagesDirectoryPath(), GetSavedBackgroundImagesDirectoryPath());
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

                        if (t.Source == State.Painting)
                        {
                            this.header.Text = "Saved";
                            this.subHeader.Text = "to the iPad for future reference";

                            FlashWindow();
                            this.paintingSession.SavePainting(this.camera, this.canvas, this.width, this.height,
                                GetSavedImagesDirectoryPath(), GetSavedBackgroundImagesDirectoryPath());

                            this.timer.Interval = TimeSpan.FromSeconds(7);
                        }
                        else if (t.Source == State.ConfirmingLeavingSavingImage)
                        {
                            this.timer.Interval = TimeSpan.FromSeconds(2);
                        }

                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        if (t.Destination == State.HandPickup)
                        {
                            this.paintingSession.ClearCanvas(this.canvas);
                            this.paintingSession = null;
                        }
                    })
                .Permit(Trigger.TimerTick, State.HandPickup)
                .Permit(Trigger.PersonLeaves, State.ConfirmingLeavingSavingImage);

            this.stateMachine.Configure(State.ConfirmingLeavingSavingImage)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Confirming leaving saving image...");
                        this.timer.Interval = TimeSpan.FromSeconds(3);
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
                .Permit(Trigger.PersonEnters, State.SavingImage)
                .Permit(Trigger.TimerTick, State.WaitingForPresence)
                .Ignore(Trigger.PersonLeaves);
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
                            if (this.stateMachine.IsInState(State.HandPickup) || this.stateMachine.IsInState(State.Painting))
                            {
                                DrawUserPointerIfNeeded(primaryBody.Joints[JointType.HandRight]);
                                if (this.stateMachine.IsInState(State.HandPickup))
                                {
                                    if (!this.timer.IsEnabled && IsJointInCanvasView(primaryBody.Joints[JointType.HandRight]))
                                    {
                                        Debug.WriteLine("Hand entered frame");
                                        this.timer.Start();
                                    }
                                }
                                else if (this.stateMachine.IsInState(State.Painting))
                                {
                                    this.paintingSession.Paint(primaryBody, this.currentBrush, this.canvas);
                                }
                            }

                            var isPrimaryBodyInFrame = IsBodyInFrame(primaryBody);
                            if (this.stateMachine.IsInState(State.ConfirmingLeavingHandPickup)
                                || this.stateMachine.IsInState(State.ConfirmingLeavingPainting)
                                || this.stateMachine.IsInState(State.ConfirmingLeavingSavingImage))
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

        /// <summary>
        /// Determines if the given joint is over the visible portion of the canvas.
        /// </summary>
        /// <param name="joint"></param>
        /// <returns></returns>
        private bool IsJointInCanvasView(Joint joint)
        {
            if (joint.TrackingState == TrackingState.NotTracked)
            {
                return false;
            }

            ColorSpacePoint jointPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(joint.Position);
            if (float.IsInfinity(jointPoint.X) || float.IsInfinity(jointPoint.Y))
            {
                return false;
            }

            Point canvasViewTopLeft = this.canvasView.TranslatePoint(new Point(), this);
            Point canvasViewBottomRight = this.canvasView.TranslatePoint(new Point(this.canvasView.ActualWidth, this.canvasView.ActualHeight), this);
            var canvasViewRect = new Rect(canvasViewTopLeft, canvasViewBottomRight);
            return canvasViewRect.Contains(jointPoint.X, jointPoint.Y);
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
