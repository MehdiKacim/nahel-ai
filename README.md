# Ollamock

> Ollamock is an Ollama-compatible runtime gateway and local AI launcher.

## Architecture

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Shells IA     │────▶│   Ollamock       │────▶│   Runtimes      │
│  (Codex/Claude) │     │  Gateway + Tray  │     │ (OpenVINO/etc)  │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌──────────────────┐
                        │   Ollamock.App   │
                        │  (MAUI Tray)     │
                        └──────────────────┘
```

## Projects

| Project | Role |
|---------|------|
| `Ollamock.Service` | Windows Service - API gateway, backend launcher, observability |
| `Ollamock.App` | MAUI Desktop App - Tray icon, dashboard, launchers |

## Quick Start

```powershell
# Build
.\scripts\build.ps1

# Install
.\scripts\install.ps1 -InstallPath "C:\Tools\ollamock"
```

## Supported Tools (Launchers)

| Tool | Type | API Format | Auto-Install |
|------|------|-----------|-------------|
| Claude Code | CLI | Anthropic | ✅ npm |
| Codex CLI | CLI | OpenAI | ✅ npm |
| OpenCode | CLI | Generic | ✅ curl |
| Droid | CLI | Generic | ✅ npm |
| Cline | VS Code ext | OpenAI | ❌ Manual |
| OpenWebUI | Web | OpenAI | ✅ pip/docker |
| AnythingLLM | Desktop | OpenAI | ❌ Download |

## Tray Menu

- 🟢 Bridge Running / 🟡 Backend Warming / 🔴 Backend Down
- Open Ollamock
- Launch Codex / Claude
- Start/Stop Bridge
- Restart Backends
- Open Logs
- Settings
- Quit Ollamock

## Dashboard

- **Home**: Bridge status, active model, backends health, RAM
- **Launchers**: Detect, install, configure, launch tools
- **Models**: Start/stop, device (CPU/GPU/NPU), RAM usage
- **Backends**: Port, type, runtime version, restart count
- **Logs**: Live stream
- **Settings**: Config JSON editor

## API Compatibility

### OpenAI
- `GET /v1/models`
- `POST /v1/chat/completions`
- `POST /v1/embeddings`

### Anthropic
- `POST /v1/messages`

### Ollama
- `GET /api/tags`
- `POST /api/generate`
- `POST /api/chat`
- `POST /api/embed`
- `GET /api/version`

## Config

```json
{
  "Providers": {
    "llm-openvino": {
      "Executable": "C:\Tools\llama-openvino\llama-server.exe",
      "Port": 8080
    }
  },
  "Models": {
    "ov-llama3.1": {
      "ProviderId": "llm-openvino",
      "ModelPath": "C:\Models\llama-3.1-8b.gguf",
      "Device": "NPU",
      "FallbackDevice": "GPU"
    }
  }
}
```

## License

MIT
