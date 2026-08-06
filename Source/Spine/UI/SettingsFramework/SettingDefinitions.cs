using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Spine.UI.SettingsFramework
{
    /// <summary>
    /// Compact constructors for the standard settings rows shared by consumer
    /// mods. Gameplay meaning and callbacks remain consumer-owned.
    /// </summary>
    public static class SettingDefinitions
    {
        private static readonly HashSet<string> AuditedSettingsTypes =
            new HashSet<string>(StringComparer.Ordinal);

        public static SettingDefinition Header(
            string id,
            string label,
            string labelKey = null) =>
            Base(id, SettingType.Header, label, labelKey);

        public static SettingDefinition Toggle(
            string id,
            string fieldName,
            string label,
            string labelKey = null,
            string tooltip = null,
            string tooltipKey = null,
            string parentId = null,
            bool simple = true,
            bool controlsChildren = false,
            string scribeKey = null,
            Action<object> onChanged = null)
        {
            SettingDefinition definition = Base(
                id,
                SettingType.Bool,
                label,
                labelKey,
                tooltip,
                tooltipKey);
            definition.FieldName = fieldName;
            definition.ScribeKey = scribeKey;
            definition.ParentId = parentId;
            definition.ShowInSimpleView = simple;
            definition.ControlsChildVisibility = controlsChildren;
            definition.OnChanged = onChanged;
            return definition;
        }

        /// <summary>
        /// A float dragged along a range. The bound field must be a float.
        /// Defaults to 0..1 with a two-decimal readout; use
        /// <see cref="SettingRefinements.Range"/>,
        /// <see cref="SettingRefinements.Step"/>, and
        /// <see cref="SettingRefinements.ShowsPercent"/> to shape it.
        /// </summary>
        /// <remarks>
        /// The parameters here deliberately mirror <see cref="Toggle"/>, so the
        /// leading arguments mean the same thing in every factory and a reader
        /// never has to check which one they are looking at. Everything a
        /// slider alone needs is a refinement instead, because a bare
        /// <c>0.35f, 1f</c> in the middle of a call tells a reader nothing.
        /// </remarks>
        public static SettingDefinition Slider(
            string id,
            string fieldName,
            string label,
            string labelKey = null,
            string tooltip = null,
            string tooltipKey = null,
            string parentId = null,
            bool simple = true,
            string scribeKey = null,
            Action<object> onChanged = null)
        {
            SettingDefinition definition = Base(
                id,
                SettingType.Slider,
                label,
                labelKey,
                tooltip,
                tooltipKey);
            definition.FieldName = fieldName;
            definition.ScribeKey = scribeKey;
            definition.ParentId = parentId;
            definition.ShowInSimpleView = simple;
            definition.OnChanged = onChanged;
            return definition;
        }

        public static SettingDefinition Enum(
            string id,
            string fieldName,
            Type enumType,
            string label,
            string labelKey = null,
            string tooltip = null,
            string tooltipKey = null,
            string parentId = null,
            bool simple = true,
            string scribeKey = null,
            Func<object, string> labelProvider = null,
            Func<object, string> descriptionProvider = null)
        {
            SettingDefinition definition = Base(
                id,
                SettingType.Enum,
                label,
                labelKey,
                tooltip,
                tooltipKey);
            definition.FieldName = fieldName;
            definition.ScribeKey = scribeKey;
            definition.ParentId = parentId;
            definition.ShowInSimpleView = simple;
            definition.EnumType = enumType;
            definition.EnumLabelProvider = labelProvider;
            definition.EnumDescriptionProvider = descriptionProvider;
            return definition;
        }

        public static SettingDefinition Colour(
            string id,
            string fieldName,
            string label,
            string labelKey = null,
            string tooltipKey = null,
            string scribeKey = null)
        {
            SettingDefinition definition = Base(
                id,
                SettingType.Color,
                label,
                labelKey,
                tooltipKey: tooltipKey);
            definition.FieldName = fieldName;
            definition.ScribeKey = scribeKey;
            return definition;
        }

        public static SettingDefinition Button(
            string id,
            string label,
            Action<object> action,
            string labelKey = null,
            string tooltipKey = null)
        {
            SettingDefinition definition = Base(
                id,
                SettingType.Button,
                label,
                labelKey,
                tooltipKey: tooltipKey);
            definition.OnChanged = action;
            return definition;
        }

        public static SettingDefinition Custom(
            string id,
            Func<Rect, string, string, object, bool, bool> drawer,
            string label = "",
            string labelKey = "")
        {
            SettingDefinition definition = Base(
                id,
                SettingType.Custom,
                label,
                labelKey);
            definition.CustomDrawer = drawer;
            return definition;
        }

        internal static void Prepare(
            object settings,
            IReadOnlyList<SettingDefinition> definitions)
        {
            if (settings == null || definitions == null)
            {
                return;
            }

            Type settingsType = settings.GetType();
            object pristine = null;
            for (int index = 0; index < definitions.Count; index++)
            {
                SettingDefinition definition = definitions[index];
                if (definition == null)
                {
                    continue;
                }

                if (definition.SortOrder == int.MinValue)
                {
                    definition.SortOrder = index;
                }

                if (definition.DefaultValue != null ||
                    string.IsNullOrEmpty(definition.FieldName))
                {
                    continue;
                }

                if (pristine == null)
                {
                    pristine = Activator.CreateInstance(settingsType);
                }

                FieldInfo field = settingsType.GetField(
                    definition.FieldName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (field != null)
                {
                    definition.DefaultValue = field.GetValue(pristine);
                }
            }

            ValidatePresentation(settingsType, definitions);
        }

        /// <summary>
        /// Development-time audit of a consumer's setting definitions. Reports two
        /// things a player would otherwise discover the hard way: an interactive
        /// setting with nothing to read when they hover it, and a translation key
        /// that will not resolve at runtime.
        /// Runs once per settings type and only under dev mode, so a shipped game
        /// pays nothing and players never see it.
        /// </summary>
        /// <remarks>
        /// This used to assume Prepare was only reached after language data had
        /// loaded. It is not: Prepare also runs from the scribe path, which
        /// RimWorld executes while constructing mods, before LanguageDatabase has
        /// an active language. CanTranslate dereferences that language, so the
        /// audit threw a NullReferenceException straight out of ExposeData and
        /// took the entire settings load with it - every setting silently
        /// reverted to its default on every launch, for every consumer, whenever
        /// dev mode was on. The language check below is load-bearing, not
        /// defensive.
        /// </remarks>
        private static void ValidatePresentation(
            Type settingsType,
            IReadOnlyList<SettingDefinition> definitions)
        {
            if (settingsType == null || !Prefs.DevMode)
            {
                return;
            }

            // Bail before claiming the audit slot, so the audit still runs once
            // the settings page draws and the keys can actually be resolved.
            if (LanguageDatabase.activeLanguage == null)
            {
                return;
            }

            if (!AuditedSettingsTypes.Add(settingsType.FullName))
            {
                return;
            }

            List<string> problems = new List<string>();
            for (int index = 0; index < definitions.Count; index++)
            {
                SettingDefinition definition = definitions[index];
                if (definition == null)
                {
                    continue;
                }

                string id = string.IsNullOrEmpty(definition.Id)
                    ? "(no id)"
                    : definition.Id;

                if (!string.IsNullOrEmpty(definition.LabelKey) &&
                    !definition.LabelKey.CanTranslate())
                {
                    problems.Add(
                        "  " + id + ": LabelKey '" + definition.LabelKey +
                        "' has no translation entry.");
                }

                if (!string.IsNullOrEmpty(definition.TooltipKey) &&
                    !definition.TooltipKey.CanTranslate())
                {
                    problems.Add(
                        "  " + id + ": TooltipKey '" + definition.TooltipKey +
                        "' has no translation entry.");
                }

                if (WantsExplanation(definition.Type) &&
                    string.IsNullOrEmpty(definition.TooltipKey) &&
                    string.IsNullOrEmpty(definition.Tooltip))
                {
                    problems.Add(
                        "  " + id + ": no tooltip. A player who hovers this " +
                        "setting has nothing to read.");
                }
            }

            if (problems.Count == 0)
            {
                return;
            }

            Log.Warning(
                "[Spine] Settings audit for " + settingsType.Name +
                " found " + problems.Count + " issue(s):\n" +
                string.Join("\n", problems.ToArray()));
        }

        private static bool WantsExplanation(SettingType type)
        {
            return type == SettingType.Bool ||
                type == SettingType.Color ||
                type == SettingType.Enum ||
                type == SettingType.Button ||
                type == SettingType.Slider;
        }

        private static SettingDefinition Base(
            string id,
            SettingType type,
            string label,
            string labelKey,
            string tooltip = null,
            string tooltipKey = null) =>
            new SettingDefinition
            {
                Id = id,
                Type = type,
                Label = label,
                LabelKey = labelKey,
                Tooltip = tooltip,
                TooltipKey = tooltipKey,
                ShowInSimpleView = true,
                ShowInAdvancedView = true
            };
    }

    /// <summary>
    /// Post-construction refinements for setting definitions.
    ///
    /// These exist so the factory signatures above never have to change again.
    /// Adding a parameter to an existing public method is a BINARY-BREAKING
    /// change in C#: a caller bakes the whole argument list into its IL, so a
    /// consumer compiled against the old signature throws MissingMethodException
    /// against the new assembly even though its source would still compile
    /// unchanged. That once broke every mod built on Spine, from a change that
    /// looked additive.
    ///
    /// So: DO NOT add parameters to the factories. Add a refinement here. A new
    /// method is always binary-safe, and consumers opt in by recompiling rather
    /// than by breaking.
    /// </summary>
    public static class SettingRefinements
    {
        /// <summary>Holds this entry outside the scrolling region.</summary>
        public static SettingDefinition Pinned(
            this SettingDefinition definition,
            SettingPin pin)
        {
            if (definition != null)
            {
                definition.Pin = pin;
            }

            return definition;
        }

        /// <summary>
        /// Shows this entry only while the predicate holds for the settings
        /// object. Named ShownWhen rather than VisibleWhen so it cannot be
        /// confused with the field of that name, which is a delegate.
        /// </summary>
        public static SettingDefinition ShownWhen(
            this SettingDefinition definition,
            Func<object, bool> predicate)
        {
            if (definition != null)
            {
                definition.VisibleWhen = predicate;
            }

            return definition;
        }

        /// <summary>Hides this entry from the Simple view.</summary>
        public static SettingDefinition AdvancedOnly(
            this SettingDefinition definition)
        {
            if (definition != null)
            {
                definition.ShowInSimpleView = false;
            }

            return definition;
        }

        /// <summary>
        /// Bounds a slider. A reversed pair is treated as the range the caller
        /// meant rather than as an empty one, because an unusable slider is a
        /// worse outcome than a silently reordered pair.
        /// </summary>
        public static SettingDefinition Range(
            this SettingDefinition definition,
            float min,
            float max)
        {
            if (definition != null)
            {
                definition.SliderMin = Mathf.Min(min, max);
                definition.SliderMax = Mathf.Max(min, max);
            }

            return definition;
        }

        /// <summary>
        /// Quantises a slider to multiples of <paramref name="step"/>, so a
        /// player lands on 0.75 rather than 0.7431. Zero or less leaves it
        /// continuous.
        /// </summary>
        public static SettingDefinition Step(
            this SettingDefinition definition,
            float step)
        {
            if (definition != null)
            {
                definition.SliderStep = step;
            }

            return definition;
        }

        /// <summary>
        /// Reads a slider out as a whole percentage. Intended for a normalised
        /// 0..1 range; on any other range the number shown will not match the
        /// value stored.
        /// </summary>
        public static SettingDefinition ShowsPercent(
            this SettingDefinition definition)
        {
            return definition.ShowsValue(
                value => Mathf.RoundToInt(value * 100f) + "%");
        }

        /// <summary>
        /// Replaces the numeric readout beside a slider. Use for units, counts,
        /// or a word standing in for a band of values.
        /// </summary>
        public static SettingDefinition ShowsValue(
            this SettingDefinition definition,
            Func<float, string> formatter)
        {
            if (definition != null)
            {
                definition.SliderValueFormatter = formatter;
            }

            return definition;
        }
    }

}
