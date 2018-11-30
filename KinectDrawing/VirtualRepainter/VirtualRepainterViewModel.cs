using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace VirtualRepainter
{
    public class VirtualRepainterViewModel : INotifyPropertyChanged
    {
        public VirtualRepainterViewModel()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string HeaderText { get; private set; }
        public string SubHeaderText { get; private set; }

        public ImageSource CameraImageSource { get; }

        public Visibility PersonOutlineVisibility { get; private set; } = Visibility.Visible;
        public string CountdownValue { get; private set; }

        public Visibility UserPointerVisibility { get; private set; } = Visibility.Collapsed;
        public int UserPointerPositionX { get; private set; }
        public int UserPointerPositionY { get; private set; }
    }
}
