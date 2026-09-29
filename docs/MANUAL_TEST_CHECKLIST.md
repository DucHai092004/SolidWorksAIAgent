# SW-MATE AI - Manual Test Checklist

Checkpoint: 2026-09-29
Cách dùng: test trên file bản sao; điền `[x]` khi PASS, `[!]` khi FAIL và ghi lỗi bên cạnh.

## A. Hệ thống và CAD cơ bản
- [ ] T01 GetModelInfo — mở Part/Assembly/Drawing; kỳ vọng trả đúng loại tài liệu, tên và trạng thái.
- [ ] T02 CreatePart — không cần tài liệu đang mở; kỳ vọng tạo Part mới và active document là Part.
- [ ] T03 CreateSketch — mở Part; kỳ vọng tạo/đi vào Sketch trên mặt phẳng hợp lệ.
- [ ] T04 CreateRectangle — Sketch active; Width=60, Height=40; kỳ vọng hình chữ nhật đúng kích thước.
- [ ] T05 CreateCircle — Sketch active; Diameter=10, X=0, Y=0; kỳ vọng đường tròn đúng vị trí/kích thước.
- [ ] T06 AddDimension — chọn entity phù hợp; Value=20; kỳ vọng tạo dimension và không lỗi selection.
- [ ] T07 ModifyDimension — Name=D1@Sketch1, Value=20; kỳ vọng dimension đổi đúng giá trị.
- [ ] T08 Extrude — Sketch kín; Depth=20; kỳ vọng Boss-Extrude được tạo đúng chiều sâu.
- [ ] T09 CutExtrude — Sketch kín trên solid; Depth=20; kỳ vọng Cut-Extrude xuất hiện và cắt đúng.
- [ ] T10 FilletPlateCorners — Part dạng tấm; Radius=5; kỳ vọng bo đúng các góc mục tiêu.
- [ ] T11 ChamferPlateCorners — Part dạng tấm; Distance=3; kỳ vọng vát đúng các góc mục tiêu.
- [ ] T12 CreatePlate — Width=100, Height=80, Thickness=10; kỳ vọng tạo tấm 100x80x10.
- [ ] T13 CreatePlateWithHole — 100x80x10, HoleDiameter=10; kỳ vọng tạo tấm và lỗ đúng.

## B. Model Reader
- [ ] T14 ReadFeatureTree — Part có feature; kỳ vọng danh sách theo cây feature hợp lý.
- [ ] T15 ReadFeatures — Part có feature; kỳ vọng đọc được tên/type feature.
- [ ] T16 ReadFeatureDependencies — Part có nhiều feature; kỳ vọng trả quan hệ phụ thuộc trực tiếp.
- [ ] T17 AnalyzeFeatureImpact — FeatureName=Boss-Extrude1; kỳ vọng chỉ ra feature downstream có thể bị ảnh hưởng.
- [ ] T18 ReadSketches — Part có Sketch; kỳ vọng liệt kê đúng Sketch.
- [ ] T19 ReadDimensions — Part có dimension; kỳ vọng tên và giá trị đọc được hợp lý.- [ ] T20 ReadMaterial — Part có material; kỳ vọng tên vật liệu CAD đúng.
- [ ] T21 ReadMassProperties — solid hợp lệ; kỳ vọng mass/volume/center of mass có giá trị hợp lý.
- [ ] T22 ReadCustomProperties — tài liệu có property; kỳ vọng đọc đúng key/value.
- [ ] T23 ReadSelectedObject — chọn face/edge/feature; kỳ vọng nhận đúng loại đối tượng đang chọn.
- [ ] T24 ReadBoundingBox — solid hợp lệ; kỳ vọng kích thước bao gần đúng kích thước model.

## C. Assembly
- [ ] T25 ReadAssembly — mở Assembly; kỳ vọng trả thông tin tổng quan Assembly.
- [ ] T26 ReadComponents — Assembly có component; kỳ vọng số lượng/tên component đúng.
- [ ] T27 ReadMates — Assembly có mate; kỳ vọng liệt kê mate và trạng thái.
- [ ] T28 CheckInterference — Assembly test có/không có va chạm đã biết; kỳ vọng kết quả khớp fixture.
- [ ] T29 InsertComponent — Path trỏ tới Part test; kỳ vọng component được chèn đúng file.
- [ ] T30 MoveComponent — ComponentName và XYZ hợp lệ; kỳ vọng component di chuyển đúng.
- [ ] T31 AddMate — chọn geometry phù hợp, MateType=Coincident; kỳ vọng mate mới xuất hiện.
- [ ] T32 DeleteMate — MateName=Coincident1 trên bản sao; kỳ vọng mate bị xóa và assembly vẫn mở được.
- [ ] T33 ReplaceComponent — ComponentName + NewPath trên bản sao; kỳ vọng component được thay đúng.

