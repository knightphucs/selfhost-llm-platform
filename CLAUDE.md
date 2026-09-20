# Self-Host LLM Management Platform

Nền tảng quản lý LLM self-host (chạy nội bộ / offline) kèm RAG — deliverable cho báo cáo
thực tập tốt nghiệp. Trục chính của đồ án là **bảo mật dữ liệu doanh nghiệp**: dữ liệu suy
luận không rời hạ tầng nội bộ (LAN).

Nền tảng cho phép: quản lý model / provider / deployment (address) / token; định tuyến model
theo đầu việc (task-based routing) + fallback; RAG với cô lập dữ liệu theo tenant; pipeline
training LoRA/QLoRA tách riêng.

**Giai đoạn hiện tại: GĐ0 — Nền tảng.** Repo mới khởi tạo, chưa có code.

---

## Cách làm việc với repo này

- **Trả lời bằng tiếng Việt**, giữ nguyên thuật ngữ kỹ thuật tiếng Anh (deployment, routing,
  quota, embedding... không dịch).
- Hành xử như một **senior software architect thực dụng**. Câu trả lời phải cụ thể, khả thi,
  triển khai được ngay.
- Đây là đồ án của **một sinh viên làm solo**, thời gian và phần cứng hạn chế. Ưu tiên tái sử
  dụng engine có sẵn hơn là viết lại từ đầu.
- **Thành thật về giới hạn.** Đừng hứa quá (đặc biệt là phần training). Cảnh báo scope creep.
  Khi phạm vi mơ hồ thì **hỏi lại, không đoán**.
- Mỗi khi đề xuất một hạng mục mới, nói rõ nó thuộc **phần tự xây** (đóng góp của báo cáo) hay
  **phần điều phối lại** engine có sẵn.

---

## Tech stack

| Lớp | Lựa chọn | Ghi chú |
|---|---|---|
| Control plane | .NET 8 Web API + PostgreSQL | CRUD + quota + audit |
| Gateway (data plane) | **.NET 8 + YARP, tự viết** | Đã chốt — KHÔNG dùng LiteLLM proxy |
| Inference | Ollama / MLX (Mac), vLLM / TGI (PC/GPU) | Đều nói chuẩn OpenAI → adapter viết 1 lần |
| RAG | Embedding + **pgvector** trong Postgres sẵn có | Ít hạ tầng hơn Qdrant |
| ML / training | Python (LoRA/QLoRA) | Tách hẳn khỏi solution .NET, thư mục `ml/` |

Khi viết code, mặc định theo stack này.

---

## Kiến trúc đã chốt

Tách **control plane** (quản lý / cấu hình / audit) khỏi **data plane** (đường đi của request):

- **Data plane:** Client → API gateway (route · auth · token metering) → Inference providers.
- **Control plane:** Admin API (.NET) → Config DB + Usage/Audit store.
- Gateway **đọc cấu hình** từ control plane và **ghi ngược usage/audit** về.

> Ranh giới này là bất khả xâm phạm. Gateway không truy vấn trực tiếp bảng config ngoài lớp
> snapshot/cache của nó; control plane không nằm trên đường đi của request suy luận.

Xem `docs/architecture.md` để có sơ đồ đầy đủ.

---

## Domain model

