using System;
using UnityEngine;

namespace Spine.UI.SettingsFramework
{
    /// <summary>
    /// Receives semantic color-preview activity from the reusable settings drawer.
    /// The host decides where and how that preview is rendered.
    /// </summary>
    public interface ISettingColorPreviewSink
    {
        void PreviewHover(SettingDefinition definition, Color color);
        void BeginPicker(SettingDefinition definition, Color color);
        void PreviewPicker(SettingDefinition definition, Color color);
        void EndPicker(SettingDefinition definition);
    }

    public enum SettingClassification
    {
        Preference,
        State
    }

    /// <summary>
    /// Where an entry is drawn relative to the scrolling region.
    /// </summary>
    public enum SettingPin
    {
        /// <summary>Normal entry inside the scrolling list.</summary>
        None,

        /// <summary>Held above the list; stays visible while the list scrolls.</summary>
        Top,

        /// <summary>Held below the list; stays visible while the list scrolls.</summary>
        Bottom
    }

    /// <summary>
    /// Defines a single configurable setting with optional parent-child relationships.
    /// </summary>
    public class SettingDefinition
    {
        /// <summary>
        /// Unique identifier for the setting (e.g., "highlights.master").
        /// </summary>
        public string Id;

        /// <summary>
        /// Name of the field on the settings object that stores this value.
        /// Null for non-data entries such as headers, spacers, and buttons.
        /// </summary>
        public string FieldName;

        /// <summary>
        /// XML key used for settings scribing. Null means use FieldName.
        /// </summary>
        public string ScribeKey;

        /// <summary>
        /// Optional legacy default used only when an XML key is absent.
        /// </summary>
        public object ScribeDefaultOverride;

        /// <summary>
        /// True when a setting needs a hand-written Scribe call because its absent-key default
        /// depends on other migrated values.
        /// </summary>
        public bool DisableAutoScribe;

        /// <summary>
        /// Preferences are scribed and reset by the registry. State is not reset.
        /// </summary>
        public SettingClassification Classification = SettingClassification.Preference;

        /// <summary>
        /// Parent setting identifier. Null indicates a root item.
        /// </summary>
        public string ParentId;

        /// <summary>
        /// Human-readable label shown in the UI (fallback if no translation is found).
        /// </summary>
        public string Label;

        /// <summary>
        /// Optional translation-key override. Null uses the registry convention;
        /// an empty value intentionally uses the already-localized fallback.
        /// </summary>
        public string LabelKey;

        /// <summary>
        /// Tooltip text shown on hover (fallback if no translation is found).
        /// </summary>
        public string Tooltip;

        /// <summary>
        /// Optional tooltip translation-key override. Null uses the registry
        /// convention; an empty value intentionally uses the fallback.
        /// </summary>
        public string TooltipKey;

        /// <summary>
        /// Optional non-displayed aliases that make this setting easier to find in search.
        /// Use for mod names, common synonyms, or legacy terms that should not clutter the label.
        /// </summary>
        public string[] SearchKeywords;

        /// <summary>
        /// Controls draw order within a hierarchy level. Lower values appear first.
        /// </summary>
        public int SortOrder = int.MinValue;

        /// <summary>
        /// Widget type used to render this setting.
        /// </summary>
        public SettingType Type;

        /// <summary>
        /// Default value used when resetting the setting.
        /// </summary>
        public object DefaultValue;

        /// <summary>
        /// Enum type for enum-based settings.
        /// </summary>
        public Type EnumType;

        /// <summary>
        /// Optional player-facing label provider for enum values.
        /// </summary>
        public Func<object, string> EnumLabelProvider;

        /// <summary>
        /// Optional player-facing description provider for enum values.
        /// </summary>
        public Func<object, string> EnumDescriptionProvider;

        /// <summary>
        /// If true, the setting is visible in the Simple view.
        /// </summary>
        public bool ShowInSimpleView;

        /// <summary>
        /// If true, the setting is visible in the Advanced view. Defaults to true.
        /// </summary>
        public bool ShowInAdvancedView = true;

        /// <summary>
        /// Optional predicate that determines runtime visibility.
        /// </summary>
        public Func<object, bool> VisibleWhen;

        /// <summary>
        /// When true and this is a boolean parent, children are disabled when the parent is unchecked.
        /// </summary>
        public bool ControlsChildVisibility;

        /// <summary>
        /// If true, a restart warning should be shown after changing the value.
        /// </summary>
        public bool RequiresRestart;

        /// <summary>
        /// Callback invoked when the value changes. Receives the settings object.
        /// </summary>
        public Action<object> OnChanged;

        /// <summary>
        /// Draws a custom row. Return true when the row changed settings.
        /// </summary>
        public Func<Rect, string, string, object, bool, bool> CustomDrawer;

        /// <summary>
        /// Holds this entry outside the scrolling region so it stays visible while
        /// the rest of the page scrolls. Useful for a live preview that a player
        /// needs to watch while changing the settings that feed it.
        /// Ignored when the pinned bands would take more than half the page, so a
        /// page can never pin away the list it belongs to.
        /// </summary>
        public SettingPin Pin = SettingPin.None;

        /// <summary>Lowest value a slider setting can take.</summary>
        public float SliderMin;

        /// <summary>Highest value a slider setting can take.</summary>
        public float SliderMax = 1f;

        /// <summary>
        /// Quantises a slider to multiples of this value. Zero leaves it
        /// continuous. Use it when the underlying value has a meaningful
        /// granularity, so a player cannot land on 0.7431.
        /// </summary>
        public float SliderStep;

        /// <summary>
        /// Renders the numeric readout beside a slider. Null shows two decimal
        /// places. Supply one to show a percentage, a tick count, or a word.
        /// </summary>
        public Func<float, string> SliderValueFormatter;

    }
}
