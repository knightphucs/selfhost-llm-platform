# Sequence diagrams

Bốn luồng chính của hệ thống. Mỗi luồng kèm ghi chú về điểm cần cẩn thận khi implement.

---

## 1. Chat completion — task routing, fallback và metering

Luồng xương sống của data plane. Client gửi `task` hoặc virtual model, gateway tự chọn
deployment thật, nếu deployment ưu tiên chết thì tụt xuống bản thay thế, và mọi request đều
được đếm token.

```mermaid
sequenceDiagram
    autonumber
    actor C as Client
    participant GW as Gateway
    participant CFG as ConfigSnapshot
    participant D1 as Deployment ưu tiên<br/>vLLM @ PC
    participant D2 as Deployment fallback<br/>Ollama @ Mac
    participant CP as ControlPlane.Api

    C->>GW: POST /v1/chat/completions<br/>model=code-fast, stream=true<br/>Authorization Bearer sk-xxx
    GW->>CFG: tra ApiKey hash
    CFG-->>GW: Consumer + Tenant + Quota

    alt ApiKey không hợp lệ
        GW-->>C: 401 Unauthorized
    end

    GW->>CFG: kiểm quota hiện tại
    alt vượt rate hoặc budget
        GW-->>C: 429 Too Many Requests
    end

    GW->>CFG: resolve virtual model code-fast
    CFG-->>GW: [D1 ưu tiên 1, D2 ưu tiên 2]

    GW->>D1: POST /v1/chat/completions
    D1--xGW: connection refused hoặc 503

    Note over GW,D2: Chỉ fallback khi lỗi kết nối / 5xx / timeout.<br/>Lỗi 4xx do client thì trả thẳng về, không fallback.

    GW->>D2: POST /v1/chat/completions
    D2-->>GW: 200 stream SSE

    loop mỗi chunk SSE
        D2-->>GW: data chunk
        GW-->>C: relay chunk
    end

    D2-->>GW: chunk cuối, có thể kèm usage
    GW-->>C: data DONE

    alt response có trường usage
        GW->>GW: lấy prompt_tokens + completion_tokens
    else engine không trả usage
        GW->>GW: đếm bằng tokenizer của model (ước lượng)
    end

    GW->>CP: ghi UsageRecord + cập nhật bộ đếm quota
    CP-->>GW: ack
```

Lưu ý implement:

- **Metering là hậu kiểm.** Quota được kiểm *trước* khi gọi dựa trên bộ đếm hiện tại, và cập
  nhật *sau* khi có usage thật. Một request vượt ngưỡng ở giữa chừng vẫn chạy xong — đây là
  best-effort, phải nói rõ giới hạn này trong báo cáo thay vì giả vờ nó chặt chẽ.
- **Ghi UsageRecord không được chặn response.** Client đã nhận xong stream trước khi usage được
  ghi. Nếu ghi lỗi thì log lại, không trả lỗi cho client.
- Sai số tokenizer khi engine không trả `usage`: nêu con số trong báo cáo là *ước lượng*.

---

## 2. RAG — ingest và query với cô lập tenant

Điểm bảo mật cốt lõi: **không có code path nào truy vấn vector mà thiếu filter `tenant_id`**.

### 2a. Ingest tài liệu

```mermaid
sequenceDiagram
    autonumber
    actor U as User (tenant A)
    participant CP as ControlPlane.Api
    participant ING as Rag ingest pipeline
    participant GW as Gateway
    participant EMB as Deployment embed
    participant DB as PostgreSQL + pgvector

    U->>CP: POST /rag/collections/{id}/documents
    CP->>CP: xác thực + resolve tenant_id = A
    CP->>ING: ingest(document, tenant_id=A)
    ING->>ING: chunking theo độ dài + overlap

    loop mỗi batch chunk
        ING->>GW: POST /v1/embeddings (model=embed)
        GW->>EMB: forward
        EMB-->>GW: vectors
        GW->>GW: ghi UsageRecord cho embedding
        GW-->>ING: vectors
    end

    ING->>DB: INSERT chunk + embedding<br/>kèm tenant_id = A
    DB-->>ING: ok
    ING-->>CP: số chunk đã ingest
    CP-->>U: 201 Created
```

Embedding cũng đi **qua gateway**, không gọi thẳng engine — để được đếm token và audit như mọi
request khác.

### 2b. Query

```mermaid
sequenceDiagram
    autonumber
    actor U as User (tenant A)
    participant CP as ControlPlane.Api
    participant RAG as Rag retrieval
    participant GW as Gateway
    participant EMB as Deployment embed
    participant DB as PostgreSQL + pgvector
    participant LLM as Deployment chat

    U->>CP: POST /rag/query { question }
    CP->>CP: resolve tenant_id = A từ ApiKey
    CP->>RAG: retrieve(question, tenant_id=A)
    RAG->>GW: POST /v1/embeddings
    GW->>EMB: forward
    EMB-->>GW: query vector
    GW-->>RAG: query vector

    RAG->>DB: SELECT FROM chunk<br/>WHERE tenant_id = 'A'<br/>ORDER BY khoảng cách vector LIMIT k

    Note over RAG,DB: tenant_id nằm trong WHERE, KHÔNG lọc sau khi<br/>lấy kết quả. Filter ở tầng app là lỗi bảo mật.

    DB-->>RAG: top-k chunk của tenant A
    RAG->>GW: POST /v1/chat/completions<br/>prompt + context đã ghép
    GW->>LLM: forward
    LLM-->>GW: câu trả lời
    GW-->>RAG: câu trả lời
    RAG-->>CP: answer + citations
    CP-->>U: 200 OK
```

