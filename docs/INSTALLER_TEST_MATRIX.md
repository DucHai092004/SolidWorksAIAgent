# SW-MATE AI v0.2.0 — Ma trận kiểm thử cài đặt và cập nhật

Trạng thái **PASS** chỉ dành cho các phép thử đã chạy. **PENDING** không được tính là thành công.

| ID | Trường hợp | Kỳ vọng | Trạng thái |
|---|---|---|---|
| PKG-01 | Build AddIn Release x64, không tự RegAsm trong build | 0 error | PASS |
| PKG-02 | Build updater .NET Framework 4.8 x64 | 0 error | PASS |
| PKG-03 | Inno Setup tạo .exe | EXE hợp lệ | PASS |
| PKG-04 | SHA256 của EXE | Khớp file .sha256 | PASS |
| PKG-05 | Phiên bản DLL, updater và installer | Đều 0.2.0 | PASS |
| PKG-06 | Parser release và version | Nhận đúng tag/asset | PASS |
| PKG-07 | SHA256 sai hoặc URL khác GitHub | Không chấp nhận | PASS (self-test) |
| PKG-08 | Installer không tự tắt SOLIDWORKS | Cấu hình CloseApplications=no | PASS (static) |
| PKG-09 | Test Core regression | 43 pass, 0 fail | PASS |
| PKG-10 | RegAsm /regfile dry-run | Tạo registry script, đúng COM GUID | PASS |
| INS-01 | Cài lần đầu Windows x64 có .NET 4.8 | Copy file + COM registration | PENDING (cần UAC) |
| INS-02 | SOLIDWORKS 2021 Tools > Add-Ins | Hiện SW-MATE AI | PENDING |
| INS-03 | Mở TaskPane | UI hoạt động, nút cập nhật hiện | PENDING |
| INS-04 | Cài khi SOLIDWORKS đang mở | Chặn, không tắt SOLIDWORKS | PENDING |
| INS-05 | Cài thiếu .NET Framework 4.8 | Báo yêu cầu, không cài | PENDING |
| UPD-01 | Không có public Release | Báo rõ chưa có bản phát hành | PENDING (API trả 404) |
| UPD-02 | Có Release mới và digest hợp lệ | Tải + xác minh SHA256 | PENDING |
| UPD-03 | Release có digest sai/thiếu | Không chạy installer | PENDING (logic đã self-test) |
| UPD-04 | SOLIDWORKS đang mở | Đợi người dùng đóng, không kill | PENDING |
| UPD-05 | Cài đè bản cũ | Không cần uninstall, AppId không đổi | PENDING |
| UPD-06 | Người dùng hủy UAC | Báo lỗi, giữ CAD | PENDING |
| UPD-07 | Tải đứt mạng | Xóa file .partial, không chạy EXE | PENDING |
| UN-01 | Uninstall | Gỡ COM và file chương trình | PENDING |
| UN-02 | Uninstall khi SOLIDWORKS đang mở | Chặn | PENDING |
| SEC-01 | Ký số Authenticode | Chứng thư hợp lệ | PENDING (chưa có certificate) |

## Quy trình nghiệm thu runtime

1. Lưu toàn bộ Part/Assembly/Drawing, đóng SOLIDWORKS.
2. Chạy bộ cài bằng quyền admin, lưu installer log.
3. Mở SOLIDWORKS 2021, bật Add-in và kiểm tra TaskPane.
4. Kiểm tra nút cập nhật, mạng lỗi và trạng thái không có Release.
5. Tạo phiên bản kế tiếp trên GitHub Release, kiểm tra tải + SHA-256.
6. Đóng SOLIDWORKS, chờ cập nhật tự cài đè.
7. Mở lại SOLIDWORKS, kiểm tra COM registration và các skill cơ bản.
8. Gỡ cài đặt, xác minh registry và dữ liệu CAD không bị xóa.

**Không đánh dấu INS/UPD/UN PASS chỉ dựa trên compile hoặc phân tích code.**
