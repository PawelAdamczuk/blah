using NAudio.Wave;

namespace Blah.Audio;

internal class AudioRecorder : IDisposable
{
    private WaveInEvent? _waveIn;
    private MemoryStream? _recordedAudio;
    private bool _isRecording;
    private bool _audioDetected;
    private int _lastWorkingDevice = 0; // Remember last working device
    private const float SILENCE_THRESHOLD = 0.01f; // Adjust this if needed

    public bool IsRecording => _isRecording;
    public event Action? AudioDetected;

    public void Start()
    {
        if (_isRecording) return;

        Console.WriteLine("Starting recording...");

        // Try last working device first (fast path)
        if (TryStartDevice(_lastWorkingDevice))
        {
            return;
        }
        
        // Last working device failed, try all available devices
        var availableDevices = GetAvailableDevices();
        
        if (availableDevices.Count == 0)
        {
            Console.WriteLine("No audio input devices available.");
            return;
        }

        foreach (var deviceNumber in availableDevices)
        {
            // Skip the device we already tried
            if (deviceNumber == _lastWorkingDevice)
                continue;
                
            if (TryStartDevice(deviceNumber))
            {
                _lastWorkingDevice = deviceNumber;
                
                // Show device name only when falling back to non-default device
                if (deviceNumber != 0)
                {
                    var caps = WaveInEvent.GetCapabilities(deviceNumber);
                    Console.WriteLine($"Using audio device: {caps.ProductName}");
                }
                return;
            }
        }
        
        Console.WriteLine("Failed to start recording on any available device.");
    }

    private static List<int> GetAvailableDevices()
    {
        var devices = new List<int>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            try
            {
                var caps = WaveInEvent.GetCapabilities(i);
                devices.Add(i);
            }
            catch
            {
                // Device not accessible, skip
            }
        }
        return devices;
    }

    private bool TryStartDevice(int deviceNumber)
    {
        try
        {
            _isRecording = true;
            _audioDetected = false;
            _recordedAudio = new MemoryStream();
            
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(16000, 1)
            };
            
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            
            _waveIn.StartRecording();
            
            // Update last working device on success
            _lastWorkingDevice = deviceNumber;
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Device {deviceNumber} failed: {ex.Message}");
            _waveIn?.Dispose();
            _waveIn = null;
            _isRecording = false;
            return false;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        _recordedAudio?.Write(e.Buffer, 0, e.BytesRecorded);
        
        // Detect if this is actual audio (not silence)
        if (!_audioDetected && HasAudio(e.Buffer, e.BytesRecorded))
        {
            _audioDetected = true;
            AudioDetected?.Invoke();
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null && _isRecording)
        {
            Console.WriteLine($"Recording stopped unexpectedly: {e.Exception.Message}");
            _isRecording = false;
        }
    }

    private bool HasAudio(byte[] buffer, int bytesRecorded)
    {
        // Check if there's any significant audio signal
        for (int i = 0; i < bytesRecorded - 1; i += 2)
        {
            short sample = BitConverter.ToInt16(buffer, i);
            float normalized = Math.Abs(sample / 32768f);
            
            if (normalized > SILENCE_THRESHOLD)
            {
                return true;
            }
        }
        
        return false;
    }

    public float[]? Stop()
    {
        if (!_isRecording) return null;

        try
        {
            Console.WriteLine("Finished recording.");
            
            if (_waveIn == null || _recordedAudio == null)
            {
                return null;
            }
            
            _waveIn.StopRecording();
            _waveIn.Dispose();
            _isRecording = false;
            
            if (_recordedAudio.Length == 0)
            {
                _recordedAudio.Dispose();
                return null;
            }
            
            byte[] audioBytes = _recordedAudio.ToArray();
            _recordedAudio.Dispose();
            
            return ConvertBytesToFloat(audioBytes, audioBytes.Length);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Stop recording error: {ex.Message}");
            return null;
        }
    }

    public void Cancel()
    {
        // Same as Stop(), but explicitly for cancellation
        // Discards audio without returning it
        if (_isRecording)
        {
            Stop(); // Discard return value
        }
    }

    private static float[] ConvertBytesToFloat(byte[] audioBytes, int length)
    {
        int sampleCount = length / 2; // 16-bit audio = 2 bytes per sample
        float[] floatData = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(audioBytes, i * 2);
            floatData[i] = sample / 32768f; // Normalize to [-1, 1]
        }
        
        return floatData;
    }

    public void Dispose()
    {
        _waveIn?.Dispose();
        _recordedAudio?.Dispose();
    }
}
