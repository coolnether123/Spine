# Spine

Spine is the feature-neutral runtime shared by CoolNether123 RimWorld mods.
It provides shared settings/UI infrastructure, guarded patching infrastructure,
and proven RimWorld-neutral utilities. Its supported surface is being narrowed
to cohesive capability facades so internal implementations can change without
breaking every consumer. It does not add player-facing gameplay by itself.

## Install

Install Harmony, then install this folder as an ordinary RimWorld mod and place
Spine before mods that declare `CoolNether123.Spine` as a dependency. Do not
copy `Spine.dll` into consumer mods.

Spine does not yet have a public Workshop or download URL. The verified local
RimWorld 1.6 distribution therefore ships Spine and its gameplay consumers
together. Copy the `Spine` folder alongside any gameplay-mod folder you choose
to install. Each gameplay
mod remains independently selectable and depends on no other gameplay mod.

## Consumer rules

- Enter Spine through its runtime and capability facades. Do not bind to
  implementation types or infer support from an assembly version.
- Request only the capabilities the consumer actually uses. Pass the
  consumer's own ownership identity, including its `Harmony` instance for
  patching, so diagnostics and teardown remain attributable to that mod.
- Prefer exact utility operations with stable meaning. Do not request a public
  API for a single mod action, screen, or special case; keep that code in the
  consumer.
- Depend only on the facilities the consumer actually uses.
- Keep gameplay, compatibility adapters, and mod-specific settings definitions
  in the consumer repository.
- Treat Better Work Tab as read-only provenance. New shared changes belong in
  this standalone repository and can be adopted by BWT separately.

Architecture and provenance are documented in
[`docs/architecture.md`](docs/architecture.md) and
[`docs/research/source-provenance.md`](docs/research/source-provenance.md).
The contextual-settings public contract and BWT migration path are documented
in [`docs/contextual-settings.md`](docs/contextual-settings.md).
The final 1.6 checks are recorded in
[`docs/verification.md`](docs/verification.md).

Spine 1.3 advertises the versioned `ModSettingsPages` capability. A consumer
supplies its setting definitions and settings object once; the returned page
owns the standard localized drawer, simple/advanced view state, contextual
settings lease, and draw lifecycle. Gameplay mods still own every setting's
meaning and persistence fields. This replaces repeated consumer-side settings
UI wrappers without turning individual mod screens into Spine APIs.

Spine also advertises the versioned `ContextualSettings` capability. Consumers
bind a visible rectangle to an exact setting, settings group, or mod root.
Spine owns Alt-left-click detection, overlap arbitration, event consumption,
deferred settings-window opening, normal-page scrolling, and highlighting. It
does not filter the page or add Alt-click hints to gameplay tooltips. Its input
hook exists only while a consumer holds a lease. The settings facade also hides
search, category filters, and view filters when the setting count is too small
for them to be useful.
