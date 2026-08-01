# Spine compact compatibility matrix

All runtime rows use RimWorld 1.6.4871, Core only, local source/runtime
packages, and dependency-ordered loading unless stated otherwise.

| Combination or assertion | Scenario/evidence | Classification | Smallest response |
|---|---|---|---|
| Spine alone | Existing isolated Core + Harmony + Agent + Spine run | Patch required | Make tooltip patch opt-in and publish runtime descriptor |
| TechSense + Spine | One-consumer run; settings captured; clean exit | Compatible with documented limitation | Fix systemic Spine blockers |
| PIT + Spine | Loaded in all-consumer run; settings write/read and capture | Compatible with documented limitation | Keep PIT Harmony owner and settings state consumer-owned |
| SOS2 Readouts + Spine + VF + SOS2 | Loaded in all-consumer run; settings write/read and capture | Compatible with documented limitation | Keep SOS2 domain behavior outside Spine |
| Faction Lens + Spine | Loaded in all-consumer run; owner-attributed patch and settings write/read | Compatible with documented limitation | Add dependency URLs; keep privacy/map logic outside Spine |
| Every pair among four consumers | All pairs coexisted simultaneously, one Spine assembly; pairs were not isolated in this lane | Compatible with documented limitation | Use gameplay agents' pairwise reports for feature-level claims |
| All four consumers | Three maps, save/reload, world map, 2,000 ticks, all settings pages | Compatible with documented limitation | Retain all-consumer smoke; fix blockers |
| BWT + external Spine | No dependency/reference; BWT embeds its own source; not staged | Integration opportunity | Plan migration separately; do not claim current integration |
| Missing Spine | Required package and CLR dependency; harness rejects unresolved closure | Compatible with documented limitation | Supply dependency download URL; do not add silent fallback |
| Older Spine | No minimum version or runtime handshake | Patch required | Negotiate required capabilities through one runtime facade and fail clearly before service use |
| Newer capability-compatible Spine | Descriptor unit type exists but is neither advertised nor consumed | Patch required | Advertise capability facades through one runtime facade; do not expose implementation types |
| Public API granularity | Many inherited utilities exist, including one-consumer and currently unused systems | Patch required | Keep a few cohesive facades public; expose exact reusable operations and keep one-caller helpers internal |
| Shared registration/failure isolation | Unit tests quarantine throwing render provider and preserve/dispose healthy providers | Compatible | Keep owner-scoped disposable tokens |
| Consumer removal | Restart-based RimWorld loading only; no hot unload | Compatible with documented limitation | Document restart requirement |
| Repeated settings opening | Four clean 1920x1080 captures; independent persisted XML | Compatible | Add automated geometry/input checks later |
| Contextual Alt-click settings | No public router/registry; only private drawer focus machinery | Integration opportunity | Build only with two adopting consumers |
| Harmony ownership | Separate consumer owner IDs; Spine owns three tooltip hooks | Patch required | Preserve consumer owners; remove automatic Spine owner work |
| Duplicate DLLs | No consumer ships Spine.dll; BWT embedded code is in another assembly | Compatible | Keep packaging gate |
| Fluent unique/no/ambiguous calls | Reflection fixture returned PatternReplaced/NoMatch/AmbiguousMatch | Compatible | Move fixture into standalone automated suite |
| Fluent fallback and basic stack validation | Original stream restored after throw; valid/underflow streams distinguished | Compatible | Preserve fallback contract |
| Fluent labels/branches/exception blocks/guards/returns | Advertised test harness absent from standalone repository | Inconclusive | Ship/run representative fixture suite before stable API claim |
| Idle gameplay work | Runtime reports Spine prefix + postfix + finalizer on ActiveTip.TipRect without request | Patch required | Make service opt-in or move it to a UI/gameplay consumer |
| HugsLib/XML/settings/performance frameworks | Not installed in local catalog; no static dependency | Inconclusive | Test only when locally sourced and relevant |
| Royalty/Ideology/Biotech/Anomaly/Odyssey | No Spine DLC code path; not run individually | N/A for Spine | Cover consumer DLC surfaces together in one later grouped lane |

Evidence root for grouped rows:
`C:\Users\PrecisionX\AppData\Local\Temp\RimWorldAgentTasks\1.6\TechSenseFilters-2a52c4605fae4681aaa37f09320e8a9e`.
