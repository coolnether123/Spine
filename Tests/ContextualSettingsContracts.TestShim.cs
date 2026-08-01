namespace Spine.UI.ContextualSettings
{
    internal enum ContextualSettingsTargetLevel
    {
        Root = 0,
        Group = 1,
        Exact = 2
    }

    internal readonly struct ContextualSettingsTarget
    {
        internal ContextualSettingsTarget(
            ContextualSettingsTargetLevel level,
            string settingId = null,
            string fallbackGroupId = null)
        {
            Level = level;
            SettingId = settingId;
            FallbackGroupId = fallbackGroupId;
        }

        internal ContextualSettingsTargetLevel Level { get; }
        internal string SettingId { get; }
        internal string FallbackGroupId { get; }
    }
}
