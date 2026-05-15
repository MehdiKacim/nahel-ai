# PR1 — Codex compatibility

Goal:

Make this work:

```txt
Codex App
↓
localhost:11434/v1
↓
Ollamock
↓
llama.cpp / OpenVINO
```

## Endpoints to implement or harden

### Required
- [ ] GET `/v1/models`
- [ ] POST `/v1/chat/completions`
- [ ] POST `/v1/embeddings`

### Recommended
- [ ] POST `/api/show`
- [ ] GET `/api/tags`
- [ ] GET `/api/version`

## Acceptance tests

```bash
curl http://localhost:11434/v1/models
curl -X POST http://localhost:11434/v1/chat/completions
```

Then:

```bash
ollama launch codex-app
```

Expected:

- Codex sees bridge models
- streaming works
- backend switch invisible
- native Ollama fallback survives

## Principle

Do NOT fork Ollama.

Steal:
- launcher behavior
- profile writing
- model catalog generation

Keep:
- bridge ownership
- runtime ownership
