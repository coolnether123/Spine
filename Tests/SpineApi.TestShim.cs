namespace Spine.Api
{
    internal static class SpineApi
    {
        internal static SettingsFacadeTestDouble Settings { get; } =
            new SettingsFacadeTestDouble();
    }

    internal sealed class SettingsFacadeTestDouble
    {
        internal void Scribe(
            object settings,
            System.Collections.Generic.IReadOnlyList<
                Spine.UI.SettingsFramework.SettingDefinition> definitions)
        {
            throw new System.NotSupportedException(
                "Game-backed settings scribing is outside this test executable.");
        }

        internal System.Collections.Generic.IReadOnlyCollection<string>
            ApplyPreferenceDefaults(
                object settings,
                System.Collections.Generic.IReadOnlyList<
                    Spine.UI.SettingsFramework.SettingDefinition> definitions)
        {
            throw new System.NotSupportedException();
        }

        internal void NotifyPreferenceChanges(
            object settings,
            System.Collections.Generic.IReadOnlyList<
                Spine.UI.SettingsFramework.SettingDefinition> definitions,
            System.Collections.Generic.IEnumerable<string> changedFields = null)
        {
            throw new System.NotSupportedException();
        }

        internal string EffectiveScribeKey(
            Spine.UI.SettingsFramework.SettingDefinition definition)
        {
            return string.IsNullOrEmpty(definition?.ScribeKey)
                ? definition?.FieldName
                : definition.ScribeKey;
        }
    }
}
