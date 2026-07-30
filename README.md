# Spine

Spine is the feature-neutral runtime shared by CoolNether123 RimWorld mods.
It provides settings/UI components, bounded caches, revision and dirty-state
utilities, Harmony ownership helpers, and guarded transpiler building blocks.
It does not add player-facing gameplay by itself.

## Install

Install Harmony, then install this folder as an ordinary RimWorld mod and place
Spine before mods that declare `CoolNether123.Spine` as a dependency. Do not
copy `Spine.dll` into consumer mods.

## Consumer rules

- Pass the consumer's own `Harmony` instance to Spine patch helpers so Harmony
  ownership remains attributable to that mod.
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
