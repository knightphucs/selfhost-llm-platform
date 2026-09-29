# Self-Host LLM Management Platform

Nền tảng quản lý LLM self-host (chạy nội bộ / offline) kèm RAG — đồ án báo cáo thực tập tốt
nghiệp. Trục chính: **bảo mật dữ liệu doanh nghiệp** — dữ liệu suy luận không rời hạ tầng nội
bộ (LAN).

Hệ thống tách **control plane** (quản lý model / provider / deployment / quota / audit) khỏi
**data plane** (gateway OpenAI-compatible định tuyến request tới các inference engine trong
LAN), hỗ trợ task-based routing kèm fallback, token metering, RAG cô lập theo tenant và
pipeline fine-tune LoRA/QLoRA dạng pluggable.

## Trạng thái

**GĐ0 — Nền tảng: xong.** Control plane (CRUD + RBAC + audit + config snapshot), Gateway
OpenAI-compatible trên YARP (auth ApiKey, task routing, fallback trước byte đầu tiên, quota,
metering, SSE), Persistence (pgvector, cô lập tenant bằng composite FK, audit append-only) và
test suite (unit + integration Testcontainers + E2E). GĐ1 kế tiếp: Worker.Health, CI, provider trên PC.

## Bắt đầu nhanh

Yêu cầu: .NET SDK 8 (pin trong `global.json`), Docker.

```bash
# Postgres 16 + pgvector
docker compose -f deploy/docker-compose.yml up -d

# Build & test
dotnet build
dotnet test

# Chạy host (profile Development)
dotnet run --project src/SelfHostLlm.ControlPlane.Api   # http://localhost:5001
dotnet run --project src/SelfHostLlm.Gateway            # http://localhost:8080
dotnet run --project src/SelfHostLlm.Worker.Health      # http://localhost:5002

# Health check
curl localhost:5001/health/live     # process còn sống
curl localhost:5001/health/ready    # 503 nếu Postgres không kết nối được
```

## Chạy thử GĐ0 (demo thật)

```bash
# 1. Postgres + schema (migration KHÔNG tự chạy — người vận hành chạy tay)
docker compose -f deploy/docker-compose.yml up -d
dotnet tool restore
dotnet tool run dotnet-ef database update -p src/SelfHostLlm.Persistence -s src/SelfHostLlm.ControlPlane.Api

# 2. Secret dev — không commit
dotnet user-secrets set "Bootstrap:AdminUsername" "admin" --project src/SelfHostLlm.ControlPlane.Api
dotnet user-secrets set "Bootstrap:AdminPassword" "<mật khẩu ≥ 10 ký tự>" --project src/SelfHostLlm.ControlPlane.Api
dotnet user-secrets set "InternalApi:Token" "<token dài ngẫu nhiên>" --project src/SelfHostLlm.ControlPlane.Api
dotnet user-secrets set "ControlPlane:InternalToken" "<cùng token>" --project src/SelfHostLlm.Gateway

# 3. Engine + hai host (mỗi lệnh một terminal)
ollama serve && ollama pull qwen2.5:3b
dotnet run --project src/SelfHostLlm.ControlPlane.Api   # :5001 — lần đầu tạo PlatformAdmin
dotnet run --project src/SelfHostLlm.Gateway            # :8080 — kéo snapshot mỗi 30 giây

# 4. Kịch bản demo: cấu hình qua CP → chat qua Gateway → usage + audit
ADMIN_PASS='<mật khẩu>' deploy/demo/smoke.sh
```

CP và Gateway dùng chung key ring Data Protection (mặc định `~/Library/Application Support/SelfHostLlm/dp-keys`)
để Gateway giải mã API key phía engine mà CP đã mã hoá.

## Tài liệu

| File | Nội dung |
|---|---|
| [ROADMAP.md](ROADMAP.md) | Tiến độ theo giai đoạn: ngày, nhánh, quyết định, việc tồn đọng, kế hoạch GĐ sau |
| [CLAUDE.md](CLAUDE.md) | Kiến trúc đã chốt, domain model, quy ước code, ràng buộc phần cứng |
| [docs/architecture.md](docs/architecture.md) | Context / container / component diagram, tự xây vs tái sử dụng |
| [docs/sequences.md](docs/sequences.md) | Chat completion + fallback + metering, RAG, health check, training |
| [docs/deployment.md](docs/deployment.md) | Hạ tầng demo Mac + PC qua LAN, Ollama vs vLLM |
| [docs/erd.md](docs/erd.md) | Mô hình dữ liệu đầy đủ |
| [docs/solution-layout.md](docs/solution-layout.md) | Cây thư mục solution .NET 8 — **chờ duyệt** |

Diagram viết bằng Mermaid, GitHub render trực tiếp.

## Stack

.NET 8 / C# cho control plane và gateway (ASP.NET Core + YARP) · PostgreSQL + pgvector ·
Ollama / MLX / vLLM / TGI cho inference · Python cho pipeline LoRA/QLoRA.

## Hạ tầng demo

- **Mac M5 16GB** — control plane, gateway, Postgres + pgvector, một model nhỏ làm fallback.
- **PC i7-12700K / RTX 3070 Ti 8GB / 32GB RAM** — provider GPU chính, model 7B–8B 4-bit, đăng
  ký vào control plane qua IP nội bộ.

Chi tiết ở [docs/deployment.md](docs/deployment.md).
