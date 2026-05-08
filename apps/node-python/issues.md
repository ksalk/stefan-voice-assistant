# Code Issues

## Critical

### 1. `play_audio` blocks the aiohttp event loop (`http_server.py:51`)

`play_audio` calls `sd.wait()` which blocks the thread until playback finishes. Because aiohttp runs on an asyncio event loop, this means no other HTTP requests can be served while audio is playing (potentially 5–10 seconds).

**Fix:** Run `play_audio` in an executor:
```python
await asyncio.get_event_loop().run_in_executor(None, play_audio, audio_data, node_state)
```

---

### 2. `ws_client.py` `__main__` block references undefined names

```python
parser.add_argument("--server-ws-url", default=DEFAULT_SERVER_WS_URL)  # NameError: not defined
parser.add_argument("--node-secret", default=config.NODE_SECRET, ...)  # NameError: config not imported
asyncio.run(run(args.server_ws_url, args.node_secret, ssl_no_verify=...))  # missing positional arg: device_id
```

The file crashes immediately if run directly.

---

### 3. Tests call `/health` but expect JSON — all state tests fail (`test_server.py:26`)

`/health` returns plain text `"I'm alive"`, not JSON. The tests that call `/health` and then do `resp.json()` and assert on `data["state"]` will always fail with a parse error. State lives on `/status`.

```python
# broken
resp = await client.get("/health")
data = await resp.json()          # parse error — plain text response
assert data["state"] == "listening"
```

---

## High

### 4. No timeout on `requests.post` calls (`remote_server.py:55, 76`)

Neither the registration nor the command dispatch call has a `timeout=` parameter. If the server is unreachable or hangs, both calls block indefinitely, freezing the main thread — no wakeword detection, no recovery possible.

**Fix:**
```python
response = requests.post(..., timeout=10)
```

---

### 5. Unhandled exception from `play_audio` kills the main loop (`remote_server.py:64`)

If `play_audio` raises (malformed WAV, audio device error, etc.), the exception propagates out of `dispatch_audio_command` with no catch, crashing the main wakeword loop permanently. `node_state["speaking"]` is correctly reset via `finally` inside `play_audio`, but the caller has no recovery.

**Fix:** Wrap the `play_audio` call in a try/except in `dispatch_audio_command`.

---

### 6. Audio buffer grows unbounded during server round-trip (`command_listener.py`)

While `dispatch_audio_command` is running (POST + TTS playback — potentially 5–15 seconds), the microphone stream stays open and the audio callback keeps appending to `buffer`. There is no size cap on the deque. After the call returns, `buffer.clear()` discards everything, but in the meantime memory usage grows without bound proportional to round-trip latency.

**Fix:** Cap the deque with `maxlen`:
```python
buffer: deque = deque(maxlen=MAX_BUFFER_FRAMES)
```

---

## Medium

### 7. `buffer_samples` can go negative (`command_listener.py`)

`buffer_samples` is decremented in both the wakeword loop and inside `record_command`. If the audio callback fires concurrently and the returned value from `record_command` is slightly out of sync, `buffer_samples` can go negative. The guard `if buffer_samples < target_input_frame_samples` then keeps re-entering `continue` on every iteration until enough callbacks fire to bring it back above the threshold — it will recover, but it's an unnecessary source of latency and confusion.

---

### 8. No echo suppression — mic is active during speaker playback (`command_listener.py`)

While TTS audio is playing back, the microphone stream remains open. The node may detect its own speaker output as a wakeword (echo triggering) or accumulate the playback audio in the buffer. There is no muting or echo gate during `node_state["speaking"] == True`.

---

## Low

### 9. Skip overshoot can clip the first word of a command (`audio.py`)

In `record_command`, the skip loop pops whole chunks until `skipped >= skip_samples`. If `INPUT_FRAME_SAMPLES` doesn't evenly divide `skip_samples`, the last popped chunk overshoots by up to one full frame. This means slightly more audio than intended is discarded, occasionally clipping the leading edge of the first spoken word.

---

### 10. Module-level constants in `audio.py` are frozen before `patch_configs` runs (`audio.py:19`, `main.py`)

```python
# audio.py — evaluated at import time
INPUT_SAMPLE_RATE = audioConfig.INPUT_SAMPLE_RATE
```

`main.py` imports from `audio` before calling `patch_configs`, so any CLI overrides to `audioConfig` fields are not reflected in these module-level constants. In practice `patch_configs` doesn't currently touch `INPUT_SAMPLE_RATE`, but the pattern is fragile and will silently break if a CLI override is added later.
