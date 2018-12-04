using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KinectRecorder;
using Microsoft.Win32;
using VirtualRepainter.PaintAlgorithm;
using VirtualRepainter.ViewModels;

namespace VirtualRepainter
{
    public class VirtualRepainterViewModel : INotifyPropertyChanged
    {
        private SensorRecordingPlayer sensorRecordingPlayer;
        private readonly Brush paintBrush = Brushes.Black;
        private readonly HandRightOnlyPaintAlgorithm paintAlgorithm = new HandRightOnlyPaintAlgorithm();
        private readonly BackgroundWorker recordingFileOpenerBackgroundWorker = new BackgroundWorker();
        private readonly BackgroundWorker backgroundImageFileOpenerBackgroundWorker = new BackgroundWorker();

        public VirtualRepainterViewModel()
        {
            this.recordingFileOpenerBackgroundWorker.DoWork += (s, e) =>
                {
                    var recordingFilePath = (string)e.Argument;

                    var sensorRecordingData = File.ReadAllText(recordingFilePath);
                    this.sensorRecordingPlayer = new SensorRecordingPlayer(sensorRecordingData);
                    this.sensorRecordingPlayer.SensorBodyFrameCaptured += (s1, e1) =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                                {
                                    SensorBody primaryBody = e1.Bodies[0];
                                    this.PaintLines.Add(this.paintAlgorithm.Paint(primaryBody, this.paintBrush));
                                });
                        };
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

#pragma warning disable CS0067 // PropertyChanged is used by Fody-generated property setters
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        public ImageSource CameraImageSource { get; private set; }

        public ObservableCollection<PaintLine> PaintLines { get; set; } = new ObservableCollection<PaintLine>();

        public bool IsOpenFindRecordingFileDialogCommandEnabled { get; private set; } = true;
        public ICommand OpenFindRecordingFileDialogCommand { get; }
        public bool IsOpenFindBackgroundImageFileDialogCommandEnabled { get; private set; } = true;
        public ICommand OpenFindBackgroundImageFileDialogCommand { get; }

        public void OpenRecordingFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                DefaultExt = ".json",
                Filter = "JSON Files (*.json)|*.json"
            };

            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                this.recordingFileOpenerBackgroundWorker.RunWorkerAsync(dialog.FileName);
                this.IsOpenFindRecordingFileDialogCommandEnabled = false;
            }
        }

        public void OpenFindBackgroundImageFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                DefaultExt = ".png",
                Filter = "PNG Files (*.png)|*.png"
            };

            var result = dialog.ShowDialog();
            if (result.HasValue && result.Value)
            {
                this.backgroundImageFileOpenerBackgroundWorker.RunWorkerAsync(dialog.FileName);
                this.IsOpenFindBackgroundImageFileDialogCommandEnabled = false;
            }
        }
    }
}
