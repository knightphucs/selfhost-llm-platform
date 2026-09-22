# Self-Host LLM Management Platform

Nền tảng quản lý LLM self-host (chạy nội bộ / offline) kèm RAG — deliverable cho báo cáo
thực tập tốt nghiệp. Trục chính của đồ án là **bảo mật dữ liệu doanh nghiệp**: dữ liệu suy
luận không rời hạ tầng nội bộ (LAN).

Nền tảng cho phép: quản lý model / provider / deployment (address) / token; định tuyến model
theo đầu việc (task-based routing) + fallback; RAG với cô lập dữ liệu theo tenant; pipeline
training LoRA/QLoRA tách riêng.

**Giai đoạn hiện tại: GĐ0 — Nền tảng.** Repo mới khởi tạo, chưa có code.

**Định hướng thi công đã chốt:** dựng **kiến trúc đầy đủ ngay từ đầu** (12 project, Clean
Architecture, observability, test suite) thay vì dựng tối giản rồi refactor. Đổi lại GĐ0 dài
hơn. Hệ quả: audit log, RBAC và task routing được implement **sớm hơn** lộ trình gốc.

---

## Cách làm việc với repo này

- **Trả lời bằng tiếng Việt**, giữ nguyên thuật ngữ kỹ thuật tiếng Anh (deployment, routing,
  quota, embedding... không dịch). Định danh trong code (class, method, biến) bằng tiếng Anh;
  comment và XML doc bằng tiếng Việt.
- Hành xử như một **senior software architect thực dụng**. Câu trả lời phải cụ thể, khả thi,
  triển khai được ngay.
- Đây là đồ án của **một sinh viên làm solo**, thời gian và phần cứng hạn chế. Ưu tiên tái sử
  dụng engine có sẵn hơn là viết lại từ đầu.
- **Thành thật về giới hạn.** Đừng hứa quá (đặc biệt là phần training). Cảnh báo scope creep.
  Khi phạm vi mơ hồ thì **hỏi lại, không đoán**.
- Mỗi khi đề xuất một hạng mục mới, nói rõ nó thuộc **phần tự xây** (đóng góp của báo cáo) hay
  **phần điều phối lại** engine có sẵn.
- **Commit theo Conventional Commits** (`feat(gateway):`, `fix(persistence):`, `test(...)`,
  `docs:`). Commit nhỏ, mỗi commit build xanh.
- Sau mỗi bước lớn: chạy `dotnet build && dotnet test` trước khi báo hoàn thành.
- **Không tự chạy `dotnet ef database update`** — tạo migration xong thì dừng và báo.
- **Không sửa file trong `docs/`** trừ khi được yêu cầu rõ ràng. Đó là tài liệu đã chốt và là
  phụ lục báo cáo.
- Khi một yêu cầu mâu thuẫn với file này → **dừng lại và hỏi**, không tự giải quyết.

---

## Tech stack

| Lớp | Lựa chọn | Ghi chú |
|---|---|---|
| Control plane | .NET 8 Web API + PostgreSQL 16 | CRUD + quota + audit |
| Gateway (data plane) | **.NET 8 + YARP full pipeline, tự viết** | Đã chốt — KHÔNG dùng LiteLLM proxy. Dựng trên YARP full pipeline, xem QĐ-3 |
| Inference | Ollama / MLX (Mac), vLLM / TGI (PC/GPU) | Đều nói chuẩn OpenAI → adapter viết 1 lần |
| RAG | Embedding + **pgvector** trong Postgres sẵn có | Ít hạ tầng hơn Qdrant |
| ML / training | Python (LoRA/QLoRA) | Tách hẳn khỏi solution .NET, thư mục `ml/` |

Khi viết code, mặc định theo stack này.

---

## Kiến trúc đã chốt

Tách **control plane** (quản lý / cấu hình / audit) khỏi **data plane** (đường đi của request):