| Thực thể | Vai trò | Ghi chú |
|---|---|---|
| `Model` | Model logic | Họ, params, quantization, context length, capabilities (chat/embedding/vision), task tags |
| `Provider` | Loại backend suy luận | vLLM / Ollama / TGI / llama.cpp / endpoint OpenAI-compatible ngoài; mỗi loại 1 adapter |
| `Deployment` (ModelInstance) | Cầu nối Model × Provider tại 1 address | Nơi Address (base URL + auth) sống; health, tải, GPU/RAM |
| `Route` / `RoutingPolicy` | Luật gateway chọn deployment | Theo task / tải / chất lượng-chi phí-độ trễ |
| `ApiKey` / `Consumer` | Chủ thể gọi API | Gắn quota |
| `UsageRecord` | Bản ghi 1 request | prompt/completion tokens, model, thời điểm |
| `Quota` | Giới hạn | token/phút (rate) + token/tháng (budget) |
| `Tenant` | Đơn vị cô lập dữ liệu | Mọi bảng RAG và usage đều mang `tenant_id` |
| `AuditLog` | Vết thao tác quản trị | Ai · làm gì · lên thực thể nào · khi nào |
| `Collection` / `Document` / `Chunk` / `Embedding` | Dữ liệu RAG | Vector nằm ở pgvector |
| `Dataset` / `TrainingJob` / `ModelVersion` | Pipeline fine-tune | Pluggable, mức demo |

**Quan hệ trục:** `Model` (1—n) `Deployment` (n—1) `Provider`. Một model chạy được nhiều nơi;
gateway coi các deployment của cùng một model là **endpoint thay thế nhau**.

Xem `docs/erd.md` để có ERD đầy đủ. Tên thực thể ở đây và ở ERD phải khớp nhau.

---

## Chức năng chính

1. Quản lý model / provider / deployment (CRUD, health check, đăng ký address).
2. Gateway OpenAI-compatible (auth bằng ApiKey, streaming SSE).
3. Token metering — đếm ở gateway (dùng `usage` từ response, hoặc tokenizer của model khi
   response không trả `usage`), ghi `UsageRecord`, aggregate, enforce quota.
4. Task routing + fallback.
5. RAG — embedding qua gateway + vector DB, cô lập theo tenant.
6. Training pipeline — job LoRA/QLoRA tách riêng, dạng pluggable.
7. Audit & RBAC.

---

## Task-based routing

- **Explicit (làm trước):** client gửi `task` hoặc chọn virtual model (`code-fast`,
  `chat-general`, `embed`) → gateway map sang deployment thật.
- **Implicit (nâng cao):** classifier nhẹ đọc prompt để tự chọn task.
- **Fallback:** deployment ưu tiên down → tụt xuống bản thay thế.
- Phân loại: general chat, coding, embedding (bắt buộc cho RAG), tuỳ chọn
  summarization/classification rẻ cho khối lượng lớn.

---

## Ràng buộc phần cứng — ĐỌC TRƯỚC KHI ĐỀ XUẤT MODEL

- **Mac (M5, 16GB):** control plane + gateway + Postgres/pgvector + 1 model nhỏ (Ollama/MLX)
  làm provider phụ / fallback.
- **PC (i7-12700K, RTX 3070 Ti — 8GB VRAM, 32GB RAM):** provider GPU chính.

Quy tắc cứng:

- **Không đề xuất giải pháp cần quá 8GB VRAM.** Serving tốc độ cao giới hạn ở model **7B–8B
  quantize 4-bit** (Qwen2.5-7B, Llama-3.1-8B). Không nhét 13B+ vào VRAM.
- 32GB RAM cho phép Ollama offload model lớn hơn xuống RAM (chậm hơn) nếu cần demo — nhưng để
  tốc độ tương tác thì giữ 7B–8B trong VRAM.
- **Ollama** (native Windows + CUDA): tự offload, ít ma sát — an toàn cho demo.
- **vLLM** (qua WSL2): "chất" enterprise hơn, cần model AWQ/GPTQ 4-bit và chỉnh
  `--max-model-len` + `--gpu-memory-utilization` cho vừa 8GB.
- Embedding model (nhỏ) đặt trên PC hoặc Mac đều được.

Hai provider khác loại ở khác address → minh hoạ Provider/Address/Deployment; dữ liệu không rời
LAN (điểm bảo mật). Xem `docs/deployment.md`.

---

## Build vs reuse — định vị đóng góp

LiteLLM / OpenWebUI đã làm sẵn nhiều phần. Đóng góp của báo cáo nằm ở lớp tự xây:

