# P1 — BOM Excel + Ảnh

Branch: `feat/export-bom-excel-images`

Mục tiêu P1: xuất BOM Excel có ảnh thumbnail, xử lý được BOM nhỏ/lớn, hierarchy, file lỗi/missing, Toolbox và các trạng thái component đặc biệt mà không làm treo phiên SOLIDWORKS người dùng.

## Kiến trúc đã xác nhận

Luồng production của tab **Bóc tách & Xuất**:

1. Đọc cấu trúc BOM bằng `CreateBOM` với `ExportExcel=false`.
2. Tạo `BomWorkerJob` + manifest checkpoint.
3. Khởi chạy `SwMateAI.BomWorker.exe` ở process riêng.
4. Worker CoCreate một phiên SOLIDWORKS automation riêng, mở từng Part/Assembly read-only, đưa về Isometric + Zoom Fit và lưu PNG.
5. Worker ghi checkpoint manifest sau từng item.
6. UI đọc manifest, áp ảnh vào `BomResult`.
7. `BomExcelExporter` dùng OpenXML tạo XLSX và nhúng ảnh; không cần Microsoft Excel.

Không sử dụng luồng `LLM -> arbitrary C#`. BOM vẫn đi qua Tool/Skill + validation hiện hữu.

## Kết quả TC011–TC022

| Test case | Kết quả | Bằng chứng runtime |
|---|---|---|
| TC011 — BOM Excel + ảnh, 10 chi tiết | PASS | Main P1 integration: 10 unique components, 10 BOM rows, thumbnail cho mọi row, workbook tồn tại |
| TC012 — Assembly >500 component | PASS | Stress fixture 510 occurrences; `AddComponents3` bulk 510 component ~1.9 s; BOM read 510 occurrences -> 1 row Qty=510 ~19.1 s; worker 1 thumbnail; XLSX tạo thành công; full process exit 0 |
| TC013 — Sub-assembly đa cấp | PASS | Dedicated hierarchy runner: 1 subassembly Level 0 + 2 parts Level 1, 3 occurrences, process exit 0 |
| TC014 — Tên file tiếng Việt/ký tự đặc biệt | PASS | `Trục_Đỡ_Ø25` được giữ đúng trong BOM |
| TC015 — Hidden component | PASS | IncludeHidden=false bỏ hidden; IncludeHidden=true giữ hidden; số row đúng |
| TC016 — Suppressed component | PASS | Suppressed component được skip an toàn; BOM row count đúng |
| TC017 — Missing linked component | PASS | Missing component được skip và có warning thay vì crash |
| TC018 — Chi tiết nhiều bề mặt cong | PASS | Runner tự tạo plate 120×80×20, 4 lỗ xuyên Ø14 + fillet R10; xác nhận 8 non-planar faces; worker tạo PNG + nhúng Excel; exit 0 |
| TC019 — SolidWorks Toolbox | PASS | Toolbox thật: `countersunk bolt_ai.sldprt`, `ToolboxPartType=1`, config `PreviewCfg`; BOM giữ đúng configuration/metadata fallback |
| TC020 — Custom Properties trống | PASS | Export không abort khi property rỗng |
| TC021 — File BOM đã tồn tại | PASS | Tạo unique suffix; không overwrite file cũ |
| TC022 — Không có Active CAD | PASS (contract) | `AgentCore.ExecuteTool("CreateBOM")` trả safe error: `Tool 'CreateBOM' cannot execute: No active SOLIDWORKS document.`; `Execute` không chạy. Manual UI smoke test vẫn giữ trong acceptance checklist |

## Regression sau P1

### Build + Core

`scripts\Test-All.cmd`

- Full solution build: PASS
- Core automated tests: **78/78 PASS**
- Có sẵn stress OpenXML: `TC012_ExporterHandles600RowsWith600EmbeddedThumbnails` PASS

### SOLIDWORKS integration

`scripts\Test-SolidWorks.cmd`

- **46/46 PASS**
- CAD creation, model readers, Drawing automation, Assembly, Manufacturing Breakdown, BOM, native Drawing BOM, balloons, semantic table read và PDF/DXF export đều không regression.

## Ghi chú kỹ thuật

- `AddComponent4` theo từng occurrence gây RPC/COM instability trong stress fixture. Test harness TC012 chuyển sang API bulk `IAssemblyDoc.AddComponents3`, đúng mục đích stress và tránh 510 COM calls rời rạc.
- TC012 production-path không dùng `CreateBOM ExportExcel=true` legacy. Nó test đúng pipeline hiện tại: BOM read -> worker ảnh -> OpenXML XLSX.
- Worker sở hữu vòng đời SOLIDWORKS automation riêng và tự `ExitApp()`; không attach vào phiên SOLIDWORKS người dùng.
- TC018 trước đây dùng fixture Toolbox đại diện; checkpoint hiện tại đã nâng lên fixture tự sinh có 8 mặt cong để test render hình học phức tạp có kiểm chứng.
- TC022 automated contract đã PASS. Còn một manual smoke-test giao diện khi SOLIDWORKS mở nhưng không có document để xác nhận thông báo UI cuối cùng.

## Trạng thái P1

**P1 BOM Excel + Ảnh: AUTOMATED PASS.**

Điều kiện còn lại trước khi coi là acceptance hoàn toàn ở UI: chạy TC022 bằng nút thật trong trạng thái SOLIDWORKS không mở document. Việc này không chặn chuyển sang phát triển P2, nhưng phải được giữ trong acceptance checklist cuối.
