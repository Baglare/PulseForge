using System;
using UnityEngine;

namespace PulseForge.Runtime.Unity.Timing
{
    public sealed class DspAudioSongClock : ISongClock
    {
        private readonly AudioSource audioSource;
        private readonly AudioClip audioClip;
        private readonly double scheduleLeadTimeSeconds;
        private double scheduledStartDspTime;
        private double pausedTimeSeconds;

        public DspAudioSongClock(AudioSource audioSource, AudioClip audioClip, double scheduleLeadTimeSeconds = 0.1d)
        {
            if (audioSource == null)
            {
                throw new ArgumentNullException(nameof(audioSource));
            }

            if (audioClip == null)
            {
                throw new ArgumentNullException(nameof(audioClip));
            }

            if (double.IsNaN(scheduleLeadTimeSeconds) || double.IsInfinity(scheduleLeadTimeSeconds) || scheduleLeadTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(scheduleLeadTimeSeconds));
            }

            this.audioSource = audioSource;
            this.audioClip = audioClip;
            this.scheduleLeadTimeSeconds = scheduleLeadTimeSeconds;
        }

        public bool IsRunning { get; private set; }

        public bool IsPaused { get; private set; }

        public double CurrentTimeSeconds
        {
            get
            {
                if (IsPaused)
                {
                    return pausedTimeSeconds;
                }

                if (!IsRunning)
                {
                    return 0d;
                }

                double elapsedSeconds = AudioSettings.dspTime - scheduledStartDspTime;
                return elapsedSeconds < 0d ? 0d : elapsedSeconds;
            }
        }

        public void Start()
        {
            audioSource.clip = audioClip;
            scheduledStartDspTime = AudioSettings.dspTime + scheduleLeadTimeSeconds;
            audioSource.PlayScheduled(scheduledStartDspTime);
            IsRunning = true;
            IsPaused = false;
            pausedTimeSeconds = 0d;
        }

        public void Stop()
        {
            audioSource.Stop();
            IsRunning = false;
            IsPaused = false;
            pausedTimeSeconds = 0d;
        }

        public void Pause()
        {
            if (!IsRunning)
            {
                return;
            }

            pausedTimeSeconds = CurrentTimeSeconds;
            audioSource.Pause();
            IsRunning = false;
            IsPaused = true;
        }

        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            scheduledStartDspTime = AudioSettings.dspTime - pausedTimeSeconds;
            audioSource.UnPause();
            IsPaused = false;
            IsRunning = true;
        }

        public void Restart()
        {
            Stop();
            Start();
        }
    }
}