```
Data plane:    Client → Gateway (auth · routing · fallback · metering · SSE) → Inference engines
Control plane: ControlPlane.Api → PostgreSQL (config · usage · audit · vector)
Liên kết:      CP --config snapshot--> GW         (pull, cache, 30s, có ETag)
               GW --UsageRecord------> PostgreSQL (async, batch, ngoài critical path)
```

Ba bất biến, không được vi phạm:

1. **Control plane KHÔNG nằm trên đường đi của request suy luận.** CP chết thì Gateway vẫn
   phục vụ được bằng `ConfigSnapshot` gần nhất.
2. **Gateway KHÔNG query thẳng bảng config.** Chỉ đọc qua `ConfigSnapshot` lấy từ
   `GET /internal/config-snapshot`. Ngoại lệ duy nhất: ghi `usage_record` và `audit_log`.
3. **Mọi truy vấn dữ liệu RAG phải có `tenant_id` trong `WHERE` của chính câu truy vấn
   vector** — không bao giờ lọc tenant sau khi đã lấy top-k.

Xem `docs/architecture.md` để có sơ đồ đầy đủ.

---

## Domain model

| Thực thể | Vai trò | Ghi chú |
|---|---|---|
| `Model` | Model logic | Họ, params, quantization, context length, capabilities (chat/embedding/vision), task tags |
| `Provider` | Loại backend suy luận | vLLM / Ollama / TGI / llama.cpp / endpoint OpenAI-compatible ngoài; mỗi loại 1 adapter |
| `Deployment` (ModelInstance) | Cầu nối Model × Provider tại 1 address | Nơi Address (base URL + auth) sống; health, tải, GPU/RAM |
| `VirtualModel` | Tên model ảo theo task mà client gọi | `code-fast`, `chat-general`, `embed` |
| `Route` / `RoutingPolicy` | Luật gateway chọn deployment | Ánh xạ VirtualModel → Deployment, kèm `priority` để fallback |
| `ApiKey` / `Consumer` | Chủ thể gọi API | Gắn quota |
| `UsageRecord` | Bản ghi 1 request | prompt/completion tokens, model, thời điểm |
| `Quota` | Giới hạn | token/phút (rate) + token/tháng (budget) |
| `Tenant` | Đơn vị cô lập dữ liệu | Mọi bảng RAG và usage đều mang `tenant_id` |
| `User` / `Role` | Chủ thể quản trị và phân quyền | RBAC trên control plane |
| `AuditLog` | Vết thao tác quản trị | Ai · làm gì · lên thực thể nào · khi nào — append-only |
| `Collection` / `Document` / `Chunk` | Dữ liệu RAG | Vector là cột `embedding` của `Chunk`, kiểu pgvector |
| `Dataset` / `TrainingJob` / `ModelVersion` | Pipeline fine-tune | Pluggable, mức demo |

**Quan hệ trục:** `Model` (1—n) `Deployment` (n—1) `Provider`. Một model chạy được nhiều nơi;
gateway coi các deployment của cùng một model là **endpoint thay thế nhau**.

Xem `docs/erd.md` để có ERD đầy đủ. Tên thực thể và tên cột ở đây phải **khớp tuyệt đối** với ERD.

Mọi entity có dữ liệu người dùng implement `ITenantScoped { Guid TenantId }`, kể cả khi
`tenant_id` suy ra được qua FK. Mọi repository method truy vấn entity `ITenantScoped`
**bắt buộc nhận `tenantId` làm tham số** — không có overload nào thiếu nó.

---

## Quyết định kỹ thuật đã chốt

