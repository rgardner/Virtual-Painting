using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Stateless;

namespace KinectDrawing
{
    /// <summary>
    /// Interaction logic for VirtualPainting.xaml
    /// </summary>
    public partial class VirtualPainting : UserControl
    {
        private enum Trigger
        {
            PageLoaded,
            TimerTick,
            PersonLeaves,
        };

        private enum State
        {
            Start,
            Painting,
            SavingImage,
            Done,
        };

        private readonly StateMachine<State, Trigger> stateMachine;
        private State currentState = State.Start;

        private readonly DispatcherTimer timer = new DispatcherTimer();

        private static readonly BitmapImage paintOverlayImage = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_paint.PNG"));
        private static readonly BitmapImage savedOverlayImage = new BitmapImage(new Uri(@"pack://application:,,,/Images/overlay_saved.PNG"));

        public VirtualPainting(ImageSource backgroundImage)
        {
            InitializeComponent();

            this.timer.Tick += (s, e) =>
                {
                    this.stateMachine.Fire(Trigger.TimerTick);
                };

            this.camera.Source = backgroundImage;

            this.stateMachine = new StateMachine<State, Trigger>(() => this.currentState, s => this.currentState = s);
            ConfigureStateMachine();
            this.stateMachine.Fire(Trigger.PageLoaded);
        }

        private void ConfigureStateMachine()
        {
            this.stateMachine.OnTransitioned(t =>
                {
                    this.timer.Stop();
                });

            this.stateMachine.Configure(State.Start)
                .Permit(Trigger.PageLoaded, State.Painting);

            this.stateMachine.Configure(State.Painting)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Painting...");
                        this.overlay.Source = paintOverlayImage;
                        this.timer.Interval = new TimeSpan(0, 0, 15);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.SavingImage)
                .Permit(Trigger.PersonLeaves, State.Done);

            this.stateMachine.Configure(State.SavingImage)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Saving image...");
                        this.overlay.Source = savedOverlayImage;
                        this.timer.Interval = new TimeSpan(0, 0, 3);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Painting)
                .Permit(Trigger.PersonLeaves, State.Done);

            this.stateMachine.Configure(State.Done)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Navigating to Photo Booth page...");
                        var mainWindow = (MainWindow)Window.GetWindow(this);
                        mainWindow.currentPage.Children.Clear();
                        mainWindow.currentPage.Children.Add(new PhotoBooth());
                    })
                .Ignore(Trigger.PersonLeaves);
        }
    }
}
