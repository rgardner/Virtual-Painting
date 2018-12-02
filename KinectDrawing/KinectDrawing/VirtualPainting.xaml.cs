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

            public PersonDetectionState(int bodyIndex, bool? isPrimary = null, Body body = null, Rect? bodyPresenceArea = null)
            {
                this.BodyIndex = bodyIndex;
                if (isPrimary.HasValue && body != null && bodyPresenceArea.HasValue)
                {
                    Refresh(isPrimary.Value, body, bodyPresenceArea.Value);
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

            public void Refresh(bool isPrimary, Body body, Rect bodyPresenceArea)
            {
                this.IsPrimary = isPrimary;
                this.IsHuman = body.IsHuman();
                this.IsInFrame = PersonDetector.IsInFrame(body, bodyPresenceArea);
                this.DistanceFromSensor = body.DistanceFromSensor().ToString("0.00");
                this.TrackedJointCount = body.TrackedJoints(includeInferred: false).Count();
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
        private PersonCalibrator personCalibrator;
        private string headerText = Properties.Resources.WaitingForPresenceHeader;
        private string subHeaderText = Properties.Resources.WaitingForPresenceSubHeader;
        private Visibility personOutlineVisibility = Visibility.Visible;
        private Visibility userPointerVisibility = Visibility.Collapsed;
        private double userPointerPositionX = 0.0;
        private double userPointerPositionY = 0.0;
        private const double HumanRatioTolerance = 0.2f;

        private ObservableCollection<PersonDetectionState> personDetectionStates = new ObservableCollection<PersonDetectionState>();

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
                if (Settings.IsDebugViewEnabled)
                {
                    DrawRect(this.bodyPresenceArea, this.hitTestingFrame);
                }
            }

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
                    SetHeadersForState(t.Destination);
                    this.timer.Stop();
                });

            this.stateMachine.Configure(State.WaitingForPresence)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Waiting for presence...");
                        this.PersonOutlineVisibility = Visibility.Visible;
                        if (!Settings.IsTestModeEnabled)
                        {
                            this.colorReader.IsPaused = false;
                        }

                        this.primaryPerson = null;
                    })
                .Permit(Trigger.PersonEnters, State.ConfirmingPresence);

            this.stateMachine.Configure(State.ConfirmingPresence)
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
                        this.PersonOutlineVisibility = Visibility.Collapsed;
                        FlashWindow();

                        if (!Settings.IsTestModeEnabled)
                        {
                            this.colorReader.IsPaused = true;
                        }

                        this.timer.Interval = TimeSpan.FromSeconds(0.75);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.HandPickup)
                .Ignore(Trigger.PersonLeaves);

            this.stateMachine.Configure(State.HandPickup)
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
                            this.currentBrush = GetRandomBrush();
                            this.paintingSession = CreatePaintingSession();

                            // Save start time so we can resume the timer if the person leaves and
                            // re-enters the frame.
                            this.paintingSessionTimeRemaining = null;
                            this.paintingSessionStartTime = DateTime.Now;
                            this.timer.Interval = TimeSpan.FromSeconds(15);
                        }

                        this.UserPointerVisibility = Visibility.Visible;
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        this.UserPointerVisibility = Visibility.Collapsed;
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
                            // TODO: Switch subHeader when done recording
                            // this.subHeader.Text = "to the iPad for future reference";

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
                case State.ConfirmingLeavingHandPickup:
                case State.Painting:
                case State.ConfirmingLeavingPainting:
                    this.HeaderText = Properties.Resources.PaintingHeader;
                    this.SubHeaderText = Properties.Resources.PaintingSubHeader;
                    break;
                case State.SavingImage:
                case State.ConfirmingLeavingSavingImage:
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
                    UpdatePersonDetectionStatesIfNeeded();

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
                            var isPrimaryPersonPresent = PersonDetector.IsPersonPresent(primaryBody, this.bodyPresenceArea, this.primaryPerson.ExpectedMaxDistance);
                            if (this.stateMachine.IsInState(State.ConfirmingLeavingHandPickup)
                                || this.stateMachine.IsInState(State.ConfirmingLeavingPainting)
                                || this.stateMachine.IsInState(State.ConfirmingLeavingSavingImage))
                            {
                                // Last frame the primary body was missing from the frame, detect
                                // if they have returned.
                                if (isPrimaryPersonPresent)
                                {
                                    this.stateMachine.Fire(Trigger.PersonEnters);
                                }
                            }
                            else if (!isPrimaryPersonPresent)
                            {
                                this.stateMachine.Fire(Trigger.PersonLeaves);
                            }
                            else
                            {
                                // Primary person is in the frame and is a valid distance from the camera.
                                if (this.stateMachine.IsInState(State.ConfirmingPresence))
                                {
                                    // Calibrate the primary person's distance
                                    this.personCalibrator.AddDistanceFromCamera(primaryBody.DistanceFromSensor());
                                }
                                else if (this.stateMachine.IsInState(State.HandPickup) || this.stateMachine.IsInState(State.Painting))
                                {
                                    DrawUserPointerIfNeeded(primaryBody.Joints[JointType.HandRight]);

                                    bool isSelectingNewUserButton = IsSelectingNewUserButton(primaryBody.Joints[JointType.HandRight]);
                                    UpdatePrimaryPersonDetectionHandState(isSelectingNewUserButton);

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
                                        var primaryBodyAsSensorBody = new SensorBody(primaryBody, this.sensor);
                                        var frameAsSensorFrame = new SensorBodyFrame(frame, this.sensor);
                                        this.paintingSession.Paint(primaryBodyAsSensorBody, this.currentBrush, this.canvas, frameAsSensorFrame);
                                    }
                                }
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
            if (Settings.IsDebugViewEnabled)
            {
                RefreshPersonDetectionStates();

                if (this.primaryPerson == null)
                {
                    return;
                }

                Body primaryBody = this.bodies[this.primaryPerson.BodyIndex];
                if (primaryBody == null)
                {
                    return;
                }

                Joint shoulderLeft = primaryBody.Joints[JointType.ShoulderLeft];
                Joint shoulderRight = primaryBody.Joints[JointType.ShoulderRight];

                if ((shoulderLeft.TrackingState == TrackingState.NotTracked) || (shoulderRight.TrackingState == TrackingState.NotTracked))
                {
                    return;
                }

                ColorSpacePoint shoulderLeftPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(shoulderLeft.Position);
                ColorSpacePoint shoulderRightPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(shoulderRight.Position);

                var bodyX1 = shoulderLeftPoint.X;
                var bodyY1 = shoulderLeftPoint.Y;
                var bodyX2 = shoulderRightPoint.X;
                var bodyY2 = shoulderRightPoint.Y;
                if (float.IsInfinity(bodyX1) || float.IsInfinity(bodyY1) || float.IsInfinity(bodyX2) || float.IsInfinity(bodyY2))
                {
                    return;
                }

                var bodyRect = new Rect(bodyX1, bodyY1, Math.Abs(bodyX2 - bodyX1), Math.Abs(bodyY2 - bodyY1));
                DrawRect(bodyRect, this.hitTestingBody);
            }
        }

        private void UpdatePrimaryPersonDetectionHandState(bool isSelectingNewUserButton)
        {
            PersonDetectionState personDetectionState = this.PersonDetectionStates.Where(s => s.IsPrimary).FirstOrDefault();
            if (isSelectingNewUserButton)
            {
                personDetectionState.SelectingNewUserButtonFrameCount++;
            }
            else
            {
                personDetectionState.SelectingNewUserButtonFrameCount = 0;
            }
        }

        private void DrawUserPointerIfNeeded(Joint hand)
        {
            if (hand.TrackingState != TrackingState.NotTracked)
            {
                ColorSpacePoint handPoint = this.sensor.CoordinateMapper.MapCameraPointToColorSpace(hand.Position);

                var x = handPoint.X;
                var y = handPoint.Y;

                if (!float.IsInfinity(x) && !float.IsInfinity(y))
                {
                    this.UserPointerPositionX = x;
                    this.UserPointerPositionY = y;
                }
            }
        }

        private bool IsSelectingNewUserButton(Joint hand)
        {
            Point locationFromWindow = this.newUserButton.TranslatePoint(new Point(), this);
            var newUserButtonRect = new Rect(locationFromWindow.X, locationFromWindow.Y, this.newUserButton.ActualWidth, this.newUserButton.ActualHeight);
            var handPoint = hand.Position.ToPoint(Visualization.Color);
            return newUserButtonRect.Contains(handPoint);
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
