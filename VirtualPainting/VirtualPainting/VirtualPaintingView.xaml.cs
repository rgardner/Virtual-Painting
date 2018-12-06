using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using VirtualPainting.PaintAlgorithm;
using VirtualPainting.PaintingSession;
using KinectRecorder;
using LightBuzz.Vitruvius;
using Microsoft.Kinect;
using Stateless;
using Stateless.Graph;

namespace VirtualPainting
{
    /// <summary>
    /// Interaction logic for VirtualPaintingView.xaml
    /// </summary>
    public partial class VirtualPaintingView : UserControl, INotifyPropertyChanged
    {
        public enum Trigger
        {
            PersonEnters,
            PersonLeaves,
            TimerTick,
        };

        public enum State
        {
            WaitingForPresence,
            ConfirmingPresence,
            Countdown,
            Snapshot,
            Painting,
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
            public double MedianDistanceFromCameraInMeters { get; set; } = Settings.BodyDistanceFromCameraThresholdInMeters;
            public double ExpectedMaxDistance => MedianDistanceFromCameraInMeters + Settings.BodyDistanceVariationThresholdInMeters;
        }

        private class Skeleton
        {
            private const double PrimaryUserOpacity = 0.50;
            private const double SecondaryUserOpacity = PrimaryUserOpacity;
            private const double HiddenOpacity = 0.0;

            private readonly Line headLine = CreateLine();
            private readonly Line rightShoulderLine = CreateLine();
            private readonly Line leftShoulderLine = CreateLine();
            private readonly Line rightElbowLine = CreateLine();
            private readonly Line leftElbowLine = CreateLine();
            private readonly Line topSpineLine = CreateLine();
            private readonly Line bottomSpineLine = CreateLine();

            private readonly Ellipse headPoint = CreatePoint();
            private readonly Ellipse spineShoulderPoint = CreatePoint();
            private readonly Ellipse spineMidPoint = CreatePoint();
            private readonly Ellipse spineBasePoint = CreatePoint();
            private readonly Ellipse shoulderLeftPoint = CreatePoint();
            private readonly Ellipse shoulderRightPoint = CreatePoint();
            private readonly Ellipse elbowLeftPoint = CreatePoint();
            private readonly Ellipse elbowRightPoint = CreatePoint();

            private readonly Ellipse rightHandPointer;

            public static VirtualPaintingView ParentVP { get; set; }
            public static Canvas CanvasScreen { get; set; }
            public static Rect CameraView { get; set; }

            public Skeleton()
            {
                this.rightHandPointer = new Ellipse()
                {
                    Stroke = Settings.MyGray,
                    Opacity = HiddenOpacity,
                    Width = UserPointerRadiusInitialValue,
                    Height = UserPointerRadiusInitialValue,
                };

                CanvasScreen.Children.Add(this.headLine);
                CanvasScreen.Children.Add(this.rightShoulderLine);
                CanvasScreen.Children.Add(this.leftShoulderLine);
                CanvasScreen.Children.Add(this.rightElbowLine);
                CanvasScreen.Children.Add(this.leftElbowLine);
                CanvasScreen.Children.Add(this.topSpineLine);
                CanvasScreen.Children.Add(this.bottomSpineLine);

                CanvasScreen.Children.Add(this.headPoint);
                CanvasScreen.Children.Add(this.spineShoulderPoint);
                CanvasScreen.Children.Add(this.spineMidPoint);
                CanvasScreen.Children.Add(this.spineBasePoint);
                CanvasScreen.Children.Add(this.shoulderLeftPoint);
                CanvasScreen.Children.Add(this.shoulderRightPoint);
                CanvasScreen.Children.Add(this.elbowLeftPoint);
                CanvasScreen.Children.Add(this.elbowRightPoint);

                CanvasScreen.Children.Add(this.rightHandPointer);
            }

