using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Spine.Api;
using Spine.UI.SettingsFramework;
using UnityEngine;
using Verse;

namespace Spine.UI.ContextualSettings
{
    internal sealed class ContextualSettingsService : IContextualSettingsFacade
    {
        internal const string ApiId = "CoolNether123.Spine.ContextualSettings";
        internal const string HarmonyId = ApiId;
        internal static readonly SemanticVersion ApiVersion = new SemanticVersion(1, 0, 0);
        private const string DefaultHint = "Alt-click to open settings";

        private static readonly object Sync = new object();
        private static readonly FieldInfo DialogModField = AccessTools.Field(
            typeof(Dialog_ModSettings),
            "mod");

        private readonly ContextualSettingsRouterCore router =
            new ContextualSettingsRouterCore();
        private readonly DeferredContextualActionQueue deferred =
            new DeferredContextualActionQueue();
        private readonly Dictionary<string, ConsumerRegistration> consumers =
            new Dictionary<string, ConsumerRegistration>(StringComparer.Ordinal);
        private HarmonyLib.Harmony harmony;
        private bool installed;
        private bool routedCurrentEvent;
        private Event routedEvent;

        internal static readonly ContextualSettingsService Instance =
            new ContextualSettingsService();

        private ContextualSettingsService()
        {
        }

        public SpineApiDescriptor Descriptor => new SpineApiDescriptor(
            ApiId,
            ApiVersion,
            SpineCapability.ContextualSettings | SpineCapability.Settings);

        public IContextualSettingsLease Acquire(
            string consumerId,
            Mod mod,
            SettingsListDrawer settingsDrawer,
            object settingsObject)
        {
            if (string.IsNullOrWhiteSpace(consumerId))
            {
                throw new ArgumentException(
                    "A contextual-settings consumer identifier is required.",
                    nameof(consumerId));
            }

            if (mod == null) throw new ArgumentNullException(nameof(mod));
            if (settingsDrawer == null) throw new ArgumentNullException(nameof(settingsDrawer));
            if (settingsObject == null) throw new ArgumentNullException(nameof(settingsObject));

            lock (Sync)
            {
                if (consumers.ContainsKey(consumerId))
                {
                    throw new InvalidOperationException(
                        "Contextual settings consumer is already registered: " + consumerId);
                }

                var registration = new ConsumerRegistration(
                    consumerId,
                    mod,
                    settingsDrawer,
                    settingsObject);
                consumers.Add(consumerId, registration);
                router.Acquire(consumerId);
                EnsureInstalled();
                return new Lease(this, registration);
            }
        }

        private bool Bind(
            ConsumerRegistration consumer,
            Rect visibleRect,
            ContextualSettingsTarget target,
            ContextualSettingsBindingOptions options)
        {
            if (consumer == null || consumer.Released)
            {
                return false;
            }

            Event evt = Event.current;
            if (options.RegisterTooltip)
            {
                string tooltip = ComposeTooltip(options.FeatureTooltip, options.SettingsHint);
                if (!string.IsNullOrEmpty(tooltip))
                {
                    TooltipHandler.TipRegion(visibleRect, tooltip);
                }
            }

            long frame = Time.frameCount;
            if (evt != null && evt.type == EventType.Repaint)
            {
                lock (Sync)
                {
                    router.Register(
                        consumer.ConsumerId,
                        new ContextualHitRect(
                            visibleRect.x,
                            visibleRect.y,
                            visibleRect.width,
                            visibleRect.height),
                        target,
                        options.Priority,
                        frame);
                }
            }

            if (evt == null || routedEvent == evt && routedCurrentEvent)
            {
                return evt != null && evt.type == EventType.Used;
            }

            routedEvent = evt;
            routedCurrentEvent = true;
            var pointerEvent = new ContextualPointerEvent(
                ConvertEventType(evt.type),
                evt.button,
                evt.alt,
                evt.mousePosition.x,
                evt.mousePosition.y);

            ContextualBindingRecord winner;
            lock (Sync)
            {
                if (!router.TryRoute(pointerEvent, frame, out winner) ||
                    !consumers.TryGetValue(winner.ConsumerId, out ConsumerRegistration winnerConsumer) ||
                    winnerConsumer.Released)
                {
                    return false;
                }

                if (!deferred.Enqueue(() => OpenSettings(winnerConsumer, winner.Target)))
                {
                    return false;
                }
            }

            evt.Use();
            return true;
        }

