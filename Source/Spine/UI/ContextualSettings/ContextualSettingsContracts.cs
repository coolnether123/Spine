using System;
using Spine.Api;
using Spine.UI.SettingsFramework;
using UnityEngine;
using Verse;

namespace Spine.UI.ContextualSettings
{
    public enum ContextualSettingsTargetLevel
    {
        Root = 0,
        Group = 1,
        Exact = 2
    }

    public readonly struct ContextualSettingsTarget
    {
        private ContextualSettingsTarget(
            ContextualSettingsTargetLevel level,
            string settingId,
            string fallbackGroupId)
        {
            Level = level;
            SettingId = settingId;
            FallbackGroupId = fallbackGroupId;
        }

        public ContextualSettingsTargetLevel Level { get; }
        public string SettingId { get; }
        public string FallbackGroupId { get; }

        public static ContextualSettingsTarget Exact(
            string settingId,
            string fallbackGroupId = null) =>
            new ContextualSettingsTarget(
                ContextualSettingsTargetLevel.Exact,
                RequireId(settingId),
                fallbackGroupId);

        public static ContextualSettingsTarget Group(string settingId) =>
            new ContextualSettingsTarget(
                ContextualSettingsTargetLevel.Group,
                RequireId(settingId),
                null);

        public static ContextualSettingsTarget Root() =>
            new ContextualSettingsTarget(
                ContextualSettingsTargetLevel.Root,
                null,
                null);

        private static string RequireId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A contextual settings target identifier is required.",
                    nameof(value));
            }

            return value;
        }
    }

    public readonly struct ContextualSettingsBindingOptions
    {
        /// <summary>
        /// Configures overlap priority and, when the feature already owns a
        /// useful tooltip, lets Spine register that tooltip once. Contextual
        /// settings never appends an Alt-click hint to world or feature UI.
        /// </summary>
        public ContextualSettingsBindingOptions(
            int priority = 0,
            string featureTooltip = null,
            string settingsHint = null,
            bool registerTooltip = false)
        {
            Priority = priority;
            FeatureTooltip = featureTooltip;
            SettingsHint = settingsHint;
            RegisterTooltip = registerTooltip;
        }

        public int Priority { get; }
        public string FeatureTooltip { get; }

        /// <summary>
        /// Retained for binary compatibility. Spine no longer displays
        /// contextual-settings hints beside gameplay features.
        /// </summary>
        public string SettingsHint { get; }
        public bool RegisterTooltip { get; }

        public static ContextualSettingsBindingOptions WithTooltip(
            string featureTooltip,
            int priority = 0,
            string settingsHint = null) =>
            new ContextualSettingsBindingOptions(
                priority,
                featureTooltip,
                settingsHint,
                true);

        public static ContextualSettingsBindingOptions HintOnly(
            int priority = 0,
            string settingsHint = null) =>
            new ContextualSettingsBindingOptions(
                priority,
                null,
                settingsHint,
                false);
    }

    public interface IContextualSettingsLease : IDisposable
    {
        bool Bind(
            Rect visibleRect,
            ContextualSettingsTarget target,
            ContextualSettingsBindingOptions options = default(ContextualSettingsBindingOptions));
    }

    public interface IContextualSettingsFacade
    {
        SpineApiDescriptor Descriptor { get; }

        IContextualSettingsLease Acquire(
            string consumerId,
            Mod mod,
            SettingsListDrawer settingsDrawer,
            object settingsObject);
    }
}
