using UnityEngine;

namespace PulseForge.Runtime.Unity.Timing
{
    public sealed class RealtimeSongClock : ISongClock
    {
        private double startRealtimeSeconds;
        private double stoppedTimeSeconds;

        public bool IsRunning { get; private set; }

        public bool IsPaused { get; private set; }

        public double CurrentTimeSeconds
        {
            get
            {
                if (!IsRunning)
                {
                    return ClampToNonNegative(stoppedTimeSeconds);
                }

                double elapsedSeconds = Time.realtimeSinceStartupAsDouble - startRealtimeSeconds;
                return ClampToNonNegative(elapsedSeconds);
            }
        }

        public void Start()
        {
            startRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
            stoppedTimeSeconds = 0d;
            IsRunning = true;
            IsPaused = false;
        }

        public void Stop()
        {
            if (IsRunning)
            {
                stoppedTimeSeconds = CurrentTimeSeconds;
            }

            IsRunning = false;
            IsPaused = false;
        }

        public void Pause()
        {
            if (!IsRunning)
            {
                return;
            }

            stoppedTimeSeconds = CurrentTimeSeconds;
            IsRunning = false;
            IsPaused = true;
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            startRealtimeSeconds = Time.realtimeSinceStartupAsDouble - stoppedTimeSeconds;
            IsPaused = false;
            IsRunning = true;
        }

        public void Restart()
        {
            Start();
        }

        private static double ClampToNonNegative(double seconds)
        {
            return seconds < 0d ? 0d : seconds;
        }
    }
}