## D. Manufacturing và BOM
- [ ] T34 BuildManufacturingBreakdown — Assembly có Part; kỳ vọng bóc tách đủ part và thông tin phôi.
- [ ] T35 ExportManufacturingBreakdown — đặt OutputPath/DocumentRoot hợp lệ; kỳ vọng file Excel được tạo, mở được.
- [ ] T36 ApplyStockMaterials — DocumentRoot có CSV/XLSX/PDF vật liệu; kỳ vọng Part khớp được ghi Stock Material và StockSize; case mơ hồ phải yêu cầu review.
- [ ] T37 CreateBOM — ExportExcel=true, ExportCsv=true; kỳ vọng BOM tạo đủ dòng và file export tồn tại.
- [ ] T38 InsertSolidWorksBOM — Assembly + TemplatePath hợp lệ; kỳ vọng native BOM xuất hiện.

## E. Drawing Automation và Drawing Understanding
- [ ] T39 CreateDrawing — Part/Assembly nguồn đã lưu; kỳ vọng tạo Drawing mới từ model.
- [ ] T40 CreateSheet — Name=Sheet2, PaperSize=A3, Scale=1:1; kỳ vọng sheet mới đúng tên/khổ/tỷ lệ.
- [ ] T41 InsertStandardViews — ModelPath hợp lệ; kỳ vọng các hình chiếu tiêu chuẩn được chèn.
- [ ] T42 InsertIsometricView — ModelPath hợp lệ; kỳ vọng hình trục đo xuất hiện.
- [ ] T43 CreateSection — SourceViewName=Drawing View1, Label=A; kỳ vọng hình cắt A được tạo từ view nguồn.- [ ] T44 CreateDetail — SourceViewName=Drawing View1, Label=B, RadiusMm=15; kỳ vọng hình trích B được tạo.
- [ ] T45 InsertDimensions — ViewName=Drawing View1; kỳ vọng dimension đã marked-for-drawing được chèn.
- [ ] T46 InsertDrawingBOM — Drawing Assembly + TemplatePath hợp lệ; kỳ vọng BOM table xuất hiện trên sheet.
- [ ] T47 InsertBalloon — Drawing Assembly có BOM/view; kỳ vọng balloon liên kết component/BOM.
- [ ] T48 FillTitleBlock — Title, Drawing Number, Revision; kỳ vọng khung tên/property được cập nhật đúng.
- [ ] T49 ExportPDF — OutputPath hợp lệ; kỳ vọng PDF tồn tại và mở được.
- [ ] T50 ExportDXF — OutputPath hợp lệ; kỳ vọng DXF tồn tại và mở được.
- [ ] T51 ReadDrawing — Drawing có sheet/view/dimension/BOM/note; kỳ vọng đọc được đúng các nhóm dữ liệu hiện có.

## F. Test lỗi và an toàn bắt buộc
- [ ] N01 Chạy skill Part khi đang mở Assembly/Drawing — kỳ vọng từ chối rõ ràng, không sửa nhầm tài liệu.
- [ ] N02 Chạy skill cần Assembly khi đang mở Part — kỳ vọng từ chối rõ ràng.
- [ ] N03 Chạy skill cần selection khi chưa chọn — kỳ vọng báo thiếu selection, không crash.
- [ ] N04 Nhập đường dẫn file không tồn tại — kỳ vọng báo lỗi có ý nghĩa, không treo UI.
- [ ] N05 Nhập số âm/0 cho kích thước không hợp lệ — kỳ vọng validate hoặc SOLIDWORKS từ chối an toàn.
- [ ] N06 Hủy ở bước confirmation — kỳ vọng không có thay đổi tài liệu.
- [ ] N07 ApplyStockMaterials với part code gần giống nhưng khác chữ số — kỳ vọng không auto-match sai.
- [ ] N08 ApplyStockMaterials có hai nguồn exact nhưng vật liệu xung đột — kỳ vọng RequiresReview, không ghi tự động.
- [ ] N09 PDF chỉ có ảnh, không có text — kỳ vọng báo cần review/OCR thay vì đoán vật liệu.
- [ ] N10 Export tới đường dẫn không ghi được — kỳ vọng FAIL có thông báo, không báo thành công giả.

## G. Regression tự động hiện tại
- Full solution build: PASS.
- Core automated tests: 31 PASS / 0 FAIL.
- SOLIDWORKS 2021 integration runner: 52 PASS / 0 FAIL.
- Lệnh chạy Core: `scripts\Test-All.cmd`.
- Lệnh chạy integration: `scripts\Test-SolidWorks.cmd`.

Khi có lỗi manual, ghi mã test (ví dụ T47), file đang mở, tham số, dòng Log và ảnh chụp màn hình để tái hiện lỗi.