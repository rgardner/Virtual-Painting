using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Resources;
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

        public PhotoBoothViewModel()
        {
            this.cameraSensor = new CameraSensor(KinectSensor.GetDefault());
            this.CameraImageSource = this.cameraSensor.Camera;
            this.cameraSensor.PersonEnters += (s, e) =>
                {
                    HandlePersonEnters();
                };

            this.cameraSensor.PersonLeaves += (s, e) =>
                {
                    HandlePersonLeaves();
                };

            this.stateMachine.PhotoBoothStopped += (s, e) =>
                {
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
        }

        public WriteableBitmap CameraImageSource { get; }
        public Image OverlayImageSource { get; private set; }
        public string CountdownValue { get; private set; }

#pragma warning disable CS0067 // PropertyChanged is used by Fody-generated property setters
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        private void HandlePersonEnters()
        {
            this.peoplePresentCount++;
            if (this.peoplePresentCount == 1)
            {
                this.stateMachine.EnterFirstPerson();
            }
        }

        private void HandlePersonLeaves()
        {
            this.peoplePresentCount--;
            if (this.peoplePresentCount == 0)
            {
                this.stateMachine.LeaveLastPerson();
            }
        }

        private void SetOverlayImage()
        {
            ResourceSet resources = Properties.Resources.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
            var overlayNames = new List<string>();
            foreach (DictionaryEntry entry in resources)
            {
                overlayNames.Add((string)entry.Key);
            }

            var random = new Random();
            var chosenOverlayName = overlayNames[random.Next(overlayNames.Count)];
            this.OverlayImageSource = (Bitmap)Properties.Resources.ResourceManager.GetObject(chosenOverlayName);
        }

        private void StartCountdownTimer()
        {
            const int initialCountdownValue = 3;
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
            this.countdownTimer.Start();
            this.CountdownValue = initialCountdownValue.ToString();
        }

        private void StopCountdownTimer()
        {
            this.CountdownValue = string.Empty;
            this.countdownTimer.Stop();
            this.countdownTimer = null;
        }
    }
}
