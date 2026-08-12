using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Spine.UI.ColourPicker;

namespace Spine.UI.SettingsFramework
{
    /// <summary>
    /// Stateless widget renderers for individual setting types.
    /// </summary>
    public static class SettingWidgets
    {
        private static readonly Color SettingLabelColor = new Color(0.78f, 0.77f, 0.74f);
        private static readonly Color DefaultSectionAccent = new Color(0.9f, 0.85f, 0.7f);

        /// <summary>
        /// Draws a checkbox setting with optional tooltip and disabled state.
        /// </summary>
        public static bool DrawBool(
            Rect rect,
            string label,
            ref bool value,
            string tooltip = null,
            bool disabled = false)
        {
            bool original = value;
            const float checkboxSize = 24f;
            Rect checkboxRect = new Rect(
                rect.xMax - checkboxSize,
                rect.y + ((rect.height - checkboxSize) / 2f),
                checkboxSize,
                checkboxSize);
            Rect labelRect = new Rect(
                rect.x,
                rect.y,
                Mathf.Max(0f, checkboxRect.x - rect.x - 6f),
                rect.height);
            DrawSettingLabel(labelRect, label, disabled);
            Widgets.Checkbox(checkboxRect.x, checkboxRect.y, ref value, checkboxSize, disabled);
            if (!disabled && Widgets.ButtonInvisible(labelRect))
            {
                value = !value;
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }

            return original != value;
        }

        /// <summary>
        /// Draws a color swatch with an edit button that opens a picker.
        /// </summary>
        public static bool DrawColor(
            Rect rect,
            string label,
            ref Color value,
            string tooltip = null,
            bool disabled = false,
            Action<Color, Action<Color>> openColorPicker = null,
            string editLabel = "Edit")
        {
            var colorRect = new Rect(rect.xMax - 96f, rect.y + 2f, 28f, rect.height - 4f);
            var buttonRect = new Rect(colorRect.xMax + 4f, rect.y + 2f, 60f, rect.height - 4f);
            var labelRect = new Rect(
                rect.x,
                rect.y,
                Mathf.Max(0f, colorRect.x - rect.x - 6f),
                rect.height);

            DrawSettingLabel(labelRect, label, disabled);
            Widgets.DrawBoxSolid(colorRect, value);
            Widgets.DrawBox(colorRect, 1);

            if (!disabled && Widgets.ButtonText(buttonRect, editLabel))
            {
                if (openColorPicker != null)
                {
                    openColorPicker(value, null);
                }
                else
                {
                    Find.WindowStack.Add(new Dialog_ColourPicker(value));
                }
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }

            return false;
        }

        /// <summary>
        /// Draws a labelled slider with a numeric readout. Returns true only on
        /// the frames where the value actually moved, so a caller can persist
        /// on change without writing every frame of a drag.
        /// </summary>
        public static bool DrawSlider(
            Rect rect,
            string label,
            ref float value,
            float min,
            float max,
            string valueLabel = null,
            string tooltip = null,
            bool disabled = false,
            float step = 0f)
        {
            const float ReadoutWidth = 54f;
            const float SliderHeight = 18f;

            Rect rightRect = rect.RightPart(0.48f);
            Rect labelRect = new Rect(
                rect.x,
                rect.y,
                Mathf.Max(0f, rightRect.x - rect.x - 6f),
                rect.height);
            Rect readoutRect = new Rect(
                rightRect.xMax - ReadoutWidth,
                rect.y,
                ReadoutWidth,
                rect.height);
            Rect sliderRect = new Rect(
                rightRect.x,
                rect.y + (rect.height - SliderHeight) * 0.5f,
                Mathf.Max(24f, rightRect.width - ReadoutWidth - 6f),
                SliderHeight);

            DrawSettingLabel(labelRect, label, disabled);

            TextAnchor previousAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(
                readoutRect,
                valueLabel ?? value.ToString("0.00"));
            Text.Anchor = previousAnchor;

            bool previousEnabled = GUI.enabled;
            Color previousColor = GUI.color;
            if (disabled)
            {
                GUI.enabled = false;
                GUI.color = Color.gray;
            }

            float updated = Widgets.HorizontalSlider(sliderRect, value, min, max);

            if (disabled)
            {
                GUI.enabled = previousEnabled;
                GUI.color = previousColor;
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }

            if (disabled)
            {
                return false;
            }

            if (step > 0f)
            {
                updated = Mathf.Round(updated / step) * step;
            }

            updated = Mathf.Clamp(updated, min, max);
            if (Mathf.Abs(updated - value) < 0.0001f)
            {
                return false;
            }

            value = updated;
            return true;
        }

