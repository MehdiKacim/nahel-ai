"""
Nahel OVGenAI Bridge — Minimal FastAPI server wrapping openvino_genai.LLMPipeline.
Usage:
    python ovgenai_bridge.py --model_path <path> --model_name <name> --device CPU --port 8100
"""
import argparse
import asyncio
import json
import sys
import threading
from pathlib import Path
from typing import List, Optional, Union

from fastapi import FastAPI, Request
from fastapi.responses import StreamingResponse, JSONResponse
from fastapi.middleware.cors import CORSMiddleware
import uvicorn

try:
    import openvino_genai
except ImportError:
    openvino_genai = None  # type: ignore

app = FastAPI(title="Nahel OVGenAI Bridge")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Globals populated at startup
_pipeline = None
_tokenizer = None
_model_name = None


def _load_openvino():
    import openvino_genai
    from transformers import AutoTokenizer
    return openvino_genai, AutoTokenizer


class _ChunkStreamer(openvino_genai.StreamerBase):
    """Streams decoded text in token chunks using an asyncio.Queue.
    Emits delta text (only newly decoded portion) to avoid repeated content.
    """
    def __init__(self, decoder_tokenizer, queue: asyncio.Queue):
        super().__init__()
        self._decoder_tokenizer = decoder_tokenizer
        self._queue = queue
        self._tokens_cache: List[int] = []
        self._last_print_len = 0
        self._cancelled = False
        self._ov_status = openvino_genai.StreamingStatus

    def write(self, token: Union[int, List[int]]):
        if self._cancelled:
            return self._ov_status.CANCEL
        if isinstance(token, list):
            self._tokens_cache.extend(token)
        else:
            self._tokens_cache.append(token)
        text = self._decoder_tokenizer.decode(self._tokens_cache)
        if len(text) > self._last_print_len:
            delta = text[self._last_print_len:]
            self._queue.put_nowait({"type": "token", "text": delta})
            self._last_print_len = len(text)
        return self._ov_status.RUNNING

    def end(self):
        # flush remaining
        text = self._decoder_tokenizer.decode(self._tokens_cache)
        if len(text) > self._last_print_len:
            delta = text[self._last_print_len:]
            self._queue.put_nowait({"type": "token", "text": delta})
        self._queue.put_nowait({"type": "done"})

    def cancel(self):
        self._cancelled = True


def _generate_sync(prompt, generation_config, queue):
    """Run generation in a background thread."""
    try:
        ov_genai, _ = _load_openvino()
        decoder_tokenizer = _pipeline.get_tokenizer()
        streamer = _ChunkStreamer(decoder_tokenizer, queue)
        result = _pipeline.generate(prompt, generation_config, streamer)
        if result is not None and hasattr(result, "tokens"):
            final_text = decoder_tokenizer.decode(result.tokens)
            queue.put_nowait({"type": "final", "text": final_text})
    except Exception as exc:
        queue.put_nowait({"type": "error", "message": str(exc)})


@app.get("/v1/models")
async def list_models():
    return {
        "object": "list",
        "data": [
            {
                "id": _model_name,
                "object": "model",
                "created": 0,
                "owned_by": "nahel-ovgenai",
            }
        ],
    }


