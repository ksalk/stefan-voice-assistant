"""
Application state shared between all async tasks.

With a single asyncio event loop all coroutines run on the same OS thread,
so plain attribute reads/writes are safe without locking.

The sounddevice audio callback runs on a separate OS thread, but it only
writes to the buffer deque (which is thread-safe in CPython via the GIL),
not to any AppState field.  state.speaking is only read by the callback
indirectly (echo suppression in the listener), which is also safe.
"""

from dataclasses import dataclass, field


@dataclass
class AppState:
    listening: bool = False  # True when wake-word loop is idle and waiting
    recording: bool = False  # True while capturing a command after wake word
    speaking: bool = False  # True while TTS audio is playing back

    def as_dict(self) -> dict:
        return {
            "listening": self.listening,
            "recording": self.recording,
            "speaking": self.speaking,
        }

    @property
    def status_label(self) -> str:
        if self.recording:
            return "recording"
        if self.listening:
            return "listening"
        return "initializing"
