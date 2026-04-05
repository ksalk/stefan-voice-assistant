#!/usr/bin/env python3
import argparse
import sys

from remote_server import dispatch_audio_command, register_node
from config import audioConfig, localServerConfig, nodeConfig, remoteServerConfig
from audio import list_devices, load_wav
from command_listener import start_command_listener
from http_server import start_http_server_thread
# from ws_client import DEFAULT_SERVER_WS_URL, start_ws_thread

def parse_args():
    parser = argparse.ArgumentParser(description="Wake word detection node (openWakeWord)")

    # Node configuration
    parser.add_argument('--node-name', type=str, help='Unique name for this node (default: %(default)s)')

    # Wake word / recording
    parser.add_argument('--audio-device', type=int, default=None, help='Input device index')
    parser.add_argument('--audio-max-recording-duration', type=float, help='Maximum recording duration in seconds (default: %(default)s)')
    parser.add_argument('--output-dir', type=str, help='Directory for saved recordings (default: %(default)s)')

    # Remote server
    parser.add_argument('--remote-server-url', type=str, help='URL to POST command audio to (default: %(default)s)')
    parser.add_argument('--node-secret', type=str, help='Shared secret for authenticating with the server')
    # parser.add_argument('--server-ws-url', type=str, default=DEFAULT_SERVER_WS_URL,
    #                     help='WebSocket URL for server connection (default: %(default)s)')
    # parser.add_argument('--ssl-verify', action='store_true',
    #                     help='Enable TLS certificate verification (use with a trusted/production cert)')

    # Local HTTP server
    parser.add_argument('--local-server-host', type=str, help='HTTP server bind host (default: %(default)s)')
    parser.add_argument('--local-server-port', type=int, help='HTTP server port (default: %(default)s)')
    
    # Commands
    parser.add_argument('--list-devices', action='store_true', help='List audio devices and exit')
    
    # Testing
    parser.add_argument('--test-command', type=str, default=None, metavar='WAV_FILE',
                        help='Send a WAV file directly to the server and exit (skips microphone)')

    return parser.parse_args()

def patch_configs(args):
    # Patch config values with command-line args (if provided)
    if args.node_name is not None:
        nodeConfig.NAME = args.node_name
    if args.audio_max_recording_duration is not None:
        audioConfig.MAX_RECORDING_DURATION = args.audio_max_recording_duration
    if args.output_dir is not None:
        audioConfig.RECORDINGS_OUTPUT_DIR = args.output_dir
    if args.remote_server_url is not None:
        remoteServerConfig.URL = args.remote_server_url
    if args.node_secret is not None:
        remoteServerConfig.AUTH_SECRET = args.node_secret
    if args.local_server_host is not None:
        localServerConfig.HOST = args.local_server_host
    if args.local_server_port is not None:
        localServerConfig.PORT = args.local_server_port


def main():
    args = parse_args()
    patch_configs(args)

    if args.list_devices:
        list_devices()
        return

    if not register_node():
        print("[fatal] Node registration failed. Exiting.")
        sys.exit(1)

    if args.test_command:
        print(f"[test] Loading {args.test_command}")
        audio = load_wav(args.test_command)
        dispatch_audio_command(command_audio=audio)
        return

    ## --------------------------------

    start_http_server_thread()

    # start_ws_thread(args.server_ws_url, args.node_secret, DEVICE_ID, ssl_no_verify=not args.ssl_verify)

    # Blocking — runs the mic loop forever
    start_command_listener()


if __name__ == "__main__":
    main()
