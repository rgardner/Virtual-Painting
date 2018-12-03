using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using KinectRecorder;
using LightBuzz.Vitruvius;
using Microsoft.Kinect;
using Stateless;
using Stateless.Graph;

namespace KinectDrawing
{
    /// <summary>
    /// Interaction logic for VirtualPainting.xaml
    /// </summary>
    public partial class VirtualPainting : UserControl, INotifyPropertyChanged
    {
        public enum Trigger
        {
            PersonEnters,
            PersonLeaves,
            TimerTick,
            NewUserSelected,
        };

        public enum State
        {
            WaitingForPresence,
            ConfirmingPresence,
            Countdown,
            Snapshot,
            HandPickup,
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

        public class PersonDetectionState : BindableBase
        {
            private int bodyIndex;
            private bool isPrimary = false;
            private bool isHuman = false;
            private bool isInFrame = false;
            private string distanceFromSensor = string.Empty;
            private int trackedJointCount = 0;
            private int selectingNewUserButtonFrameCount = 0;

            public PersonDetectionState(int bodyIndex, bool? isPrimary = null, Body body = null, Rect? bodyPresenceArea = null, Rect? newUserButtonArea = null)
            {
                this.BodyIndex = bodyIndex;
                if (isPrimary.HasValue && body != null && bodyPresenceArea.HasValue)
                {
                    Refresh(isPrimary.Value, body, bodyPresenceArea.Value, newUserButtonArea.Value);
                }
            }

            public int BodyIndex
            {
                get => this.bodyIndex;
                set
                {
                    if (value != this.bodyIndex)
                    {
                        this.bodyIndex = value;
                        NotifyPropertyChanged();
                    }
                }
            }

            public bool IsPrimary
            {
                get => this.isPrimary;
                set
                {
                    if (value != this.isPrimary)
                    {
                        this.isPrimary = value;
                        NotifyPropertyChanged();
                    }
                }
            }

            public bool IsHuman
            {
                get => this.isHuman;
                set
                {
                    if (value != this.isHuman)
                    {
                        this.isHuman = value;
                        NotifyPropertyChanged();
                    }
                }
            }

            public bool IsInFrame
            {
                get => this.isInFrame;
                set
                {
                    if (value != this.isInFrame)
                    {
                        this.isInFrame = value;
                        NotifyPropertyChanged();
                    }
                }
            }

            public string DistanceFromSensor
            {
                get => this.distanceFromSensor;
                set
                {
                    if (value != this.distanceFromSensor)
                    {
                        this.distanceFromSensor = value;
                        NotifyPropertyChanged();
                    }
                }
            }

            public int TrackedJointCount
            {
                get => this.trackedJointCount;
                set
                {
                    if (value != this.trackedJointCount)
                    {
                        this.trackedJointCount = value;
                        NotifyPropertyChanged();
                    }
                }
            }

            public int SelectingNewUserButtonFrameCount
            {
                get => this.selectingNewUserButtonFrameCount;
                set
                {
                    if (value != this.selectingNewUserButtonFrameCount)
                    {
                        this.selectingNewUserButtonFrameCount = value;
                        NotifyPropertyChanged();
                    }
                }
            }

            public void Refresh(bool isPrimary, Body body, Rect bodyPresenceArea, Rect newUserButtonArea)
            {
                this.IsPrimary = isPrimary;
                this.IsHuman = body.IsHuman();
                this.IsInFrame = PersonDetector.IsInFrame(body, bodyPresenceArea);
                this.DistanceFromSensor = body.DistanceFromSensor().ToString("0.00");
                this.TrackedJointCount = body.TrackedJoints(includeInferred: false).Count();

                var handPoint = body.Joints[JointType.HandRight].Position.ToPoint(Visualization.Color);
                if (newUserButtonArea.Contains(handPoint))
                {
                    this.SelectingNewUserButtonFrameCount++;
                }
                else
                {
                    this.SelectingNewUserButtonFrameCount = 0;
                }

            }

            public static bool operator==(PersonDetectionState first, PersonDetectionState second)
            {
                if (first is null && second is null)
                {
                    return true;
                }
                else if (first is null || second is null)
                {
                    return false;
                }

                return (first.IsHuman == second.IsHuman)
                    && (first.IsHuman == second.IsHuman)
                    && (first.IsInFrame == second.IsInFrame)
                    && (first.DistanceFromSensor == second.DistanceFromSensor);
            }

            public static bool operator!=(PersonDetectionState first, PersonDetectionState second)
            {
                return !(first == second);
            }

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                var hashCode = -1494063407;
                hashCode = hashCode * -1521134295 + this.IsHuman.GetHashCode();
                hashCode = hashCode * -1521134295 + this.IsInFrame.GetHashCode();
                hashCode = hashCode * -1521134295 + this.DistanceFromSensor.GetHashCode();
                return hashCode;
            }
        }

        private class Skeleton
        {
            private const double PrimaryUserOpacity = 0.25;
            private const double SecondaryUserOpacity = 0.10;
            private const double SelectNewUserOpacity = 1.0;
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