@app.post("/v1/chat/completions")
async def chat_completions(request: Request):
    ov_genai, AutoTokenizer = _load_openvino()

    body = await request.json()
    messages = body.get("messages", [])
    stream = body.get("stream", False)
    max_tokens = body.get("max_tokens") or body.get("max_new_tokens") or 128
    temperature = body.get("temperature", 1.0)
    top_p = body.get("top_p", 1.0)
    top_k = body.get("top_k", 50)
    repetition_penalty = body.get("repetition_penalty", 1.0)

    # Apply chat template
    prompt = _tokenizer.apply_chat_template(
        messages,
        add_generation_prompt=True,
        tokenize=False,
    )

    gen_config = ov_genai.GenerationConfig()
    gen_config.max_new_tokens = max_tokens
    gen_config.temperature = temperature
    gen_config.top_p = top_p
    gen_config.top_k = top_k
    gen_config.repetition_penalty = repetition_penalty

    created_ts = int(asyncio.get_event_loop().time())
    request_id = f"nahel-{threading.current_thread().ident:x}-{created_ts}"

    if stream:
        queue = asyncio.Queue()

        def _thread_worker():
            _generate_sync(prompt, gen_config, queue)

        t = threading.Thread(target=_thread_worker)
        t.start()

        async def event_stream():
            try:
                while True:
                    item = await queue.get()
                    if item["type"] == "token":
                        payload = {
                            "id": request_id,
                            "object": "chat.completion.chunk",
                            "created": created_ts,
                            "model": _model_name,
                            "choices": [
                                {
                                    "index": 0,
                                    "delta": {"content": item["text"]},
                                    "finish_reason": None,
                                }
                            ],
                        }
                        yield f"data: {json.dumps(payload)}\n\n"
                    elif item["type"] == "done":
                        payload = {
                            "id": request_id,
                            "object": "chat.completion.chunk",
                            "created": created_ts,
                            "model": _model_name,
                            "choices": [
                                {
                                    "index": 0,
                                    "delta": {},
                                    "finish_reason": "stop",
                                }
                            ],
                        }
                        yield f"data: {json.dumps(payload)}\n\n"
                        yield "data: [DONE]\n\n"
                        break
                    elif item["type"] == "error":
                        yield f"data: {json.dumps({'error': item['message']})}\n\n"
                        break
            finally:
                if t.is_alive():
                    t.join(timeout=1.0)

        return StreamingResponse(event_stream(), media_type="text/event-stream")

    # Non-streaming
    result = _pipeline.generate(prompt, gen_config)
    decoder_tokenizer = _pipeline.get_tokenizer()
    text = decoder_tokenizer.decode(result.tokens) if hasattr(result, "tokens") else ""
    prompt_tokens = len(_tokenizer.encode(prompt))
    completion_tokens = len(result.tokens) if hasattr(result, "tokens") else 0

    return {
        "id": request_id,
        "object": "chat.completion",
        "created": created_ts,
        "model": _model_name,
        "choices": [
            {
                "index": 0,
                "message": {"role": "assistant", "content": text},
                "finish_reason": "stop",
            }
        ],
        "usage": {
            "prompt_tokens": prompt_tokens,
            "completion_tokens": completion_tokens,
            "total_tokens": prompt_tokens + completion_tokens,
        },
    }


@app.get("/health")
async def health():
    return {"status": "ok", "model": _model_name}


def main():
    parser = argparse.ArgumentParser(description="Nahel OVGenAI Bridge")
    parser.add_argument("--engine", default="ov_genai", choices=["ov_genai", "optimum", "openvino"])
    parser.add_argument("--model_path", required=True)
    parser.add_argument("--model_name", required=True)
    parser.add_argument("--device", default="CPU")
    parser.add_argument("--port", type=int, default=8100)
    args = parser.parse_args()

    global _pipeline, _tokenizer, _model_name
    _model_name = args.model_name

    # Resolve model_path relative to bridge location if needed
    model_path = Path(args.model_path)
    if not model_path.is_absolute():
        bridge_dir = Path(__file__).parent.resolve()
        candidate = bridge_dir / model_path
        if candidate.exists():
            model_path = candidate
        else:
            model_path = model_path.resolve()

    print(f"[OVGenAI Bridge] Engine={args.engine}, model={model_path}, device={args.device}")

    if args.engine == "ov_genai":
        ov_genai, AutoTokenizer = _load_openvino()
        _pipeline = ov_genai.LLMPipeline(str(model_path), args.device)
        _tokenizer = AutoTokenizer.from_pretrained(str(model_path))
    elif args.engine == "optimum":
        # TODO: Load optimum-intel pipeline
        raise NotImplementedError("optimum engine not yet implemented in bridge.")
    elif args.engine == "openvino":
        # TODO: Load raw openvino inference
        raise NotImplementedError("openvino engine not yet implemented in bridge.")
    else:
        raise ValueError(f"Unknown engine: {args.engine}")

    print(f"[OVGenAI Bridge] Model '{args.model_name}' ready on port {args.port}.")
    uvicorn.run(app, host="127.0.0.1", port=args.port, log_level="warning")


if __name__ == "__main__":
    main()
