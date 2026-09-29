# SW-MATE AI — Version 1 Release Notes

## Status
Version 1 is the release line covering Phase 0 through Phase 7 of the original `AgnetAiSoliworkVer1` roadmap.

## Included capabilities
- Natural-language command parsing for core CAD, Assembly, Manufacturing, BOM and Drawing workflows.
- Agent planning, confirmation gates, result checking, logging and undo support for eligible actions.
- Part creation/editing workflows including sketches, extrudes, cuts, dimensions, plates, holes, fillets and chamfers.
- Model understanding for features, dependencies, sketches, dimensions, material, mass properties, custom properties, selected objects and bounding boxes.
- Assembly reading and editing including components, mates, interference checks, component insert/move/replace and mate add/delete.
- Manufacturing breakdown with quantities, stock classification, stock dimensions, stock weight, document-based material enrichment, image capture and Excel export.
- BOM generation with Excel/CSV export, component images and native SOLIDWORKS BOM support.
- Drawing automation: create Drawing/sheets/views, isometric, section/detail, dimensions, BOM, balloons, title-block properties, PDF and DXF export.
- Drawing understanding: native SLDDRW semantic extraction for sheets/views, dimensions, notes and tables/BOM.
- External Drawing/PDF/image evidence pipeline with native PDF text, raster readiness, Tesseract OCR adapter, PdfPig embedded-image extraction and optional `pdftoppm` full-page rendering fallback.

## Safety behavior
- Mutating actions can require confirmation.
- Automated integration tests use disposable fixtures and must not overwrite production CAD files.
- Low-confidence external OCR remains review-gated.
- Missing OCR/render dependencies never generate fabricated semantic evidence.
- Ambiguous material-source matches remain review-gated instead of being written automatically.

## Release verification
Primary command:

```bat
scripts\Test-Version1.cmd
```

Required code acceptance stages:
1. Build + Core automated tests.
2. Drawing Understanding integration.
3. Full SOLIDWORKS 2021 integration.

The most recent pre-RC checkpoint was:
- Core: 49/49 PASS.
- Drawing Understanding integration: 30/30 PASS.
- Full SOLIDWORKS integration: 46/46 PASS.

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

## Branch
Release candidate work is consolidated on:
`release-v1-rc1`