            public static VirtualPainting ParentVP { get; set; }
            public static Canvas CanvasScreen { get; set; }
            public static Rect CameraView { get; set; }
            public static Rect NewUserButton { get; set; }

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
                if (body.IsTracked)
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
                            if (NewUserButton.Contains(rightHandPoint))
                            {
                                this.rightHandPointer.Fill = Settings.MyBurntOrange;
                                this.rightHandPointer.Opacity = SelectNewUserOpacity;

                                --this.rightHandPointer.Width;
                                --this.rightHandPointer.Height;
                                if (this.rightHandPointer.Width == 0 || this.rightHandPointer.Height == 0)
                                {
                                    this.rightHandPointer.Width = UserPointerRadiusInitialValue;
                                    this.rightHandPointer.Height = UserPointerRadiusInitialValue;
                                    ParentVP.StateMachine.Fire(Trigger.NewUserSelected);
                                }
                            }
                            else
                            {
                                this.rightHandPointer.Fill = Settings.MyGray;
                                this.rightHandPointer.Width = UserPointerRadiusInitialValue;
                                this.rightHandPointer.Height = UserPointerRadiusInitialValue;
                                this.rightHandPointer.Opacity = isPrimary ? PrimaryUserOpacity : SecondaryUserOpacity;
                            }

                            Canvas.SetLeft(this.rightHandPointer, rightHandPoint.X);
                            Canvas.SetTop(this.rightHandPointer, rightHandPoint.Y);
                        }
                    }
                    else
                    {
                        this.rightHandPointer.Opacity = HiddenOpacity;
                    }
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

        private Rect cameraViewRect;
        private Rect bodyPresenceArea; // TODO: Remove and replace with cameraViewRect
        private Rect pointerZoneRect;
        private Rect newUserButtonRect;

        private CountdownTimer countdownTimer = null;
        private string countdownValue = string.Empty;

        private SolidColorBrush currentBrush;
        private IPaintingSession paintingSession = null;
        private Person primaryPerson = null;
        private PersonCalibrator personCalibrator;
        private string headerText = Properties.Resources.WaitingForPresenceHeader;
        private string subHeaderText = Properties.Resources.WaitingForPresenceSubHeader;
        private Visibility personOutlineVisibility = Visibility.Visible;
        private Visibility userPointerVisibility = Visibility.Collapsed;

        private const double UserPointerRadiusInitialValue = 30;
        private double userPointerPositionX = 0.0;
        private double userPointerPositionY = 0.0;

        private const double HumanRatioTolerance = 0.2f;

        private ObservableCollection<PersonDetectionState> personDetectionStates = new ObservableCollection<PersonDetectionState>();

        public VirtualPainting()
        {
            InitializeComponent();

            Loaded += (s, e) =>
                {
                    Point newUserButtonTopLeftPoint = this.newUserButton.TranslatePoint(new Point(), this);
                    this.newUserButtonRect = new Rect(newUserButtonTopLeftPoint.X, newUserButtonTopLeftPoint.Y, this.newUserButton.ActualWidth, this.newUserButton.ActualHeight);

                    Point pointerZoneTopLeftPoint = this.pointerZone.TranslatePoint(new Point(), this);
                    this.pointerZoneRect = new Rect(pointerZoneTopLeftPoint.X, pointerZoneTopLeftPoint.Y, this.pointerZone.ActualWidth, this.pointerZone.ActualHeight);

                    Point canvasViewTopLeftPoint = this.canvasView.TranslatePoint(new Point(), this);
                    this.cameraViewRect = new Rect(canvasViewTopLeftPoint.X, canvasViewTopLeftPoint.Y, this.canvasView.ActualWidth, this.canvasView.ActualHeight);

                    Skeleton.CanvasScreen = this.userPointerCanvas;
                    Skeleton.CameraView = this.cameraViewRect;
                    Skeleton.NewUserButton = this.newUserButtonRect;

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

                var frameX1 = Settings.BodyPresenceAreaLeftWidthRatio * this.width;
                var frameY1 = Settings.BodyPresenceAreaTopHeightRatio * this.height;
                var frameX2 = Settings.BodyPresenceAreaRightWidthRatio * this.width;
                var frameY2 = Settings.BodyPresenceAreaBottomHeightRatio * this.height;
                this.bodyPresenceArea = new Rect(frameX1, frameY1, frameX2 - frameX1, frameY2 - frameY1);
                if (Settings.IsDebugViewEnabled)
                {
                    DrawRect(this.bodyPresenceArea, this.hitTestingFrame);
                }

            }

            Skeleton.ParentVP = this;
            this.DataContext = this;
        }

