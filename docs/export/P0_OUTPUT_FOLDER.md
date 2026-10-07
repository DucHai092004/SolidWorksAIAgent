# P0 - THƯ MỤC ĐẦU RA

Trạng thái: PASS TC001-TC010.

## Implementation
- `src/SwMateAI.Core/Exporting/OutputPathResolver.cs`
- `src/SwMateAI.UI/TaskPaneControl.ExportTab.cs`
- Tab `Bóc tách & Xuất` chỉ chứa P0 trong checkpoint này.
- Để trống đường dẫn: dùng thư mục của model đang mở.
- Hỗ trợ absolute path, Unicode, ký tự đặc biệt, UNC và long path.
- Tự tạo thư mục khi thiếu.
- Kiểm tra quyền ghi và dung lượng tối thiểu 1 MB.
- Không ghi đè file cũ; tự thêm hậu tố `_001`, `_002`, ...

## Automated tests
- `src/SwMateAI.Core.Tests/OutputPathResolverTests.cs`
- TC001-TC010: PASS.
- Full Core regression tại checkpoint: 75/75 PASS.

## Backup policy
`release-v1-rc1` và các backup/tag cũ không bị sửa/xóa trong quá trình P0. P0 được phát triển trên nhánh riêng `feat/export-output-folder`.

## Next
Sau khi review/merge P0, chặng tiếp theo là P1 - BOM EXCEL + ẢNH (TC011-TC022).