| # | Quyết định | Lý do |
|---|---|---|
| QĐ-1 | Embedding: **bge-m3, `vector(1024)` cố định**, index HNSW `vector_cosine_ops` | pgvector cần dimension cố định mới index được. `Collection.embedding_dim` giữ lại làm **guard**: API từ chối ingest nếu khác `EmbeddingConstants.Dimension`. Đa-dimension là mở rộng tương lai, cần partition bảng — ghi rõ trong báo cáo |
| QĐ-2 | Gateway ghi `UsageRecord` **thẳng Postgres** qua `Channel<T>` + background batch flush, KHÔNG qua HTTP tới CP | Giảm latency; CP chết vẫn ghi được usage. Bất biến số 1 vẫn giữ: ghi là một chiều, async, ngoài critical path |
| QĐ-3 | Gateway dựng trên **YARP full pipeline** (`MapReverseProxy` + `IProxyConfigProvider` tự sinh từ `ConfigSnapshot` + transform + health check) | Đã chốt dùng đầy đủ. YARP lo proxy/transform/health check; ta xây `RouteResolver` + fallback + SSE usage tap **trên nền đó**. Hai thứ YARP KHÔNG làm hộ và phải tự viết: (a) route theo field `model` trong **body** — YARP match path/header không match body → cần middleware set destination động; (b) fallback same-request khi 5xx — YARP không retry sang destination khác, và fallback chỉ chạy được **trước byte đầu tiên** |
| QĐ-4 | Gateway **tự inject `"stream_options": {"include_usage": true}`** vào upstream request khi `stream=true` | vLLM và Ollama đều trả `usage` ở chunk cuối khi có flag này → né được tokenizer trong phần lớn trường hợp |
| QĐ-5 | Tokenizer fallback dùng `Microsoft.ML.Tokenizers`; khi dùng phải set `UsageRecord.tokens_estimated = true` | Báo cáo phải phân biệt được số đo thật và số ước lượng |
| QĐ-6 | Solution **đầy đủ 12 project** theo `docs/solution-layout.md` | Ưu tiên kiến trúc đầy đủ, tránh refactor giữa kỳ |
| QĐ-7 | **`DateTimeOffset` toàn bộ**, cột `timestamptz`, luôn UTC | Npgsql 8 rất khó tính với `DateTime.Kind` |
| QĐ-8 | `Deployment.api_key_encrypted` mã hoá bằng **ASP.NET Core Data Protection** | Không tự chế AES |
| QĐ-9 | `ApiKey` lưu **SHA-256 hash + `key_prefix`**; plaintext chỉ hiện đúng một lần lúc tạo | So sánh hash phải constant-time |

---

## Cấu trúc solution

```
src/
  SelfHostLlm.Domain/              # classlib — KHÔNG reference project nào
  SelfHostLlm.Application/         # classlib — → Domain
  SelfHostLlm.Contracts/           # classlib — standalone, KHÔNG reference Domain
  SelfHostLlm.Persistence/         # classlib — → Application, Domain
  SelfHostLlm.Adapters.Inference/  # classlib — → Application, Contracts
  SelfHostLlm.ControlPlane.Api/    # webapi
  SelfHostLlm.Gateway/             # web
  SelfHostLlm.Worker.Health/       # worker
tests/
  SelfHostLlm.Domain.UnitTests/
  SelfHostLlm.Application.UnitTests/          # ← giá trị nhất: routing, fallback, quota
  SelfHostLlm.Gateway.IntegrationTests/
  SelfHostLlm.ControlPlane.IntegrationTests/  # Testcontainers Postgres
ml/                                # Python — tách hẳn khỏi solution .NET
deploy/                            # docker-compose, script mac/pc
docs/                              # tài liệu thiết kế đã chốt
```

**Quy tắc phụ thuộc (Clean Architecture)** — chiều phụ thuộc chỉ hướng vào trong:

```
Domain ← Application ← { Persistence, Adapters.Inference } ← { ControlPlane.Api, Gateway, Worker.Health }
                                                              ↑
                                              Contracts ──────┘  (standalone)
```

- `Domain` **không tham chiếu bất cứ project nào** — có test NetArchTest khẳng định điều này.
- `Contracts` **không reference `Domain`** — DTO công khai không bị kéo theo thay đổi nội bộ
  của domain model.
- Port (interface) khai báo ở `Application/Abstractions/`, implement ở `Persistence` hoặc
  `Adapters.Inference`. Host project **chỉ wire DI**, không chứa business logic.
