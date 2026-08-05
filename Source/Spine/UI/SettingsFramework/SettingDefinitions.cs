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
            string labelKey = null,
            SettingPin pin = SettingPin.None)
        {
            SettingDefinition definition =
                Base(id, SettingType.Header, label, labelKey);
            definition.Pin = pin;
            return definition;
        }

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
            Action<object> onChanged = null,
            Func<object, bool> visibleWhen = null)
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
            definition.VisibleWhen = visibleWhen;
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
            string scribeKey = null,
            bool simple = true,
            Func<object, bool> visibleWhen = null)
        {
            SettingDefinition definition = Base(
                id,
                SettingType.Color,
                label,
                labelKey,
                tooltipKey: tooltipKey);
            definition.FieldName = fieldName;
            definition.ScribeKey = scribeKey;
            definition.ShowInSimpleView = simple;
            definition.VisibleWhen = visibleWhen;
            return definition;
        }

        public static SettingDefinition Button(
            string id,
            string label,
            Action<object> action,
            string labelKey = null,
            string tooltipKey = null,
            bool simple = true,
            Func<object, bool> visibleWhen = null)
        {
            SettingDefinition definition = Base(
                id,
                SettingType.Button,
                label,
                labelKey,
                tooltipKey: tooltipKey);
            definition.OnChanged = action;
            definition.ShowInSimpleView = simple;
            definition.VisibleWhen = visibleWhen;
            return definition;
        }

        public static SettingDefinition Custom(
            string id,
            Func<Rect, string, string, object, bool, bool> drawer,
            string label = "",
            string labelKey = "",
            SettingPin pin = SettingPin.None)
        {
            SettingDefinition definition = Base(
                id,
                SettingType.Custom,
                label,
                labelKey);
            definition.CustomDrawer = drawer;
            definition.Pin = pin;
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
        /// pays nothing and players never see it. Prepare is reached lazily, after
        /// language data has loaded, which is what makes the key check meaningful.
        /// </summary>
        private static void ValidatePresentation(
            Type settingsType,
            IReadOnlyList<SettingDefinition> definitions)
        {
            if (settingsType == null ||
                !Prefs.DevMode ||
                !AuditedSettingsTypes.Add(settingsType.FullName))
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
                type == SettingType.Button;
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
}