        public string HeaderText
        {
            get => this.headerText;

            private set
            {
                if (value != this.headerText)
                {
                    this.headerText = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public string SubHeaderText
        {
            get => this.subHeaderText;

            private set
            {
                if (value != this.subHeaderText)
                {
                    this.subHeaderText = value;
                    NotifyPropertyChanged();
                }
            }
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

        public ObservableCollection<PersonDetectionState> PersonDetectionStates
        {
            get => this.personDetectionStates;

            private set
            {
                if (value != this.personDetectionStates)
                {
                    this.personDetectionStates = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public Visibility PersonOutlineVisibility
        {
            get => this.personOutlineVisibility;

            private set
            {
                if (value != this.personOutlineVisibility)
                {
                    this.personOutlineVisibility = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public Visibility UserPointerVisibility
        {
            get => this.userPointerVisibility;

            private set
            {
                if (value != this.userPointerVisibility)
                {
                    this.userPointerVisibility = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public double UserPointerPositionX
        {
            get => this.userPointerPositionX;

            private set
            {
                if (value != this.userPointerPositionX)
                {
                    this.userPointerPositionX = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public double UserPointerPositionY
        {
            get => this.userPointerPositionY;

            private set
            {
                if (value != this.userPointerPositionY)
                {
                    this.userPointerPositionY = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public StateMachine<State, Trigger> StateMachine { get; } = new StateMachine<State, Trigger>(State.WaitingForPresence);

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
                            this.PersonOutlineVisibility = Visibility.Visible;
                            this.colorReader.IsPaused = false;
                        }

                        this.primaryPerson = null;
                    })
                .Permit(Trigger.PersonEnters, State.ConfirmingPresence)
                .Ignore(Trigger.NewUserSelected);

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
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence)
                .Permit(Trigger.NewUserSelected, State.WaitingForPresence);

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
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence)
                .Permit(Trigger.NewUserSelected, State.WaitingForPresence);

            this.StateMachine.Configure(State.Snapshot)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Snapshot!");
                        FlashWindow();

                        if (!Settings.IsTestModeEnabled)
                        {
                            this.PersonOutlineVisibility = Visibility.Collapsed;
                            this.colorReader.IsPaused = true;
                        }

                        this.timer.Interval = TimeSpan.FromSeconds(0.75);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.HandPickup)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence)
                .Permit(Trigger.NewUserSelected, State.WaitingForPresence);

            this.StateMachine.Configure(State.HandPickup)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Waiting for hand to enter frame...");
                        this.UserPointerVisibility = Visibility.Visible;

                        // Do not start the timer, this will be started in BodyReader_FrameArrived
                        // when the user pointer has entered the canvas.
                        this.timer.Interval = TimeSpan.FromSeconds(2);
                    })
                .OnExit(t =>
                    {
                        if (t.Destination != State.Painting)
                        {
                            this.UserPointerVisibility = Visibility.Collapsed;
                        }
                    })
                .Permit(Trigger.TimerTick, State.Painting)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence)
                .Permit(Trigger.NewUserSelected, State.WaitingForPresence);

            this.StateMachine.Configure(State.Painting)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Painting...");

                        this.currentBrush = GetRandomBrush();
                        this.paintingSession = CreatePaintingSession();
                        this.timer.Interval = TimeSpan.FromSeconds(15);

                        this.UserPointerVisibility = Visibility.Visible;
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.UserPointerVisibility = Visibility.Collapsed;

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
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence)
                .Permit(Trigger.NewUserSelected, State.WaitingForPresence);

            this.StateMachine.Configure(State.SavingImage)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Saving image...");

                        FlashWindow();
                        this.paintingSession.SavePainting(this.camera, this.canvas, this.width, this.height,
                            GetSavedImagesDirectoryPath(), GetSavedBackgroundImagesDirectoryPath());

                        this.timer.Interval = TimeSpan.FromSeconds(7);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.paintingSession.ClearCanvas(this.canvas);
                        this.paintingSession = null;
                    })
                .Permit(Trigger.TimerTick, State.HandPickup)
                .Permit(Trigger.PersonLeaves, State.WaitingForPresence)
                .Permit(Trigger.NewUserSelected, State.WaitingForPresence);
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
                case State.HandPickup:
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
                                else if (this.StateMachine.IsInState(State.HandPickup) || this.StateMachine.IsInState(State.Painting))
                                {
                                    if (this.StateMachine.IsInState(State.HandPickup))
                                    {
                                        if (!this.timer.IsEnabled && IsJointInCanvasView(primaryBody.Joints[JointType.HandRight]))
                                        {
                                            Debug.WriteLine("Hand entered frame");
                                            this.timer.Start();
                                        }
                                    }
                                    else if (this.StateMachine.IsInState(State.Painting))
                                    {
                                        var primaryBodyAsSensorBody = new SensorBody(primaryBody, this.sensor);
                                        var frameAsSensorFrame = new SensorBodyFrame(frame, this.sensor);
                                        this.paintingSession.Paint(primaryBodyAsSensorBody, this.currentBrush, this.canvas, frameAsSensorFrame);
                                    }
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
                            this.PersonDetectionStates.Add(new PersonDetectionState(i, isPrimary, body, this.bodyPresenceArea, this.newUserButtonRect));
                        }
                        else
                        {
                            // Refresh existing person
                            personDetectionState.Refresh(isPrimary, body, this.bodyPresenceArea, this.newUserButtonRect);                            
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
