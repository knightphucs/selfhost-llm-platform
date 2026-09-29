# Mac — control plane, gateway, Postgres, model fallback

Chi tiết: `docs/deployment.md`.

```bash
# 1. Hạ tầng lưu trữ
docker compose -f deploy/docker-compose.yml up -d

# 2. Model fallback + embedding (Ollama)
ollama serve
ollama pull bge-m3

# 3. Control plane rồi gateway
dotnet run --project src/SelfHostLlm.ControlPlane.Api   # :5001
dotnet run --project src/SelfHostLlm.Gateway            # :8080
dotnet run --project src/SelfHostLlm.Worker.Health      # :5002 (chỉ health)
```
