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
| 7 | Drawing understanding | Native SLDDRW semantic reader complete for sheets/views, dimensions, notes and tables/BOM. External-source readiness/evidence routing covers native drawings, text PDFs, raster/textless PDFs, mixed PDFs and raster images. `AnalyzeDrawingSource` is a read-only Agent tool. A Tesseract CLI OCR provider is implemented for raster image files with confidence/review gating. Actual OCR runtime validation is still pending because Tesseract is not installed/resolvable on the current test machine; raster pages inside image-only PDFs also still need a PDF-page rendering/extraction step before OCR. |
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
- Drawing Vision readiness: native SLDDRW, raster images, unsupported/missing sources, text PDF, textless/raster-candidate PDF and mixed PDF.
- PDF drawing evidence: text pages produce `PDF_TEXT` evidence with confidence metadata; textless pages produce no invented evidence and remain gated for Vision/review.
- Evidence pipeline: providers can contribute evidence without clearing the review gate; provider failures are contained as errors.
- `AnalyzeDrawingSource` tool contract: missing Path fails, raster sources return review-gated readiness, metadata is read-only and does not require an active SOLIDWORKS document.
- Tesseract raster OCR provider: supported raster extensions, successful evidence extraction, confidence propagation, low-confidence review gating and runner failure without invented evidence.

Run pure/CI-safe checks: `scripts\Test-All.cmd`

Run SOLIDWORKS 2021 integration checks: `scripts\Test-SolidWorks.cmd`

Latest Phase 7 checkpoints (2026-09-29):
- Core/build verification: 44/44 tests PASS using `scripts\Test-All.cmd` on `phase-7-drawing-vision-v2`.
- Drawing Understanding integration: 30 checks PASS, 0 FAIL on a disposable Part/Drawing fixture. It verifies Agent registration/dispatch for `AnalyzeDrawingSource`, safe review gating for an unavailable raster source, sheets, model views, inserted dimensions, active-sheet preservation, semantic dimensions and semantic note ownership/count consistency.
- Full SOLIDWORKS integration: 46 checks PASS, 0 FAIL on disposable Part/Assembly/Drawing files. It verifies CAD creation, model readers, Drawing automation, Assembly readers, manufacturing breakdown, BOM generation, native Drawing BOM, balloons, semantic table count and displayed table-cell content, and PDF/DXF export.
- Tesseract provider unit/runtime-contract tests PASS through an injected fake runner. Production resolution supports an explicit executable path, `SWMATE_TESSERACT_PATH`, `C:\Program Files\Tesseract-OCR\tesseract.exe`, or `tesseract.exe` on PATH.
- Runtime dependency check on the current Windows test machine: `tesseract --version` is not resolvable, so no claim is made that real raster OCR has passed on this machine yet.

Runtime PASS applies only to the covered SOLIDWORKS 2021 fixtures. Real raster OCR requires Tesseract to be installed/configured and then validated against known image fixtures. Image-only PDF OCR additionally requires a page-rendering/extraction stage. Arbitrary semantic interpretation of external image-only drawings must remain review-gated until those evidence paths are runtime-validated.
