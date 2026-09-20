# Đề xuất cây thư mục solution .NET 8

> **Trạng thái: CHỜ DUYỆT.** Tài liệu này chỉ liệt kê cấu trúc. Chưa project nào được tạo,
> chưa có file `.cs` / `.csproj` / `.sln` nào trong repo.

Cấu trúc theo Clean Architecture đầy đủ, khớp với kiến trúc control plane / data plane trong
`docs/architecture.md`.

---

## 1. Cây thư mục

```
selfhost-llm-platform/
├─ CLAUDE.md
├─ README.md
├─ .gitignore
├─ .editorconfig
├─ Directory.Build.props              # net8.0, nullable enable, TreatWarningsAsErrors
├─ SelfHostLlm.sln
│
├─ docs/
│  ├─ architecture.md
│  ├─ sequences.md
│  ├─ deployment.md
│  ├─ erd.md
│  └─ solution-layout.md
│
├─ src/
│  ├─ SelfHostLlm.Domain/             # KHÔNG phụ thuộc project nào
│  │   ├─ Models/                     # Model, ModelVersion, Capability, TaskTag
│  │   ├─ Providers/                  # Provider, ProviderKind
│  │   ├─ Deployments/                # Deployment, Address, HealthStatus
│  │   ├─ Routing/                    # Route, RoutingPolicy, VirtualModel
│  │   ├─ Access/                     # Tenant, Consumer, ApiKey, Quota, Role
│  │   ├─ Usage/                      # UsageRecord, TokenCount
│  │   ├─ Rag/                        # Collection, Document, Chunk
│  │   ├─ Audit/                      # AuditLog, AuditAction
│  │   └─ Common/                     # Entity base, ValueObject, Result
│  │
│  ├─ SelfHostLlm.Application/        # → Domain
│  │   ├─ Abstractions/               # PORTS: IModelRepository, IDeploymentRepository,
│  │   │                              #   IInferenceProvider, IUsageSink, IQuotaService,
│  │   │                              #   IAuditLogger, IEmbeddingClient, IVectorStore,
│  │   │                              #   ITrainingJobRunner
│  │   ├─ Models/                     # use case handler CRUD
│  │   ├─ Providers/
│  │   ├─ Deployments/                # đăng ký address, đánh giá health
│  │   ├─ Routing/                    # RouteResolver — task/virtual model → danh sách ưu tiên
│  │   ├─ Metering/                   # token counting, aggregate, enforce quota
│  │   ├─ Rag/                        # ingest pipeline, retrieval (luôn filter tenant)
│  │   ├─ Training/                   # điều phối TrainingJob
│  │   └─ Security/                   # RBAC policy, tenant resolution
│  │
│  ├─ SelfHostLlm.Contracts/          # standalone — không phụ thuộc Domain
│  │   ├─ Admin/                      # DTO cho ControlPlane REST API
│  │   └─ OpenAi/                     # ChatCompletionRequest/Response, StreamChunk,
│  │                                  #   EmbeddingsRequest/Response, Usage
│  │
│  ├─ SelfHostLlm.Persistence/        # → Application, Domain
│  │   ├─ AppDbContext.cs
│  │   ├─ Configurations/             # EF Core entity configuration
│  │   ├─ Repositories/               # implement các port repository
│  │   ├─ Vector/                     # PgVectorStore — pgvector
│  │   └─ Migrations/
│  │
│  ├─ SelfHostLlm.Adapters.Inference/ # → Application, Contracts
│  │   ├─ OpenAiCompatible/           # adapter gốc — vLLM, TGI, LM Studio đều dùng
│  │   ├─ Ollama/                     # chỉ thêm nếu cần API ngoài /v1
│  │   ├─ HealthProbe/                # probe /health, /v1/models
│  │   └─ InferenceProviderFactory.cs # ProviderKind → adapter
│  │
│  ├─ SelfHostLlm.ControlPlane.Api/   # ASP.NET Core Web API — admin
│  │   ├─ Endpoints/                  # Models, Providers, Deployments, Routes, ApiKeys,
│  │   │                              #   Quotas, Usage, Audit, Rag, Training
│  │   ├─ Auth/                       # RBAC, admin authentication
│  │   ├─ Middleware/                 # audit logging
│  │   └─ Program.cs
│  │
│  ├─ SelfHostLlm.Gateway/            # ASP.NET Core + YARP — data plane, phần tự xây
│  │   ├─ Auth/                       # ApiKeyAuthHandler
│  │   ├─ Routing/                    # virtual model → deployment, fallback chain
│  │   ├─ Metering/                   # đếm token, ghi UsageRecord, QuotaGuard
│  │   ├─ Streaming/                  # SSE passthrough, bóc usage từ chunk cuối
│  │   ├─ Endpoints/                  # /v1/chat/completions, /v1/embeddings, /v1/models
│  │   ├─ Configuration/              # ConfigSnapshot — cache config từ control plane
│  │   └─ Program.cs
│  │
│  └─ SelfHostLlm.Worker.Health/      # BackgroundService: health probe + aggregate usage
│
├─ tests/
│  ├─ SelfHostLlm.Domain.UnitTests/
│  ├─ SelfHostLlm.Application.UnitTests/         # routing, fallback, quota — giá trị nhất
│  ├─ SelfHostLlm.Gateway.IntegrationTests/      # WebApplicationFactory + fake provider
│  └─ SelfHostLlm.ControlPlane.IntegrationTests/ # Testcontainers Postgres
│
├─ ml/                                # Python — tách hẳn khỏi solution .NET
│  ├─ training/                       # LoRA / QLoRA job
│  ├─ eval/
│  ├─ scripts/
│  └─ pyproject.toml
│
└─ deploy/
   ├─ docker-compose.yml              # Postgres + pgvector, chạy trên Mac
   ├─ mac/                            # script khởi động Ollama / MLX
   ├─ pc/                             # script vLLM (WSL2) / Ollama Windows
   └─ .env.example
```

