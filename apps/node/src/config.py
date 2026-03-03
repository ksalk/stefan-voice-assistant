"""
Central configuration — all tuneable knobs, loaded from environment / .env.

Load order:
  1. .env file in the project root (if present; never overrides real env vars)
  2. Environment variables
  3. Hardcoded fallback defaults (defined in this file)
"""
import os
from pathlib import Path

from dotenv import load_dotenv

# Load .env from the project root (one level above src/)
load_dotenv(Path(__file__).parent.parent / ".env")


def _float(name: str, default: float) -> float:
    return float(os.environ.get(name, default))


def _int(name: str, default: int) -> int:
    return int(os.environ.get(name, default))


def _str(name: str, default: str) -> str:
    return os.environ.get(name, default)


# ---------------------------------------------------------------------------
# Remote server
# ---------------------------------------------------------------------------

SERVER_URL    = _str("SERVER_URL", "http://localhost:5285/command")
SERVER_WS_URL = _str("SERVER_WS_URL", "wss://localhost:7036/ws")
NODE_SECRET   = _str("NODE_SECRET", "")

# ---------------------------------------------------------------------------
# Local HTTP server
# ---------------------------------------------------------------------------

HTTP_HOST = _str("HTTP_HOST", "0.0.0.0")
HTTP_PORT = _int("HTTP_PORT", 8080)

# ---------------------------------------------------------------------------
# Wake word detection
# ---------------------------------------------------------------------------

WAKEWORD_THRESHOLD = _float("WAKEWORD_THRESHOLD", 0.6)
WAKEWORD_COOLDOWN  = _float("WAKEWORD_COOLDOWN", 1.5)
WAKEWORD_SKIP_MS   = _float("WAKEWORD_SKIP_MS", 200.0)

# ---------------------------------------------------------------------------
# Voice command recording
# ---------------------------------------------------------------------------

SILENCE_THRESHOLD   = _float("SILENCE_THRESHOLD", 200.0)
SILENCE_DURATION    = _float("SILENCE_DURATION", 1.0)
MAX_RECORD_DURATION = _float("MAX_RECORD_DURATION", 10.0)
OUTPUT_DIR          = _str("OUTPUT_DIR", "./recordings")

# ---------------------------------------------------------------------------
# TTS (piper)
# ---------------------------------------------------------------------------

_MODELS_DIR = os.path.join(os.path.dirname(__file__), "..", "models")
PIPER_MODEL = _str(
    "PIPER_MODEL",
    os.path.join(_MODELS_DIR, "en_US-hfc_female-medium.onnx"),
)
