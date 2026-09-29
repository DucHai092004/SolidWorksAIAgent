# Adoption Map For SW-MATE AI

## Áp dụng ngay trong Phase 7 / hardening

| Pattern | Module đích | Tham khảo |
|---|---|---|
| Hidden/lightweight model loading | BOM, Manufacturing, Readers | CodeStack lightweight-get-model-doc |
| BOM semantics: ExcludeFromBOM, part-number source, child show/hide/promote | BOM | CodeStack configuration-bom-options |
| Same API surface for SW + Document Manager | future read-only reader | xCAD PropertiesReader |
| Capability manifest + verification status | Skills/UI/Test matrix | solidworks-automation-skill |
| Result read-back/evidence | IntegrationRunner + UI result | AgentSCAD/Text2CAD |
| Explicit real-vs-mock/test boundary | testing | SolidworksMCP-python |

## Sau khi Phase 7 ổn định

- Chuẩn hóa `SkillDefinition` thành contract machine-readable và sinh tab Test từ registry thay vì danh sách UI thủ công.
- Tạo `ComponentModelResolver` dùng chung cho Normal/Lightweight/Suppressed/hidden-open component.
- Tách `VerificationService` khỏi từng tool để chuẩn hóa PASS / NEEDS_REVIEW / FAIL.
- Tạo execution/session record để replay một Agent run và so sánh trước/sau.
- Đồng bộ Drawing BOM configuration với referenced view.

## Nghiên cứu cho thế hệ Agent tiếp theo

- Feature Graph / mechanical IR ở mức feature intent, học pattern từ SolidPilot nhưng tự thiết kế implementation riêng.
- Semantic/persistent reference resolver cho face/edge thay cho phụ thuộc tên hoặc index mong manh.
- Drawing-to-feature-plan: Drawing Understanding -> structured engineering evidence -> plan -> build -> verify.
- External MCP adapter chỉ gọi SW-MATE Core; không để MCP layer chạm COM trực tiếp.
