# PC — provider GPU chính (RTX 3070 Ti, 8GB VRAM)

Chi tiết và bảng so sánh Ollama / vLLM: `docs/deployment.md`.

**Ollama (native Windows + CUDA)** — mặc định cho demo:

1. Đặt biến môi trường `OLLAMA_HOST=0.0.0.0:11434`, khởi động lại Ollama.
2. Mở port 11434 trong Windows Firewall cho mạng private.
3. `ollama pull qwen2.5:7b-instruct-q4_K_M`

**vLLM (WSL2)** — tuỳ chọn, model AWQ/GPTQ 4-bit:

```bash
python -m vllm.entrypoints.openai.api_server \
  --model Qwen/Qwen2.5-7B-Instruct-AWQ \
  --quantization awq \
  --max-model-len 4096 \
  --gpu-memory-utilization 0.90 \
  --port 8000 --host 0.0.0.0
```

Kiểm tra từ Mac trước khi đăng ký deployment:

```bash
curl http://192.168.1.50:11434/v1/models
```
