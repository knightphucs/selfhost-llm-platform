# Kiến trúc tổng thể

Tài liệu này mô tả kiến trúc của Self-Host LLM Management Platform ở ba mức: bối cảnh
(context), thành phần triển khai (container) và cấu tạo bên trong của Gateway (component).

Nguyên tắc xuyên suốt: **tách control plane khỏi data plane**. Control plane quản lý cấu hình,
quota và audit; data plane là đường đi thật của request suy luận. Gateway đọc cấu hình từ
control plane và ghi ngược usage/audit về — hai chiều này phải nhìn thấy được trên sơ đồ.

---

## 1. Context diagram

Ai dùng hệ thống, và hệ thống nói chuyện với cái gì bên ngoài.

```mermaid
flowchart TB
    dev["Lập trình viên / Ứng dụng nội bộ<br/>dùng SDK OpenAI-compatible"]
    admin["Quản trị viên nền tảng<br/>quản lý model, quota, RBAC"]

    platform["<b>Self-Host LLM Platform</b><br/>Control plane + Gateway<br/>.NET 8 / PostgreSQL"]

    ollama["Ollama / MLX<br/>trên Mac"]
    vllm["vLLM / TGI<br/>trên PC có GPU"]

    dev -->|"POST /v1/chat/completions<br/>Bearer ApiKey"| platform
    admin -->|"REST admin API"| platform
    platform -->|"HTTP OpenAI-compatible<br/>trong LAN"| ollama
    platform -->|"HTTP OpenAI-compatible<br/>trong LAN"| vllm

    subgraph lan["Biên LAN — dữ liệu không rời khỏi đây"]
        platform
        ollama
        vllm
    end

    classDef ext fill:#eef,stroke:#557
    classDef core fill:#dfd,stroke:#484,stroke-width:2px
    classDef engine fill:#ffe,stroke:#883
    class dev,admin ext
    class platform core
    class ollama,vllm engine
```

Không có mũi tên nào đi ra Internet trong luồng chính — đây là luận điểm bảo mật của báo cáo.

---

## 2. Container diagram — tách control plane / data plane

```mermaid
flowchart TB
    client["Client app<br/>SDK OpenAI"]
    adminui["Admin UI / curl"]

    subgraph dp["DATA PLANE — đường đi của request"]
        gw["<b>SelfHostLlm.Gateway</b><br/>ASP.NET Core + YARP<br/>auth · routing · fallback<br/>metering · SSE streaming"]
        dep1["Deployment: chat-general<br/>Ollama @ Mac"]
        dep2["Deployment: code-fast<br/>vLLM @ PC, 7B 4-bit"]
        dep3["Deployment: embed<br/>embedding model"]
    end

    subgraph cp["CONTROL PLANE — quản lý & audit"]
        api["<b>SelfHostLlm.ControlPlane.Api</b><br/>CRUD Model/Provider/Deployment<br/>Route · ApiKey · Quota · RBAC"]
        worker["<b>Worker.Health</b><br/>probe health<br/>aggregate usage"]
    end

    db[("PostgreSQL + pgvector<br/>config · usage · audit · vector")]

    client -->|"1. request kèm task/virtual model"| gw
    gw -->|"2. chọn deployment"| dep1
    gw -.->|"2b. fallback khi down"| dep2
    gw -->|"embedding cho RAG"| dep3

    api --> db
    worker --> db
    worker -.->|"health probe"| dep1
    worker -.->|"health probe"| dep2

    api ==>|"<b>config snapshot</b><br/>route · deployment · apikey · quota"| gw
    gw ==>|"<b>usage + audit</b><br/>UsageRecord, quota counter"| api

    adminui --> api

    classDef plane fill:#f7f7f7,stroke:#999
    classDef mine fill:#dfd,stroke:#484,stroke-width:2px
    classDef engine fill:#ffe,stroke:#883
    class gw,api,worker mine
    class dep1,dep2,dep3 engine
```

Hai mũi tên đậm giữa `ControlPlane.Api` và `Gateway` chính là hợp đồng giữa hai mặt phẳng:

| Chiều | Nội dung | Tần suất |
|---|---|---|
| Control → Data | Danh sách deployment, route/virtual model, ApiKey hash, quota | Cache + refresh định kỳ / khi có thay đổi |
| Data → Control | `UsageRecord`, bộ đếm quota, sự kiện audit ở mức request | Sau mỗi request, có thể batch |