            public void Render(Body body, bool isPrimary)
            {
                if (body.IsTracked && PersonDetector.IsPersonPresent(body, CameraView))
                {
                    DrawJointLine(this.headLine, body.Joints[JointType.Head], body.Joints[JointType.SpineShoulder], isPrimary);
                    DrawJointLine(this.rightShoulderLine, body.Joints[JointType.ShoulderRight], body.Joints[JointType.SpineShoulder], isPrimary);
                    DrawJointLine(this.leftShoulderLine, body.Joints[JointType.ShoulderLeft], body.Joints[JointType.SpineShoulder], isPrimary);
                    DrawJointLine(this.rightElbowLine, body.Joints[JointType.ElbowRight], body.Joints[JointType.ShoulderRight], isPrimary);
                    DrawJointLine(this.leftElbowLine, body.Joints[JointType.ElbowLeft], body.Joints[JointType.ShoulderLeft], isPrimary);
                    DrawJointLine(this.topSpineLine, body.Joints[JointType.SpineShoulder], body.Joints[JointType.SpineMid], isPrimary);
                    DrawJointLine(this.bottomSpineLine, body.Joints[JointType.SpineMid], body.Joints[JointType.SpineBase], isPrimary);

                    DrawJointPoint(this.headPoint, body.Joints[JointType.Head], isPrimary);
                    DrawJointPoint(this.spineShoulderPoint, body.Joints[JointType.SpineShoulder], isPrimary);
                    DrawJointPoint(this.spineMidPoint, body.Joints[JointType.SpineMid], isPrimary);
                    DrawJointPoint(this.spineBasePoint, body.Joints[JointType.SpineBase], isPrimary);
                    DrawJointPoint(this.shoulderLeftPoint, body.Joints[JointType.ShoulderLeft], isPrimary);
                    DrawJointPoint(this.shoulderRightPoint, body.Joints[JointType.ShoulderRight], isPrimary);
                    DrawJointPoint(this.elbowLeftPoint, body.Joints[JointType.ElbowLeft], isPrimary);
                    DrawJointPoint(this.elbowRightPoint, body.Joints[JointType.ElbowRight], isPrimary);

                    DrawHandPointer(body, isPrimary);
                }
                else
                {
                    this.headLine.Opacity = HiddenOpacity;
                    this.rightShoulderLine.Opacity = HiddenOpacity;
                    this.leftShoulderLine.Opacity = HiddenOpacity;
                    this.rightElbowLine.Opacity = HiddenOpacity;
                    this.leftElbowLine.Opacity = HiddenOpacity;
                    this.topSpineLine.Opacity = HiddenOpacity;
                    this.bottomSpineLine.Opacity = HiddenOpacity;
                    this.rightHandPointer.Opacity = HiddenOpacity;

                    this.headPoint.Opacity = HiddenOpacity;
                    this.spineShoulderPoint.Opacity = HiddenOpacity;
                    this.spineMidPoint.Opacity = HiddenOpacity;
                    this.spineBasePoint.Opacity = HiddenOpacity;
                    this.shoulderLeftPoint.Opacity = HiddenOpacity;
                    this.shoulderRightPoint.Opacity = HiddenOpacity;
                    this.elbowLeftPoint.Opacity = HiddenOpacity;
                    this.elbowRightPoint.Opacity = HiddenOpacity;
                }
            }

            private static Line CreateLine()
            {
                return new Line()
                {
                    Opacity = HiddenOpacity,
                    Stroke = Settings.MyGray,
                    StrokeThickness = 5,
                };
            }

            private static Ellipse CreatePoint()
            {
                return new Ellipse()
                {
                    Opacity = HiddenOpacity,
                    Stroke = Settings.MyGray,
                    StrokeThickness = 5,
                };
            }

            private static void DrawJointLine(Line jointLine, Joint firstJoint, Joint secondJoint, bool isPrimary)
            {
                if (firstJoint.TrackingState == TrackingState.NotTracked || secondJoint.TrackingState == TrackingState.NotTracked)
                {
                    jointLine.Opacity = HiddenOpacity;
                }
                else
                {
                    var firstJointPosition = firstJoint.Position.ToPoint(Visualization.Color);
                    var secondJointPosition = secondJoint.Position.ToPoint(Visualization.Color);
                    jointLine.X1 = firstJointPosition.X;
                    jointLine.Y1 = firstJointPosition.Y;
                    jointLine.X2 = secondJointPosition.X;
                    jointLine.Y2 = secondJointPosition.Y;
                    if (isPrimary)
                    {
                        jointLine.Stroke = Settings.MyBlue;
                        jointLine.Opacity = PrimaryUserOpacity;
                    }
                    else
                    {
                        jointLine.Stroke = Settings.MyGray;
                        jointLine.Opacity = SecondaryUserOpacity;
                    }
                }
            }

