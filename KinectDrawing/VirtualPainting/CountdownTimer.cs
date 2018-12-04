using System;
using System.ComponentModel;
using System.Windows.Threading;

namespace VirtualPainting
{
    /// <summary>
    /// Counts down from an initial value at 1 second intervals.
    /// </summary>
    class CountdownTimer : INotifyPropertyChanged
    {
        private int value;
        private readonly DispatcherTimer timer = new DispatcherTimer();

        /// <summary>
        /// Initializes a new instance of the CountdownTimer class.
        /// </summary>
        /// <param name="initialValue"></param>
        /// <exception cref="ArgumentException">Throws ArgumentException if initialValue <= 0.</exception>
        public CountdownTimer(int initialValue)
        {
            if (initialValue <= 0)
            {
                throw new ArgumentException("initialValue must be > 0");
            }

            this.value = initialValue;

            this.timer.Interval = TimeSpan.FromSeconds(1);
            this.timer.Tick += (s, e) =>
                {
                    this.Value--;
                    if (this.Value <= 0)
                    {
                        this.timer.Stop();
                    }
                };
        }

        public int Value { get; private set; }

#pragma warning disable CS0067 // PropertyChanged is used by Fody-generated property setters
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore

        public void Start()
        {
            this.timer.Start();
        }

        public void Stop()
        {
            this.timer.Stop();
        }
    }
}
