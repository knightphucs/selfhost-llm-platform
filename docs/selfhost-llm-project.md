
## Context — Knowledge base

### Kiến trúc tổng thể
Tách control plane (quản lý / cấu hình / audit) khỏi data plane (đường đi của request):
- **Data plane:** Client → API gateway (route · auth · token metering) → Inference providers.
- **Control plane:** Admin API (.NET) → Config DB + Usage/Audit store.
- Gateway đọc cấu hình từ control plane và ghi ngược usage/audit về.

### Domain model
| Thực thể | Vai trò | Ghi chú |
|---|---|---|
| `Model` | Model logic | Họ, params, quantization, context length, capabilities (chat/embedding/vision), task tags |
| `Provider` | Loại backend suy luận | vLLM / Ollama / TGI / llama.cpp / endpoint OpenAI-compatible ngoài; mỗi loại 1 adapter |
| `Deployment` (ModelInstance) | Cầu nối Model × Provider tại 1 address | Nơi Address (base URL + auth) sống; health, tải, GPU/RAM |
| `Route / RoutingPolicy` | Luật gateway chọn deployment | Theo task / tải / chất lượng-chi phí-độ trễ |
| `ApiKey / Consumer` | Chủ thể gọi API | Gắn quota |
| `UsageRecord` | Bản ghi 1 request | prompt/completion tokens, model, thời điểm |
| `Quota` | Giới hạn | token/phút (rate) + token/tháng (budget) |

Quan hệ chính: `Model` (1—n) `Deployment` (n—1) `Provider`. Một model chạy được nhiều nơi; gateway coi các deployment là endpoint thay thế nhau.

### Chức năng chính
1. Quản lý model / provider / deployment (CRUD, health check, đăng ký address).
2. Gateway OpenAI-compatible (auth bằng ApiKey, streaming).
3. Token metering — đếm ở gateway (dùng `usage` từ response hoặc tokenizer của model), ghi UsageRecord, aggregate, enforce quota.
4. Task routing + fallback.
5. RAG — embedding qua gateway + vector DB, cô lập theo tenant.
6. Training pipeline — job LoRA/QLoRA tách riêng, dạng pluggable.
7. Audit & RBAC.

### Task-based routing
- **Explicit (làm trước):** client gửi `task` hoặc chọn virtual model (`code-fast`, `chat-general`, `embed`) → gateway map sang deployment thật.
- **Implicit (nâng cao):** classifier nhẹ đọc prompt để tự chọn task.
- **Fallback:** deployment ưu tiên down → tụt xuống bản thay thế.
- Phân loại gợi ý: general chat, coding, embedding (bắt buộc cho RAG), tuỳ chọn summarization/classification rẻ cho khối lượng lớn.

### Training / fine-tune
`Dataset → LoRA/QLoRA job → Evaluation → đăng ký Model version mới → tạo Deployment mới`. Chỉ LoRA/QLoRA, không full fine-tune. Demo bằng model nhỏ; môi trường thật chạy job trên GPU server / cloud.

### Tech stack
| Lớp | Lựa chọn | Ghi chú |
|---|---|---|
| Control plane | .NET 8 Web API + PostgreSQL | CRUD + quota + audit |
| Gateway | .NET (YARP) tự viết **hoặc** LiteLLM proxy | Cân nhắc build-vs-reuse |
| Inference | Ollama / MLX (Mac), vLLM / TGI (PC/GPU) | Đều nói chuẩn OpenAI → adapter viết 1 lần |
| RAG | Embedding + pgvector (trong Postgres sẵn có) hoặc Qdrant | pgvector = ít hạ tầng hơn |

Định vị đóng góp: LiteLLM/OpenWebUI đã làm sẵn nhiều phần. Đóng góp của báo cáo nên là lớp tự xây (quản lý deployment + task routing + audit bảo mật), còn suy luận thì điều phối engine có sẵn.

### Hạ tầng demo (cấu hình thực tế)
- **Mac (M5, 16GB):** control plane + gateway + Postgres/pgvector + 1 model nhỏ (Ollama/MLX) làm provider phụ / fallback.
- **PC (i7-12700K, RTX 3070 Ti — 8GB VRAM, 32GB RAM):** provider GPU chính, model 7B–8B 4-bit; đăng ký vào control plane bằng IP nội bộ (LAN).
- Hai provider khác loại ở khác address → minh hoạ Provider/Address/Deployment; dữ liệu không rời LAN (điểm bảo mật).

Ràng buộc & tận dụng:
- **8GB VRAM** ⇒ serving nhanh giới hạn ở model 7B–8B quantize 4-bit (Qwen2.5-7B, Llama-3.1-8B). Không nhét 13B+ vào VRAM.
- **32GB RAM** ⇒ thoải mái chạy cả stack trên PC; Ollama có thể offload model lớn hơn xuống RAM (chậm hơn) nếu muốn demo model to hơn — nhưng để tốc độ tương tác thì giữ 7B–8B trong VRAM.
- **Ollama** (native Windows + CUDA): tự offload, ít ma sát — an toàn cho demo.
- **vLLM** (qua WSL2): "chất" enterprise hơn, cần model AWQ/GPTQ 4-bit + chỉnh `--max-model-len` và `--gpu-memory-utilization` cho vừa 8GB.
- Embedding model (nhỏ) đặt trên PC hoặc Mac đều được.

### Lộ trình
- **GĐ0 — Nền tảng:** control plane .NET + Postgres, CRUD Model/Provider/Deployment, gateway proxy tới 1 provider (Ollama trên Mac).
- **GĐ1 — Multi-provider + metering:** thêm provider trên PC (khác address), token metering + UsageRecord + quota, health check.
- **GĐ2 — Task routing + RAG:** virtual models theo task + fallback; embedding + pgvector, RAG pipeline, cô lập tenant.
- **GĐ3 — Training + security:** interface LoRA job (demo nhỏ), audit log, RBAC, đóng gói báo cáo.

### Rủi ro & lưu ý
- Trùng lặp open-source → định vị rõ phần tự xây vs tái sử dụng.
- Training thật vượt khả năng phần cứng → giữ ở mức pluggable/demo.
- Bảo mật là trục chính: audit, RBAC, cô lập RAG, dữ liệu ở lại LAN.