Gateway **không** truy vấn thẳng bảng config ngoài lớp snapshot của nó, và control plane
**không** nằm trên đường đi của request suy luận — nếu control plane chết, gateway vẫn phục vụ
được bằng snapshot gần nhất.

---

## 3. Component diagram — bên trong Gateway

Đây là phần tự xây trọng tâm của báo cáo.

```mermaid
flowchart LR
    req["Request<br/>/v1/chat/completions"]

    subgraph gateway["SelfHostLlm.Gateway"]
        auth["ApiKeyAuthHandler<br/>xác thực + gắn Consumer/Tenant"]
        quota["QuotaGuard<br/>chặn khi vượt rate/budget"]
        resolver["RouteResolver<br/>task/virtual model → danh sách<br/>deployment ưu tiên"]
        adapter["IInferenceProvider<br/>adapter theo ProviderKind"]
        stream["StreamingRelay<br/>SSE passthrough"]
        meter["MeteringMiddleware<br/>bóc usage hoặc đếm bằng tokenizer"]
        sink["UsageSink<br/>ghi UsageRecord"]
        cfg[("ConfigSnapshot<br/>cache từ control plane")]
    end

    backend["Deployment thật<br/>vLLM / Ollama"]

    req --> auth --> quota --> resolver --> adapter --> backend
    backend --> stream --> meter --> sink
    stream --> resp["Response tới client"]
    resolver -.->|"đọc route/deployment"| cfg
    quota -.->|"đọc quota"| cfg
    auth -.->|"đọc ApiKey"| cfg
    adapter -.->|"deployment down → thử bản kế tiếp"| resolver

    classDef mine fill:#dfd,stroke:#484,stroke-width:2px
    class auth,quota,resolver,adapter,stream,meter,sink mine
```

Điểm cần chú ý khi implement:

- **Metering với streaming**: với SSE, `usage` chỉ xuất hiện ở chunk cuối (và không phải engine
  nào cũng trả). Fallback là đếm bằng tokenizer của model — chấp nhận sai số nhỏ, phải ghi rõ
  trong báo cáo là con số ước lượng.
- **Fallback**: `RouteResolver` trả về một *danh sách có thứ tự*, không phải một deployment.
  Adapter thất bại → thử phần tử kế tiếp. Chỉ fallback khi lỗi kết nối / 5xx / timeout, không
  fallback khi client gửi request sai (4xx).
- **Quota**: kiểm tra trước khi gọi (dựa trên bộ đếm hiện tại) và cập nhật sau khi có usage
  thật. Đây là best-effort, không phải transaction chặt — nêu rõ giới hạn này.

---

## 4. Tự xây vs tái sử dụng

Ranh giới đóng góp của báo cáo:

```mermaid
flowchart TB
    subgraph build["TỰ XÂY — đóng góp của báo cáo"]
        b1["Quản lý Model / Provider / Deployment<br/>+ health, đăng ký address LAN"]
        b2["Task routing + fallback chain"]
        b3["Token metering + quota enforcement"]
        b4["Audit log + RBAC + cô lập tenant cho RAG"]
        b5["Gateway OpenAI-compatible viết bằng .NET"]
    end

    subgraph reuse["TÁI SỬ DỤNG — điều phối lại"]
        r1["vLLM / Ollama / TGI / MLX<br/>engine suy luận"]
        r2["YARP — reverse proxy primitives"]
        r3["PostgreSQL + pgvector<br/>lưu trữ & vector search"]
        r4["EF Core"]
        r5["PEFT / transformers cho LoRA"]
    end

    build --> reuse

    classDef mine fill:#dfd,stroke:#484,stroke-width:2px
    classDef ext fill:#eee,stroke:#999
    class b1,b2,b3,b4,b5 mine
    class r1,r2,r3,r4,r5 ext
```

So với LiteLLM / OpenWebUI: các công cụ đó đã làm sẵn proxy và UI. Điểm khác biệt được bảo vệ
trong báo cáo là **mô hình quản trị deployment theo address + task routing + lớp audit/tenant
isolation hướng doanh nghiệp**, chứ không phải bản thân việc proxy request.
