# Contextual settings capability

Spine runtime 1.0 exposes `SpineApi.ContextualSettings` with capability ID
`CoolNether123.Spine.ContextualSettings` version 1.0. Consumers negotiate
`SpineCapability.ContextualSettings`, acquire one lease for the mod lifetime,
and call `Bind` where a visible immediate-mode rectangle is known.

```csharp
lease.Bind(
    visibleRect,
    ContextualSettingsTarget.Exact("display.format", "display.header"),
    ContextualSettingsBindingOptions.WithTooltip(featureTooltip));
```

Exact targets fall back to the named group, then to the mod settings root. A
context jump always clears search and explicit filters, shows the ordinary
settings page, scrolls the resolved row into view, and briefly highlights that
row. It never creates a temporary contextual filter. Hidden, renamed, or
unavailable targets fail to the same safe root behavior.

Bindings are recorded only while their UI is drawn. Alt-left-click is resolved
in screen coordinates so rectangles from nested GUI groups and different
windows compare correctly. Exact targets outrank groups and roots, then explicit
priority and stable registration order break ties. Spine consumes the event,
queues one settings open for the next update, scrolls the resolved row into
view, and highlights it for 1.45 seconds. Ordinary clicks, right-clicks,
hovering, and keyboard events are ignored.

`ContextualSettingsBindingOptions` can omit tooltip work or let Spine register
an existing feature tooltip once. Spine does not append or register an
“Alt-click” hint over gameplay UI. The convention is documented in mod
documentation instead of repeated across world labels, overlays, and
controls. The older hint fields remain binary-compatible but are presentation
no-ops in the current 1.0 capability.

The shared settings drawer adapts its toolbar to the number of configurable
rows. Below five settings it shows only section headers and rows. From five
through ten it adds search but keeps all settings in one unfiltered view. Above
ten it may show search, consumer-provided filters, and simple/advanced views.
Headers and spacers do not count toward those thresholds; consumers do not
implement this policy themselves.

The deferred-open Harmony owner is
`CoolNether123.Spine.ContextualSettings`. It is installed when the first lease
is acquired and removed with the final lease. Registration and opening failures
are isolated; no scanning, polling, or gameplay work occurs with no consumer.

Ordinary gameplay mods should acquire their
settings page through `SpineApi.Settings`. The returned `IModSettingsPage`
exposes the already-associated contextual-settings lease, so consumers do not
repeat drawer construction or a second acquisition. Direct
`SpineApi.ContextualSettings.Acquire` remains available for nonstandard pages
and backward compatibility.

## Better Work Tab migration

Better Work Tab remains independent and keeps its established domain router.
A later migration can retain `BWTWorkTabContextSettingsRouter` and its feature
mapping while replacing only the pending-request/window-opening layer with one
Spine binding per BWT-owned rectangle. BWT's existing request labels and target
IDs map to exact or group targets. No Work-tab rule, schedule, priority, or
Fluffy behavior belongs in Spine, and BWT must not require external Spine until
that migration is deliberately released.