Câu truy vấn thật ở bước retrieval:

```sql
-- <=> là cosine distance, khớp index HNSW vector_cosine_ops (QĐ-1)
SELECT c.id, c.content, c.embedding <=> @queryVector AS distance
FROM chunk c
WHERE c.tenant_id = @tenantId
  AND c.collection_id = @collectionId
ORDER BY c.embedding <=> @queryVector
LIMIT @k;
```

Lưu ý implement:

- `tenant_id` phải nằm trong `WHERE` của chính câu truy vấn vector, **không** lọc sau khi đã
  lấy top-k. Lọc sau vừa sai kết quả vừa rò rỉ thông tin về dữ liệu tenant khác.
- Dùng composite index `(tenant_id, ...)` cộng với index vector — xem `docs/erd.md`.
- Nên có một test khẳng định: tenant B query không bao giờ nhìn thấy chunk của tenant A.

---

## 3. Đăng ký deployment và health check

Minh hoạ quan hệ `Model × Provider × Address` và cơ chế giữ cho routing luôn biết cái gì đang
sống.

```mermaid
sequenceDiagram
    autonumber
    actor A as Admin
    participant CP as ControlPlane.Api
    participant DB as PostgreSQL
    participant W as Worker.Health
    participant DEP as Deployment<br/>http://192.168.1.50:8000
    participant GW as Gateway

    A->>CP: POST /providers { kind = vLLM }
    CP->>DB: lưu Provider
    A->>CP: POST /models { Qwen2.5-7B, 4-bit, tags=[coding] }
    CP->>DB: lưu Model
    A->>CP: POST /deployments<br/>{ modelId, providerId, baseUrl, apiKey }
    CP->>DEP: probe thử GET /v1/models
    DEP-->>CP: 200 danh sách model
    CP->>DB: lưu Deployment, health = Healthy
    CP->>DB: ghi AuditLog (ai tạo, lúc nào)
    CP-->>A: 201 Created

    loop mỗi 30 giây
        W->>DEP: GET /health hoặc /v1/models
        alt phản hồi ok
            W->>DB: health = Healthy, cập nhật latency
        else timeout hoặc lỗi
            W->>DB: tăng failure count
            alt vượt ngưỡng liên tiếp
                W->>DB: health = Unhealthy
            end
        end
    end

    GW->>CP: refresh config snapshot
    CP-->>GW: deployment + health hiện tại
    Note over GW: RouteResolver loại deployment Unhealthy<br/>khỏi danh sách ưu tiên
```

Lưu ý implement:

- Đánh dấu Unhealthy sau **nhiều lần** lỗi liên tiếp, không phải một lần — tránh nhấp nháy khi
  mạng LAN chập chờn.
- Gateway vẫn có thể gặp deployment chết giữa hai lần probe → fallback ở mục 1 là lớp bảo vệ
  thứ hai, không thể thay thế bằng health check.

---

## 4. Training LoRA/QLoRA — vòng đời model version

Vẽ ở mức **interface**. Đây là phần pluggable, demo bằng model nhỏ. Không hứa hẹn training
thật chạy được trên phần cứng demo.

```mermaid
sequenceDiagram
    autonumber
    actor A as Admin
    participant CP as ControlPlane.Api
    participant DB as PostgreSQL
    participant R as ITrainingJobRunner
    participant PY as Python job (ml/training)
    participant GPU as GPU server / máy PC

    A->>CP: POST /datasets (upload)
    CP->>DB: lưu Dataset
    A->>CP: POST /training-jobs<br/>{ baseModel, datasetId, LoRA config }
    CP->>DB: lưu TrainingJob status = Queued
    CP->>R: enqueue(job)
    R->>PY: chạy script LoRA/QLoRA
    PY->>GPU: fine-tune

    loop trong lúc chạy
        PY-->>CP: báo tiến độ / log
        CP->>DB: cập nhật status = Running
    end

    PY-->>R: adapter weights + metrics
    R->>CP: job hoàn tất
    CP->>DB: TrainingJob status = Succeeded

    A->>CP: POST /training-jobs/{id}/evaluate
    CP->>PY: chạy eval trên tập giữ lại
    PY-->>CP: điểm số
    CP->>DB: lưu kết quả evaluation

    alt kết quả đạt
        A->>CP: POST /models/{id}/versions (đăng ký version mới)
        CP->>DB: lưu ModelVersion trỏ tới adapter
        A->>CP: POST /deployments (deploy version mới)
        CP->>DB: lưu Deployment mới
        Note over CP: Từ đây version mới tham gia routing<br/>như mọi deployment khác
    else không đạt
        A->>CP: huỷ, giữ version cũ
    end
```

Lưu ý phạm vi:

- Chỉ **LoRA/QLoRA**, không full fine-tune.
- `ITrainingJobRunner` là một port — GĐ3 chỉ cần một implementation gọi script local là đủ để
  demo. Không xây job scheduler riêng.
- Môi trường thật chạy job trên GPU server / cloud. Trên máy demo chỉ chạy model rất nhỏ để
  chứng minh vòng đời `Dataset → Job → Eval → ModelVersion → Deployment` là khép kín.