- EF Core code-first; `DbContext`, entity configuration và migration đều ở `Persistence`.
- Mỗi loại backend suy luận implement chung `IInferenceProvider`; adapter OpenAI-compatible là
  adapter gốc, loại khác chỉ thêm khi thật sự khác chuẩn.

Chi tiết cây thư mục con: `docs/solution-layout.md`.

---

## Chuẩn code

**Bắt buộc**

- .NET 8 / C# 12. `Nullable=enable`, `TreatWarningsAsErrors=true`, `ImplicitUsings=enable`.
- **Central Package Management** — mọi version ở `Directory.Packages.props`; `.csproj` chỉ có
  `<PackageReference Include="..." />` không kèm `Version`.
- `file-scoped namespace`; `sealed` mặc định; `record` cho DTO và value object.
- Mọi I/O đều async, luôn truyền `CancellationToken` xuống tận cùng. Không `async void`,
  không `.Result` / `.Wait()`.
- Không dùng exception cho luồng nghiệp vụ dự đoán được → dùng `Result<T>` trong
  `Domain/Common/`. `ErrorKind`: NotFound, Validation, Conflict, Unauthorized, Forbidden,
  Unavailable, RateLimited (→ 429, vượt quota).

**API**

- Minimal API + endpoint group, `TypedResults`, extension `MapXxxEndpoints(this IEndpointRouteBuilder)`.
- Lỗi theo **RFC 7807 ProblemDetails**, qua `IExceptionHandler` (.NET 8) + `AddProblemDetails()`.
  Riêng Gateway trả lỗi theo **format của OpenAI** (`{ "error": { message, type, code } }`)
  để SDK client hiểu được.
- Validation bằng **FluentValidation**, một validator cho mỗi request DTO, chạy trong pipeline
  behavior chứ không rải trong handler.
- Admin API dưới `/api/v1/...`. Gateway giữ nguyên `/v1/...` để tương thích SDK OpenAI.

**Database**

- EF Core 8 + Npgsql + `EFCore.NamingConventions` → `UseSnakeCaseNamingConvention()`.
- Một `IEntityTypeConfiguration<T>` riêng cho mỗi entity trong `Persistence/Configurations/`.
- Enum lưu dạng **string**, không phải int. Kèm check constraint.
- Không `EnsureCreated()`. Luôn migration. Index và raw SQL (HNSW, RLS) viết trong migration
  bằng `migrationBuilder.Sql(...)`.
- Soft delete (`enabled` / `revoked_at`) cho `ApiKey` và `Deployment` — xoá cứng làm mất toàn
  vẹn usage lịch sử.

**Observability**

- **Serilog** structured logging, enrich `TenantId` / `ConsumerId` / `TraceId`. Console JSON ở
  Production, file rolling daily.
- **OpenTelemetry** tracing + metrics, exporter OTLP, tắt được bằng `Otel:Enabled=false`
  (mặc định false để dev không cần dựng collector).
- Health check `/health/live` và `/health/ready` ở mọi host project.
- **Không bao giờ log**: API key plaintext, nội dung prompt, nội dung chunk RAG. Chỉ log
  `key_prefix`, độ dài, hash.

**Resilience**

- **Polly v8** qua `Microsoft.Extensions.Http.Resilience`: timeout + circuit breaker cho HTTP
  tới engine. **Không retry ở tầng HTTP** — retry là việc của `RouteResolver` + `FallbackExecutor`,
  tránh retry hai tầng nhân nhau.

**Test**

- xUnit + FluentAssertions + NSubstitute. Integration dùng Testcontainers.
- Đặt tên: `MethodName_Scenario_ExpectedResult`.
- Bắt buộc có test: `RouteResolver` (priority, loại Unhealthy, fallback chain),
  `FallbackExecutor` (5xx fallback, 4xx không fallback), `QuotaService`, `ApiKeyHasher`,
  **và một test khẳng định tenant B không truy vấn được chunk của tenant A**.

---

## Chức năng chính