        private static string ComposeTooltip(string featureTooltip, string settingsHint)
        {
            string hint = string.IsNullOrWhiteSpace(settingsHint)
                ? DefaultHint
                : settingsHint.Trim();
            if (string.IsNullOrWhiteSpace(featureTooltip))
            {
                return hint;
            }

            return featureTooltip.TrimEnd() + "\n\n" + hint;
        }

        private static ContextualPointerEventType ConvertEventType(EventType type)
        {
            switch (type)
            {
                case EventType.MouseDown:
                    return ContextualPointerEventType.MouseDown;
                case EventType.MouseMove:
                    return ContextualPointerEventType.MouseMove;
                case EventType.Repaint:
                    return ContextualPointerEventType.Repaint;
                default:
                    return ContextualPointerEventType.None;
            }
        }

        private void EnsureInstalled()
        {
            if (installed)
            {
                return;
            }

            MethodInfo update = AccessTools.Method(typeof(Root), "Update");
            if (update == null)
            {
                throw new MissingMethodException(typeof(Root).FullName, "Update");
            }

            harmony = new HarmonyLib.Harmony(HarmonyId);
            harmony.Patch(
                update,
                postfix: new HarmonyMethod(
                    typeof(ContextualSettingsService),
                    nameof(AfterRootUpdate)));
            installed = true;
        }

        private static void AfterRootUpdate()
        {
            Instance.DrainDeferred();
        }

        private void DrainDeferred()
        {
            lock (Sync)
            {
                routedCurrentEvent = false;
                routedEvent = null;
                deferred.Drain(exception =>
                    Log.ErrorOnce(
                        "[Spine] Contextual settings navigation failed safely: " + exception,
                        172384921));
            }
        }

        private static void OpenSettings(
            ConsumerRegistration consumer,
            ContextualSettingsTarget target)
        {
            if (consumer == null || consumer.Released || Find.WindowStack == null)
            {
                return;
            }

            consumer.SettingsDrawer.PrepareContextNavigation(
                target,
                consumer.SettingsObject);

            Dialog_ModSettings current =
                Find.WindowStack.WindowOfType<Dialog_ModSettings>();
            if (current != null)
            {
                Mod currentMod = DialogModField?.GetValue(current) as Mod;
                if (ReferenceEquals(currentMod, consumer.Mod))
                {
                    return;
                }

                Find.WindowStack.TryRemove(current, false);
            }

            Find.WindowStack.Add(new Dialog_ModSettings(consumer.Mod));
        }

        private void Release(ConsumerRegistration consumer)
        {
            lock (Sync)
            {
                if (consumer == null || consumer.Released)
                {
                    return;
                }

                consumer.Released = true;
                consumers.Remove(consumer.ConsumerId);
                router.Release(consumer.ConsumerId);
                if (consumers.Count != 0 || !installed)
                {
                    return;
                }

                deferred.Clear();
                harmony.UnpatchAll(HarmonyId);
                harmony = null;
                installed = false;
            }
        }

        private sealed class ConsumerRegistration
        {
            internal ConsumerRegistration(
                string consumerId,
                Mod mod,
                SettingsListDrawer settingsDrawer,
                object settingsObject)
            {
                ConsumerId = consumerId;
                Mod = mod;
                SettingsDrawer = settingsDrawer;
                SettingsObject = settingsObject;
            }

            internal string ConsumerId { get; }
            internal Mod Mod { get; }
            internal SettingsListDrawer SettingsDrawer { get; }
            internal object SettingsObject { get; }
            internal bool Released { get; set; }
        }

        private sealed class Lease : IContextualSettingsLease
        {
            private readonly ContextualSettingsService owner;
            private readonly ConsumerRegistration consumer;

            internal Lease(
                ContextualSettingsService owner,
                ConsumerRegistration consumer)
            {
                this.owner = owner;
                this.consumer = consumer;
            }

            public bool Bind(
                Rect visibleRect,
                ContextualSettingsTarget target,
                ContextualSettingsBindingOptions options = default(ContextualSettingsBindingOptions)) =>
                owner.Bind(consumer, visibleRect, target, options);

            public void Dispose() => owner.Release(consumer);
        }
    }
}
