# ---------------------------------------------------------------------------
# Shared node state
#
# Written by the listener loop (main thread) and read by HTTP handlers
# (aiohttp daemon thread). Simple dict reads/writes are effectively atomic
# under CPython's GIL for this use case, so no explicit locking is needed.
#
# If this code is ever run on a GIL-free interpreter (e.g. PyPy or
# free-threaded CPython 3.13+), wrap all mutations in a threading.Lock.
# ---------------------------------------------------------------------------

node_state: dict = {
    "listening": False,   # True when idle wake-word loop is active
    "recording": False,   # True while capturing a command after wake word
}
