# SW-MATE AI - Cài đặt EXE và cập nhật online

## Cấu hình hỗ trợ

- Windows x64, .NET Framework 4.8 trở lên, SOLIDWORKS 2021 (Add-in x64).
- Cài đặt bằng quyền Administrator vì phải đăng ký COM qua RegAsm x64.
- File cài đặt: `dist/SW-MATE-AI-Setup-0.2.0.exe`.
- Mã GUID Add-in giữ nguyên: `5C9A5E3B-4F8A-4B2C-9D1E-A7F2B3C4D5E6`.
- Inno Setup giữ `AppId` cố định qua các phiên bản để nâng cấp trực tiếp.

## Cài lần đầu

1. Lưu file CAD và đóng SOLIDWORKS.
2. Chạy `SW-MATE-AI-Setup-0.2.0.exe` (Windows sẽ hỏi quyền quản trị).
3. Installer kiểm tra .NET Framework 4.8, chép thư viện và đăng ký Add-in qua RegAsm x64.
4. Mở SOLIDWORKS > Tools > Add-Ins, kiểm tra `SW-MATE AI` đã xuất hiện. Đánh dấu **Active Add-ins** để bật ngay và **Start Up** để tự nạp trong những lần mở sau.
5. Trong TaskPane chọn **KIỂM TRA CẬP NHẬT ONLINE** khi muốn kiểm tra ngay.

## Cập nhật phiên bản mới

- Mỗi lần SOLIDWORKS mở, trình cập nhật kiểm tra tối đa 1 lần/24 giờ.
- Nguồn: GitHub Releases **công khai** của `DucHai092004/SolidWorksAIAgent`, endpoint `releases/latest`.
- Chỉ tải asset có tên `SW-MATE-AI-Setup-<version>.exe` và SHA-256 digest hợp lệ trong GitHub API.
- Khi có bản mới, người dùng xác nhận tải. Sau khi tải, updater kiểm tra SHA-256 trước khi chạy installer.
- Người dùng lưu file CAD rồi đóng SOLIDWORKS. Trình cập nhật chờ SOLIDWORKS thoát (tối đa 15 phút), sau đó mở installer bằng UAC.
- Inno Setup dùng cùng `AppId` và thư mục cài đặt nên **cài đè nâng cấp, không phải gỡ thủ công**.
- Không tự đóng SOLIDWORKS, không đụng file Part/Assembly/Drawing và không xóa thư mục dữ liệu người dùng.
- Cần giữ `SwMateAI.Updater.exe` đi cùng Add-in trong thư mục cài đặt.

## Build

Mở PowerShell trong repo:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Installer.ps1 -Version 0.2.0
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Installer.ps1 -Version 0.2.0
```

Build dùng Visual Studio 2022 MSBuild và Inno Setup 6. File cài đặt nằm trong `dist/`; file `.sha256` đi kèm để đối chiếu.

**Khi nâng phiên bản:** sửa `AssemblyVersion/AssemblyFileVersion` trong `src/SwMateAI.AddIn/Properties/AssemblyInfo.cs` và `src/SwMateAI.Updater/Program.cs`, rồi truyền đúng `-Version` cho build script. Giữ nguyên GUID Add-in và AppId installer.

## Phát hành online

Có thể chuẩn bị **Draft Release** (chưa kích hoạt cập nhật cho người dùng) bằng `scripts/Stage-Release.ps1` sau khi push commit đã test. Chỉ xuất bản bản chính thức sau khi test cài đặt trên máy sạch và test SOLIDWORKS runtime đạt:

1. Đẩy mã nguồn đã test lên GitHub.
2. Tạo **GitHub Release công khai, không phải Draft/Prerelease**, tag `v0.2.0`.
3. Đính kèm đúng file `SW-MATE-AI-Setup-0.2.0.exe` và `.sha256` từ `dist/`.
4. Kiểm tra GitHub API trả về `assets[].digest = sha256:<64 ký tự hex>`.
5. Trên máy đang cài bản cũ, nhấn **KIỂM TRA CẬP NHẬT ONLINE** và thử quy trình nâng cấp.

**Lưu ý:** Nếu repository là private hoặc chưa có GitHub Release công khai, updater sẽ thông báo chưa có bản phát hành. Không nhúng GitHub token cá nhân vào phần mềm.

## Kiểm thử và giới hạn

- Build Release x64: phải PASS.
- Updater `--self-test`: test phân tích phiên bản, JSON Release, SHA-256.
- Verify hash `.exe` so với `.sha256`: phải PASS.
- Inno Setup compile: phải PASS.
- Integration: cài lần đầu, kiểm tra COM registration trong SOLIDWORKS, nâng cấp khi đang có bản cũ, uninstall; **cần kiểm thử runtime thực tế**.
- Online end-to-end: chỉ có thể PASS sau khi Release công khai thực sự tồn tại.
- Bộ cài chưa ký số Authenticode. Khi phân phối chính thức nên ký số installer và updater bằng chứng thư đáng tin cậy. SHA-256 của GitHub giúp kiểm tra tính toàn vẹn nhưng không thay thế ký số.
- Chưa tự động rollback khi installer thất bại; hãy giữ bản cài/backup trước khi nâng cấp.
