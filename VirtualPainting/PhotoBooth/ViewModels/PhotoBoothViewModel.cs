using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace PhotoBooth.ViewModels
{
    class PhotoBoothViewModel : INotifyPropertyChanged
    {
        private readonly CameraSensor cameraSensor;
        private readonly PhotoBoothStateMachine stateMachine = new PhotoBoothStateMachine();
        private int peoplePresentCount = 0;
        private CountdownTimer countdownTimer = null;
        private readonly Random random = new Random();

        public PhotoBoothViewModel()
        {
            this.cameraSensor = new CameraSensor(KinectSensor.GetDefault());
            this.CameraImageSource = this.cameraSensor.Camera;
            this.cameraSensor.PersonEnters += (s, e) =>
                {
                    this.peoplePresentCount++;
                    if (this.peoplePresentCount == 1)
                    {
                        this.stateMachine.EnterFirstPerson();
                    }
                };

            this.cameraSensor.PersonLeaves += (s, e) =>
                {
                    this.peoplePresentCount--;
                    if (this.peoplePresentCount == 0)
                    {
                        this.stateMachine.LeaveLastPerson();
                    }
                };

            this.stateMachine.PhotoBoothStopped += (s, e) =>
                {
                    this.FlashingBackground = false;
                    this.OverlayImageSource = null;
                };

            this.stateMachine.PhotoBoothCountdownStarted += (s, e) =>
                {
                    SetOverlayImage();
                    StartCountdownTimer();
                };

            this.stateMachine.PhotoBoothCountdownStopped += (s, e) =>
                {
                    StopCountdownTimer();
                };

            this.stateMachine.PhotoBoothSnapshotTaken += (s, e) =>
                {
                    // TODO
                    this.FlashingBackground = true;
                    // Save picture
                    // Show message saying they will be sent out after the party
                };
        }

        public ImageSource CameraImageSource { get; }
        public ImageSource OverlayImageSource { get; private set; }
        public string CountdownValue { get; private set; }
        public bool FlashingBackground { get; private set; } = false;

#pragma warning disable CS0067 // PropertyChanged is used by Fody-generated property setters
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        private void SetOverlayImage()
        {
            var overlayImageFilePaths = Directory.EnumerateFiles("Images", "*.png").ToArray();
            var chosenOverlayImageFilePath = overlayImageFilePaths[this.random.Next(overlayImageFilePaths.Length)];

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(chosenOverlayImageFilePath, UriKind.Relative);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();

            this.OverlayImageSource = image;
        }

        private void StartCountdownTimer()
        {
            const int initialCountdownValue = 7;
            this.countdownTimer = new CountdownTimer(initialCountdownValue);
            this.countdownTimer.PropertyChanged += (s1, e1) =>
                {
                    if (string.Equals(e1.PropertyName, "Value"))
                    {
                        if (this.countdownTimer.Value == 0)
                        {
                            this.stateMachine.FinishCountdown();
                        }
                        else
                        {
                            this.CountdownValue = countdownTimer.Value.ToString();
                        }
                    }
                };

            this.CountdownValue = initialCountdownValue.ToString();
            this.countdownTimer.Start();
        }

        private void StopCountdownTimer()
        {
            this.CountdownValue = string.Empty;
            this.countdownTimer.Stop();
            this.countdownTimer = null;
        }
    }
}