            private static void DrawJointPoint(Ellipse headPoint, Joint joint, bool isPrimary)
            {
                if (joint.TrackingState == TrackingState.NotTracked)
                {
                    headPoint.Opacity = HiddenOpacity;
                }
                else
                {
                    var jointPoint = joint.Position.ToPoint(Visualization.Color);
                    Canvas.SetLeft(headPoint, jointPoint.X);
                    Canvas.SetTop(headPoint, jointPoint.Y);

                    if (isPrimary)
                    {
                        headPoint.Stroke = Settings.MyBlue;
                        headPoint.Opacity = PrimaryUserOpacity;
                    }
                    else
                    {
                        headPoint.Stroke = Settings.MyGray;
                        headPoint.Opacity = SecondaryUserOpacity;
                    }
                }
            }

            private void DrawHandPointer(Body body, bool isPrimary)
            {
                var rightHand = body.Joints[JointType.HandRight];
                if (rightHand.TrackingState == TrackingState.Tracked)
                {
                    var rightHandPoint = rightHand.Position.ToPoint(Visualization.Color);
                    if (isPrimary)
                    {
                        // Move blue painting pointer
                        ParentVP.UserPointerPositionX = rightHandPoint.X;
                        ParentVP.UserPointerPositionY = rightHandPoint.Y;
                    }

                    if (isPrimary && CameraView.Contains(rightHandPoint))
                    {
                        // Hide the gray right hand dot
                        this.rightHandPointer.Opacity = HiddenOpacity;
                    }
                    else
                    {
                        this.rightHandPointer.Fill = Settings.MyGray;
                        this.rightHandPointer.Width = UserPointerRadiusInitialValue;
                        this.rightHandPointer.Height = UserPointerRadiusInitialValue;
                        this.rightHandPointer.Opacity = isPrimary ? PrimaryUserOpacity : SecondaryUserOpacity;

                        Canvas.SetLeft(this.rightHandPointer, rightHandPoint.X);
                        Canvas.SetTop(this.rightHandPointer, rightHandPoint.Y);
                    }
                }
                else
                {
                    this.rightHandPointer.Opacity = HiddenOpacity;
                }
            }
        }

        private readonly DispatcherTimer timer = new DispatcherTimer();
        private KinectSensor sensor = null;
        private ColorFrameReader colorReader = null;
        private BodyFrameReader bodyReader = null;
        private IList<Body> bodies = null;
        private IList<Skeleton> skeletons = null;

        private int width = 0;
        private int height = 0;
        private byte[] pixels = null;
        private WriteableBitmap bitmap = null;

        private Rect bodyPresenceArea;

        private CountdownTimer countdownTimer = null;

        private SolidColorBrush currentBrush;
        private IPaintingSession paintingSession = null;
        private Person primaryPerson = null;
        private PersonCalibrator personCalibrator;

        private const double UserPointerRadiusInitialValue = 30;
        private const double HumanRatioTolerance = 0.2f;

        public VirtualPaintingView()
        {
            InitializeComponent();

            Loaded += (s, e) =>
                {
                    Point canvasViewTopLeftPoint = this.canvasView.TranslatePoint(new Point(), this);
                    this.bodyPresenceArea = new Rect(canvasViewTopLeftPoint.X, canvasViewTopLeftPoint.Y, this.canvasView.ActualWidth, this.canvasView.ActualHeight);

                    if (Settings.IsDebugViewEnabled)
                    {
                        DrawRect(this.bodyPresenceArea, this.hitTestingFrame);
                    }

                    Skeleton.CanvasScreen = this.userPointerCanvas;
                    Skeleton.CameraView = this.bodyPresenceArea;

                    for (int i = 0; i < this.skeletons.Count; i++)
                    {
                        this.skeletons[i] = new Skeleton();
                    }
                };

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
                    this.StateMachine.Fire(Trigger.TimerTick);
                };

            ConfigureStateMachine();

            if (Settings.GenerateStateMachineGraph)
            {
                string stateMachineGraph = UmlDotGraph.Format(this.StateMachine.GetInfo());
                Debug.WriteLine(stateMachineGraph);
            }

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
                this.skeletons = new Skeleton[this.bodies.Count];

