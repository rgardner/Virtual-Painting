using System.Windows;
using System.Windows.Input;

namespace VirtualPainting
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Cursor = Cursors.None;
        }
    }
}
