# Lộ trình & nhật ký thi công

> **Đọc file này đầu mỗi phiên làm việc.** `CLAUDE.md` giữ quy tắc và kiến trúc đã chốt; file
> này giữ *trạng thái*: đã làm tới đâu, vào ngày nào, trên nhánh nào, còn nợ gì, phiên sau làm gì.
> Cập nhật ở cuối mỗi bước lớn: sửa ngày, chuyển mục "Việc tồn đọng", ghi quyết định mới.

**Cập nhật lần cuối:** 2026-09-24

---

## Bắt đầu phiên sau từ đây

1. Nhánh làm việc hiện tại: **`gd0/gateway`**. Nó chứa toàn bộ GĐ0 và **chưa merge vào `main`,
   chưa push**.
2. Việc cần bạn làm trước khi bắt đầu GĐ1 (xem "Việc tồn đọng"):
   - Merge chuỗi nhánh GĐ0 vào `main`.
   - Chạy `database update`.
   - Đặt secret và chạy thử `deploy/demo/smoke.sh`.
3. Kiểm tra nhanh repo còn xanh:
   ```bash
   docker compose -f deploy/docker-compose.yml up -d
   dotnet build && dotnet test      # kỳ vọng: 0 warning, 349 test pass
   ```
4. Bước kế tiếp: **lên plan GĐ1** (mục GĐ1 bên dưới). Chốt các "câu hỏi mở" trước khi code.

---

## Tổng quan theo giai đoạn

| GĐ | Nội dung | Bắt đầu | Xong | Nhánh | Trạng thái |
|---|---|---|---|---|---|
| **GĐ0** | Nền tảng: 12 project, Domain, Persistence, Application, Adapters, ControlPlane.Api, Gateway | 2026-09-21 | 2026-09-23 | `gd0/*` → `gd0/gateway` | **xong**, chưa merge |
| GĐ1 | Worker.Health + observability, CI, provider trên PC, so sánh Ollama vs vLLM | — | — | `gd1/*` (dự kiến) | chưa bắt đầu |
| GĐ2 | RAG ingest/query + Row Level Security; implicit routing nếu còn giờ | — | — | — | chưa |
| GĐ3 | `ITrainingJobRunner` + LoRA nhỏ trong `ml/`, số liệu, đóng gói báo cáo | — | — | — | chưa |

Hạn nộp báo cáo: **— (chưa ghi)**. Điền vào đây để chia thời gian cho GĐ1–GĐ3.

---

## GĐ0 — Nền tảng (2026-09-21 → 2026-09-23) · xong

### Các bước và nhánh

Mỗi nhánh tách từ nhánh ngay trước nó, nên merge `gd0/gateway` là có đủ tất cả.

| # | Bước | Nhánh | Commit | Ngày |
|---|---|---|---|---|
| 1 | Scaffold 12 project, build config, health/Serilog/OTel, docker-compose | `gd0/scaffold` | 6 | 2026-09-22 |
| 2 | Domain (entity, invariant, `Result`) + Contracts (OpenAI, Admin DTO) | `gd0/domain-contracts` | 7 | 2026-09-22 |
| 3 | Persistence: EF + pgvector, composite FK tenant, migration `InitialSchema`, `PgVectorStore` | `gd0/persistence` | 9 | 2026-09-22 |
| 4a | Application core: dispatcher, `ApiKeyHasher`, `RouteResolver`, `FallbackExecutor`, quota | `gd0/application-core` | 7 | 2026-09-23 |
| 4b | Use case CRUD, repository generic, audit trail, `TenantAccessBehavior` | `gd0/application-crud` | 7 | 2026-09-23 |
| 5 | ControlPlane.Api: Identity bearer, RBAC, endpoint admin, config snapshot | `gd0/controlplane` | 5 | 2026-09-23 |
| 6 | Adapters.Inference: adapter OpenAI-compatible, probe, tokenizer | `gd0/adapters` | 2 | 2026-09-23 |
| 7 | Gateway trên YARP: auth ApiKey, routing, fallback, quota, metering, SSE, E2E | `gd0/gateway` | 6 | 2026-09-23 |