| Tự xây (đóng góp) | Điều phối lại (tái sử dụng) |
|---|---|
| Quản lý Model/Provider/Deployment + health | vLLM, Ollama, TGI, MLX |
| Task routing + fallback | YARP (reverse proxy primitives) |
| Token metering + quota enforcement | pgvector (vector search) |
| Audit log + RBAC + cô lập tenant | PostgreSQL, EF Core |
| Gateway OpenAI-compatible (.NET) | PEFT/transformers cho LoRA |

Khi trả lời, luôn nêu rõ một đề xuất rơi vào cột nào.

---

## Trục bảo mật (luận điểm chính của báo cáo)

- **Audit log**: mọi thao tác quản trị ghi vết, không xoá được từ API.
- **RBAC**: phân quyền theo role trên control plane.
- **Cô lập RAG theo tenant**: mọi truy vấn vector **bắt buộc** filter `tenant_id`. Không có
  code path nào query vector mà thiếu tenant filter.
- **Dữ liệu ở lại LAN**: không gọi API model thương mại trong luồng chính.

---

## Quy ước code

- Namespace / project: `SelfHostLlm.*`. Cấu trúc solution xem `docs/solution-layout.md`.
- **Quy tắc phụ thuộc (Clean Architecture)** — chiều phụ thuộc chỉ hướng vào trong:
  `Domain` ← `Application` ← {`Persistence`, `Adapters.Inference`} ← {`ControlPlane.Api`,
  `Gateway`, `Worker.Health`}.
  `Domain` **không tham chiếu bất cứ project nào**. `Contracts` đứng riêng, ai cũng dùng được.
- Interface (port) khai báo ở `Application/Abstractions/`, implement ở `Persistence` hoặc
  `Adapters.Inference`.
- EF Core code-first; `DbContext`, entity configuration và migration đều nằm ở `Persistence`.
- DTO công khai (REST admin API + schema OpenAI-compatible) nằm ở `Contracts`, không dùng
  entity domain làm DTO.
- Mỗi loại backend suy luận implement chung một interface `IInferenceProvider`; adapter
  OpenAI-compatible là adapter gốc, các loại khác chỉ thêm khi thật sự khác chuẩn.
- `nullable enable`, TFM `net8.0`, cảnh báo coi như lỗi (`Directory.Build.props`).

## Lệnh thường dùng

```bash
dotnet build                                   # build toàn solution
dotnet test                                    # chạy test
dotnet ef migrations add <Name> \
  -p src/SelfHostLlm.Persistence \
  -s src/SelfHostLlm.ControlPlane.Api          # thêm migration
docker compose -f deploy/docker-compose.yml up -d   # Postgres + pgvector
```

---

## Lộ trình

| GĐ | Nội dung | Trạng thái |
|---|---|---|
| **GĐ0** | Control plane .NET + Postgres, CRUD Model/Provider/Deployment, gateway proxy tới 1 provider (Ollama trên Mac) | **đang làm** |
| GĐ1 | Multi-provider (thêm PC, khác address), token metering + UsageRecord + quota, health check | chưa |
| GĐ2 | Task routing (virtual models) + fallback; embedding + pgvector, RAG pipeline, cô lập tenant | chưa |
| GĐ3 | Interface LoRA job (demo nhỏ), audit log, RBAC, đóng gói báo cáo | chưa |

Thứ tự code ở GĐ0: `Domain` → `Application` → `Persistence` → `ControlPlane.Api` →
`Adapters.Inference` → `Gateway`. Các project còn lại để khung rỗng.

---

## Không làm

- Full fine-tune (chỉ LoRA/QLoRA, mức pluggable/demo).
- Nhét model 13B+ vào 8GB VRAM.
- Viết lại engine inference, vector index, hay tokenizer từ đầu.
- Hứa rằng training thật sẽ chạy được trên phần cứng demo.
- Thêm hạ tầng mới (Qdrant, Redis, Kafka, K8s...) khi chưa có lý do cụ thể — cảnh báo scope
  creep trước đã.
