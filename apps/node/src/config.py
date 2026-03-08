from pathlib import Path
from pydantic_settings import BaseSettings, SettingsConfigDict

_ENV_FILE = Path(__file__).parent.parent / ".env"

class TTSConfig(BaseSettings):
    """
    Configuration settings for text-to-speech synthesis.
    """

    # Full (relative or absolute) path to the .onnx model file.
    # Example: models/en_US-hfc_female-medium.onnx
    PIPER_MODEL: str = "models/en_US-hfc_female-medium.onnx"

    model_config = SettingsConfigDict(env_file=_ENV_FILE, env_prefix="TTS_", extra="ignore")

class AudioInputConfig(BaseSettings):
    """
    Configuration settings for audio input processing.
    """

    # General audio input settings
    INPUT_DEVICE: int | None = None  # None means default device

    # Wake word detection settings
    WAKEWORD_THRESHOLD: float = 0.6
    WAKEWORD_COOLDOWN: float = 1.5

    # Command recording settings
    SKIP_MS_AFTER_WAKEWORD: float = 200.0
    END_SILENCE_THRESHOLD: float = 200.0
    END_SILENCE_DURATION: float = 1.0
    MAX_RECORDING_DURATION: float = 10.0
    RECORDINGS_OUTPUT_DIR: str = "./recordings"

    model_config = SettingsConfigDict(env_file=_ENV_FILE, env_prefix="AUDIO_", extra="ignore")

class RemoteServerConfig(BaseSettings):
    """
    Configuration settings for the remote server connection.
    """

    URL: str = "http://localhost:5285/command"
    WS_URL: str = "wss://localhost:7036/ws"
    AUTH_SECRET: str = ""

    model_config = SettingsConfigDict(env_file=_ENV_FILE, env_prefix="REMOTE_SERVER_", extra="ignore")

class LocalServerConfig(BaseSettings):
    """
    Configuration settings for the local HTTP server.
    """

    HOST: str = "0.0.0.0"
    PORT: int = 8000

    model_config = SettingsConfigDict(env_file=_ENV_FILE, env_prefix="LOCAL_SERVER_", extra="ignore")


ttsConfig = TTSConfig()
audioConfig = AudioInputConfig()
remoteServerConfig = RemoteServerConfig()
localServerConfig = LocalServerConfig()