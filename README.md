# Alexa-like Wake Word MVP (openWakeWord)

Listens for the wake word "alexa", then records the command that follows and saves it as a `.wav` file. Uses openWakeWord with the built-in `alexa` model.

## License note
- openWakeWord code: Apache-2.0
- Built-in pre-trained models (including `alexa`): CC BY-NC-SA 4.0 (non-commercial)

## Requirements
- Python 3.9+
- Linux / Raspberry Pi OS
- Microphone input device

## System dependencies (Linux/Raspberry Pi)
```bash
sudo apt-get update
sudo apt-get install -y portaudio19-dev
```

## Install
```bash
python -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
```

# Download Vosk model

Download a Vosk English model (the small one is ~40 MB, good for Pi):
      
wget https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip
unzip vosk-model-small-en-us-0.15.zip -d vosk-model

Or use the full vosk-model-en-us-0.22 (~1.8 GB) for better accuracy.

## Run
```bash
python src/main.py
```

Say "alexa" followed by your command. Recording starts automatically after the wake word is detected and stops when silence is detected. The command audio is saved to `./recordings/command_<timestamp>.wav`.

## List audio devices
```bash
python src/main.py --list-devices
```

## Select a device and adjust threshold
```bash
python src/main.py --device 2 --threshold 0.6
```

## All options
```
--device INT              Input device index (default: system default)
--threshold FLOAT         Wake word detection threshold (default: 0.6)
--silence-threshold FLOAT RMS energy below which audio is silence (default: 200)
--silence-duration FLOAT  Seconds of consecutive silence to stop recording (default: 1.0)
--max-record-duration FLOAT  Safety cap on recording length in seconds (default: 10.0)
--output-dir PATH         Directory to save command .wav files (default: ./recordings)
```

## Notes
- Audio stream: 16 kHz mono int16
- Frame size: 20 ms (320 samples)
- Default wake word threshold: 0.6
- Cooldown between detections: 1.5 seconds
- Recording stops on 1 second of silence (tunable via `--silence-duration`)
- If `--silence-threshold` is too sensitive for your mic, increase it; if recording never stops, raise it further
