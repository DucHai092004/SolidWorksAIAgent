# External source snapshots

Thư mục này chứa shallow-clone (`--depth 1`) của các repo GitHub bên ngoài để tra cứu code tại chỗ.
Toàn bộ nội dung con của `sources/` được `.gitignore`; chỉ file README này được track trong SW-MATE AI.

Quy tắc:
- Không thêm project/reference ngoài vào solution build của SW-MATE AI.
- Không copy code AGPL/GPL vào production nếu chưa đánh giá nghĩa vụ license.
- Với repo license không rõ, chỉ dùng để đọc pattern/API và tự viết lại implementation.
- Khi học một pattern, đối chiếu lại SolidWorks 2021 API trước khi áp dụng.
- Mọi thay đổi production vẫn phải có Core test và SolidWorks integration test riêng.

Danh sách hiện tại được quản lý bởi `../Update-References.ps1`.
