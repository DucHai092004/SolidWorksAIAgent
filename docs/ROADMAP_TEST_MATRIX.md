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
| 0 | Agent foundation / orchestration / safety / result checking | Included in Version 1 RC; covered by build/Core regression and live Agent dispatch checks |
| 1 | Basic CAD agent | Included in Version 1 RC; disposable SOLIDWORKS creation workflow PASS |
| 2 | Model understanding | Included in Version 1 RC; model-reader regression PASS |
| 3 | Assembly | Included in Version 1 RC; component/mate/interference integration regression PASS |
| 4 | Manufacturing breakdown | Included in Version 1 RC; breakdown + Excel runtime regression PASS; manual image/document-cleanup spot check remains in acceptance guide |
| 5 | BOM | Included in Version 1 RC. Legacy flat, Top-level, Parts-only and Indented modes implemented. SOLIDWORKS Show/Hide/Promote subassembly child behavior covered by 25/25 hierarchy integration checks. Excel/CSV/image/native Drawing BOM paths retained. |
| 6 | Drawing automation | Included in Version 1 RC: create/sheet/views/section/detail/dimensions/Drawing BOM/balloons/title-block properties/PDF/DXF; SOLIDWORKS 2021 integration PASS |
| 7 | Drawing understanding | Native SLDDRW semantic reader complete for sheets/views, dimensions, notes and tables/BOM. External-source readiness/evidence routing covers native drawings, text PDFs, raster/textless PDFs, mixed PDFs and raster images. `AnalyzeDrawingSource` is a read-only Agent tool. Tesseract CLI OCR is implemented for raster images. PDF pages with insufficient native text first try the largest embedded PdfPig image; if that image is unavailable or not PNG-convertible, an optional `pdftoppm` full-page renderer is used before OCR. Confidence/review gating remains mandatory. The complete code path is implemented and regression-tested, but real OCR/render runtime validation is still pending because neither Tesseract nor `pdftoppm` is installed/resolvable on the current test machine. |
| 8 | Mechanical design copilot | Future roadmap; outside Version 1 |
| 9 | Manufacturing cost | Future roadmap; outside Version 1 |
| 10 | Autonomous mechanical agent | Future roadmap; outside Version 1 |

## Current automated tests
- Part-code normalization and chained CAD extensions.
- Stock rules: round and prismatic.
- Approved stock-thickness lookup and fallback.
- Stock-weight safety when external stock material differs from CAD material.
- CSV material extraction and source discovery exclusions.
- Exact/fuzzy-safe part matching.
- Natural-language routing for stock-material commands.
- BOM parser regression.
- BOM hierarchy resolver: TopLevel / PartsOnly / Indented × Show / Hide / Promote.
- BOM suppressed/excluded-node behavior.
- Drawing-understanding planner routing.
- Semantic drawing dimension extraction: name, owner view, type, system-unit value/position and tolerance metadata.
- Semantic drawing note extraction: owner view and note text.
- Semantic drawing table extraction: title/type, row/column dimensions and displayed cell text.
- Drawing Vision readiness: native SLDDRW, raster images, unsupported/missing sources, text PDF, textless/raster-candidate PDF and mixed PDF.
- PDF drawing evidence: text pages produce `PDF_TEXT` evidence with confidence metadata; textless pages do not invent evidence and remain gated when raster evidence is unavailable.
- Evidence pipeline: providers can contribute evidence without clearing the review gate; provider failures are contained as errors.
- `AnalyzeDrawingSource` tool contract: missing Path fails, raster sources return review-gated readiness, metadata is read-only and does not require an active SOLIDWORKS document.
- Tesseract raster OCR provider: supported raster extensions, successful evidence extraction, confidence propagation, low-confidence review gating and runner failure without invented evidence.
- PDF raster OCR provider: skips native-text pages, produces page-scoped `PDF_RASTER_TESSERACT` evidence for extractable raster pages and keeps low-confidence evidence review-gated.
- Full-page PDF renderer fallback: when PdfPig cannot supply an OCR-ready raster, an injected page renderer can supply a full-page image; renderer success continues into OCR and renderer failure returns explicit review/error evidence without guessing.

## Verification commands
Run full Version 1 acceptance:

`scripts\Test-Version1.cmd`

Individual checks:
- Pure/CI-safe build + Core: `scripts\Test-All.cmd`
- Full SOLIDWORKS 2021 regression: `scripts\Test-SolidWorks.cmd`

## Final Version 1 RC checkpoint
Verified on `release-v1-rc1`:
- Core/build verification: **59/59 tests PASS**.
- Drawing Understanding integration: **30/30 PASS**.
- BOM hierarchy integration: **25/25 PASS** using disposable Part -> Subassembly -> Top Assembly fixtures. All nine mode/display combinations PASS and `LegacyFlat` remains available.
- Full SOLIDWORKS integration: **46/46 PASS**. It verifies CAD creation, model readers, Drawing automation, Assembly readers, manufacturing breakdown, BOM generation, native Drawing BOM, balloons, semantic table content and PDF/DXF export.
- `scripts\Test-Version1.cmd`: **VERSION 1 ACCEPTANCE: PASS**.

## External OCR/runtime status
- Tesseract provider unit/runtime-contract tests PASS through an injected fake runner. Production resolution supports an explicit executable path, `SWMATE_TESSERACT_PATH`, `C:\Program Files\Tesseract-OCR\tesseract.exe`, or `tesseract.exe` on PATH.
- PDF raster and full-page renderer tests PASS through injected fake extractors/runners/renderers. Full-page rendering uses optional `pdftoppm` with `SWMATE_PDFTOPPM_PATH` or `pdftoppm.exe` on PATH.
- Runtime dependency checks on the current Windows test machine: both `tesseract --version` and `pdftoppm -v` are not resolvable.
- Issue #8 tracks installation/configuration and real known-fixture validation. No claim is made that real raster OCR or full-page PDF rendering has passed on this machine yet.

Runtime PASS applies to the covered SOLIDWORKS 2021 fixtures and mocked OCR/render provider contracts. External raster semantics remain review-gated until Issue #8 runtime evidence paths are validated on known fixtures.

## Version 1 release status
Automated Version 1 scope (Phase 0–7) is code-complete on `release-v1-rc1`. Remaining work for the user's next verification session is the manual smoke test in `docs/VERSION1_ACCEPTANCE.md` plus external dependency validation tracked by Issue #8. Phase 8–10 are not part of Version 1 acceptance.
