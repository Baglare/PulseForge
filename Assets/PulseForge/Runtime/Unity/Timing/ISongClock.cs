namespace PulseForge.Runtime.Unity.Timing
{
    public interface ISongClock
    {
        bool IsRunning { get; }

        double CurrentTimeSeconds { get; }

        void Start();

        void Stop();

        void Restart();
    }
}
