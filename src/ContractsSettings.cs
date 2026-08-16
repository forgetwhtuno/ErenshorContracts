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

        [Config("LauncherX", "UI", "Saved Contracts launcher horizontal position, normalized 0..1. Invalid/legacy pixel values recover to the safe default.")]
        public float LauncherX = -1f;

        [Config("LauncherY", "UI", "Saved Contracts launcher vertical position, normalized 0..1. Invalid/legacy pixel values recover to the safe default.")]
        public float LauncherY = -1f;

        [Config("ShowStandaloneLauncherWithHub", "UI", "Show Contracts launcher while a usable Suite Hub bridge is present. If Hub or this module bridge is unavailable, the standalone launcher is forced visible for recovery.")]
        public bool ShowStandaloneLauncherWithHub = false;

        [Config("WindowX", "UI", "Saved Contracts window horizontal position, normalized 0..1. Invalid/legacy pixel values recover to the safe default.")]
        public float WindowX = -1f;

        [Config("WindowY", "UI", "Saved Contracts window vertical position, normalized 0..1. Invalid/legacy pixel values recover to the safe default.")]
        public float WindowY = -1f;

        [Config("WindowWidth", "UI", "Contracts window width in pixels.")]
        public float WindowWidth = 690f;

        [Config("WindowHeight", "UI", "Contracts window height in pixels.")]
        public float WindowHeight = 540f;

        // Retain the old key for config compatibility; it is now explicitly the local-board slot count.
        [Config("DailySlots", "Contracts", "Number of deterministic local contracts offered for the current playable zone, clamped to 1-6.")]
        public int DailySlots = 3;

        [Config("GlobalSlots", "Contracts", "Number of deterministic global contracts offered across zones, clamped to 1-3.")]
        public int GlobalSlots = 2;

        [Config("PatrolMinutes", "Contracts", "Minutes required by the built-in Local Patrol, clamped to 5-60. Default 15.")]
        public int PatrolMinutes = 15;

        [Config("GlobalPatrolMinutes", "Contracts", "Active-play minutes required by Long Watch, clamped to 30-120. Default 60.")]
        public int GlobalPatrolMinutes = 60;

        [Config("LocalRefreshMinutes", "Contracts", "Active-play minutes between local board refresh opportunities, clamped to 15-240. Default 45.")]
        public int LocalRefreshMinutes = 45;

        [Config("GlobalRefreshMinutes", "Contracts", "Active-play minutes between global board refresh opportunities, clamped to 60-480. Default 120.")]
        public int GlobalRefreshMinutes = 120;

        [Config("EnableNativeXpRewards", "Contracts", "Compatibility gate for verified direct personal XP rewards. Default true: contracts defer the entire claim while a raid is active.")]
        public bool EnableNativeXpRewards = true;

        [Config("RewardConfigVersion", "Contracts", "Internal one-time reward configuration migration marker. Do not edit.")]
        public int RewardConfigVersion = 0;

        [Config("ProfileKey", "Contracts", "Local sidecar profile key used to keep board rotation stable. Change it only if you intentionally want a separate Contracts profile.")]
        public string ProfileKey = "local";
    }
}
