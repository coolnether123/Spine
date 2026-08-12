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
    }
}
