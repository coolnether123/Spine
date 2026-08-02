using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Spine.UI.SettingsFramework
{
    /// <summary>
    /// Compact constructors for the standard settings rows shared by consumer
    /// mods. Gameplay meaning and callbacks remain consumer-owned.
    /// </summary>
    public static class SettingDefinitions
    {
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
