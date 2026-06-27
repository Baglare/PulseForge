using UnityEngine;

namespace PulseForge.Runtime.Unity.Timing
{
    public sealed class RealtimeSongClock : ISongClock
    {
        private double startRealtimeSeconds;
        private double stoppedTimeSeconds;

        public bool IsRunning { get; private set; }

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
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            stoppedTimeSeconds = CurrentTimeSeconds;
            IsRunning = false;
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
