# SW-MATE AI — Roadmap & Test Matrix

Source of roadmap: `AgnetAiSoliworkVer1.docx`.

## Test policy
- Every checkpoint must compile before commit.
- Pure logic gets repeatable MSTest coverage.
- SOLIDWORKS COM actions require integration/runtime validation in SOLIDWORKS 2021.
- Mutating integration tests create temporary documents or use disposable test files.
- Never overwrite user production CAD files during automated tests.
- A feature is not marked runtime PASS merely because it compiles.

## Roadmap checkpoints
| Phase | Scope | Current checkpoint |
|---|---|---|
| 0 | Agent foundation / orchestration / safety / result checking | Existing; regression review required |
| 1 | Basic CAD agent | Existing; runtime regression required |
| 2 | Model understanding | Existing; runtime regression required |
| 3 | Assembly | Existing; runtime regression required |
| 4 | Manufacturing breakdown | Extended in current version; unit + runtime regression required |
| 5 | BOM | Existing; regression required |
| 6 | Drawing automation | Implemented: create/sheet/views/section/detail/dimensions/Drawing BOM/balloons/title-block properties/PDF/DXF; SOLIDWORKS 2021 integration PASS |
| 7 | Drawing understanding | SLDDRW API reader implemented; 80 integration checks PASS on 2026-09-29; PDF/image vision remains future |
| 8 | Mechanical design copilot | Future roadmap; implement only with explicit engineering rules/data |
| 9 | Manufacturing cost | Future roadmap; requires approved price/process data |
| 10 | Autonomous mechanical agent | Long-term integration phase |

## Current automated tests
- Part-code normalization and chained CAD extensions.
- Stock rules: round and prismatic.
- Approved stock-thickness lookup and fallback.
- Stock-weight safety when external stock material differs from CAD material.
- CSV material extraction and source discovery exclusions.
- Exact/fuzzy-safe part matching.
- Natural-language routing for stock-material commands.
- BOM parser regression.
- Capability registry evidence mapping (Verified vs Pilot).

Run pure/CI-safe checks: `scripts\Test-All.cmd`

Run SOLIDWORKS 2021 integration checks: `scripts\Test-SolidWorks.cmd`

Latest integration checkpoint (2026-09-29): 87 checks PASS, 0 FAIL using disposable Part/Assembly/Drawing files under the Windows temp directory. It verifies SOLIDWORKS startup readiness; Part and Feature creation; GetModelInfo; feature/sketch/dimension/dependency readers; feature impact analysis; selected-object/material/custom-property/bounding-box/mass readers; Assembly readers; manufacturing breakdown export and verified stock-material write-back; BOM configuration Part Number Source semantics; persisted ExcludeFromBOM behavior in an isolated Assembly fixture; hidden loading of unloaded source files for BOM/manufacturing image capture; automatic closure of Agent-opened temporary documents; Drawing views/section/detail/dimensions/title block/PDF/DXF; balloons; and ReadDrawing sheets/views/dimensions/BOM/notes. Core automated suite: 33/33 PASS. This is runtime evidence for the covered fixture, not a claim that all manual scenarios pass.