        /// <summary>
        /// Draws an enum dropdown button.
        /// </summary>
        public static void DrawEnum(
            Rect rect,
            string label,
            object currentValue,
            Type enumType,
            string tooltip = null,
            bool disabled = false,
            Action<object> onSelected = null,
            Func<object, string> labelProvider = null,
            Func<object, string> descriptionProvider = null)
        {
            var buttonRect = rect.RightPart(0.48f);
            var labelRect = new Rect(
                rect.x,
                rect.y,
                Mathf.Max(0f, buttonRect.x - rect.x - 6f),
                rect.height);

            DrawSettingLabel(labelRect, label, disabled);

            bool prevEnabled = GUI.enabled;
            if (disabled)
            {
                GUI.enabled = false;
                GUI.color = Color.gray;
            }

            string currentLabel = ResolveEnumLabel(
                enumType,
                currentValue,
                labelProvider);

            if (Widgets.ButtonText(buttonRect, currentLabel) && enumType != null)
            {
                var options = new List<FloatMenuOption>();
                var optionDescriptions = new Dictionary<FloatMenuOption, string>();
                var seenValues = new HashSet<long>();
                FloatMenuOption selectedOption = null;
                long currentNumericValue = Convert.ToInt64(currentValue);
                foreach (var enumValue in Enum.GetValues(enumType))
                {
                    // Enum aliases are useful for serialized-setting migrations, but should not
                    // create duplicate choices in the player-facing dropdown.
                    if (!seenValues.Add(Convert.ToInt64(enumValue)))
                    {
                        continue;
                    }

                    var local = enumValue;
                    string optionLabel = ResolveEnumLabel(
                        enumType,
                        local,
                        labelProvider);
                    var option = new FloatMenuOption(optionLabel, () => onSelected?.Invoke(local));
                    options.Add(option);
                    optionDescriptions[option] = ResolveEnumDescription(
                        local,
                        tooltip,
                        descriptionProvider);
                    if (Convert.ToInt64(local) == currentNumericValue)
                    {
                        selectedOption = option;
                    }
                }

                Find.WindowStack.Add(new DescribedFloatMenu(options, selectedOption, label, tooltip, optionDescriptions));
            }

            if (disabled)
            {
                GUI.enabled = prevEnabled;
                GUI.color = Color.white;
            }

            if (!string.IsNullOrEmpty(tooltip) && !DescribedFloatMenu.AnyOpen)
            {
                // Keep the value button free of tooltip ownership. Otherwise a tooltip that was
                // opened over the button can remain above the enum menu and obscure its choices.
                TooltipHandler.TipRegion(labelRect, tooltip);
            }
        }

        private static string ResolveEnumLabel(
            Type enumType,
            object value,
            Func<object, string> labelProvider)
        {
            if (enumType == null || value == null)
            {
                return string.Empty;
            }

            string suppliedLabel = labelProvider?.Invoke(value);
            if (!string.IsNullOrWhiteSpace(suppliedLabel))
            {
                return suppliedLabel;
            }

            return value.ToString();
        }