                this.camera.Source = this.bitmap;
            }

            Skeleton.ParentVP = this;
            this.DataContext = this;
        }

        public string HeaderText { get; private set; } = Properties.Resources.WaitingForPresenceHeader;

        public string SubHeaderText { get; private set; } = Properties.Resources.WaitingForPresenceSubHeader;

        public string CountdownValue { get; private set; }

        public ObservableCollection<PersonDetectionState> PersonDetectionStates { get; set; } = new ObservableCollection<PersonDetectionState>();

        public bool IsUserTakingPicture { get; private set; } = true;

        public bool IsUserPainting { get; private set; }

        public double UserPointerPositionX { get; private set; }

        public double UserPointerPositionY { get; private set; }

        public StateMachine<State, Trigger> StateMachine { get; } = new StateMachine<State, Trigger>(State.WaitingForPresence);

#pragma warning disable CS0067 // PropertyChanged is used by Fody-generated property setters
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

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
            this.StateMachine.OnTransitioned(t =>
                {
                    SetHeadersForState(t.Destination);
                    this.timer.Stop();
                });

            this.StateMachine.Configure(State.WaitingForPresence)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Waiting for presence...");

                        if (!Settings.IsTestModeEnabled)
                        {
                            this.IsUserTakingPicture = true;
                            this.colorReader.IsPaused = false;
                        }

                        this.primaryPerson = null;
                    })
                .Permit(Trigger.PersonEnters, State.ConfirmingPresence);

            this.StateMachine.Configure(State.ConfirmingPresence)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Confirming presence...");
                        this.personCalibrator = new PersonCalibrator();
                        this.timer.Interval = TimeSpan.FromSeconds(1);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.primaryPerson.MedianDistanceFromCameraInMeters = this.personCalibrator.CalculateMedianDistance();
                        this.personCalibrator = null;
                    })
                .Permit(Trigger.TimerTick, State.Countdown)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.StateMachine.Configure(State.Countdown)
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
                                        this.StateMachine.Fire(Trigger.TimerTick);
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

            this.StateMachine.Configure(State.Snapshot)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Snapshot!");
                        FlashWindow();

                        if (!Settings.IsTestModeEnabled)
                        {
                            this.IsUserTakingPicture = false;
                            this.colorReader.IsPaused = true;
                        }

                        this.timer.Interval = TimeSpan.FromSeconds(0.75);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Painting)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.StateMachine.Configure(State.Painting)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Painting...");

                        ResetUserPointerPosition();
                        this.IsUserPainting = true;

                        this.currentBrush = GetRandomBrush();
                        this.paintingSession = CreatePaintingSession();
                        this.timer.Interval = TimeSpan.FromSeconds(10);

                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.IsUserPainting = false;

                        // Save the painting session if one exists
                        if ((t.Destination == State.WaitingForPresence) && (this.paintingSession != null))
                        {
                            this.paintingSession.SavePainting(this.camera, this.canvas, this.width, this.height,
                                GetSavedImagesDirectoryPath(), GetSavedBackgroundImagesDirectoryPath());
                            this.paintingSession.ClearCanvas(this.canvas);
                            this.paintingSession = null;
                        }
                    })
                .Permit(Trigger.TimerTick, State.SavingImage)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);

            this.StateMachine.Configure(State.SavingImage)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Saving image...");

                        FlashWindow();
                        this.paintingSession.SavePainting(this.camera, this.canvas, this.width, this.height,
                            GetSavedImagesDirectoryPath(), GetSavedBackgroundImagesDirectoryPath());

                        this.timer.Interval = TimeSpan.FromSeconds(4);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.paintingSession.ClearCanvas(this.canvas);
                        this.paintingSession = null;
                    })
                .Permit(Trigger.TimerTick, State.WaitingForPresence)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence);
        }

        private void SetHeadersForState(State state)
        {
            switch (state)
            {
                case State.WaitingForPresence:
                case State.ConfirmingPresence:
                case State.Countdown:
                    this.HeaderText = Properties.Resources.WaitingForPresenceHeader;
                    this.SubHeaderText = Properties.Resources.WaitingForPresenceSubHeader;
                    break;
                case State.Snapshot:
                    this.HeaderText = Properties.Resources.SnapshotHeader;
                    this.SubHeaderText = string.Empty;
                    break;
                case State.Painting:
                    this.HeaderText = Properties.Resources.PaintingHeader;
                    this.SubHeaderText = Properties.Resources.PaintingSubHeader;
                    break;
                case State.SavingImage:
                    this.HeaderText = Properties.Resources.SavingImageHeader;
                    this.SubHeaderText = Properties.Resources.SavingImageSubHeader;
                    break;
                default:
                    Debug.Assert(false, $"Unknown State: {state}");
                    break;
            }
        }

        private void FlashWindow()
        {
            var sb = (Storyboard)FindResource("FlashAnimation");
            Storyboard.SetTarget(sb, this.flashOverlay);
            sb.Begin();
        }

        private void ResetUserPointerPosition()
        {
            this.UserPointerPositionX = (this.width / 2) + 100;
            this.UserPointerPositionY = this.height / 2;
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
                    if (Settings.IsDebugViewEnabled)
                    {
                        RefreshPersonDetectionStates();
                    }

                    DrawSkeletons();

                    if (this.primaryPerson == null)
                    {
                        // If we are not tracking anyone, make the first tracked person in the
                        // frame the primary body.
                        for (int i = 0; i < this.bodies.Count; i++)
                        {
                            var body = this.bodies[i];
                            if (body != null && body.IsTracked && PersonDetector.IsPersonPresent(body, this.bodyPresenceArea))
                            {
                                this.primaryPerson = new Person(i, body.TrackingId);
                                this.StateMachine.Fire(Trigger.PersonEnters);
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
                            var isPrimaryPersonPresent = PersonDetector.IsPersonPresent(primaryBody, this.bodyPresenceArea, this.primaryPerson.ExpectedMaxDistance);
                            if (isPrimaryPersonPresent)
                            {
                                // Primary person is in the frame and is a valid distance from the camera.
                                if (this.StateMachine.IsInState(State.ConfirmingPresence))
                                {
                                    // Calibrate the primary person's distance
                                    this.personCalibrator.AddDistanceFromCamera(primaryBody.DistanceFromSensor());
                                }
                                else if (this.StateMachine.IsInState(State.Painting))
                                {
                                    var primaryBodyAsSensorBody = new SensorBody(primaryBody, this.sensor);
                                    var frameAsSensorFrame = new SensorBodyFrame(frame, this.sensor);
                                    this.paintingSession.Paint(primaryBodyAsSensorBody, this.currentBrush, this.canvas, frameAsSensorFrame);
                                }
                            }
                            else
                            {
                                this.StateMachine.Fire(Trigger.PersonLeaves);
                            }
                        }
                        else
                        {
                            this.StateMachine.Fire(Trigger.PersonLeaves);
                        }
                    }
                }
            }
        }

        private void DrawSkeletons()
        {
            for (int i = 0; i < this.bodies.Count; i++)
            {
                this.skeletons[i].Render(this.bodies[i], i == this.primaryPerson?.BodyIndex);
            }
        }

        private void RefreshPersonDetectionStates()
        {
            if (Settings.IsDebugViewEnabled)
            {
                for (int i = 0; i < this.bodies.Count; i++)
                {
                    Body body = this.bodies[i];
                    PersonDetectionState personDetectionState = this.PersonDetectionStates.Where(b => b.BodyIndex == i).FirstOrDefault();
                    if (body == null || !body.IsTracked)
                    {
                        // Remove previously tracked person
                        if (personDetectionState != null)
                        {
                            this.PersonDetectionStates.Remove(personDetectionState);
                        }
                    }
                    else
                    {
                        bool isPrimary = (i == this.primaryPerson?.BodyIndex);
                        if (personDetectionState == null)
                        {
                            // Add new person
                            this.PersonDetectionStates.Add(new PersonDetectionState(i, isPrimary, body, this.bodyPresenceArea));
                        }
                        else
                        {
                            // Refresh existing person
                            personDetectionState.Refresh(isPrimary, body, this.bodyPresenceArea);                            
                        }
                    }
                }
            }
        }

        private void UpdatePersonDetectionStatesIfNeeded()
        {
        }

        private IPaintingSession CreatePaintingSession()
        {
            var paintAlgorithm = (IPaintAlgorithm)Activator.CreateInstance(Settings.PaintAlgorithm);
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
