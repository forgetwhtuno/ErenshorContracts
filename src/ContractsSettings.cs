using System;
using Lunaris.Config;

namespace ErenshorContracts
{
    // Loader-neutral ConfigEntry-style shim. Keeping the Value surface makes the Lunaris
    // migration mechanical and lets the existing call sites keep their proven access pattern.
    internal sealed class ContractsConfigEntry<T>
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal ContractsConfigEntry(Func<T> get, Action<T> set)
        {
            _get = get;
            _set = set;
        }

        internal T Value
        {
            get { return _get(); }
            set { _set(value); }
        }
    }

    internal sealed class ContractsSettings
    {
        public ContractsSettings() { }

        [Config("LauncherX", "UI", "Saved Contracts launcher X position. -1 places it near the right side on first use.")]
        public float LauncherX = -1f;

        [Config("LauncherY", "UI", "Saved Contracts launcher Y position. -1 places it below the usual map area on first use.")]
        public float LauncherY = -1f;

        [Config("WindowX", "UI", "Saved Contracts window X position.")]
        public float WindowX = -1f;

        [Config("WindowY", "UI", "Saved Contracts window Y position.")]
        public float WindowY = -1f;

        [Config("WindowWidth", "UI", "Contracts window width in pixels.")]
        public float WindowWidth = 690f;

        [Config("WindowHeight", "UI", "Contracts window height in pixels.")]
        public float WindowHeight = 540f;

        [Config("DailySlots", "Contracts", "Number of deterministic daily contracts offered in each scene, clamped to 1-6.")]
        public int DailySlots = 3;

        [Config("PatrolMinutes", "Contracts", "Minutes required by the built-in Local Patrol fallback, clamped to 1-60.")]
        public int PatrolMinutes = 3;

        [Config("ProfileKey", "Contracts", "Local sidecar profile key used to keep daily rotation stable. Change it only if you intentionally want a separate Contracts profile.")]
        public string ProfileKey = "local";
    }
}
