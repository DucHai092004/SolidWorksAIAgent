# SW-MATE AI Reference Knowledge

Mục đích: biến các dự án GitHub và tài liệu SolidWorks API thành knowledge base có kiểm soát cho việc phát triển SW-MATE AI.

## Nguồn chính

1. `../codestack-master/` — CodeStack/Xarial: SolidWorks API, Document Manager, Drawing, Assembly, BOM, deployment.
2. `sources/solidworks-automation-skill/` — automation/skill/MCP, capability registry, review gates, C# add-in host.
3. `sources/mcp-server-solidworks/` — SolidPilot: Feature Graph IR, deterministic compiler, C# .NET 4.8 execution boundary.
4. `sources/SolidworksMCP-python/` — MCP tool coverage, adapter safety, mock/real separation, session logging.
5. `sources/SolidworksMCP-TS/` — COM routing, VBA fallback, stdio transport discipline.
6. `sources/xcad-examples/` — xCAD.NET add-in, WPF TaskPane, PropertyReader, installer examples.
7. `sources/solidworks-api-angelsix/` — C# SolidWorks API learning/reference project.
8. `sources/AgentSCAD/` — NL-to-CAD pipeline, deterministic geometry validation, ambiguity/human gates.
9. `sources/Text2CAD/` — contract-driven agent pipeline and failure classification.
10. `sources/freecad-robust-mcp/` — mature MCP bridge/tool registration/testing patterns.

## Cách dùng khi code

Trước khi thêm hoặc sửa một SolidWorks skill: tra `PROJECT_CATALOG.md` → `ARCHITECTURE_PATTERNS.md` → CodeStack/xCAD/source phù hợp → đối chiếu SolidWorks 2021 API → viết integration test → mới sửa production.

Không coi code bên ngoài là source of truth. Source of truth của SW-MATE AI vẫn là code hiện tại + test thực trên SolidWorks 2021.
