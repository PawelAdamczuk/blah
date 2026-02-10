using Blah.Audio;
using Blah.Core;
using Blah.Input;
using Blah.Speech;
using Blah.UI;
using Blah.Win32;

namespace Blah;

class Program
{
    private static AppState _currentState = AppState.Idle;
    private static readonly object _stateLock = new object();
    private static CancellationTokenSource? _operationCts = null;
    private static int _typingDelayMs = 1; // Default: 1ms delay for compatibility

    enum AppState 
    { 
        Idle,
        Recording,
        Transcribing,
        Typing
    }

    static async Task Main(string[] args)
    {
        // Handle --help flag
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return;
        }

        Console.WriteLine("Blah starting...");
        
        // Parse --model argument (defaults to Medium, warns on invalid)
        var modelType = ParseModelArgument(args);
        
        // Parse --delay argument (defaults to 0ms)
        _typingDelayMs = ParseDelayArgument(args);
        
        try
        {
            // Initialize components
            var transcriber = new Transcriber();
            await transcriber.InitializeAsync(modelType);
            
            var audioRecorder = new AudioRecorder();
            var indicator = new IndicatorWindow();
            var keyboardHook = new KeyboardHook();
            
            // Show red indicator when audio is actually detected
            audioRecorder.AudioDetected += () =>
            {
                indicator.SetLarge(); // Grow to full size when audio detected
            };
            
            // Wire up stop (Win+Esc) handler
            keyboardHook.StopRequested += () => 
            {
                Task.Run(async () =>
                {
                    AppState stateAtRequest;
                    CancellationTokenSource? ctsToCancel;
                    
                    lock (_stateLock)
                    {
                        stateAtRequest = _currentState;
                        ctsToCancel = _operationCts;
                        
                        if (_currentState == AppState.Idle)
                        {
                            // Already idle, nothing to do
                            return;
                        }
                        
                        // Immediately transition to idle
                        _currentState = AppState.Idle;
                        _operationCts = null;
                    }
                    
                    // Cancel outside the lock to avoid deadlock
                    if (ctsToCancel != null)
                    {
                        Console.WriteLine("Canceled by user, going back to idle.");
                        ctsToCancel.Cancel();
                        ctsToCancel.Dispose();
                    }
                    
                    // Handle state-specific cleanup
                    if (stateAtRequest == AppState.Recording)
                    {
                        audioRecorder.Cancel(); // Discard audio
                    }
                    
                    // Always return to idle visual state
                    indicator.SetIdle();
                });
            };
            
            // Wire up event handlers
            keyboardHook.ToggleRecordingRequested += () => 
            {
                Task.Run(async () =>
                {
                    AppState currentState;
                    lock (_stateLock)
                    {
                        currentState = _currentState;
                    }
                    
                    if (currentState == AppState.Idle)
                    {
                        // START RECORDING
                        lock (_stateLock)
                        {
                            if (_currentState != AppState.Idle) return; // Double-check
                            
                            _currentState = AppState.Recording;
                            _operationCts?.Dispose(); // Clean up any old token
                            _operationCts = new CancellationTokenSource();
                        }
                        
                        // Ensure indicator is visible
                        indicator.EnsureVisible();
                        
                        // Start recording - show small red dot (waiting for mic)
                        indicator.SetColor(Constants.COLOR_RED);
                        indicator.SetSmall();
                        indicator.Show();
                        audioRecorder.Start();
                    }
                    else if (currentState == AppState.Recording)
                    {
                        // STOP RECORDING & START TRANSCRIPTION
                        CancellationToken token;
                        lock (_stateLock)
                        {
                            if (_currentState != AppState.Recording) return; // Double-check
                            
                            _currentState = AppState.Transcribing;
                            token = _operationCts?.Token ?? CancellationToken.None;
                        }
                        
                        // Immediately change to YELLOW when stopping recording
                        indicator.SetColor(Constants.COLOR_YELLOW);
                        
                        var audioData = audioRecorder.Stop();
                        
                        // Check cancellation before transcription
                        if (token.IsCancellationRequested)
                        {
                            lock (_stateLock) { _currentState = AppState.Idle; }
                            indicator.SetIdle();
                            return;
                        }
                        
                        if (audioData != null)
                        {
                            try
                            {
                                var transcription = await transcriber.TranscribeAsync(audioData, token);
                                
                                // Check cancellation after transcription
                                if (token.IsCancellationRequested)
                                {
                                    lock (_stateLock) { _currentState = AppState.Idle; }
                                    indicator.SetIdle();
                                    return;
                                }
                                
                                if (transcription != null)
                                {
                                    // START TYPING
                                    lock (_stateLock)
                                    {
                                        if (_currentState != AppState.Transcribing) return; // Could have been canceled
                                        _currentState = AppState.Typing;
                                    }
                                    
                                    // Typing - change to GREEN
                                    indicator.SetColor(Constants.COLOR_GREEN);
                                    
                                    await TextInput.TypeTextAsync(transcription, token, _typingDelayMs);
                                    
                                    // Check cancellation after typing
                                    if (token.IsCancellationRequested)
                                    {
                                        lock (_stateLock) { _currentState = AppState.Idle; }
                                        indicator.SetIdle();
                                        return;
                                    }
                                }
                            }
                            catch (TaskCanceledException)
                            {
                                // Cancellation already handled, just clean up
                                lock (_stateLock) { _currentState = AppState.Idle; }
                                indicator.SetIdle();
                                return;
                            }
                        }
                        
                        // All done - return to idle state
                        lock (_stateLock)
                        {
                            _currentState = AppState.Idle;
                            _operationCts?.Dispose();
                            _operationCts = null;
                        }
                        indicator.SetIdle();
                    }
                    else
                    {
                        // Win+Enter pressed during Transcribing or Typing
                        // Treat as cancel (same as Win+Esc)
                        CancellationTokenSource? ctsToCancel;
                        
                        lock (_stateLock)
                        {
                            ctsToCancel = _operationCts;
                            _currentState = AppState.Idle;
                            _operationCts = null;
                        }
                        
                        if (ctsToCancel != null)
                        {
                            Console.WriteLine("Canceled by user, going back to idle.");
                            ctsToCancel.Cancel();
                            ctsToCancel.Dispose();
                        }
                        
                        indicator.SetIdle();
                    }
                });
            };
            
            Console.WriteLine("Ready! Win+Enter to record, Win+Esc to stop. Ctrl+C to exit.");
            
            // Create indicator window and show idle state
            indicator.Create();
            indicator.SetIdle();
            
            // Install keyboard hook
            if (!keyboardHook.Install())
            {
                Console.WriteLine("Failed to install keyboard hook!");
                return;
            }
            
            // Run message loop
            MessageLoop.Run();
            
            // Cleanup
            keyboardHook.Dispose();
            indicator.Destroy();
            audioRecorder.Dispose();
            transcriber.Dispose();
            _operationCts?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Blah - Voice-to-Text Application");
        Console.WriteLine();
        Console.WriteLine("Usage: Blah.exe [OPTIONS]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --model <name>    Select Whisper model to use (default: medium)");
        Console.WriteLine("  --delay <ms>      Typing delay in milliseconds (default: 1)");
        Console.WriteLine("  --help, -h        Show this help message");
        Console.WriteLine();
        Console.WriteLine("Available models:");
        Console.WriteLine("  tiny     - 75 MB   (fastest, lowest accuracy)");
        Console.WriteLine("  base     - 142 MB  (fast, good for simple dictation)");
        Console.WriteLine("  small    - 466 MB  (balanced speed and accuracy)");
        Console.WriteLine("  medium   - 1.5 GB  (slower, very accurate) [default]");
        Console.WriteLine("  large    - 2.9 GB  (slowest, best accuracy)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Blah.exe");
        Console.WriteLine("  Blah.exe --model small");
        Console.WriteLine("  Blah.exe --delay 0");
        Console.WriteLine("  Blah.exe --delay 5");
        Console.WriteLine("  Blah.exe --model small --delay 0");
        Console.WriteLine();
        Console.WriteLine("Hotkeys:");
        Console.WriteLine("  Win+Enter - Start/stop recording");
        Console.WriteLine("  Win+Esc   - Cancel and return to idle");
        Console.WriteLine();
        Console.WriteLine("Visual Indicator:");
        Console.WriteLine("  A small triangle appears in the bottom-right corner showing app state:");
        Console.WriteLine("  \u001b[34mBlue\u001b[0m (small)   - Idle, ready to record");
        Console.WriteLine("  \u001b[31mRed\u001b[0m (small)    - Waiting for audio input");
        Console.WriteLine("  \u001b[31mRed\u001b[0m (large)    - Recording audio");
        Console.WriteLine("  \u001b[33mYellow\u001b[0m         - Transcribing speech to text");
        Console.WriteLine("  \u001b[32mGreen\u001b[0m          - Typing transcribed text");
        Console.WriteLine();
        Console.WriteLine("How it works:");
        Console.WriteLine("  1. Press Win+Enter to start recording");
        Console.WriteLine("  2. Speak into your microphone (indicator turns red when audio detected)");
        Console.WriteLine("  3. Press Win+Enter again to stop and transcribe");
        Console.WriteLine("  4. Text is automatically typed at your cursor position");
        Console.WriteLine("  5. Press Win+Esc anytime to cancel and return to idle");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  Language is automatically detected. Speak clearly in one language for best results.");
        Console.WriteLine("  Some apps like Notepad may need delay > 0. Default (1ms) works well.");
        Console.WriteLine();
        Console.WriteLine("Made by Paweł Adamczuk | paweladamczuk.com");
    }

