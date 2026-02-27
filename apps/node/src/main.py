#!/usr/bin/env python3
import argparse

from audio import (
    DEFAULT_SILENCE_THRESHOLD,
    DEFAULT_SILENCE_DURATION,
    DEFAULT_MAX_RECORD_DURATION,
    DEFAULT_OUTPUT_DIR,
    list_devices,
    load_wav,
)
from listener import DEFAULT_THRESHOLD, _dispatch_command, run_listener
from server import DEFAULT_HTTP_HOST, DEFAULT_HTTP_PORT, start_http_thread

DEFAULT_SERVER_URL = "http://localhost:5285/command"


def parse_args():
    parser = argparse.ArgumentParser(description="Wake word detection node (openWakeWord)")

    parser.add_argument('--list-devices', action='store_true',
                        help='List audio devices and exit')
    parser.add_argument('--device', type=int, default=None,
                        help='Input device index')

    # Wake word / recording
    parser.add_argument('--threshold', type=float, default=DEFAULT_THRESHOLD,
                        help='Wake word detection threshold (default: %(default)s)')
    parser.add_argument('--silence-threshold', type=float, default=DEFAULT_SILENCE_THRESHOLD,
                        help='RMS energy below which audio is silence (default: %(default)s)')
    parser.add_argument('--silence-duration', type=float, default=DEFAULT_SILENCE_DURATION,
                        help='Seconds of consecutive silence to stop recording (default: %(default)s)')
    parser.add_argument('--max-record-duration', type=float, default=DEFAULT_MAX_RECORD_DURATION,
                        help='Maximum recording duration in seconds (default: %(default)s)')
    parser.add_argument('--output-dir', type=str, default=DEFAULT_OUTPUT_DIR,
                        help='Directory for saved recordings (default: %(default)s)')

    # Remote server
    parser.add_argument('--server-url', type=str, default=DEFAULT_SERVER_URL,
                        help='URL to POST command audio to (default: %(default)s)')

    # Testing
    parser.add_argument('--test-command', type=str, default=None, metavar='WAV_FILE',
                        help='Send a WAV file directly to the server and exit (skips microphone)')

    # HTTP server
    parser.add_argument('--http-host', type=str, default=DEFAULT_HTTP_HOST,
                        help='HTTP server bind host (default: %(default)s)')
    parser.add_argument('--http-port', type=int, default=DEFAULT_HTTP_PORT,
                        help='HTTP server port (default: %(default)s)')

    return parser.parse_args()


def main():
    args = parse_args()

    if args.list_devices:
        list_devices()
        return

    if args.test_command:
        print(f"[test] Loading {args.test_command}")
        audio = load_wav(args.test_command)
        _dispatch_command(audio, args.server_url)
        return

    # Start HTTP server before model load so /health returns "initializing"
    # while the wake word model is being loaded.
    start_http_thread(args.http_host, args.http_port)

    # Blocking — runs the mic loop forever
    run_listener(
        threshold=args.threshold,
        silence_threshold=args.silence_threshold,
        silence_duration=args.silence_duration,
        max_record_duration=args.max_record_duration,
        device=args.device,
        server_url=args.server_url,
    )


if __name__ == '__main__':
    main()
