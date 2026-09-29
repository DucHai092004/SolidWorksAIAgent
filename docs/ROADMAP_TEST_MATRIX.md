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
| 7 | Drawing understanding | SLDDRW semantic reader implemented for sheets/views, dimensions, notes and tables/BOM; Vision/OCR for raster or non-native content remains future work |
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
- Drawing-understanding planner routing.
- Semantic drawing dimension extraction: name, owner view, type, system-unit value/position and tolerance metadata.
- Semantic drawing note extraction: owner view and note text.
- Semantic drawing table extraction: title/type, row/column dimensions and displayed cell text.

Run pure/CI-safe checks: `scripts\Test-All.cmd`

Run SOLIDWORKS 2021 integration checks: `scripts\Test-SolidWorks.cmd`

Latest Phase 7 checkpoints (2026-09-29):
- Core/build verification: 23/23 tests PASS using `scripts\Test-All.cmd`.
- Drawing Understanding integration: 27 checks PASS, 0 FAIL on a disposable Part/Drawing fixture. It verifies sheets, model views, inserted dimensions, active-sheet preservation, semantic dimensions and semantic note ownership/count consistency.
- Full SOLIDWORKS integration: 46 checks PASS, 0 FAIL on disposable Part/Assembly/Drawing files. It verifies CAD creation, model readers, Drawing automation, Assembly readers, manufacturing breakdown, BOM generation, native Drawing BOM, balloons, semantic table count and displayed table-cell content, and PDF/DXF export.

Runtime PASS applies only to the covered SOLIDWORKS 2021 fixtures. Vision/OCR and arbitrary external PDF/image drawings are not yet covered by this checkpoint.
