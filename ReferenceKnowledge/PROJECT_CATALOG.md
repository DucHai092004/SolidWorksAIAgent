# Project Catalog

Đánh giá tại 2026-09-29. Mức ưu tiên là mức hữu ích đối với SW-MATE AI, không phải xếp hạng chất lượng chung.

| Repo | License | Vai trò tham khảo | Ưu tiên |
|---|---|---|---|
| xarial/codestack | theo repo gốc | API patterns, Document Manager, BOM, Drawing, Assembly | Rất cao |
| wzyn20051216/solidworks-automation-skill | MIT | capability registry, subskills, review/evidence, add-in host | Rất cao |
| eyfel/mcp-server-solidworks (SolidPilot) | AGPL-3.0 | IR/compiler/execution split, semantic references, reverse engineering | Rất cao, pattern-only |
| andrewbartels1/SolidworksMCP-python | MIT | 132-tool MCP, circuit breaker, docs index, session replay | Cao |
| vespo92/SolidworksMCP-TS | MIT | COM/VBA routing, stdio discipline, mock-vs-real warnings | Trung bình-cao |
| xarial/xcad-examples | chưa rõ ở metadata repo | TaskPane/WPF, PropertyReader, installer, xCAD add-in patterns | Rất cao |
| angelsix/solidworks-api | xem license trong source | C# API/tutorial structure, SolidDna patterns | Trung bình |
| Kevoyuan/AgentSCAD | MIT | deterministic validators, human decision gates, outcome ledger | Cao |
| MLYengineering/Text2CAD | MIT | explicit contracts, planner/validator/repair/execution separation | Cao |
| spkane/freecad-addon-robust-mcp-server | MIT | 150+ tool bridge, GUI/headless transport, integration/quality gates | Cao |

## Nhóm SolidWorks trực tiếp

Ưu tiên tra theo thứ tự: CodeStack → xCAD examples → solidworks-automation-skill → SolidPilot → MCP Python/TS.
Lý do: ba nguồn đầu gần C#/.NET/SolidWorks lifecycle hiện tại nhất; các MCP repo bổ sung kiến trúc agent/tool contract.

## wzyn20051216/solidworks-automation-skill

- URL: https://github.com/wzyn20051216/solidworks-automation-skill
- Snapshot: local `sources/solidworks-automation-skill/`.
- Điểm đáng học: `capabilities.yaml` làm nguồn thật duy nhất; trạng thái verified/pilot/blocked; health check; subskill routing; evidence/review JSON; C# add-in host net48/x64.
- Mapping: `SkillCatalog`, TaskPane test tab, roadmap/test matrix, future health-check, drawing review, capability gating.
- Không copy nguyên pipeline Python; SW-MATE AI giữ C# Core làm execution authority.

## eyfel/mcp-server-solidworks / SolidPilot

- URL: https://github.com/eyfel/mcp-server-solidworks
- Snapshot: local `sources/mcp-server-solidworks/`.
- License: AGPL-3.0 — chỉ học ý tưởng/pattern nếu SW-MATE AI vẫn proprietary/MIT-style.
- Điểm đáng học: Feature Graph IR; deterministic compiler; C# .NET 4.8 là lớp duy nhất chạm COM; adapter ở rìa; objective compare/verification; semantic reference resolver; drawing-to-IR.
- Mapping: `Planning`, `Agent`, `Tools`, Drawing Understanding, future durable feature references.

## andrewbartels1/SolidworksMCP-python

- URL: https://github.com/andrewbartels1/SolidworksMCP-python
- License: MIT.
- Điểm đáng học: real/mock mode tách rõ; adapter safety/circuit breaker; session logging/replay; docs API index; tool catalog lớn và tutorial regression.
- Mapping: diagnostics, command history, reproducible agent sessions, future API lookup helper.

## vespo92/SolidworksMCP-TS

- URL: https://github.com/vespo92/SolidworksMCP-TS
- License: MIT.
- Điểm đáng học: route thao tác đơn giản qua COM trực tiếp và thao tác phức tạp qua VBA fallback; không ghi log ra stdout của MCP stdio; feature-tree traversal ổn định hơn chọn bằng tên trong một số tình huống.
- Mapping: fallback execution strategy và logging discipline nếu sau này SW-MATE AI có MCP adapter ngoài process.
- Lưu ý: phần lớn tool của repo này chưa được test live; không dùng làm bằng chứng API correctness.

## xarial/xcad-examples

- URL: https://github.com/xarial/xcad-examples
- Snapshot: local `sources/xcad-examples/`.
- Điểm đáng học: WPF/WinForms hosting, TaskPane, PropertyManagerPage, PropertyReader dùng SolidWorks API + Document Manager với cùng codebase, MSI installer.
- Mapping: `SwMateAI.AddIn`, `SwMateAI.UI`, deployment, future Document Manager adapter.

## AgentSCAD + Text2CAD

- URLs: https://github.com/Kevoyuan/AgentSCAD và https://github.com/MLYengineering/Text2CAD
- License: MIT.
- Điểm đáng học chung: LLM chỉ diễn giải/lập kế hoạch; geometry/execution/validation phải deterministic; ambiguity phải dừng để người dùng quyết định; artifacts và outcome phải lưu để kiểm tra lại.
- Mapping: NaturalLanguageParser → Plan → Tools → Verify, undo checkpoint, evidence bundle, failure classification.

## FreeCAD Robust MCP

- URL: https://github.com/spkane/freecad-addon-robust-mcp-server
- License: MIT.
- Điểm đáng học: bridge tách khỏi CAD process, nhiều transport, GUI/headless capability flags, tool registration quy mô lớn, integration test, security/quality gates.
- Mapping: future external agent bridge/MCP server; không thay SolidWorks C# execution layer.
