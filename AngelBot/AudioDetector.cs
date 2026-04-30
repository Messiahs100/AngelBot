using NAudio.CoreAudioApi;
using NAudio.Dsp;
using NAudio.Wave;

namespace AngelBot;

public class AudioDetector : IDisposable
{
    private const int NBands = 16;
    private const int BufferMax = 48000 * 2;
    private const int SnapshotSize = 8192;

    private WasapiLoopbackCapture? _capture;
    private readonly object _lock = new();

    // RMS tracking
    private readonly List<double> _rmsHistory = new();
    private const int RmsHistoryMax = 100;
    private double _baselineRms = 0;
    private double _currentRms = 0;

    // Bite detection
    private bool _biteDetected = false;
    private int _biteSticky = 0;
    private int _spikeCooldown = 0;

    // Audio buffer for fingerprinting
    private readonly List<float> _audioBuffer = new();

    // Fingerprints
    private float[]? _biteFingerprint;
    private float[]? _nibbleFingerprint;

    public int Sensitivity { get; set; } = 70;
    private int _channels = 2;
    private int _sampleRate = 48000;
    public bool IsRunning { get; private set; }

    public bool Start()
    {
        if (IsRunning) return true;
        try
        {
            _capture = new WasapiLoopbackCapture();
            _sampleRate = _capture.WaveFormat.SampleRate;
            _channels = _capture.WaveFormat.Channels;
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
            IsRunning = true;
            Console.WriteLine($"[Audio] WASAPI Loopback gestartet ({_sampleRate} Hz, {_channels}ch)");
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Audio] Fehler: {e.Message}");
            return false;
        }
    }

    public void Stop()
    {
        IsRunning = false;
        if (_capture != null)
        {
            try { _capture.StopRecording(); _capture.Dispose(); } catch { }
            _capture = null;
        }
        lock (_lock)
        {
            _rmsHistory.Clear();
            _baselineRms = 0;
            _biteDetected = false;
            _biteSticky = 0;
            _audioBuffer.Clear();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        int floatCount = e.BytesRecorded / 4;
        var samples = new float[floatCount];
        Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);

        // Mix to mono
        float[] mono;
        if (_channels > 1)
        {
            mono = new float[floatCount / _channels];
            for (int i = 0; i < mono.Length; i++)
            {
                float sum = 0;
                for (int c = 0; c < _channels; c++)
                    sum += samples[i * _channels + c];
                mono[i] = sum / _channels;
            }
        }
        else mono = samples;

        ProcessAudio(mono);
    }

    private void ProcessAudio(float[] mono)
    {
        double sumSq = 0;
        foreach (var s in mono) sumSq += s * s;
        double rms = Math.Sqrt(sumSq / mono.Length);

        lock (_lock)
        {
            _currentRms = rms;
            _rmsHistory.Add(rms);
            if (_rmsHistory.Count > RmsHistoryMax)
                _rmsHistory.RemoveAt(0);

            _audioBuffer.AddRange(mono);
            if (_audioBuffer.Count > BufferMax)
                _audioBuffer.RemoveRange(0, _audioBuffer.Count - BufferMax);

            if (_rmsHistory.Count >= 10)
            {
                var sorted = _rmsHistory.Take(_rmsHistory.Count - 5).OrderBy(x => x).ToList();
                _baselineRms = sorted[sorted.Count / 2];
            }

            if (_spikeCooldown > 0) { _spikeCooldown--; return; }

            if (_baselineRms > 0)
            {
                double multiplier = 1.5 + (100 - Sensitivity) / 100.0 * 4.0;
                double threshold = _baselineRms * multiplier;
                if (rms > threshold && rms > 0.003)
                {
                    Console.WriteLine($"[Audio-Spike] RMS={rms:F5} Threshold={threshold:F5}");
                    if (_biteFingerprint != null)
                    {
                        int snapLen = Math.Min(SnapshotSize, _audioBuffer.Count);
                        var snapshot = _audioBuffer.GetRange(_audioBuffer.Count - snapLen, snapLen).ToArray();
                        if (IsBiteSound(snapshot))
                        {
                            Console.WriteLine("[Audio-Spike] → Fingerprint: BISS erkannt");
                            _biteDetected = true;
                            _biteSticky = 8;
                            _spikeCooldown = 20;
                        }
                        else Console.WriteLine("[Audio-Spike] → Fingerprint: kein Biss");
                    }
                    else
                    {
                        _biteDetected = true;
                        _biteSticky = 8;
                        _spikeCooldown = 20;
                    }
                }
            }
        }
    }

    public (bool detected, double rms) CheckForBite()
    {
        lock (_lock)
        {
            bool detected;
            if (_biteDetected)
            {
                _biteDetected = false;
                detected = true;
            }
            else if (_biteSticky > 0)
            {
                _biteSticky--;
                detected = true;
            }
            else detected = false;
            return (detected, _currentRms);
        }
    }

    public double GetAudioLevel()
    {
        lock (_lock) return Math.Min(_currentRms * 10.0, 1.0);
    }

    public double CurrentRms { get { lock (_lock) return _currentRms; } }
    public double BaselineRms { get { lock (_lock) return _baselineRms; } }

    // ── Fingerprint ──────────────────────────────────────────────────────────

    public bool LoadReferenceSounds(byte[] biteWav, byte[]? nibbleWav = null)
    {
        var fp = LoadWavFingerprint(biteWav);
        if (fp == null) return false;
        _biteFingerprint = fp;
        if (nibbleWav != null)
            _nibbleFingerprint = LoadWavFingerprint(nibbleWav);
        return true;
    }

    private float[]? LoadWavFingerprint(byte[] wavData)
    {
        try
        {
            var (data, rate) = ReadWav(wavData);
            if (data == null) return null;
            float max = data.Max(Math.Abs);
            if (max > 0) for (int i = 0; i < data.Length; i++) data[i] /= max;
            return ComputeBandProfile(data, rate);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Audio] Fehler beim Laden: {e.Message}");
            return null;
        }
    }

    private static (float[]? data, int rate) ReadWav(byte[] wavData)
    {
        using var reader = new WaveFileReader(new MemoryStream(wavData));
        int rate = reader.WaveFormat.SampleRate;
        int ch = reader.WaveFormat.Channels;
        int bits = reader.WaveFormat.BitsPerSample;
        var enc = reader.WaveFormat.Encoding;

        var raw = new byte[reader.Length];
        reader.Read(raw, 0, raw.Length);

        float[] all;
        if (enc == WaveFormatEncoding.IeeeFloat && bits == 32)
        {
            all = new float[raw.Length / 4];
            Buffer.BlockCopy(raw, 0, all, 0, raw.Length);
        }
        else if (enc == WaveFormatEncoding.Pcm && bits == 16)
        {
            all = new float[raw.Length / 2];
            for (int i = 0; i < all.Length; i++)
                all[i] = BitConverter.ToInt16(raw, i * 2) / 32768f;
        }
        else if (enc == WaveFormatEncoding.Pcm && bits == 8)
        {
            all = new float[raw.Length];
            for (int i = 0; i < raw.Length; i++)
                all[i] = (raw[i] - 128) / 128f;
        }
        else return (null, rate);

        int maxMono = (int)(rate * 1.5);
        float[] mono;
        if (ch > 1)
        {
            int len = Math.Min(all.Length / ch, maxMono);
            mono = new float[len];
            for (int i = 0; i < len; i++)
            {
                float sum = 0;
                for (int c = 0; c < ch; c++) sum += all[i * ch + c];
                mono[i] = sum / ch;
            }
        }
        else mono = all[..Math.Min(all.Length, maxMono)];

        return (mono, rate);
    }

    private static float[]? ComputeBandProfile(float[] data, int sampleRate)
    {
        if (data.Length < 256) return null;

        int n = 1;
        while (n < data.Length) n <<= 1;
        n >>= 1; // largest power of 2 <= data.Length

        var complex = new Complex[n];
        for (int i = 0; i < n; i++)
            complex[i] = new Complex { X = data[i], Y = 0 };

        int m = (int)Math.Log2(n);
        FastFourierTransform.FFT(true, m, complex);

        double[] freqs = new double[n / 2];
        double[] mag = new double[n / 2];
        for (int i = 0; i < n / 2; i++)
        {
            freqs[i] = (double)i * sampleRate / n;
            mag[i] = Math.Sqrt(complex[i].X * complex[i].X + complex[i].Y * complex[i].Y);
        }

        double[] bandEdges = new double[NBands + 1];
        for (int i = 0; i <= NBands; i++)
            bandEdges[i] = Math.Pow(10, Math.Log10(20) + i * (Math.Log10(20000) - Math.Log10(20)) / NBands);

        float[] profile = new float[NBands];
        for (int b = 0; b < NBands; b++)
        {
            var inBand = freqs.Select((f, i) => (f, i))
                              .Where(x => x.f >= bandEdges[b] && x.f < bandEdges[b + 1])
                              .Select(x => mag[x.i])
                              .ToArray();
            if (inBand.Length > 0)
                profile[b] = (float)inBand.Average();
        }

        double norm = Math.Sqrt(profile.Sum(x => x * x));
        if (norm > 0) for (int i = 0; i < NBands; i++) profile[i] /= (float)norm;

        return profile;
    }

    private bool IsBiteSound(float[] audio)
    {
        var profile = ComputeBandProfile(audio, _sampleRate);
        if (profile == null) return true;

        float biteSim = Cosine(profile, _biteFingerprint!);
        if (_nibbleFingerprint != null)
        {
            float nibbleSim = Cosine(profile, _nibbleFingerprint);
            return biteSim > nibbleSim && biteSim > 0.45f;
        }
        return biteSim > 0.45f;
    }

    private static float Cosine(float[] a, float[] b)
    {
        float dot = 0;
        for (int i = 0; i < a.Length; i++) dot += a[i] * b[i];
        return Math.Clamp(dot, 0f, 1f);
    }

    public void Dispose() => Stop();
}
