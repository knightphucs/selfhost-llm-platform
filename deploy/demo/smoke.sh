#!/usr/bin/env bash
# Demo GĐ0 end-to-end trên máy thật: đăng nhập admin → cấu hình tenant qua Control plane →
# gọi /v1/chat/completions qua Gateway → xem usage và audit log.
#
# Điều kiện (xem README "Chạy thử GĐ0"):
#   - Postgres đã chạy và đã `dotnet ef database update`.
#   - ControlPlane (:5001) chạy với Bootstrap:AdminUsername/AdminPassword + InternalApi:Token.
#   - Gateway (:8080) chạy với ControlPlane:InternalToken giống CP.
#   - Một engine OpenAI-compatible đang chạy (mặc định Ollama trên Mac) và đã pull model.
set -euo pipefail

CP="${CP:-http://localhost:5001}"
GW="${GW:-http://localhost:8080}"
ADMIN_USER="${ADMIN_USER:-admin}"
ADMIN_PASS="${ADMIN_PASS:?Đặt ADMIN_PASS = mật khẩu Bootstrap:AdminPassword}"
ENGINE_URL="${ENGINE_URL:-http://localhost:11434}"      # PC: http://192.168.1.50:11434
REMOTE_MODEL="${REMOTE_MODEL:-qwen2.5:3b}"               # tên model phía engine (ollama list)
SNAPSHOT_WAIT="${SNAPSHOT_WAIT:-31}"                      # Gateway kéo snapshot mỗi 30 giây

need() { command -v "$1" >/dev/null || { echo "Thiếu $1"; exit 1; }; }
need curl; need jq

step() { printf '\n\033[1m== %s\033[0m\n' "$*"; }
post() { curl -fsS -X POST "$CP$1" -H "Authorization: Bearer $TOKEN" -H 'content-type: application/json' -d "$2"; }

step "1. Đăng nhập admin"
TOKEN=$(curl -fsS -X POST "$CP/api/v1/auth/login" -H 'content-type: application/json' \
  -d "{\"username\":\"$ADMIN_USER\",\"password\":\"$ADMIN_PASS\"}" | jq -r .accessToken)
TENANT=$(curl -fsS "$CP/api/v1/auth/me" -H "Authorization: Bearer $TOKEN" | jq -r .tenantId)
echo "tenant = $TENANT"
API="/api/v1/tenants/$TENANT"
SUFFIX=$(date +%s)

step "2. Model, provider, deployment (CP probe engine ngay khi đăng ký)"
MODEL=$(post "$API/models" "{\"name\":\"demo-model-$SUFFIX\",\"family\":\"qwen2.5\",\"paramSize\":\"3B\",\"quantization\":\"Q4_K_M\",\"contextLength\":32768,\"capabilities\":[\"Chat\"],\"taskTags\":[\"chat\"]}" | jq -r .id)
PROVIDER=$(post "$API/providers" '{"name":"Ollama","kind":"Ollama","description":null}' | jq -r .id)
DEPLOYMENT=$(post "$API/deployments" "{\"modelId\":\"$MODEL\",\"modelVersionId\":null,\"providerId\":\"$PROVIDER\",\"baseUrl\":\"$ENGINE_URL\",\"remoteModelName\":\"$REMOTE_MODEL\",\"apiKey\":null}")
echo "$DEPLOYMENT" | jq '{id, baseUrl, remoteModelName, healthStatus, latencyMsP50}'
DEPLOYMENT_ID=$(echo "$DEPLOYMENT" | jq -r .id)

step "3. Virtual model 'chat-demo-$SUFFIX' + route"
VM=$(post "$API/virtual-models" "{\"name\":\"chat-demo-$SUFFIX\",\"task\":\"Chat\",\"description\":null}" | jq -r .id)
post "$API/virtual-models/$VM/routes" "{\"virtualModelId\":\"$VM\",\"deploymentId\":\"$DEPLOYMENT_ID\",\"priority\":0,\"weight\":1}" >/dev/null

step "4. Consumer + API key (plaintext chỉ hiện đúng lần này)"
CONSUMER=$(post "$API/consumers" '{"name":"smoke-test","description":null}' | jq -r .id)
KEY=$(post "$API/consumers/$CONSUMER/api-keys" "{\"consumerId\":\"$CONSUMER\",\"expiresAt\":null}" | jq -r .key)
echo "api key prefix = ${KEY:0:10}…"

step "5. Chờ Gateway kéo snapshot (${SNAPSHOT_WAIT}s)"
sleep "$SNAPSHOT_WAIT"

step "6. Chat qua Gateway (stream=false)"
curl -fsS "$GW/v1/chat/completions" -H "Authorization: Bearer $KEY" -H 'content-type: application/json' \
  -d "{\"model\":\"chat-demo-$SUFFIX\",\"messages\":[{\"role\":\"user\",\"content\":\"Chào bạn, trả lời ngắn gọn 1 câu.\"}]}" \
  | jq '{model, answer: .choices[0].message.content, usage}'

step "7. Chat streaming (SSE)"
curl -fsSN "$GW/v1/chat/completions" -H "Authorization: Bearer $KEY" -H 'content-type: application/json' \
  -d "{\"model\":\"chat-demo-$SUFFIX\",\"stream\":true,\"messages\":[{\"role\":\"user\",\"content\":\"Đếm từ 1 đến 5.\"}]}" | head -c 600; echo

step "8. Usage (Gateway ghi batch tối đa 2 giây) và audit log"
sleep 3
FROM=$(date -u -v-1H +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -d '-1 hour' +%Y-%m-%dT%H:%M:%SZ)
TO=$(date -u -v+1H +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -d '+1 hour' +%Y-%m-%dT%H:%M:%SZ)
curl -fsS "$CP$API/usage/summary?from=$FROM&to=$TO" -H "Authorization: Bearer $TOKEN" | jq .
curl -fsS "$CP$API/audit-logs?pageSize=10" -H "Authorization: Bearer $TOKEN" | jq '.items[] | {action, entityType, occurredAt}'