---

## 2. Quy tắc phụ thuộc

```
Domain  ←  Application  ←  { Persistence, Adapters.Inference }  ←  { ControlPlane.Api, Gateway, Worker.Health }
                                                                    ↑
                                                    Contracts ──────┘  (standalone, ai cũng dùng được)
```

- `Domain` **không tham chiếu bất cứ project nào** — đây là ràng buộc kiểm tra được và nên có
  một test khẳng định nó.
- Interface (port) khai báo ở `Application/Abstractions/`, implement ở `Persistence` hoặc
  `Adapters.Inference`. Host project chỉ wire DI.
- `Contracts` không tham chiếu `Domain` để DTO công khai không bị kéo theo thay đổi nội bộ của
  domain model.

---

## 3. Vai trò từng project

| Project | Loại | Nội dung |
|---|---|---|
| `Domain` | classlib | Entity, value object, enum, invariant nghiệp vụ |
| `Application` | classlib | Use case, port, RouteResolver, metering logic, RAG pipeline |
| `Contracts` | classlib | DTO admin API + schema OpenAI-compatible |
| `Persistence` | classlib | EF Core, repository, pgvector, migration |
| `Adapters.Inference` | classlib | Adapter tới vLLM / Ollama / TGI, health probe |
| `ControlPlane.Api` | web | Admin REST API, RBAC, audit middleware |
| `Gateway` | web | Data plane OpenAI-compatible, YARP, auth, routing, metering |
| `Worker.Health` | worker | Health probe định kỳ, aggregate usage |

---

## 4. Thứ tự triển khai ở GĐ0

Chỉ code thật ở bốn project lõi. Các project còn lại tạo khung rỗng để cấu trúc và sơ đồ layer
trong báo cáo khớp với repo thật, nhưng chưa cần nội dung.

| Thứ tự | Project | GĐ0 làm gì |
|---|---|---|
| 1 | `Domain` | Model, Provider, Deployment, Route + common base |
| 2 | `Application` | Use case CRUD + `RouteResolver` bản đơn giản |
| 3 | `Persistence` | `AppDbContext`, configuration, migration đầu tiên |
| 4 | `ControlPlane.Api` | Endpoint CRUD Model / Provider / Deployment |
| 5 | `Adapters.Inference` | `OpenAiCompatible` adapter + health probe |
| 6 | `Gateway` | Proxy `/v1/chat/completions` tới **một** provider (Ollama trên Mac) |
| — | `Worker.Health`, `Contracts` một phần, 4 test project | khung rỗng, để GĐ1 |

Cảnh báo thành thật: mười hai project là nhiều boilerplate cho một người làm solo. Nếu đến giữa
GĐ1 mà việc tách `Application` khỏi `ControlPlane.Api` chỉ sinh ra lớp chuyển tiếp rỗng, hãy
cân nhắc gộp lại — cấu trúc phục vụ báo cáo, không phải ngược lại.

---

## 5. Lệnh tạo solution (chạy sau khi duyệt)

```bash
dotnet new sln -n SelfHostLlm

dotnet new classlib -o src/SelfHostLlm.Domain
dotnet new classlib -o src/SelfHostLlm.Application
dotnet new classlib -o src/SelfHostLlm.Contracts
dotnet new classlib -o src/SelfHostLlm.Persistence
dotnet new classlib -o src/SelfHostLlm.Adapters.Inference
dotnet new webapi   -o src/SelfHostLlm.ControlPlane.Api
dotnet new web      -o src/SelfHostLlm.Gateway
dotnet new worker   -o src/SelfHostLlm.Worker.Health

dotnet new xunit -o tests/SelfHostLlm.Domain.UnitTests
dotnet new xunit -o tests/SelfHostLlm.Application.UnitTests
dotnet new xunit -o tests/SelfHostLlm.Gateway.IntegrationTests
dotnet new xunit -o tests/SelfHostLlm.ControlPlane.IntegrationTests

dotnet sln add $(find src tests -name "*.csproj")
```

Package chính sẽ cần: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Pgvector.EntityFrameworkCore`,
`Yarp.ReverseProxy`, `Microsoft.EntityFrameworkCore.Design`, `Testcontainers.PostgreSql`.
