using System;
using System.Collections.Generic;
using System.Linq;
using Spine.Api;
using Spine.Caching;
using Spine.Harmony;
using Spine.UI.ContextualSettings;
using Spine.UI.SettingsFramework;
using static RimWorld.ModTestSupport.Test;

namespace Spine.Tests
{
    internal static class Program
    {
        private static int Main()
        {
            Start("Spine contracts");
            Run("cache bounds eviction reset and accounting", TestBoundedCache);
            Run("semantic version and capability comparison", TestVersionCapabilities);
            Run("patch operation identities", TestPatchOperationIdentities);
            Run("contextual Alt-left detection and rejection", TestContextualInput);
            Run("contextual overlap priority and duplicate binding", TestContextualOverlap);
            Run("contextual multiple consumers release and cleanup", TestContextualLifecycle);
            Run("contextual deferred opening and exception isolation", TestContextualDeferredActions);
            Run("contextual exact group and root navigation", TestContextualNavigation);
            Run("contextual scroll and highlight presentation", TestContextualPresentation);
            Run("compact settings presentation thresholds", TestSettingsPresentationPolicy);
            Run("settings preparation is lazy and idempotent", TestSettingsPreparation);
            Run("legacy settings factories retain the 1.0 contract", TestLegacySettingDefinitions);
            Run("typed settings schema builds compatible definitions", TestSettingsSchema);
            Run("typed settings schema preserves definition metadata", TestSettingsSchemaMetadata);
            Run("contextual tooltips never add hints", TestContextualTooltipComposition);

            return Finish();
        }

        private static void TestBoundedCache()
        {
            var released = new List<string>();
            var cache = new BoundedLruCache<string, string>(
                10,
                release: (key, value) => released.Add(key));
            Require(cache.AddOrUpdate("a", "A", 4), "add a");
            Require(cache.AddOrUpdate("b", "B", 4), "add b");
            Require(
                cache.TryGet("a", out string value) && value == "A",
                "hit a");
            Require(!cache.TryGet("missing", out _), "miss missing");
            Require(cache.AddOrUpdate("c", "C", 4), "add c");
            Equal(2, cache.EntryCount, "entry count after eviction");
            Equal(8L, cache.UsedBytes, "bytes after eviction");
            Equal(1L, cache.Evictions, "eviction count");
            Require(released.Contains("b"), "least-recent b released");
            Require(!cache.AddOrUpdate("oversized", "X", 11), "oversized rejected");
            Equal(1L, cache.Hits, "hit counter");
            Equal(1L, cache.Misses, "miss counter");

            cache.Reset();
            Equal(0, cache.EntryCount, "reset count");
            Equal(0L, cache.UsedBytes, "reset bytes");
            Equal(0L, cache.Hits, "reset hits");
            Equal(0L, cache.Misses, "reset misses");
            Equal(0L, cache.Evictions, "reset evictions");
        }

