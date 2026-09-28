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
| 6 | Drawing automation | Partial: create/sheet/views/section/detail exist; remaining roadmap skills to implement |
| 7 | Drawing understanding | Not complete; SLDDRW reader first, Vision later |
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

Run: `scripts\Test-All.cmd`