Test cuối GĐ0: Domain 106 · Application 143 · ControlPlane integration 67 · Gateway integration 33
(có E2E). Tổng **349**.

### Quyết định đã chốt trong GĐ0 (bổ sung cho bảng QĐ trong CLAUDE.md)

| Chủ đề | Quyết định |
|---|---|
| Tenant | Thêm `tenant_id` cho Deployment, Route, ApiKey, Quota, ModelVersion; **composite FK** `(tenant_id, x_id) → parent(tenant_id, id)`. ERD đã đồng bộ |
| Admin auth | ASP.NET Core Identity đầy đủ + **bearer token** (1 giờ, refresh 7 ngày). Không có tự đăng ký. Bảng Identity đổi tên theo ERD |
| Admin đầu tiên | Seed lúc khởi động từ `Bootstrap:*`, chỉ khi DB đã migrate và chưa có user. Không tự migrate |
| Secret engine | Data Protection, key ring ở **thư mục file ngoài DB** (mặc định `~/Library/Application Support/SelfHostLlm/dp-keys`), CP và Gateway dùng chung |
| Dispatcher | Tự viết (~80 dòng), không dùng MediatR (license thương mại từ v13) |
| Repository | Một `ITenantRepository<T>` generic — lọc tenant ở **đúng một chỗ** |
| Output handler | Trả entity Domain; ControlPlane map sang DTO Contracts |
| ErrorKind | Thêm `RateLimited` (→ 429) |
| Fallback | Thêm **408 và 429** từ upstream vào danh sách fallback (ngoài lỗi kết nối, timeout, 5xx) |
| Internal API | `/internal/config-snapshot` bảo vệ bằng `X-Internal-Token` (so sánh constant-time), ETag theo nội dung |
| Audit | Trigger DB chặn UPDATE/DELETE/TRUNCATE trên `audit_log`; audit lưu cùng transaction với thay đổi |
| Tên bảng | Số ít, trùng tên thực thể ERD (`chunk`, `api_key`...) |

### Lệch kế hoạch và các bẫy đã gặp (để khỏi vấp lại)

- YARP 2.x **không cho thay `HttpContent`** của request gửi đi → body được viết lại trên `HttpContext.Request`.
- `UserStore` của Identity mặc định tự lưu → dùng `AppUserStore` (AutoSaveChanges = false) và override `UpdateAsync`.
- Serilog `CreateBootstrapLogger` chỉ "freeze" được một lần mỗi process → dùng logger thường. Nếu không, host song song trong test bị vỡ.
- Môi trường Testing không validate DI → mọi `WebApplicationFactory` trong test bật `ValidateOnBuild`.
- Namespace `Persistence.Vector` che kiểu `Pgvector.Vector` trong code migration → đổi thành `VectorStore/`.
- `dotnet-ef` global trên máy là bản 10, không chạy được với runtime 8 → dùng local tool (`dotnet tool run dotnet-ef`).
- Analyzer tắt có chủ đích: CA1716, CA1000 (`.editorconfig`); CA1711 cho riêng `Collection`.

### Giới hạn phải nêu trong báo cáo

- Quota in-memory: chỉ đúng khi chạy **một** instance Gateway, và là best-effort (kiểm trước, cộng sau).
- Tokenizer dự phòng là tiktoken `cl100k_base` — chỉ xấp xỉ cho Qwen/Llama, nên đánh dấu `tokens_estimated`.
- Key ring Data Protection trên macOS chỉ được bảo vệ bằng quyền file.
- Usage tháng hiện tính thẳng từ `usage_record` (GĐ1 chuyển sang `usage_aggregate`).
- `weight` của route mới dùng để sắp thứ tự, chưa có chia tải ngẫu nhiên theo trọng số.

### Việc tồn đọng sau GĐ0

