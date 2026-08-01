# Contextual settings capability

Spine runtime 1.1 exposes `SpineApi.ContextualSettings` with capability ID
`CoolNether123.Spine.ContextualSettings` version 1.0. Consumers negotiate
`SpineCapability.ContextualSettings`, acquire one lease for the mod lifetime,
and call `Bind` where a visible immediate-mode rectangle is known.

```csharp
lease.Bind(
    visibleRect,
    ContextualSettingsTarget.Exact("display.format", "display.header"),
    ContextualSettingsBindingOptions.WithTooltip(featureTooltip));
```

Exact targets fall back to the named group, then to the unfiltered mod settings
root. Group targets show the group and its children. A root target opens the
ordinary settings page. Hidden, renamed, or unavailable targets fail to the
same safe root behavior.

Bindings are recorded only while their UI is drawn. Alt-left-click is resolved
in screen coordinates so rectangles from nested GUI groups and different
windows compare correctly. Exact targets outrank groups and roots, then explicit
priority and stable registration order break ties. Spine consumes the event,
queues one settings open for the next update, scrolls the resolved row into
view, and highlights it for 1.45 seconds. Ordinary clicks, right-clicks,
hovering, and keyboard events are ignored.

`ContextualSettingsBindingOptions` can omit tooltip work, append the standard
hint to a feature tooltip, or register the hint alone. Consumers remain
responsible only for deciding which feature maps to which setting. Spine owns
composition and registration so a feature does not create a second tooltip.

The deferred-open Harmony owner is
`CoolNether123.Spine.ContextualSettings`. It is installed when the first lease
is acquired and removed with the final lease. Registration and opening failures
are isolated; no scanning, polling, or gameplay work occurs with no consumer.

## Better Work Tab migration

Better Work Tab remains independent and keeps its established domain router.
A later migration can retain `BWTWorkTabContextSettingsRouter` and its feature
mapping while replacing only the pending-request/window-opening layer with one
Spine binding per BWT-owned rectangle. BWT's existing request labels and target
IDs map to exact or group targets. No Work-tab rule, schedule, priority, or
Fluffy behavior belongs in Spine, and BWT must not require external Spine until
that migration is deliberately released.
