using Whisper.net;
using Whisper.net.Ggml;

namespace Blah.Speech;

internal class Transcriber : IDisposable
{
    private WhisperFactory? _whisperFactory;
    private WhisperProcessor? _whisperProcessor;
    private readonly HashSet<string> _silencePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "[ Silence ]",
        "[BLANK_AUDIO]"
    };

    public async Task InitializeAsync(GgmlType modelType = GgmlType.Medium)
    {
        string modelFileName = GetModelFileName(modelType);
        string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, modelFileName);
        string tempPath = modelPath + ".tmp";
        
        // Clean up any stale temp file from previous interrupted download
        if (File.Exists(tempPath))
        {
            Console.WriteLine("Removing incomplete download from previous session...");
            File.Delete(tempPath);
        }
        
        if (!File.Exists(modelPath))
        {
            await DownloadModelWithProgressAsync(modelType, modelPath, tempPath);
        }
        
        Console.WriteLine($"Loading {modelType} model...");
        
        // Wrap model loading in try-catch to detect corruption
        try
        {
            _whisperFactory = WhisperFactory.FromPath(modelPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load model: {ex.Message}");
            Console.WriteLine($"Deleting corrupted model file: {modelFileName}");
            File.Delete(modelPath);
            throw new InvalidOperationException(
                $"Failed to load the whisper model. The file may be corrupted. " +
                $"Please restart the application to re-download.", ex);
        }
        
        string? prompt = LoadPromptFromFile();

        var builder = _whisperFactory.CreateBuilder()
            .WithLanguage("auto");

        if (!string.IsNullOrEmpty(prompt))
        {
            builder = builder.WithPrompt(prompt);
            Console.WriteLine($"Using Whisper prompt ({prompt.Length} chars)");
        }

        _whisperProcessor = builder.Build();
    }

    private static string GetModelFileName(GgmlType modelType)
    {
        // Convert enum to lowercase filename
        // GgmlType.Medium -> "ggml-medium.bin"
        // GgmlType.Small -> "ggml-small.bin"
        string modelName = modelType.ToString().ToLowerInvariant();
        return $"ggml-{modelName}.bin";
    }

    private async Task DownloadModelWithProgressAsync(GgmlType modelType, string modelPath, string tempPath)
    {
        long expectedBytes = GetExpectedModelSize(modelType);
        string expectedSize = FormatSize(expectedBytes);
        
        Console.WriteLine($"Downloading {modelType} model ({expectedSize})...");
        
        try
        {
            // Download to temp file with explicit stream disposal
            {
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromHours(1) };
                var downloader = new WhisperGgmlDownloader(httpClient);
                
                // Use their downloader to get the stream
                await using var modelStream = await downloader.GetGgmlModelAsync(modelType);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                
                var buffer = new byte[8192];
                long totalRead = 0;
                var lastUpdateTime = DateTime.UtcNow;
                
                int bytesRead;
                while ((bytesRead = await modelStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    
                    var now = DateTime.UtcNow;
                    var elapsed = now - lastUpdateTime;
                    
                    // Report progress every 2 seconds
                    if (elapsed.TotalSeconds >= 2)
                    {
                        var percentage = expectedBytes > 0 ? (totalRead * 100.0 / expectedBytes) : 0;
                        Console.WriteLine($"  {FormatSize(totalRead)} / {expectedSize} ({percentage:F1}%)");
                        
                        lastUpdateTime = now;
                    }
                }
                
                // Final completion message
                Console.WriteLine($"  Download complete!");
            } // Streams are explicitly disposed here before file move
            
            // Atomic rename: only make the file "official" after successful download
            File.Move(tempPath, modelPath);
        }
        catch (Exception ex)
        {
            // Clean up temp file on any failure
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
            
            Console.WriteLine($"Download failed: {ex.Message}");
            throw new InvalidOperationException($"Failed to download model: {ex.Message}", ex);
        }
    }

    private static long GetExpectedModelSize(GgmlType modelType)
    {
        // Sizes based on actual model files and help text
        return modelType switch
        {
            GgmlType.Tiny => 75L * 1024 * 1024,           // 75 MB
            GgmlType.Base => 142L * 1024 * 1024,          // 142 MB
            GgmlType.Small => 466L * 1024 * 1024,         // 466 MB
            GgmlType.Medium => 1536L * 1024 * 1024,       // 1.5 GB
            GgmlType.LargeV3 => 2900L * 1024 * 1024,      // 2.9 GB
            _ => 0 // Unknown size, progress will show without percentage
        };
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F0} MB";
        else
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
    }

    private string? LoadPromptFromFile()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string txtPath = Path.Combine(baseDir, "prompt.txt");
        string mdPath = Path.Combine(baseDir, "prompt.md");
        
        List<string> prompts = new List<string>();
        
        // Try to load prompt.txt
        if (File.Exists(txtPath))
        {
            try
            {
                string content = File.ReadAllText(txtPath);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    prompts.Add(content);
                    Console.WriteLine("Loaded prompt.txt");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading prompt.txt: {ex.Message}");
            }
        }
        
        // Try to load prompt.md
        if (File.Exists(mdPath))
        {
            try
            {
                string content = File.ReadAllText(mdPath);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    prompts.Add(content);
                    Console.WriteLine("Loaded prompt.md");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading prompt.md: {ex.Message}");
            }
        }
        
        if (prompts.Count == 0)
        {
            return null;
        }
        
        // Combine prompts with newline separator
        string combined = string.Join("\n", prompts);
        
        // Validate max length and trim if needed
        const int MAX_PROMPT_LENGTH = 224;
        if (combined.Length > MAX_PROMPT_LENGTH)
        {
            combined = combined.Substring(0, MAX_PROMPT_LENGTH);
            Console.WriteLine($"Prompt truncated to {MAX_PROMPT_LENGTH} characters");
        }
        
        return combined;
    }

    public async Task<string?> TranscribeAsync(float[] audioData, CancellationToken cancellationToken = default)
    {
        if (_whisperProcessor == null)
        {
            return null;
        }
        
        try
        {
            Console.WriteLine("Starting transcription...");
            
            string transcription = "";
            await foreach (var result in _whisperProcessor.ProcessAsync(audioData, cancellationToken))
            {
                transcription += result.Text;
            }
            
            Console.WriteLine("Transcription finished.");
            
            if (!string.IsNullOrWhiteSpace(transcription))
            {
                string trimmed = transcription.Trim();
                
                // Remove any silence markers that might appear in the text
                foreach (var pattern in _silencePatterns)
                {
                    trimmed = trimmed.Replace(pattern, "", StringComparison.OrdinalIgnoreCase).Trim();
                }
                
                // Check if anything meaningful is left after removing silence markers
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    Console.WriteLine("Skipping silence-only transcription");
                    return null;
                }
                
                return trimmed;
            }
            
            return null;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("Transcription canceled.");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Transcription error: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        _whisperProcessor?.Dispose();
        _whisperFactory?.Dispose();
    }
}
