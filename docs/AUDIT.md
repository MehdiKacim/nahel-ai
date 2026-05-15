# Ollamock / OllamaBridge technical audit

Date: 2026-05-15

## Verdict

The project already has the right architecture for the idea:

```txt
Client shells
  Codex App / Claude Code / OpenWebUI / AnythingLLM
        ↓
localhost:11434
        ↓
OllamaBridge / Ollamock
        ↓
managed backends or native Ollama fallback
```

This is not just an OpenVINO wrapper. It is an Ollama-compatible runtime gateway.

## What is already good

### 1. Correct port strategy

The README already defines the right production posture:

```txt
Bridge      :11434
Ollama real :11436
Backends    :808x
```

This is the correct lazy-dev setup because Ollama/Codex/Claude integrations expect `localhost:11434`.

### 2. Clean DI boundaries

`Program.cs` already wires:

- `IBackendLauncher`
- `IRouter`
- `IRequestAdapter`
- `LogSink`
- `MetricsSink`
- `BridgeLifecycleService`
- `ProcessWatchdog`

This means the code is not a random proxy. The provider boundary already exists.

### 3. Backend process lifecycle exists

`BackendLauncher` already handles:

- process spawn
- environment variables
- argument templating
- health checks
- restart counts
- RAM sampling
- process disposal

This is the right place to keep runtime-specific ugliness.

### 4. Observability exists early

The bridge already tracks:

- request id
- TTFT
- token/s
- RAM MB
- duration
- backend id
- model id

This is the important part. The bridge is useful because it sees both sides.

## Current gaps

### 1. Codex App needs `/v1/models`

Codex-style clients generally expect OpenAI-compatible discovery:

```txt
GET /v1/models
```

This should list managed bridge models and optionally append native Ollama fallback models.

### 2. Codex App launcher flow also benefits from `/api/show`

Ollama's launcher uses model metadata to create a Codex App model catalog.

The bridge should expose:

```txt
POST /api/show
```

For managed models, return at least:

- model info
- context length
- capabilities
- vision capability when `MmprojPath` exists or backend adapter is `vision`

### 3. `/v1/chat/completions` should map alias model to backend model path

Current managed `/v1/chat/completions` forwards the original body to the backend.

For managed bridge aliases, safer behavior is:

```txt
model: ov-qwen-coder
↓
model: C:\Models\qwen-coder.gguf
```

or whatever the backend expects.

### 4. `/v1/embeddings` is needed

`/api/embed` exists, but OpenAI-compatible clients may use:

```txt
POST /v1/embeddings
```

Add it and route only to embedding backends.

### 5. Streaming is the core risk

The bridge currently converts SSE to Ollama NDJSON for `/api/generate`.

Critical cases to test:

- client cancellation
- backend crash during stream
- first token timing
- empty chunks
- final `[DONE]`
- non-streaming OpenAI requests

### 6. Native fallback proxy currently works for POST-style paths

For GET paths like `/v1/models`, `/api/tags`, and maybe future `/api/ps`, use explicit GET proxy helpers, not only POST forwarding.

## Ollama launch findings

Ollama's `cmd/launch` is not tied to inference. It is an integration launcher.

For Codex App, it does roughly:

```txt
1. detect Codex.exe
2. find Codex config path
3. backup current config
4. write Ollama-managed profile
5. write ollama-launch-models.json
6. launch or restart Codex
7. provide restore flow
```

This means Ollamock can reproduce the useful part without embedding Ollama runtime.

## Product direction

The project should evolve from:

```txt
OllamaBridge = OpenVINO proxy
```

to:

```txt
Ollamock = local AI shell launcher + Ollama-compatible runtime gateway
```

Meaning:

```txt
[Start Codex Local]
  ↓
start bridge
start selected backend
write Codex profile/catalog
launch Codex.exe
monitor logs/perf
```

## Recommended next PRs

### PR 1: Codex compatibility

- add `GET /v1/models`
- add `POST /v1/embeddings`
- add `POST /api/show`
- improve `/v1/chat/completions` alias-to-real-model mapping

### PR 2: Codex App launcher module

Create:

```txt
Launchers/CodexAppLauncher.cs
Launchers/CodexConfigWriter.cs
Launchers/CodexModelCatalogWriter.cs
```

Responsibilities:

- detect Codex.exe on Windows
- locate Codex config
- backup config
- write Ollamock profile
- write model catalog from bridge models
- start/restart Codex
- restore previous config

### PR 3: Admin UI launcher tab

Add buttons:

```txt
Start Bridge
Start Chat Backend
Start Embedding Backend
Configure Codex
Launch Codex
Restore Codex
```

### PR 4: Stream hardening

- propagate cancellation
- record backend HTTP failures
- handle non-SSE JSON response
- test final chunk behavior

## Naming

`Ollamock` is a strong internal/product name because it communicates the compatibility trick:

```txt
clients think they talk to Ollama
but the engine is mocked/replaced behind the contract
```

For serious README wording:

```txt
Ollamock: an Ollama-compatible runtime gateway and local agent launcher.
```
