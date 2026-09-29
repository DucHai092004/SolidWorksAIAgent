# Architecture Patterns To Reuse

## 1. Capability registry as source of truth

Pattern từ `solidworks-automation-skill`: mỗi capability có trạng thái và version support rõ ràng.
Áp dụng cho SW-MATE AI: tạo manifest machine-readable cho 51+ skills với `verified/pilot/blocked`, document type, destructive flag, minimum SW version và test IDs.
Không để UI, docs và runner tự khai báo capability riêng rẽ.

## 2. LLM plans; deterministic code executes

Pattern chung từ SolidPilot, AgentSCAD và Text2CAD.
Natural language không được gọi COM tùy ý. Luồng chuẩn:
`Intent -> structured plan/IR -> validation -> deterministic tool execution -> verification -> result/evidence`.
SW-MATE AI hiện đã gần mô hình này qua Parser/Planner/Tools; nên tiếp tục củng cố thay vì cho AI sinh COM code trực tiếp.

## 3. One authoritative CAD execution boundary

SolidPilot chỉ cho một lớp C# .NET 4.8 chạm COM.
SW-MATE AI nên giữ nguyên nguyên tắc: `SwMateAI.Core/Tools` là execution authority; UI/agent/MCP tương lai chỉ gọi contract.
Điều này giúp test, undo, error handling và STA/COM lifecycle nhất quán.

## 4. Explicit tool contracts and schema drift tests

MCP repos tốt đều có schema/tool catalog và test để phát hiện parameter drift.
Áp dụng: mỗi skill nên có Name, Parameters, Preconditions, SideEffects, Result schema và Verification rule.
Integration runner phải fail nếu UI/catalog khai báo skill mà registry không có hoặc parameter contract lệch.

## 5. Verification is part of the skill, not an afterthought

AgentSCAD/Text2CAD phân biệt “artifact tồn tại” với “thiết kế đúng”.
Áp dụng: mỗi thao tác phải có read-back hoặc measurable check khi khả thi: feature count/type, bbox, custom property, table count, exported file structure, Excel shapes/media, etc.
Kết quả UI nên phân biệt `executed`, `verified`, `needs_review`, `failed`.

## 6. Ambiguity gate

Nếu yêu cầu cơ khí có nhiều cách hiểu, không nên tự chọn im lặng.
Planner cần phát hiện thiếu kích thước, mặt tham chiếu, configuration, BOM mode, mate reference hoặc manufacturing assumption.
Các quyết định nguy hiểm/destructive nên có preview/plan và checkpoint undo trước khi chạy.

## 7. Read-only/offline paths before opening CAD documents

CodeStack/Xarial cho thấy Document Manager có thể đọc assembly tree, properties và preview mà không cần mở UI; lightweight component cũng có thể load ẩn vào memory.
Áp dụng: Reader/BOM/Manufacturing ưu tiên read-only/offline; chỉ activate document khi thao tác cần graphics/native state.
Điều này giảm lỗi focus, file bị mở hàng loạt và COM state drift.

## 8. Evidence bundles and replay

MCP Python + AgentSCAD lưu session/job artifacts thay vì chỉ log text.
Áp dụng: mỗi Agent run có `plan.json`, `actions.json`, `verification.json`, paths output và error classification; không lưu secrets.
Lịch sử này phục vụ debug, reproducibility và sau này học pattern từ các ca thành công.

## 9. Separate transport from CAD logic

FreeCAD Robust MCP và SolidPilot đều tách bridge/adapter khỏi execution.
Nếu thêm MCP cho SW-MATE AI: MCP chỉ là adapter; không nhét SolidWorks business logic vào server transport.
UI WPF, MCP, CLI và test runner phải dùng chung Core tool contracts.