        private static void TestVersionCapabilities()
        {
            SemanticVersion preview = SemanticVersion.Parse("1.2.3-alpha.1");
            SemanticVersion release = SemanticVersion.Parse("1.2.3");
            SemanticVersion next = SemanticVersion.Parse("1.3.0");
            Require(preview < release, "prerelease precedes release");
            Require(release < next, "minor comparison");
            Equal(1UL << 2, (ulong)SpineCapability.BoundedCaches,
                "bounded-cache capability id is stable");
            Equal(1UL << 8, (ulong)SpineCapability.Settings,
                "settings capability id is stable");
            Equal(1UL << 9, (ulong)SpineCapability.HarmonyPatching,
                "Harmony capability id is stable");
            Equal(1UL << 10, (ulong)SpineCapability.FluentTranspilers,
                "fluent-transpiler capability id is stable");
            Equal(1UL << 11, (ulong)SpineCapability.TooltipSizing,
                "tooltip capability id is stable");
            Equal(1UL << 12, (ulong)SpineCapability.ContextualSettings,
                "contextual-settings capability id is stable");
            Equal(1UL << 13, (ulong)SpineCapability.ModSettingsPages,
                "settings-page capability id is stable");
            Equal(1UL << 14, (ulong)SpineCapability.SettingsSchema,
                "settings-schema capability id is stable");
            Equal(1UL << 15, (ulong)SpineCapability.SettingsPreviewTransactions,
                "settings-preview transaction capability id is stable");

            var descriptor = new SpineApiDescriptor(
                "spine",
                release,
                SpineCapability.BoundedCaches |
                SpineCapability.FluentTranspilers);
            Require(
                descriptor.Supports(
                    new SemanticVersion(1, 2, 0),
                    SpineCapability.BoundedCaches | SpineCapability.FluentTranspilers),
                "supported handshake");
            Require(
                !descriptor.Supports(next, SpineCapability.BoundedCaches),
                "minimum version rejected");
            Require(
                !descriptor.Supports(release, SpineCapability.TooltipSizing),
                "missing capability rejected");

            var requirement = new SpineRequirement(
                "Spine.Tests",
                new SemanticVersion(1, 0, 0),
                SpineCapability.Settings |
                SpineCapability.TooltipSizing |
                SpineCapability.ContextualSettings |
                SpineCapability.ModSettingsPages |
                SpineCapability.SettingsSchema |
                SpineCapability.SettingsPreviewTransactions);
            var runtime = SpineRuntimeFacade.Instance;
            var supported = runtime.Check(requirement);
            Require(
                supported.IsCompatible,
                "runtime facade supports current requirement");
            Equal(
                "CoolNether123.Spine",
                runtime.Descriptor.ApiId,
                "runtime facade API id");
            Equal(
                new SemanticVersion(1, 1, 0),
                runtime.Descriptor.Version,
                "settings-page capability runtime version");
            var descriptorField = typeof(SpineRuntimeFacade).GetField(
                "CurrentDescriptor",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Require(
                descriptorField != null && descriptorField.IsInitOnly,
                "runtime descriptor is cached in a static readonly field");
            runtime.Require(requirement);

            const SpineCapability unknownCapability =
                (SpineCapability)(1UL << 63);
            var unavailable = runtime.Check(new SpineRequirement(
                "Future.Consumer",
                new SemanticVersion(1, 0, 0),
                unknownCapability));
            Require(
                !unavailable.IsCompatible,
                "runtime facade rejects missing capability");
            Equal(
                unknownCapability,
                unavailable.MissingCapabilities,
                "runtime facade reports exact missing capability");

            var tooNew = runtime.Check(new SpineRequirement(
                "Future.Consumer",
                new SemanticVersion(2, 0, 0),
                SpineCapability.None));
            Require(
                !tooNew.IsCompatible,
                "runtime facade rejects newer minimum");
        }

        private static void TestSettingsPreparation()
        {
            var settings = new CountingSettings();
            CountingSettings.ConstructorCalls = 0;
            var schema = new SettingsSchema<CountingSettings>();
            schema.Root.Toggle(
                "enabled",
                value => value.Enabled,
                "Enabled");
            schema.Root.Define("header", SettingType.Header, "Header");

            SettingsPreparation.Prepare(settings, schema.Definitions);
            Equal(1, CountingSettings.ConstructorCalls,
                "first unresolved default creates one pristine settings object");
            Equal(true, schema.Definitions[0].DefaultValue,
                "default is read from the pristine settings object");
            Equal(0, schema.Definitions[0].SortOrder, "first sort order prepared");
            Equal(1, schema.Definitions[1].SortOrder, "second sort order prepared");

            SettingsPreparation.Prepare(settings, schema.Definitions);
            Equal(1, CountingSettings.ConstructorCalls,
                "prepared definitions do not create another pristine object");
        }

#pragma warning disable CS0618
        private static void TestLegacySettingDefinitions()
        {
            Require(
                Attribute.IsDefined(typeof(SettingDefinitions), typeof(ObsoleteAttribute)),
                "legacy settings facade is marked obsolete");

            SettingDefinition toggle = SettingDefinitions.Toggle(
                "enabled",
                "Enabled",
                "Enabled",
                labelKey: "Enabled_Label",
                tooltip: "Enabled tooltip",
                tooltipKey: "Enabled_Tooltip",
                parentId: "general",
                simple: false,
                controlsChildren: true,
                scribeKey: "legacyEnabled");
            Equal(SettingType.Bool, toggle.Type, "legacy toggle type");
            Equal("Enabled", toggle.FieldName, "legacy toggle field");
            Equal("general", toggle.ParentId, "legacy toggle parent");
            Equal("legacyEnabled", toggle.ScribeKey, "legacy toggle scribe key");
            Require(!toggle.ShowInSimpleView, "legacy toggle simple-view flag");
            Require(toggle.ControlsChildVisibility, "legacy toggle child control");

            SettingDefinition slider = SettingDefinitions.Slider(
                "size", "Size", "Size", scribeKey: "legacySize");
            Equal(SettingType.Slider, slider.Type, "legacy slider type");
            Equal("legacySize", slider.ScribeKey, "legacy slider scribe key");

            SettingDefinition enumDefinition = SettingDefinitions.Enum(
                "mode", "Mode", typeof(SchemaMode), "Mode");
            Equal(typeof(SchemaMode), enumDefinition.EnumType, "legacy enum type");

            SettingDefinition colour = SettingDefinitions.Colour(
                "color", "Color", "Color", tooltipKey: "Color_Tooltip");
            Equal(SettingType.Color, colour.Type, "legacy colour type");
            Equal("Color_Tooltip", colour.TooltipKey, "legacy colour tooltip key");

            SettingDefinition header = SettingDefinitions.Header("general", "General");
            Equal(SettingType.Header, header.Type, "legacy header type");
            SettingDefinition button = SettingDefinitions.Button("reset", "Reset", _ => { });
            Equal(SettingType.Button, button.Type, "legacy button type");
            SettingDefinition custom = SettingDefinitions.Custom("custom", null);
            Equal(SettingType.Custom, custom.Type, "legacy custom type");
        }
#pragma warning restore CS0618

        private static void TestSettingsSchema()
        {
            var schema = new SettingsSchema<SchemaSettings>(
                SettingsSchemaConventions.LowerCamelCase,
                definition => definition.SearchKeywords = new[] { "schema" });
            SettingsScope<SchemaSettings> section = schema.Section(
                "group",
                "Group");
            SettingDefinition enabled = section.Toggle(
                    "enabled",
                    settings => settings.Enabled,
                    "Enabled",
                    "Tooltip",
                    onChanged: settings => settings.Changed++).
                DefaultTo(true).
                ControlsChildren().
                ScribeAs("legacyKey");
            SettingDefinition size = section.Slider(
                "size",
                settings => settings.Size,
                "Size",
                "Tooltip",
                onChanged: settings => settings.Changed++).
                Range(0f, 1f);
            SettingDefinition mode = section.Enum(
                "mode",
                settings => settings.Mode,
                "Mode",
                "Tooltip",
                labelProvider: value => value.ToString());
            SettingDefinition colour = section.Colour(
                "color",
                settings => settings.Color,
                "Color",
                "Tooltip")
                .Localized("ColorLabel", "ColorTooltip");

            Equal(5, schema.Definitions.Count,
                "section header and four rows retain insertion order");
            Equal("group", schema.Definitions[0].Id,
                "section creates a header row");
            Equal("group", enabled.ParentId,
                "section rows target the section header");
            Equal("legacyKey", enabled.ScribeKey,
                "explicit scribe refinement wins");
            Equal("size", size.ScribeKey,
                "lower-camel convention preserves an already lower-camel key");
            Equal("mode", mode.ScribeKey,
                "enum receives a convention key");
            Equal("color", colour.ScribeKey,
                "colour receives a convention key");
            Equal("schema", colour.SearchKeywords[0],
                "schema decorator applies to every added definition");
            Equal("ColorLabel", colour.LabelKey,
                "localized refinement sets the label key");
            Equal("ColorTooltip", colour.TooltipKey,
                "localized refinement sets the tooltip key");
            Require(enabled.ControlsChildVisibility, "children refinement enabled");
            Equal(true, enabled.DefaultValue, "default refinement applied");
            Equal("showPreview",
                SettingsSchemaConventions.LowerCamelCase("ShowPreview"),
                "lower-camel convention converts PascalCase fields");
            Equal("Mode", mode.EnumLabelProvider(SchemaMode.Mode),
                "typed enum label provider adapts to object callback");
            var changedSettings = new SchemaSettings();
            enabled.OnChanged(changedSettings);
            Equal(1, changedSettings.Changed,
                "typed change callback adapts to the runtime object callback");

            var underSchema = new SettingsSchema<SchemaSettings>();
            SettingDefinition under = underSchema.Under("parent").Toggle(
                "under",
                settings => settings.Enabled,
                "Under");
            Equal(1, underSchema.Definitions.Count,
                "Under adds no row of its own");
            Equal("parent", under.ParentId,
                "Under scopes subsequent rows under its parent");

            SettingDefinition nested = underSchema.Root.Under("nested-parent").Toggle(
                "nested",
                settings => settings.Enabled,
                "Nested");
            Equal("nested-parent", nested.ParentId,
                "root scopes can create nested scopes directly");

            SettingDefinition derived = underSchema.Root.DerivedEnum(
                "derived",
                settings => settings.Mode,
                (settings, value) => settings.Mode = value,
                "Derived mode",
                labelProvider: value => "Derived:" + value);
            SettingDefinition readOnly = underSchema.Root.ReadOnly(
                "summary",
                settings => settings.Mode.ToString(),
                "Summary");
            var derivedSettings = new SchemaSettings();
            derived.ValueSetter(derivedSettings, SchemaMode.Mode);
            Equal(SchemaMode.Mode, derived.ValueGetter(derivedSettings),
                "derived enum adapts typed getter and setter callbacks");
            Equal("Derived:Mode", derived.EnumLabelProvider(SchemaMode.Mode),
                "derived enum retains its typed label provider");
            Equal("Mode", readOnly.ReadOnlyValueProvider(derivedSettings),
                "read-only row adapts its typed value provider");

            bool resetCalled = false;
            nested.WithCustomReset(_ => true, _ => resetCalled = true);
            Require(nested.CustomHasNonDefaultValue(new SchemaSettings()),
                "custom reset refinement stores the non-default predicate");
            nested.CustomReset(new SchemaSettings());
            Require(resetCalled,
                "custom reset refinement stores the reset action");

            var nullConvention = new SettingsSchema<SchemaSettings>();
            SettingDefinition root = nullConvention.Root.Toggle(
                "root",
                settings => settings.Enabled,
                "Root");
            Equal<string>(null, root.ScribeKey,
                "null convention preserves null scribe-key behavior");

            RequireRejectsSelector(
                () => schema.Root.Toggle(
                    "property",
                    settings => settings.EnabledProperty,
                    "Property"),
                "property selector rejected");
            RequireRejectsSelector(
                () => schema.Root.Toggle(
                    "nested",
                    settings => settings.Nested.Enabled,
                    "Nested"),
                "nested selector rejected");
            RequireRejectsSelector(
                () => schema.Root.Toggle(
                    "method",
                    settings => settings.ReadEnabled(),
                    "Method"),
                "method selector rejected");
        }

        private static void TestSettingsSchemaMetadata()
        {
            Equal(6, (int)SettingType.Slider, "existing slider enum value retained");
            Equal(7, (int)SettingType.Int, "new integer enum value appended");
            Equal(8, (int)SettingType.Float, "new float enum value appended");
            Equal(9, (int)SettingType.Spacer, "new spacer enum value appended");
            Equal(10, (int)SettingType.DropdownListAdder,
                "new dropdown enum value appended");
            Equal(11, (int)SettingType.NumericInt,
                "new numeric-integer enum value appended");
            Equal(12, (int)SettingType.ReadOnly,
                "read-only enum value appended");

            var schema = new SettingsSchema<SchemaSettings>(
                SettingsSchemaConventions.LowerCamelCase);
            SettingDefinition integer = schema.Root.Int(
                "count",
                settings => settings.Count,
                "Count",
                "Count tooltip").
                DefaultTo(3).
                ValueRange(-2f, 8f).
                ValueLabels("Low", "High").
                FormattedAs("{0:0}");
            SettingDefinition floating = schema.Root.Float(
                "ratio",
                settings => settings.Ratio,
                "Ratio");
            SettingDefinition numeric = schema.Root.NumericInt(
                "numeric",
                settings => settings.Count,
                "Numeric");
            SettingDefinition button = schema.Root.Button(
                "button",
                "Apply",
                "Apply tooltip",
                settings => settings.Changed++);
            SettingDefinition dropdown = schema.Root.DropdownListAdder(
                "options",
                "Add option",
                () => new[] { "one", "two" },
                value => { });
            SettingDefinition custom = schema.Root.Custom(
                "custom",
                (rect, label, tooltip, settings, disabled) =>
                {
                    settings.Changed++;
                    return true;
                },
                "Custom",
                "Custom tooltip");
            SettingDefinition arbitrary = schema.Root.Field(
                "arbitrary",
                settings => settings.Selected,
                SettingType.Custom,
                "Arbitrary",
                configure: definition =>
                {
                    definition.SearchKeywords = new[] { "legacy", "alias" };
                    definition.Classification = SettingClassification.State;
                    definition.DisableAutoScribe = true;
                    definition.CustomHasNonDefaultValue = _ => true;
                    definition.CustomReset = _ => { };
                    definition.Suppressions = new List<SettingSuppression>
                    {
                        new SettingSuppression
                        {
                            When = _ => true,
                            Reason = _ => "Managed elsewhere"
                        }
                    };
                });
            var configuredSchema = new SettingsSchema<SchemaSettings>();
            configuredSchema.Section("header", "Header", null, definition =>
                {
                    definition.HeaderColor = new UnityEngine.Color();
                    definition.EmphasizeAsHeader = true;
                });
            SettingDefinition header = configuredSchema.Definitions[0];

            Equal(SettingType.Int, integer.Type, "typed integer definition type");
            Equal(nameof(SchemaSettings.Count), integer.FieldName,
                "typed integer field name");
            Equal("count", integer.ScribeKey, "typed integer scribe key convention");
            Equal(3, integer.DefaultValue, "integer default refinement");
            Equal(-2f, integer.MinValue, "integer lower bound");
            Equal(8f, integer.MaxValue, "integer upper bound");
            Equal("Low", integer.MinLabel, "numeric lower label");
            Equal("High", integer.MaxLabel, "numeric upper label");
            Equal("{0:0}", integer.ValueFormat, "numeric value format");
            Equal(SettingType.Float, floating.Type, "typed float definition type");
            Equal(SettingType.NumericInt, numeric.Type,
                "typed numeric integer definition type");
            var changedSettings = new SchemaSettings();
            button.OnChanged(changedSettings);
            Equal(1, changedSettings.Changed, "typed button callback adapts");
            Equal(SettingType.DropdownListAdder, dropdown.Type,
                "dropdown definition type");
            Equal("one", dropdown.DropdownOptionsProvider().First(),
                "dropdown options provider retained");
            Equal(SettingType.Custom, custom.Type, "custom definition type");
            Require(custom.CustomDrawer(
                    new UnityEngine.Rect(), "Custom", "Tooltip", changedSettings, false),
                "typed custom drawer adapts");
            Equal(2, changedSettings.Changed, "custom callback invoked");
            Equal(SettingClassification.State, arbitrary.Classification,
                "arbitrary definition preserves legacy classification");
            Require(arbitrary.DisableAutoScribe, "arbitrary scribe opt-out preserved");
            Equal("legacy", arbitrary.SearchKeywords[0],
                "arbitrary search metadata preserved");
            Require(arbitrary.GetActiveSuppression(changedSettings) != null,
                "suppression metadata is usable through the definition");
            Equal("header", header.Id, "configured section still adds a header");
            Require(header.EmphasizeAsHeader, "header configuration retained");

            SettingDefinition suppressed = schema.Root.Toggle(
                "suppressed",
                settings => settings.Enabled,
                "Suppressed").SuppressedWhen<SchemaSettings>(
                    settings => !settings.Enabled,
                    settings => "Managed elsewhere",
                    suppressorSettingId: "count",
                    linkLabel: "Count");
            var suppressedSettings = new SchemaSettings();
            suppressedSettings.Enabled = false;
            SettingSuppression activeSuppression =
                suppressed.GetActiveSuppression(suppressedSettings);
            Require(activeSuppression != null, "typed suppression activates");
            Equal("Managed elsewhere", activeSuppression.ResolveReason(suppressedSettings),
                "typed suppression reason adapts");
            Equal("count", activeSuppression.SuppressorSettingId,
                "suppression target retained");
            suppressedSettings.Enabled = true;
            Require(suppressed.GetActiveSuppression(suppressedSettings) == null,
                "typed suppression deactivates");

            var hierarchy = new SettingsHierarchy(new[]
            {
                header,
                schema.Root.Spacer("gap"),
                integer
            });
            Equal(1, hierarchy.SettingCount,
                "headers and spacers are excluded from configurable count");
            Require(hierarchy.GetFlattenedForView(SettingsViewMode.All, suppressedSettings)
                .Any(definition => definition.Type == SettingType.Spacer),
                "spacer remains in display traversal");
        }

        private static void RequireRejectsSelector(
            Action action,
            string description)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Require(rejected, description);
        }