        private static string ResolveEnumDescription(
            object value,
            string settingDescription,
            Func<object, string> descriptionProvider)
        {
            if (value == null)
            {
                return settingDescription ?? string.Empty;
            }

            string suppliedDescription = descriptionProvider?.Invoke(value);
            if (!string.IsNullOrWhiteSpace(suppliedDescription))
            {
                return suppliedDescription;
            }

            return settingDescription ?? string.Empty;
        }

        /// <summary>
        /// Draws a clickable button.
        /// </summary>
        public static bool DrawButton(
            Rect rect,
            string label,
            string tooltip = null,
            bool disabled = false)
        {
            bool prevEnabled = GUI.enabled;
            if (disabled)
            {
                GUI.enabled = false;
                GUI.color = Color.gray;
            }

            bool clicked = Widgets.ButtonText(rect, label);

            if (disabled)
            {
                GUI.enabled = prevEnabled;
                GUI.color = Color.white;
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }

            return clicked;
        }

        /// <summary>
        /// Draws a styled section header.
        /// </summary>
        public static void DrawHeader(Rect rect, string label, Color? color = null)
        {
            var oldFont = Text.Font;
            var oldAnchor = Text.Anchor;
            var oldColor = GUI.color;

            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Color accent = ResolveSectionAccent(color);
            GUI.color = accent;
            Rect labelRect = new Rect(rect.x + 10f, rect.y, Mathf.Max(0f, rect.width - 10f), rect.height);
            Widgets.Label(labelRect, label);
            Widgets.DrawBoxSolid(new Rect(
                labelRect.x,
                rect.yMax - 4f,
                labelRect.width,
                2f), accent);

            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }

        internal static void DrawSubheader(Rect rect, string label, Color? color = null)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            Color accent = ResolveSectionAccent(color);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.Lerp(SettingLabelColor, accent, 0.72f);
            Rect labelRect = new Rect(rect.x + 8f, rect.y, Mathf.Max(0f, rect.width - 8f), rect.height);
            Widgets.Label(labelRect, label);
            Widgets.DrawBoxSolid(
                new Rect(labelRect.x, rect.yMax - 3f, labelRect.width, 1f),
                new Color(accent.r, accent.g, accent.b, 0.58f));

            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }

        internal static Color ResolveSectionAccent(Color? color = null)
        {
            return color ?? DefaultSectionAccent;
        }

        internal static void DrawSectionPanel(Rect rect, float headerHeight, Color? color = null)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Color accent = ResolveSectionAccent(color);
            Color panelColor = new Color(0.075f, 0.075f, 0.075f, 0.86f);
            Color headerColor = Color.Lerp(
                new Color(0.11f, 0.11f, 0.11f, 0.72f),
                new Color(accent.r, accent.g, accent.b, 0.72f),
                0.12f);
            Color edgeColor = new Color(0.34f, 0.34f, 0.34f, 0.68f);
            Color innerEdgeColor = new Color(accent.r, accent.g, accent.b, 0.34f);
            Widgets.DrawBoxSolid(rect, panelColor);
            Widgets.DrawBoxSolid(new Rect(
                rect.x + 2f,
                rect.y + 2f,
                Mathf.Max(0f, rect.width - 4f),
                Mathf.Min(Mathf.Max(0f, headerHeight - 1f), Mathf.Max(0f, rect.height - 4f))), headerColor);

            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width, 1f), edgeColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), edgeColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 1f, rect.height), edgeColor);
            Widgets.DrawBoxSolid(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), edgeColor);
            Widgets.DrawBoxSolid(new Rect(rect.x + 1f, rect.y + headerHeight, Mathf.Max(0f, rect.width - 2f), 1f), innerEdgeColor);

        }

        private static void DrawSettingLabel(Rect rect, string label, bool disabled)
        {
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (!disabled)
            {
                GUI.color = SettingLabelColor;
            }

            Widgets.Label(rect, label);
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }

    }
}
