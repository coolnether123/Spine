# Spine compact compatibility matrix

This matrix describes the current Spine 1.0 boundary. The original 2026-07-31
pre-boundary findings remain preserved in
`spine-compatibility-investigation.md` as historical evidence; they are not
current release requirements. Runtime rows use RimWorld 1.6.4871 and
dependency-ordered local packages unless stated otherwise.

| Combination or assertion | Current evidence | Classification | Release response |
|---|---|---|---|
| Spine alone | Core tests cover capability negotiation, lease cleanup, contextual routing, settings presentation, caching, and exception isolation. Tooltip hooks are lease-scoped and no service performs gameplay work without a consumer. | Compatible | Ship only the scoped core runtime. |
| Filter Signals + Spine | Consumer-facade contract passes; the combined suite reached a generated map with consumer-owned Harmony patches and no target-mod exception. | Compatible | Keep filter and production semantics in Filter Signals. |
| Prisoner Interaction Timer + Spine | Consumer-facade contract passes; settings and lifecycle hooks remain consumer-owned and the combined suite loaded cleanly. | Compatible | Preserve its optional save-node/removal contract rather than serializing a custom game-component type. |
| SOS2 Weapon Readouts + Spine + Vehicle Framework + SOS2 | Consumer-facade contract passes; the combined SOS2 lane reached a generated map without a CoolNether123-owned exception. | Compatible with documented limitation | SOS2 and Vehicle Framework remain required external content; their domain behavior stays outside Spine. |
| Faction Lens + Spine | Consumer-facade contract passes; the combined suite loaded its world-label owner separately and reached a generated map cleanly. | Compatible | Keep world ownership and hidden-information rules in Faction Lens. |
| Mech Muster, Filter by Example, Caravan Readiness, and Task Break + Spine | All four consumer-facade contracts pass and all four loaded with the earlier consumers in the combined suite. | Compatible | Retain each mod's gameplay semantics and lifecycle in that consumer. |
| All eight gameplay consumers | The final combined lane `coolnether-suite-355cca1875a740909cbc91d9c1a59c57` reached a generated map; post-map error scanning found no target-mod exception and shutdown was clean. | Compatible | Keep the complete-suite smoke as an integration gate, not a substitute for domain scenarios. |
| BWT without standalone Spine | `LoadFolders.xml` selects BWT's self-contained assembly; the embedded build contains its shared mirror and fluent transpiler in `Better Work Tab.dll`. | Compatible | Keep the embedded payload available. |
| BWT with standalone Spine | The external build references core `Spine.dll` once, negotiates Spine 1.0 plus `BoundedCaches`, embeds BWT's own transpiler implementation, and contains no `Spine.Transpilers` reference. Mirror and API/package contracts pass. | Compatible | Load standalone Spine first and select exactly one BWT assembly variant. |
| Missing Spine for a required gameplay consumer | Metadata and the harness dependency closure reject the missing runtime before play. | Compatible with documented limitation | Install Spine; do not add a silent consumer-local fallback. |
| Older Spine | Consumers negotiate minimum runtime and capability versions through `SpineApi.Runtime`; focused tests reject unmet requirements. | Compatible | Fail clearly before acquiring an unsupported service. |
| Newer capability-compatible Spine | Negotiation is capability-based rather than tied to exact assembly version equality. | Compatible | Preserve additive capability compatibility. |
| Contextual Alt-click settings | Core tests cover Alt-left detection, event consumption, overlap priority, deferred opening, exact/group/root fallback, scrolling, highlighting, duplicate binding, multiple consumers, and failure isolation. | Compatible | Consumers provide rectangles and semantic targets only. |
| Shared settings presentation | Core tests cover automatic search/filter thresholds and a single lazy preparation owner. | Compatible | Spine owns generic presentation density; consumers own definitions and meaning. |
| Harmony ownership | Consumers acquire owner-specific installers. Stable operation names prevent unresolved-target cache collisions; keyed warnings deduplicate process-wide. | Compatible | Never patch gameplay under a shared generic owner. |
| Duplicate runtime DLLs | Package contracts reject bundled Harmony, duplicate assembly identities, and separate Spine payloads in BWT. BWT's obsolete common-root assemblies were removed. | Compatible | Stage exact runtime files through the shared allowlist. |
| Optional fluent transpiler companion | Core `Spine.dll` excludes fluent-transpiler implementation. The developer companion has a separate package ID and emitted-IL fixture suite; no current gameplay consumer or BWT release requires it. | Developer-only | Exclude `Developer/` from the public Spine release; distribute the companion only as a separately intentional developer package. |
| Idle gameplay work | Contextual input and tooltip stabilization are acquired and released through consumer leases; focused cleanup tests pass. | Compatible | Keep services opt-in and uninstall the final hook when its last lease is released. |
| HugsLib, XML Extensions, and unrelated settings/performance frameworks | Spine has no static dependency; the historical bounded lane did not source every optional framework. | Inconclusive | Test a framework only when a real consumer shares its surface; do not claim blanket compatibility. |
| Royalty, Ideology, Biotech, Anomaly, and Odyssey | Spine contains no DLC gameplay behavior. Applicable DLC behavior is tested through the owning consumer rather than as a separate Spine feature. | N/A for Spine | Group relevant DLCs in consumer scenarios and omit out-of-scope DLC claims. |

Current build hashes and exact verification commands are recorded in
`../verification.md`. The earlier session IDs and historical classifications
remain in `spine-compatibility-investigation.md` for auditability.