        private sealed class CountingSettings
        {
            internal static int ConstructorCalls;

            public CountingSettings()
            {
                ConstructorCalls++;
            }

            public bool Enabled = true;
        }

        private sealed class SchemaSettings
        {
            public bool Enabled;
            public float Size;
            public int Count;
            public float Ratio;
            public string Selected;
            public UnityEngine.Color Color;
            public SchemaMode Mode;
            public SchemaNested Nested = new SchemaNested();
            public int Changed;

            public bool EnabledProperty => Enabled;

            public bool ReadEnabled() => Enabled;
        }

        private sealed class SchemaNested
        {
            public bool Enabled;
        }

        private enum SchemaMode
        {
            Mode
        }

        private static void TestPatchOperationIdentities()
        {
            Equal(
                "method:first unresolved target",
                HarmonyPatchOperationKeys.ForMethod(
                    " first unresolved target "),
                "method identity trims its required stable name");
            Require(
                HarmonyPatchOperationKeys.ForMethod("first") !=
                HarmonyPatchOperationKeys.ForMethod("second"),
                "two unresolved targets retain distinct cache identities");

            bool rejected = false;
            try
            {
                HarmonyPatchOperationKeys.ForMethod(null);
            }
            catch (ArgumentException)
            {
                rejected = true;
            }

            Require(rejected, "an unnamed patch is rejected before caching");
        }