    private static Whisper.net.Ggml.GgmlType ParseModelArgument(string[] args)
    {
        // Default to Medium
        const Whisper.net.Ggml.GgmlType defaultModel = Whisper.net.Ggml.GgmlType.Medium;
        
        // Find --model flag
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--model")
            {
                // Check if value exists
                if (i + 1 >= args.Length)
                {
                    Console.WriteLine("Warning: --model flag requires a value. Using default (medium).");
                    return defaultModel;
                }
                
                string modelName = args[i + 1].ToLowerInvariant();
                
                // Map string to GgmlType
                Whisper.net.Ggml.GgmlType? selectedModel = modelName switch
                {
                    "tiny" => Whisper.net.Ggml.GgmlType.Tiny,
                    "base" => Whisper.net.Ggml.GgmlType.Base,
                    "small" => Whisper.net.Ggml.GgmlType.Small,
                    "medium" => Whisper.net.Ggml.GgmlType.Medium,
                    "large" => Whisper.net.Ggml.GgmlType.LargeV3,
                    _ => null
                };
                
                if (selectedModel.HasValue)
                {
                    Console.WriteLine($"Using model: {modelName}");
                    return selectedModel.Value;
                }
                else
                {
                    Console.WriteLine($"Warning: Unknown model '{modelName}'. Using default (medium).");
                    Console.WriteLine("Run with --help to see available models.");
                    return defaultModel;
                }
            }
        }
        
        // No --model flag found, use default
        return defaultModel;
    }

    private static int ParseDelayArgument(string[] args)
    {
        // Default to 1ms (good balance of speed and compatibility)
        const int defaultDelay = 1;
        
        // Find --delay flag
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--delay")
            {
                // Check if value exists
                if (i + 1 >= args.Length)
                {
                    Console.WriteLine("Warning: --delay flag requires a value. Using default (1ms).");
                    return defaultDelay;
                }
                
                string delayValue = args[i + 1];
                
                // Parse to int
                if (int.TryParse(delayValue, out int delay))
                {
                    if (delay < 0)
                    {
                        Console.WriteLine("Warning: Delay cannot be negative. Using default (1ms).");
                        return defaultDelay;
                    }
                    
                    // Show message for non-default values (including 0)
                    if (delay != defaultDelay)
                    {
                        Console.WriteLine($"Using typing delay: {delay}ms per character");
                    }
                    return delay;
                }
                else
                {
                    Console.WriteLine($"Warning: Invalid delay value '{delayValue}'. Using default (1ms).");
                    return defaultDelay;
                }
            }
        }
        
        // No --delay flag found, use default
        return defaultDelay;
    }
}
