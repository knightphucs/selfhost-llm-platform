# Self-Host LLM Management Platform

Nền tảng quản lý LLM self-host (chạy nội bộ / offline) kèm RAG — đồ án báo cáo thực tập tốt
nghiệp. Trục chính: **bảo mật dữ liệu doanh nghiệp** — dữ liệu suy luận không rời hạ tầng nội
bộ (LAN).

Hệ thống tách **control plane** (quản lý model / provider / deployment / quota / audit) khỏi
**data plane** (gateway OpenAI-compatible định tuyến request tới các inference engine trong
LAN), hỗ trợ task-based routing kèm fallback, token metering, RAG cô lập theo tenant và
pipeline fine-tune LoRA/QLoRA dạng pluggable.

## Trạng thái

**GĐ0 — Nền tảng.** Repo mới khởi tạo: mới có tài liệu kiến trúc, chưa có code.

## Tài liệu

| File | Nội dung |
|---|---|
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