1. Quản lý model / provider / deployment (CRUD, health check, đăng ký address).
2. Gateway OpenAI-compatible (auth bằng ApiKey, streaming SSE).
3. Token metering — đếm ở gateway (`usage` từ response, hoặc tokenizer khi engine không trả),
   ghi `UsageRecord`, aggregate, enforce quota.
4. Task routing + fallback.
5. RAG — embedding **đi qua gateway** (để được đếm token và audit) + vector DB, cô lập tenant.
6. Training pipeline — job LoRA/QLoRA tách riêng, dạng pluggable.
7. Audit & RBAC.

---

## Task-based routing

- **Explicit (làm trước):** client gửi `task` hoặc chọn virtual model (`code-fast`,
  `chat-general`, `embed`) → gateway map sang deployment thật.
- **Implicit (nâng cao):** classifier nhẹ đọc prompt để tự chọn task.
- **Fallback:** `RouteResolver` trả về một **danh sách có thứ tự**, không phải một deployment.
  Chỉ fallback khi **lỗi kết nối / timeout / 5xx**, cộng thêm **408 và 429** từ upstream (engine
  bận, không phải lỗi của client). Các lỗi 4xx còn lại do client thì trả thẳng về.
- Phân loại: general chat, coding, embedding (bắt buộc cho RAG), tuỳ chọn
  summarization/classification rẻ cho khối lượng lớn.

---

## Ràng buộc phần cứng — ĐỌC TRƯỚC KHI ĐỀ XUẤT MODEL

- **Mac (M5, 16GB):** control plane + gateway + Postgres/pgvector + 1 model nhỏ (Ollama/MLX)
  làm provider phụ / fallback + embedding model.
- **PC (i7-12700K, RTX 3070 Ti — 8GB VRAM, 32GB RAM):** provider GPU chính.

Quy tắc cứng:

- **Không đề xuất giải pháp cần quá 8GB VRAM.** Serving tốc độ cao giới hạn ở model **7B–8B
  quantize 4-bit** (Qwen2.5-7B, Llama-3.1-8B). Không nhét 13B+ vào VRAM.
- 32GB RAM cho phép Ollama offload model lớn hơn xuống RAM (chậm hơn) nếu cần demo — nhưng để
  tốc độ tương tác thì giữ 7B–8B trong VRAM.
- **Ollama** (native Windows + CUDA): tự offload, ít ma sát — an toàn cho demo.
- **vLLM** (qua WSL2): "chất" enterprise hơn, cần model AWQ/GPTQ 4-bit và chỉnh
  `--max-model-len` + `--gpu-memory-utilization` cho vừa 8GB.
- Embedding model đặt trên **Mac** (bge-m3 ~2.2GB, để dành VRAM của PC cho model 7B).

Địa chỉ demo: Mac `ControlPlane.Api:5001`, `Gateway:8080`, `Postgres:5432`, `Ollama:11434`.
PC `Ollama:11434`, `vLLM:8000` — đăng ký bằng IP LAN (ví dụ `http://192.168.1.50:11434`).
Đặt IP tĩnh hoặc DHCP reservation cho PC. Xem `docs/deployment.md`.

---

## Build vs reuse — định vị đóng góp

LiteLLM / OpenWebUI đã làm sẵn nhiều phần. Đóng góp của báo cáo nằm ở lớp tự xây:

| Tự xây (đóng góp) | Điều phối lại (tái sử dụng) |
|---|---|
| Quản lý Model/Provider/Deployment + health | vLLM, Ollama, TGI, MLX |
| Task routing + fallback chain | YARP (reverse proxy, transform, health check) |
| Token metering + quota enforcement | pgvector (vector search) |
| Audit log + RBAC + cô lập tenant | PostgreSQL, EF Core, Polly, Serilog, OpenTelemetry |
| SSE usage tap + destination resolver trên nền YARP | PEFT / transformers cho LoRA |

Khi trả lời, luôn nêu rõ một đề xuất rơi vào cột nào. Đứng giữa "tự viết" và "dùng thư viện"
cho phần **tái sử dụng** → luôn chọn thư viện. Cho phần **tự xây** → chọn cái giải thích được
rõ ràng trong báo cáo.

