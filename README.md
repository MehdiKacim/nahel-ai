# OllamaBridge

Bridge Windows Service qui remplace Ollama sur :11434.
Ollama natif deplace sur :11436.

## Architecture

```
Client -> :11434 Bridge -> OpenVINO backends (:808x) ou Ollama natif (:11436)
              |
              +-> /admin Cockpit web
```

## Quick Start

```powershell
# 1. Deplacer Ollama natif
[Environment]::SetEnvironmentVariable("OLLAMA_HOST", "http://localhost:11436", "User")
# Redemarrer Ollama

# 2. Build
dotnet publish -c Release -r win-x64 --self-contained -o .\publish

# 3. Install
.\install-service.ps1 -InstallPath "C:\Tools\ollama-bridge"

# 4. Configurer
# Editer C:\Tools\ollama-bridgeppsettings.json
# - Chemins executables OpenVINO
# - Chemins modeles GGUF
# - Ports si deja occupes

# 5. Redemarrer service si config changee
Restart-Service OllamaOpenVINOBridge
```

## Usage

```bash
# LLM
curl http://localhost:11434/api/generate -d '{"model":"ov-llama3.1","prompt":"Hello"}'

# Vision
curl http://localhost:11434/api/generate -d '{"model":"ov-llava","prompt":"Decris","images":["base64..."]}'

# Embedding
curl http://localhost:11434/api/embed -d '{"model":"ov-embed","input":"texte"}'

# OpenAI-compatible
curl http://localhost:11434/v1/chat/completions -d '{"model":"ov-llama3.1","messages":[{"role":"user","content":"Hello"}]}'

# Fallback Ollama natif
curl http://localhost:11434/api/generate -d '{"model":"llama3.2","prompt":"Hello"}'

# Cockpit
start http://localhost:11434/admin
```

## Config

| Section | Role |
|---------|------|
| Bridge | Port ecoute, fallback, retention |
| Backends | Executables, args, ports, env vars, runtime version |
| Models | Mapping nom -> backend + chemin + device policy |

Variables args: `{modelPath}`, `{mmprojPath}`, `{port}`, `{contextSize}`, `{device}`, `{fallbackDevice}`

## Logs

- Ring buffer memoire (cockpit live)
- Fichier rotation: `logs/bridge-YYYYMMDD.log`

## Metrics

- CSV append: `metrics/metrics.csv`
- Header: `timestamp,req_id,model,backend,device,ttft_ms,tok_s,ram_mb,duration_ms`

## Endpoints Admin

```
GET  /admin/status
GET  /admin/models
POST /admin/models/{id}/test
POST /admin/models/{id}/toggle
GET  /admin/backends
POST /admin/backends/{id}/start|stop|restart
GET  /admin/logs
GET  /admin/logs/file
GET  /admin/metrics
GET  /admin/metrics/summary
GET  /admin/config
POST /admin/config
POST /admin/config/validate
POST /admin/config/reload
```

## Desinstall

```powershell
Stop-Service OllamaOpenVINOBridge
sc.exe delete OllamaOpenVINOBridge
```
