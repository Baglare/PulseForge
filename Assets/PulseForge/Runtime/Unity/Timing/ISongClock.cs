namespace PulseForge.Runtime.Unity.Timing
{
    public interface ISongClock
    {
        bool IsRunning { get; }

        bool IsPaused { get; }

        double CurrentTimeSeconds { get; }

        void Start();

        void Stop();

        void Pause();

        void Resume();

        void Restart();
    }
}