- [ ] Merge `gd0/gateway` (chứa cả chuỗi) vào `main`, và quyết định có push hay không.
- [ ] Chạy `dotnet tool run dotnet-ef database update -p src/SelfHostLlm.Persistence -s src/SelfHostLlm.ControlPlane.Api` trên DB dev.
- [ ] Đặt user-secrets: `Bootstrap:AdminUsername/AdminPassword`, `InternalApi:Token` (CP), `ControlPlane:InternalToken` (Gateway).
- [ ] Chạy thật `deploy/demo/smoke.sh` với Ollama. Script mới được kiểm cú pháp, chưa chạy.
- [ ] Ví dụ SQL trong `docs/sequences.md` §2b còn ghi `chunks` và toán tử `<->` (code dùng `chunk` và `<=>`). Chờ bạn cho phép sửa.
- [ ] `docs/selfhost-llm-project.md` được CLAUDE.md nhắc tới nhưng không có trong repo.

---

## GĐ1 — Vận hành & số liệu · chưa bắt đầu

**Mục tiêu:** hệ thống tự theo dõi health, số liệu usage đủ để làm biểu đồ, có CI, và chạy thật
trên hai máy (Mac + PC).

### Hạng mục dự kiến

| # | Hạng mục | Loại | Ghi chú |
|---|---|---|---|
| 1 | Worker.Health: probe định kỳ mọi deployment (dùng `IInferenceProvider` + `HealthPolicy` sẵn có) | Tự xây | Worker cần ghi health vào bảng `deployment`. Hỏi: đọc/ghi DB thẳng hay đi qua API của CP? |
| 2 | Worker.Health: tổng hợp `usage_aggregate` (giờ/ngày/tháng) | Tự xây | Sau đó `ConfigSnapshotSource` đọc aggregate thay cho `usage_record` |
| 3 | Observability: dashboard hoặc chỉ số OTel (latency, tỉ lệ fallback, token/phút) | Điều phối lại | Chưa chọn collector — **hỏi trước**, tránh thêm hạ tầng |
| 4 | CI (GitHub Actions: build + test, có Testcontainers) | Điều phối lại | Cần repo remote |
| 5 | Deployment thứ hai trên PC (Ollama native + CUDA, khác address) | Điều phối lại | Theo `docs/deployment.md` |
| 6 | So sánh Ollama vs vLLM (latency, throughput) | Số liệu báo cáo | vLLM là mục cắt đầu tiên nếu thiếu thời gian |

### Câu hỏi mở — chốt trước khi code GĐ1
- Worker.Health ghi thẳng DB (như Gateway ghi usage) hay gọi API nội bộ của CP?
- Có dựng OTel collector / Grafana không, hay chỉ xuất số liệu qua API usage để vẽ trong báo cáo?
- Repo có remote GitHub chưa (để chạy CI)?
- Hạn nộp báo cáo, để chia thời gian GĐ1–GĐ3.

---

## GĐ2 — RAG + Row Level Security · chưa

Đã có sẵn từ GĐ0:
- Entity Collection/Document/Chunk.
- `vector(1024)` + HNSW.
- `IVectorStore`/`PgVectorStore` lọc tenant trong WHERE.
- Test "tenant B không thấy chunk của A".

Còn phải làm:
- Pipeline ingest: chunking, gọi embeddings **qua Gateway**, lưu chunk.
- Endpoint query: retrieve rồi chat.
- DTO Admin cho RAG.
- Row Level Security của Postgres làm lớp phòng thủ thứ hai.
- Implicit routing, nếu còn giờ (mục cắt đầu tiên).

## GĐ3 — Training + đóng gói báo cáo · chưa

Đã có sẵn từ GĐ0: entity Dataset/TrainingJob/ModelVersion và state machine của job.

Còn phải làm:
- `ITrainingJobRunner` gọi script local.
- Script LoRA nhỏ trong `ml/`.
- Thu thập số liệu, đóng gói báo cáo.
- Theo CLAUDE.md: không hứa training thật chạy được trên phần cứng demo, giữ ở mức interface và demo.
