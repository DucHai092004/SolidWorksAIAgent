# SW-MATE AI — Version 1 Acceptance Guide

## Release scope
Version 1 closes the planned Phase 0 through Phase 7 work. Phase 8 (Mechanical Design Copilot), Phase 9 (Manufacturing Cost), and Phase 10 (Autonomous Mechanical Agent) are explicitly outside Version 1.

## One-command verification
Run from the repository root:

```bat
scripts\Test-Version1.cmd
```

Required acceptance stages:
1. Full solution build without COM registration + Core automated tests.
2. Drawing Understanding integration on disposable Part/Drawing fixtures.
3. BOM hierarchy integration on disposable Part/Subassembly/Top-assembly fixtures.
4. Full SOLIDWORKS 2021 integration on disposable Part/Assembly/Drawing fixtures.
5. Optional external OCR/render dependency report.

A Version 1 acceptance run is PASS only when stages 1–4 return exit code 0. Missing Tesseract or `pdftoppm` is reported separately and remains tracked by Issue #8; the software must not invent OCR evidence when those dependencies are unavailable.

## Final automated Version 1 checkpoint
Verified on `release-v1-rc1`:
- Core/build: **59/59 PASS**.
- Drawing Understanding integration: **30/30 PASS**.
- BOM hierarchy integration: **25/25 PASS**.
- Full SOLIDWORKS 2021 integration: **46/46 PASS**.
- `scripts\Test-Version1.cmd`: **VERSION 1 ACCEPTANCE: PASS**.

The BOM hierarchy runtime fixture verifies all nine combinations of:
- `TopLevel` / `PartsOnly` / `Indented`.
- Subassembly child display `Show` / `Hide` / `Promote`.
- Legacy flat BOM remains available for backward compatibility.

## Version 1 feature matrix
| Phase | Acceptance scope |
|---|---|
| 0 | Agent orchestration, context observation, skill registry, confirmation/undo/result checking |
| 1 | Basic Part CAD creation: sketch, rectangle/circle, extrude/cut, dimensions, plate workflows, fillet/chamfer |
| 2 | Model understanding: feature tree, features/dependencies/impact, sketches, dimensions, material, mass, properties, selection, bounding box |
| 3 | Assembly: read components/mates/interference; insert/move/replace components; add/delete mates |
| 4 | Manufacturing breakdown: unique parts, quantities, stock classification/sizing/weight, source-document material enrichment, image capture, Excel export |
| 5 | BOM: legacy flat + Top-level + Parts-only + Indented modes, Show/Hide/Promote child rules, Excel/CSV export, component images, native SOLIDWORKS BOM |
| 6 | Drawing automation: create drawing/sheet/views, isometric, section/detail, dimensions, Drawing BOM, balloons, title block, PDF/DXF export |
| 7 | Drawing understanding: native SLDDRW semantic reader, dimensions/notes/tables/BOM content, external PDF/image readiness, OCR/render evidence pipeline |

## Manual smoke test order for tomorrow
Use disposable CAD files, not production files.

### A. Startup and UI
- SOLIDWORKS loads the add-in without an exception.
- Task pane opens and connection indicator is green.
- Vietnamese/English language selector remains clearly readable on the dark UI.
- Two main tabs are visible: `Agent` and `Kiểm thử`.
- `Kiểm thử` dynamically lists the registered skills by category.
- Refresh works with no document, Part, Assembly and Drawing.
- Agent command box accepts Vietnamese commands and shows a plan before mutating actions.

### B. Part and Model Reader
- In `Kiểm thử`, use only disposable files.
- Create Part -> Sketch -> Rectangle -> Extrude.
- Create Circle -> Cut Extrude.
- Create a plate / plate with hole; test fillet or chamfer.
- Read feature tree, dimensions, material, mass properties and bounding box.
- Modify a known dimension and confirm result checking.

### C. Assembly
- Open a disposable Assembly.
- Read components and mates.
- Run interference check.
- Insert/move/replace a disposable component.
- Add and delete a disposable Mate; verify confirmation/undo behavior.

### D. Manufacturing breakdown
- Build manufacturing breakdown.
- Check quantity, stock shape, stock size and stock weight.
- Export Excel and verify Part images are visible.
- Confirm temporary Part files opened for image capture are closed afterward.
- Check Stock Material enrichment only writes high-confidence matches; ambiguous sources remain review-gated.
- Confirm `Công nghệ gia công` and `Nhà gia công` remain available for manual entry where required.

### E. BOM
- Create a normal/legacy BOM first.
- Export Excel and verify every expected row has its image when capture succeeds.
- Export CSV.
- Test hierarchy modes with `CreateBOM` parameters:
  - `Mode=TopLevel;RespectChildDisplay=true`
  - `Mode=PartsOnly;RespectChildDisplay=true`
  - `Mode=Indented;RespectChildDisplay=true`
- For a Subassembly, change SOLIDWORKS BOM child setting between Show / Hide / Promote and compare output.
- In Indented Excel output, child Part Number cells should be visually indented by hierarchy level.
- Insert native SOLIDWORKS BOM where applicable.
- Reopen/close test documents and ensure no capture-only Part remains open.

### F. Drawing automation
- Create Drawing from Part/Assembly.
- Add sheet, standard views, isometric, section and detail views.
- Insert dimensions, Drawing BOM and balloon.
- Fill title block.
- Export PDF and DXF; verify files exist.

### G. Drawing understanding
- Run `ReadDrawing` on a native SLDDRW and check sheets/views/dimensions/notes/tables.
- Run `AnalyzeDrawingSource` on a text PDF and verify `PDF_TEXT` evidence.
- Run on raster/image-only input. Missing OCR/render dependencies must return review/error information, not fabricated semantic data.

## Known external runtime dependency
Issue #8 tracks real raster OCR/render validation.
- Tesseract: configure `SWMATE_TESSERACT_PATH` or add `tesseract.exe` to PATH.
- Poppler: configure `SWMATE_PDFTOPPM_PATH` or add `pdftoppm.exe` to PATH.

On the current Windows test machine both executables are still unresolved. Until they are installed and validated against known fixtures, raster semantics remain review-gated by design.

## Release acceptance rule
Version 1 is a code-complete release candidate when:
- `scripts\Test-Version1.cmd` passes stages 1–4.
- No production CAD file is modified by automated testing.
- BOM/Manufacturing exports are manually spot-checked for images and document cleanup.
- UI startup, `Agent`/`Kiểm thử` tabs and language readability are manually checked.
- Issue #8 remains explicitly documented if external OCR/runtime validation has not yet been performed.

Current `release-v1-rc1` satisfies the automated acceptance rule. Tomorrow's remaining work is the user-facing/manual smoke test described above plus Issue #8 runtime dependency validation when those external tools are installed.
