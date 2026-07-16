using System;

namespace PulseForge.Domain.Rhythm
{
    public sealed class EncounterTargetRuntime
    {
        internal EncounterTargetRuntime(EncounterTargetData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public EncounterTargetData Data { get; }

        public EncounterTargetResult Result { get; private set; }

        public bool IsResolved => Result != null;

        internal void Resolve(EncounterTargetResult result)
        {
            if (IsResolved)
            {
                throw new InvalidOperationException("An encounter target may only be resolved once.");
            }

            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        internal void Reset()
        {
            Result = null;
        }
    }
}
