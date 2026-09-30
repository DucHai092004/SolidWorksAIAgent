# SW-MATE AI — Version 1 Release Notes

## Status
Version 1 is the release line covering Phase 0 through Phase 7 of the original `AgnetAiSoliworkVer1` roadmap. The current release candidate branch is `release-v1-rc1`.

## Included capabilities
- Natural-language command parsing for core CAD, Assembly, Manufacturing, BOM and Drawing workflows.
- Agent planning, confirmation gates, result checking, logging and undo support for eligible actions.
- Part creation/editing workflows including sketches, extrudes, cuts, dimensions, plates, holes, fillets and chamfers.
- Model understanding for features, dependencies, sketches, dimensions, material, mass properties, custom properties, selected objects and bounding boxes.
- Assembly reading and editing including components, mates, interference checks, component insert/move/replace and mate add/delete.
- Manufacturing breakdown with quantities, stock classification, stock dimensions, stock weight, document-based material enrichment, image capture and Excel export.
- BOM generation with component images and Excel/CSV export.
- BOM hierarchy modes: `LegacyFlat`, `TopLevel`, `PartsOnly`, `Indented`.
- Subassembly BOM child behavior: SOLIDWORKS `Show`, `Hide`, `Promote` settings are respected when hierarchy mode is enabled.
- Indented BOM level metadata and visual indentation in Excel without changing the existing BOM column schema.
- Native SOLIDWORKS BOM support.
- Drawing automation: create Drawing/sheets/views, isometric, section/detail, dimensions, BOM, balloons, title-block properties, PDF and DXF export.
- Drawing understanding: native SLDDRW semantic extraction for sheets/views, dimensions, notes and tables/BOM.
- External Drawing/PDF/image evidence pipeline with native PDF text, raster readiness, Tesseract OCR adapter, PdfPig embedded-image extraction and optional `pdftoppm` full-page rendering fallback.

## Version 1 test UI
- The Task Pane keeps the existing `Agent` workflow.
- A dynamic `Kiểm thử` tab is added at runtime from the actual Agent skill registry.
- Registered skills are grouped by category and can be executed individually on disposable test files.
- Parameter entry uses `Key=Value;Key2=Value2` format.
- High-risk / confirmation-required skills ask for explicit confirmation before execution.
- The language selector uses explicit dark-background / white-text styling to avoid the low-contrast issue previously reported.

## Safety behavior
- Mutating actions can require confirmation.
- Automated integration tests use disposable fixtures and must not overwrite production CAD files.
- Low-confidence external OCR remains review-gated.
- Missing OCR/render dependencies never generate fabricated semantic evidence.
- Ambiguous material-source matches remain review-gated instead of being written automatically.
- BOM hierarchy keeps `LegacyFlat` as the default so existing workflows remain backward compatible.

## Final release-candidate verification
Primary command:

```bat
scripts\Test-Version1.cmd
```

Verified on `release-v1-rc1`:
1. Core/build: **59/59 PASS**.
2. Drawing Understanding integration: **30/30 PASS**.
3. BOM hierarchy integration: **25/25 PASS**.
4. Full SOLIDWORKS 2021 integration: **46/46 PASS**.
5. Optional external dependency check reports Tesseract and `pdftoppm` as unavailable on the current machine.

Final script result: **VERSION 1 ACCEPTANCE: PASS**.

The BOM hierarchy integration creates disposable Part -> Subassembly -> Top Assembly fixtures and verifies all nine mode/display combinations plus LegacyFlat compatibility.

See `docs/VERSION1_ACCEPTANCE.md` for tomorrow's manual smoke-test order.

## External dependency note
Real raster OCR/runtime validation is tracked by GitHub Issue #8.
- Tesseract executable is not currently resolvable on the Windows test machine.
- Poppler `pdftoppm` is not currently resolvable on the Windows test machine.

The code path is implemented and contract-tested; real OCR/render output should not be called runtime-PASS until known fixtures are validated after those dependencies are configured.

## Not included in Version 1
These remain future roadmap and must not be treated as incomplete Version 1 work:
- Phase 8: Mechanical Design Copilot with deeper engineering-rule reasoning.
- Phase 9: Manufacturing Cost estimation using approved supplier/process pricing.
- Phase 10: Higher-autonomy mechanical agent workflows.

## Tomorrow's manual verification focus
1. Open SOLIDWORKS and confirm add-in/task pane startup.
2. Verify language selector contrast.
3. Verify `Agent` and `Kiểm thử` tabs.
4. Spot-test representative skills in each category.
5. Manually inspect BOM/Manufacturing Excel image output and confirm capture-only Part documents are closed afterward.
6. Test BOM hierarchy modes on a disposable nested Assembly.
7. Keep Issue #8 open until Tesseract/Poppler runtime fixtures are validated.