---

## Trục bảo mật (luận điểm chính của báo cáo)

- **Audit log**: mọi thao tác quản trị ghi vết, append-only, không có endpoint sửa/xoá.
  Ghi `before_value` / `after_value`. Request suy luận thông thường đi vào `usage_record`,
  không làm phồng audit.
- **RBAC**: `PlatformAdmin` / `TenantAdmin` / `Operator` / `Viewer`, policy-based theo
  permission string.
- **Cô lập RAG theo tenant**: mọi truy vấn vector **bắt buộc** filter `tenant_id` trong `WHERE`.
  Không có code path nào query vector mà thiếu tenant filter. Cân nhắc bật Row Level Security
  của PostgreSQL làm lớp phòng thủ thứ hai (GĐ2).
- **Dữ liệu ở lại LAN**: không gọi API model thương mại trong luồng chính.

---

## Lệnh thường dùng

```bash
# Hạ tầng
docker compose -f deploy/docker-compose.yml up -d     # Postgres + pgvector

# Build & test
dotnet build
dotnet test

# Migration (chạy từ root)
dotnet ef migrations add <Name> \
  -p src/SelfHostLlm.Persistence -s src/SelfHostLlm.ControlPlane.Api
dotnet ef database update \
  -p src/SelfHostLlm.Persistence -s src/SelfHostLlm.ControlPlane.Api

# Chạy
dotnet run --project src/SelfHostLlm.ControlPlane.Api
dotnet run --project src/SelfHostLlm.Gateway
dotnet run --project src/SelfHostLlm.Worker.Health

# Kiểm tra engine trên PC từ Mac (làm TRƯỚC khi seed deployment)
curl http://192.168.1.50:11434/v1/models
```

---

## Lộ trình

| GĐ | Nội dung | Trạng thái |
|---|---|---|
| **GĐ0** | Scaffold đầy đủ 12 project, Domain + Contracts, Persistence + migration, Application, Adapters.Inference, ControlPlane.Api (CRUD + RBAC + audit), Gateway (auth + routing + fallback + metering + SSE) | **đang làm** |
| GĐ1 | Worker.Health + observability, test suite + CI, thêm provider trên PC (khác address), so sánh Ollama vs vLLM | chưa |
| GĐ2 | RAG ingest/query hoàn chỉnh + Row Level Security; implicit routing (classifier) nếu còn giờ | chưa |
| GĐ3 | `ITrainingJobRunner` + script LoRA nhỏ trong `ml/`, đóng gói báo cáo, thu thập số liệu | chưa |

Do chọn dựng kiến trúc đầy đủ ngay từ đầu, **audit log, RBAC và task routing đã nằm ở GĐ0**
thay vì GĐ2/GĐ3 như lộ trình ban đầu trong `docs/selfhost-llm-project.md`.

Thứ tự thi công GĐ0: `scaffold` → `Domain + Contracts` → `Persistence` → `Application` →
`Adapters.Inference` → `ControlPlane.Api` → `Gateway`.

**Nếu thiếu thời gian, cắt theo thứ tự:** implicit routing → vLLM deployment thứ hai →
training (giữ interface, bỏ implementation chạy thật). **Không được cắt:** audit log,
test cô lập tenant, RBAC — đó là ba thứ chống đỡ luận điểm của báo cáo.

---

## Không làm

- Full fine-tune (chỉ LoRA/QLoRA, mức pluggable/demo).
- Nhét model 13B+ vào 8GB VRAM.
- Viết lại engine inference, vector index, hay tokenizer từ đầu.
- Hứa rằng training thật sẽ chạy được trên phần cứng demo.
- Xây job scheduler riêng cho training — `ITrainingJobRunner` gọi script local là đủ.
- Thêm hạ tầng mới (Qdrant, Redis, Kafka, K8s...) khi chưa có lý do cụ thể — cảnh báo scope
  creep trước đã.
- Log API key plaintext, nội dung prompt, hoặc nội dung chunk RAG.