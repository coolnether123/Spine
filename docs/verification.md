# Spine 1.6 verification

Verified product source:
`650fb95835d187777fae314e1de361b8991b33ee`.

## Automated contracts

Command:

```powershell
dotnet run --project Tests\Mod.Tests.csproj -c Release
```

Result: 7 passed, 0 failed. Coverage includes revision monotonicity,
dirty-region merging, bounded-cache eviction/accounting, registration and
disposal, render-pipeline ordering and exception isolation, semantic
capability comparison, immutable snapshots, and teardown clearing.

## Clean generalized build and package

`Resolve-RwtEnvironment` resolved RimWorld 1.6 plus Harmony, then
`Invoke-RwtBuild` ran the Release build with `-RequireClean`.

- Build exit code: 0.
- Source dirty: false.
- Tooling commit:
  `d28e38b2aec5bfba8186282da0b060346489f4c7`.
- Tooling dirty: false.
- RimWorld version-manifest SHA-256:
  `0174C74429A3B1D9B272002A055FDBBC6645A0BF24A085E8BF39FDD3407B505B`.
- `Assembly-CSharp.dll` SHA-256:
  `4A170804FBFEFABDB620D8914E584E58F822A58C6E304DCB76A67003588DAB28`.
- Harmony SHA-256:
  `7B9E756306FA3D7620E02A857C8927A6AB04973F9BD8A77D3866700A6DEAC55C`.
- Fresh and tracked `Spine.dll` SHA-256:
  `2441959E82AA5CAC5C96E7456213B21D1FB67881E314F85F54373A4DB8C0E2AA`.
- Fresh DLL equals the tracked release DLL byte for byte.
- Package validation:
  `RWT-BUILD-PACKAGE-VALID`.
- Build output:
  `C:\Users\PrecisionX\AppData\Local\Temp\SpineFinalVerification-d0e66cc2322842b1ba2d637e5c8e4089`.
- Build stdout SHA-256:
  `ADE1D1C972414559AD8455B9A5CD24573031B2EE3DFEBA3904B2156D1138CFCE`.
- Build stderr was empty; SHA-256:
  `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855`.

## Isolated runtime

The profile-driven five-instance harness run included a dedicated Spine lane
with exactly Core, Harmony, RimWorld Agent, and Spine active in memory.

- Evidence:
  `C:\Users\PrecisionX\AppData\Local\Temp\RimWorldAgentLive5-260d9cec699e41f29d0a407688b3f50c\live-concurrency-result.json`.
- Evidence SHA-256:
  `86BFE4FE49EDABC190FEADF0C93E41B3B25918F7B71DD2435B4C015CF571A6A6`.
- Spine lane:
  `live-test-0-fdcd36b38ff44fc69637368177b8a7b5`.
- In-memory requested-mod assertion: passed.
- Agent enabled, configuration retained, and shutdown requested: passed.
- Exit code: 0.
- Forced termination: false.
- Player log SHA-256:
  `D621DF842BC1691F9E3F354BFB39EBE0196185EFD1A687812169F2AF1717E2E1`.
- Case-insensitive scan for `Exception`, `Error`, Harmony failures, and Spine
  failures returned no matches.

Spine owns no map component, save data, player window, tick loop, or gameplay
state. UI interaction, save/load, performance, and removal behavior are
therefore verified through the consumer-mod acceptance runs rather than
inventing a feature for this library mod. TechSense Filters, Prisoner
Interaction Timer, SOS2 Weapon Readouts, and Faction Lens each loaded the same
tracked Spine DLL through declared runtime dependencies.

## Better Work Tab boundary

The source provenance is BWT `Dev` commit
`e57eee2ed748e283241eef4245893bab0bbff357`. Better Work Tab source and metadata
were not edited during extraction or this verification.
