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
RimWorld 1.6 distribution therefore ships Spine and its four gameplay
consumers together at
`A:\Dev\RimWorld\Releases\1.6\2026-07-30-program-final`. Copy the `Spine`
folder alongside any gameplay-mod folder you choose to install. Each gameplay
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
The final 1.6 checks are recorded in
[`docs/verification.md`](docs/verification.md).

The current binary predates the complete facade contract: runtime capability
negotiation is not yet operational, and tooltip stabilization is still
installed automatically. These are release blockers, not compatibility
promises. See the compatibility investigation before adopting this build as a
stable external API.
