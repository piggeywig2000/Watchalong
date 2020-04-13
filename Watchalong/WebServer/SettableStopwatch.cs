using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace WebServer
{
    /// <summary>
    /// A stopwatch with an editable elapsed time
    /// </summary>
    class SettableStopwatch
    {
        private Stopwatch stopwatch = new Stopwatch();
        private TimeSpan time;
        private Timer timer = new Timer();
        private double thresholdTime = -1.0;

        /// <summary>
        /// Creates a new SettableStopwatch with a custom inital time
        /// </summary>
        /// <param name="ticks">The initial time in ticks</param>
        public SettableStopwatch(long ticks = 0)
        {
            time = new TimeSpan(ticks);
            timer.AutoReset = false;
            timer.Stop();
            timer.Elapsed += (sender, e) => { ThresholdReached?.Invoke(this, EventArgs.Empty); };
        }

        /// <summary>
        /// Raised when the threshold is hit on the stopwatch
        /// </summary>
        public event EventHandler ThresholdReached;

        /// <summary>
        /// The elapsed time in ticks
        /// </summary>
        public long ElapsedTicks
        {
            get
            {
                time = time.Add(stopwatch.Elapsed);
                if (stopwatch.IsRunning)
                {
                    stopwatch.Restart();
                }
                else
                {
                    stopwatch.Reset();
                }
                return time.Ticks;
            }
            set
            {
                time = new TimeSpan(value);
                if (stopwatch.IsRunning)
                {
                    stopwatch.Restart();
                }
                else
                {
                    stopwatch.Reset();
                }
            }
        }

        /// <summary>
        /// The elapsed time
        /// </summary>
        public TimeSpan Elapsed
        {
            get
            {
                time = time.Add(stopwatch.Elapsed);
                if (stopwatch.IsRunning)
                {
                    stopwatch.Restart();
                }
                else
                {
                    stopwatch.Reset();
                }
                return time;
            }
            set
            {
                time = new TimeSpan(value.Ticks);
                if (stopwatch.IsRunning)
                {
                    stopwatch.Restart();
                }
                else
                {
                    stopwatch.Reset();
                }
            }
        }

        private void ResetTimer()
        {
            if (thresholdTime < 0 || !stopwatch.IsRunning)
            {
                timer.Interval = int.MaxValue;
                timer.Enabled = false;
                timer.Stop();
            }
            else
            {
                timer.Interval = (thresholdTime * 1000.0) - Elapsed.TotalMilliseconds;
                timer.Enabled = true;
                timer.Start();
            }
        }

        /// <summary>
        /// Set the time the stopwatch needs to reach for it. A negative number disables it
        /// </summary>
        /// <param name="timeoutSeconds"></param>
        public void SetThresholdTime(double timeoutSeconds)
        {
            thresholdTime = timeoutSeconds;
            ResetTimer();
        }

        /// <summary>
        /// Starts the stopwatch
        /// </summary>
        public void Start()
        {
            time = time.Add(stopwatch.Elapsed);
            stopwatch.Restart();
            ResetTimer();
        }

        /// <summary>
        /// Stops the stopwatch
        /// </summary>
        public void Stop()
        {
            time = time.Add(stopwatch.Elapsed);
            stopwatch.Reset();
            ResetTimer();
        }

        /// <summary>
        /// Resets and starts the stopwatch from a value
        /// </summary>
        public void Restart(long ticks = 0)
        {
            time = new TimeSpan(ticks);
            stopwatch.Restart();
            ResetTimer();
        }

        /// <summary>
        /// Resets and stops the stopwatch at a value
        /// </summary>
        public void Reset(long ticks = 0)
        {
            time = new TimeSpan(ticks);
            stopwatch.Reset();
            ResetTimer();
        }

        /// <summary>
        /// Whether the stopwatch is ticking
        /// </summary>
        public bool IsRunning { get => stopwatch.IsRunning; }
    }
}