        private static void TestContextualInput()
        {
            var router = CreateContextRouter();
            Require(Routes(router, ContextualPointerEventType.MouseDown, 0, true),
                "Alt-left routes and therefore consumes");
            Require(!Routes(router, ContextualPointerEventType.MouseDown, 0, false),
                "ordinary left rejected");
            Require(!Routes(router, ContextualPointerEventType.MouseDown, 1, true),
                "right-click rejected");
            Require(!Routes(router, ContextualPointerEventType.MouseMove, 0, true),
                "Alt-hover rejected");
        }

        private static void TestContextualOverlap()
        {
            var router = new ContextualSettingsRouterCore();
            router.Acquire("consumer");
            var rect = new ContextualHitRect(0f, 0f, 30f, 30f);
            Require(router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Group, "group"), 100, 10),
                "group registered");
            Require(router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "exact"), -100, 10),
                "exact registered");
            Require(!router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "exact"), -100, 10),
                "duplicate rejected");
            Require(router.TryRoute(ContextClick(), 10, out ContextualBindingRecord winner),
                "overlap routed");
            Equal("exact", winner.Target.SettingId,
                "specificity outranks explicit priority");

            router.Register("consumer", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "late"), -100, 10);
            router.TryRoute(ContextClick(), 10, out winner);
            Equal("late", winner.Target.SettingId,
                "registration order deterministically breaks ties");
        }

        private static void TestContextualLifecycle()
        {
            var router = new ContextualSettingsRouterCore();
            var rect = new ContextualHitRect(0f, 0f, 30f, 30f);
            router.Acquire("a");
            router.Acquire("b");
            router.Register("a", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "a"), 0, 5);
            router.Register("b", rect,
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "b"), 1, 5);
            Equal(2, router.ConsumerCount, "two consumers acquired");
            router.Release("b");
            Equal(1, router.ConsumerCount, "one consumer remains");
            Require(router.TryRoute(ContextClick(), 5, out ContextualBindingRecord winner),
                "remaining consumer routes");
            Equal("a", winner.ConsumerId, "released consumer removed");
            router.Release("a");
            Equal(0, router.ConsumerCount, "final consumer released");
            Equal(0, router.RegistrationCount, "registrations cleaned");
            Require(!router.TryRoute(ContextClick(), 5, out _),
                "no routing or polling state without consumers");
        }

        private static void TestContextualDeferredActions()
        {
            var queue = new DeferredContextualActionQueue();
            int opened = 0;
            Require(queue.Enqueue(() => opened++), "open queued");
            Equal(0, opened, "opening is deferred");
            Require(!queue.Enqueue(() => opened += 10), "stacked open rejected");
            Require(queue.Drain(), "deferred open drained");
            Equal(1, opened, "one settings window requested");

            Exception isolated = null;
            queue.Enqueue(() => throw new InvalidOperationException("bad target"));
            Require(queue.Drain(exception => isolated = exception),
                "bad consumer action drained");
            Require(isolated is InvalidOperationException,
                "bad target exception isolated");
            Require(queue.Enqueue(() => opened++),
                "healthy consumer can queue after failure");
            queue.Drain();
            Equal(2, opened, "healthy navigation survives failure");
        }

        private static void TestContextualNavigation()
        {
            ContextualNavigationCandidate Lookup(string id)
            {
                switch (id)
                {
                    case "exact":
                        return new ContextualNavigationCandidate(id, true, true);
                    case "group":
                        return new ContextualNavigationCandidate(id, true, false);
                    case "hidden":
                        return new ContextualNavigationCandidate(id, false, true);
                    default:
                        return default(ContextualNavigationCandidate);
                }
            }

            ContextualNavigationPlan exact = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "exact", "group"),
                Lookup);
            Equal("exact", exact.TargetId, "exact target retained");
            Require(exact.UseSimpleView, "exact target chooses simple view");
            Require(!exact.IncludeChildren, "exact target remains narrow");

            ContextualNavigationPlan group = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Group, "group"),
                Lookup);
            Equal("group", group.TargetId, "group target retained");
            Require(!group.UseSimpleView, "advanced-only group chooses advanced view");
            Require(group.IncludeChildren, "group includes its section");

            ContextualNavigationPlan fallback = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "hidden", "group"),
                Lookup);
            Equal("group", fallback.TargetId, "hidden exact target falls back to group");
            Require(fallback.IncludeChildren, "fallback group includes its section");

            ContextualNavigationPlan root = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Root),
                Lookup);
            Require(root.IsRoot, "root target opens unfiltered settings");

            ContextualNavigationPlan missing = ContextualNavigationResolver.Resolve(
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "missing", "also-missing"),
                Lookup);
            Require(missing.IsRoot, "missing exact and group safely fall back to root");
        }

        private static void TestContextualPresentation()
        {
            Equal(75f,
                ContextualPresentationMath.CenteredScroll(100f, 100f, 50f, 300f),
                "target row centered");
            Equal(0f,
                ContextualPresentationMath.CenteredScroll(10f, 100f, 30f, 300f),
                "scroll clamps at top");
            Equal(200f,
                ContextualPresentationMath.CenteredScroll(290f, 100f, 30f, 300f),
                "scroll clamps at bottom");
            Require(ContextualPresentationMath.IsHighlightActive(11.45f, 10f, 1.45f),
                "highlight remains active through its lifetime");
            Require(!ContextualPresentationMath.IsHighlightActive(11.46f, 10f, 1.45f),
                "highlight expires after its lifetime");
        }

        private static void TestSettingsPresentationPolicy()
        {
            Require(!SettingsPresentationPolicy.ShowSearch(4),
                "search must stay hidden below five settings");
            Require(SettingsPresentationPolicy.ShowSearch(5),
                "search must appear at five settings");
            Require(!SettingsPresentationPolicy.ShowFilters(10),
                "filters must stay hidden for ten settings");
            Require(!SettingsPresentationPolicy.ShowViewModes(10, 8),
                "view filtering must stay hidden for ten settings");
            Require(SettingsPresentationPolicy.ShowFilters(11),
                "filters must appear above ten settings");
            Require(!SettingsPresentationPolicy.ShowViewModes(11, 3),
                "three advanced-only settings stay in one unified view");
            Require(SettingsPresentationPolicy.ShowViewModes(11, 4),
                "four advanced-only settings enable the view toggle");
        }

        private static void TestContextualTooltipComposition()
        {
            Equal(
                "Faction relationship",
                ContextualTooltipComposition.FeatureOnly(
                    "Faction relationship  "),
                "feature tooltip changed");
            Equal<string>(
                null,
                ContextualTooltipComposition.FeatureOnly(null),
                "hint-only binding created a tooltip");
            Equal<string>(
                null,
                ContextualTooltipComposition.FeatureOnly("   "),
                "empty feature tooltip created a tooltip");
        }

        private static ContextualSettingsRouterCore CreateContextRouter()
        {
            var router = new ContextualSettingsRouterCore();
            router.Acquire("consumer");
            router.Register(
                "consumer",
                new ContextualHitRect(0f, 0f, 30f, 30f),
                new ContextualSettingsTarget(ContextualSettingsTargetLevel.Exact, "setting"),
                0,
                1);
            return router;
        }

        private static bool Routes(
            ContextualSettingsRouterCore router,
            ContextualPointerEventType type,
            int button,
            bool alt) =>
            router.TryRoute(
                new ContextualPointerEvent(type, button, alt, 10f, 10f),
                1,
                out _);

        private static ContextualPointerEvent ContextClick() =>
            new ContextualPointerEvent(
                ContextualPointerEventType.MouseDown,
                0,
                true,
                10f,
                10f);

    }
}
