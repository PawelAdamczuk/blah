using Blah.Win32;

namespace Blah.Input;

internal static class TextInput
{
    public static async Task TypeTextAsync(string text, CancellationToken cancellationToken = default, int typingDelayMs = 0)
    {
        // Filter to only alphanumerics and punctuation
        string filtered = new string(text.Where(c => 
            char.IsLetterOrDigit(c) || 
            char.IsPunctuation(c) || 
            char.IsWhiteSpace(c)
        ).ToArray());
        
        if (string.IsNullOrWhiteSpace(filtered))
        {
            return;
        }
        
        Console.WriteLine($"Starting typing text: \"{filtered}\"");
        
        foreach (char c in filtered)
        {
            // Check for cancellation before each character
            if (cancellationToken.IsCancellationRequested)
            {
                Console.WriteLine("Typing canceled.");
                return;
            }
            
            INPUT[] inputs = new INPUT[2];
            
            inputs[0] = new INPUT
            {
                type = Constants.INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = Constants.KEYEVENTF_UNICODE,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            
            inputs[1] = new INPUT
            {
                type = Constants.INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = 0,
                        wScan = c,
                        dwFlags = Constants.KEYEVENTF_UNICODE | Constants.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            
            User32.SendInput(2, inputs, INPUT.Size);
            
            // Optional delay between characters for compatibility
            if (typingDelayMs > 0)
            {
                try
                {
                    await Task.Delay(typingDelayMs, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("Typing canceled.");
                    return;
                }
            }
        }
        
        Console.WriteLine("Typing finished.");
    }
}
