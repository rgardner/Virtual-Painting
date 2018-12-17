﻿using System;
using System.Diagnostics;
using System.Windows.Threading;
using Stateless;

namespace PhotoBooth
{
    class PhotoBoothStateMachine
    {
        private enum State
        {
            WaitingForPresence,
            ConfirmingPresence,
            Countdown,
            TakeSnapshot,
            Finished,
        }

        private enum Trigger
        {
            EnterFirstPerson,
            LeaveLastPerson,
            TimerTick,
            CountdownFinished,
        }

        private readonly StateMachine<State, Trigger> stateMachine = new StateMachine<State, Trigger>(State.WaitingForPresence);
        private readonly DispatcherTimer timer = new DispatcherTimer();

        public PhotoBoothStateMachine()
        {
            ConfigureStateMachine();
            this.timer.Tick += (s, e) =>
                {
                    this.stateMachine.Fire(Trigger.TimerTick);
                };
        }

        public event EventHandler PhotoBoothStopped;
        public event EventHandler PhotoBoothCountdownStarted;
        public event EventHandler PhotoBoothCountdownStopped;
        public event EventHandler PhotoBoothSnapshotTaken;
        public event EventHandler PhotoBoothEnteredFinished;
        public event EventHandler PhotoBoothLeftFinished;

        public void EnterFirstPerson()
        {
            this.stateMachine.Fire(Trigger.EnterFirstPerson);
        }

        public void LeaveLastPerson()
        {
            this.stateMachine.Fire(Trigger.LeaveLastPerson);
        }

        public void FinishCountdown()
        {
            this.stateMachine.Fire(Trigger.CountdownFinished);
        }

        private void ConfigureStateMachine()
        {
            this.stateMachine.OnTransitioned(t =>
                {
                    this.timer.Stop();
                });

            this.stateMachine.Configure(State.WaitingForPresence)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Waiting for presence...");
                        PhotoBoothStopped?.Invoke(this, null);
                    })
                .Permit(Trigger.EnterFirstPerson, State.ConfirmingPresence);

            this.stateMachine.Configure(State.ConfirmingPresence)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Confirming presence...");
                        this.timer.Interval = TimeSpan.FromSeconds(2);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Countdown)
                .Permit(Trigger.LeaveLastPerson, State.WaitingForPresence);

            this.stateMachine.Configure(State.Countdown)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Starting countdown...");
                        PhotoBoothCountdownStarted?.Invoke(this, null);
                    })
                .OnExit(t =>
                    {
                        PhotoBoothCountdownStopped?.Invoke(this, null);
                    })
                .Permit(Trigger.CountdownFinished, State.TakeSnapshot)
                .Permit(Trigger.LeaveLastPerson, State.WaitingForPresence);

            this.stateMachine.Configure(State.TakeSnapshot)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Taking picture...");
                        PhotoBoothSnapshotTaken?.Invoke(this, null);
                        this.timer.Interval = TimeSpan.FromSeconds(5);
                        this.timer.Start();
                    })
                .Permit(Trigger.TimerTick, State.Finished)
                .Permit(Trigger.LeaveLastPerson, State.WaitingForPresence);

            this.stateMachine.Configure(State.Finished)
                .OnEntry(t =>
                    {
                        Debug.WriteLine("Entered Finished...");
                        PhotoBoothEnteredFinished?.Invoke(this, null);
                        this.timer.Interval = TimeSpan.FromSeconds(5);
                        this.timer.Start();
                    })
                .OnExit(t =>
                    {
                        Debug.WriteLine("Leaving Finished...");
                        PhotoBoothLeftFinished?.Invoke(this, null);
                    })
                .Permit(Trigger.TimerTick, State.Countdown)
                .Permit(Trigger.LeaveLastPerson, State.WaitingForPresence);
        }
    }
}
