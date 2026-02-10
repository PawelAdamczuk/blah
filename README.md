# Blah

Fully local Windows Whisper-based dictation.

## Usage

```
Blah.exe [OPTIONS]
```

## Options

- `--model <name>` - Select Whisper model to use (default: medium)
- `--delay <ms>` - Typing delay in milliseconds (default: 1)
- `--help, -h` - Show help message

## Available Models

| Model  | Size   | Description                          |
|--------|--------|--------------------------------------|
| tiny   | 75 MB  | Fastest, lowest accuracy             |
| base   | 142 MB | Fast, good for simple dictation      |
| small  | 466 MB | Balanced speed and accuracy          |
| medium | 1.5 GB | Slower, very accurate (default)      |
| large  | 2.9 GB | Slowest, best accuracy               |

## Examples

```bash
# Use default settings (medium model)
Blah.exe

# Use a smaller/faster model
Blah.exe --model small

# Adjust typing speed
Blah.exe --delay 0
Blah.exe --delay 5

# Combine options
Blah.exe --model small --delay 0
```

## Hotkeys

- **Win+Enter** - Start/stop recording
- **Win+Esc** - Cancel and return to idle

## Visual Indicator

A small triangle appears in the bottom-right corner showing the app state:

- **Blue (small)** - Idle, ready to record
- **Red (small)** - Waiting for audio input
- **Red (large)** - Recording audio
- **Yellow** - Transcribing speech to text
- **Green** - Typing transcribed text

## How It Works

1. Press **Win+Enter** to start recording
2. Speak into your microphone (indicator turns red when audio detected)
3. Press **Win+Enter** again to stop and transcribe
4. Text is automatically typed at your cursor position
5. Press **Win+Esc** anytime to cancel and return to idle

## Custom Prompts

You can optionally create a `prompt.txt` or `prompt.md` file next to the executable to provide context for better transcription accuracy. The prompt helps Whisper understand your domain-specific vocabulary or expected output format.

Maximum prompt length: 224 characters.

## Requirements

- Windows 11 or Windows Server 2022 (or newer)
- Microsoft Visual C++ Redistributable for Visual Studio 2022 (x64)
- Microphone
- Disk space for selected model (75 MB to 2.9 GB)

## Typing delay tips
From my testing, the terminal accepts even 0ms delay. But some apps, notably Notepad.exe, struggle with it. I've been running the default 1ms and it seems to work fine everywhere, even if it adds some waiting time.

## Inference speed
I've been using the medium model on a laptop with an Intel ARC integrated graphics card and it seems quick enough.

## Languages
It auto-guesses the language you're using. I only tested it with Polish and English. It struggles with short phrases in Polish - if you want it to successfully guess that you're speaking Polish, a good tactic is to use a lot of Polish words in the first sentence. After that, wven if you mix in English words, it seems to handle it fine.

## Building from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
# Build
dotnet build -c Release

# Create single executable
dotnet publish -c Release
```

The output will be in `bin/Release/net10.0/win-x64/publish/Blah.exe` (~52 MB, includes .NET runtime).

## Credits

Made by Paweł Adamczuk | [paweladamczuk.com](https://paweladamczuk.com)

Built with:
- [Whisper.net](https://github.com/sandrohanea/whisper.net) - .NET bindings for whisper.cpp
- [NAudio](https://github.com/naudio/NAudio) - Audio capture
- [OpenAI Whisper](https://github.com/openai/whisper) - Speech recognition model
