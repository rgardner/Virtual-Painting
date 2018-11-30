using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using KinectRecorder;
using VirtualRepainter.ViewModels;
using System.Windows.Media.Imaging;

namespace VirtualRepainter
{
    public class VirtualRepainterViewModel : INotifyPropertyChanged
    {
        private SensorRecordingPlayer sensorRecordingPlayer;
        private readonly BackgroundWorker recordingFileOpenerBackgroundWorker = new BackgroundWorker();
        private readonly BackgroundWorker backgroundImageFileOpenerBackgroundWorker = new BackgroundWorker();

        public VirtualRepainterViewModel()
        {
            this.recordingFileOpenerBackgroundWorker.DoWork += (s, e) =>
                {
                    var recordingFilePath = (string)e.Argument;

                    string sensorRecordingData = File.ReadAllText(recordingFilePath);
                    this.sensorRecordingPlayer = new SensorRecordingPlayer(sensorRecordingData);

                    this.sensorRecordingPlayer.Start();
                };

            this.backgroundImageFileOpenerBackgroundWorker.DoWork += (s, e) =>
                {
                    var backgroundImageFilePath = (string)e.Argument;
                    var backgroundImage = new BitmapImage();
                    backgroundImage.BeginInit();
                    backgroundImage.UriSource = new Uri(backgroundImageFilePath);
                    backgroundImage.EndInit();
                    backgroundImage.Freeze();
                    e.Result = backgroundImage;
                };
            this.backgroundImageFileOpenerBackgroundWorker.RunWorkerCompleted += (s, e) =>
                {
                    this.CameraImageSource = (BitmapImage)e.Result;
                };

            this.OpenFindRecordingFileDialogCommand = new RelayCommand(o => OpenRecordingFileDialog());
            this.OpenFindBackgroundImageFileDialogCommand = new RelayCommand(o => OpenFindBackgroundImageFileDialog());
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ImageSource CameraImageSource { get; private set; }

        public Visibility UserPointerVisibility { get; private set; } = Visibility.Collapsed;
        public int UserPointerPositionX { get; private set; } = 0;
        public int UserPointerPositionY { get; private set; } = 0;

        public ICommand OpenFindRecordingFileDialogCommand { get; }
        public ICommand OpenFindBackgroundImageFileDialogCommand { get; }

        public void OpenRecordingFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                DefaultExt = ".json",
                Filter = "JSON Files (*.json)|*.json"
            };

            bool? result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                this.recordingFileOpenerBackgroundWorker.RunWorkerAsync(dialog.FileName);
            }
        }

        public void OpenFindBackgroundImageFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                DefaultExt = ".png",
                Filter = "PNG Files (*.png)|*.png"
            };

            bool? result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                this.backgroundImageFileOpenerBackgroundWorker.RunWorkerAsync(dialog.FileName);
            }
        }
    }
}
