using System.Reflection;
using System.Windows.Forms;

namespace AngelBot;

public enum BotState { Idle, Casting, Waiting, BiteDetected, Reeling, Filleting }

public class BotEngine : IDisposable
{
    public BotConfig Config { get; set; }
    public BotState State { get; private set; } = BotState.Idle;
    public bool IsRunning { get; private set; }

    public Action<BotState>? OnStateChange;
    public Action<string>? OnLog;
    public Action<int, int, int>? OnStatsUpdate; // fish, casts, timeouts
    public Action<int, int, int, int>? OnRegionUpdate; // x, y, w, h

    public int FishCaught { get; private set; }
    public int CastCount { get; private set; }
    public int TimeoutCount { get; private set; }
    public DateTime? StartTime { get; private set; }

    private ScreenDetector? _screen;
    private AudioDetector? _audio;
    private InputController? _input;
    private Thread? _thread;
    private CancellationTokenSource _cts = new();
    private int _consecutiveTimeouts = 0;
    private readonly Random _rng = new();

    public BotEngine(BotConfig config) { Config = config; }

    private void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        Console.WriteLine(line);
        OnLog?.Invoke(line);
    }

    private void SetState(BotState state)
    {
        State = state;
        OnStateChange?.Invoke(state);
    }

    private void UpdateStats()
        => OnStatsUpdate?.Invoke(FishCaught, CastCount, TimeoutCount);

    public void Start()
    {
        if (IsRunning) return;
        if (!InitComponents()) { Log("⚠ Initialisierung fehlgeschlagen!"); return; }

        IsRunning = true;
        _cts = new CancellationTokenSource();
        FishCaught = 0; CastCount = 0; TimeoutCount = 0;
        StartTime = DateTime.Now;
        _consecutiveTimeouts = 0;

        _thread = new Thread(RunLoop) { IsBackground = true };
        _thread.Start();
        Log("Bot gestartet");
        SetState(BotState.Casting);
    }

    public void Stop()
    {
        if (!IsRunning && _thread == null) return;
        IsRunning = false;
        _cts.Cancel();
        // Bot-Thread reagiert sofort auf Cancellation — kurzes Join reicht
        _thread?.Join(300);
        _thread = null;
        try { _audio?.Stop(); } catch { }
        try { _screen?.Dispose(); } catch { }
        _screen = null;
        _audio  = null;
        _input  = null;
        SetState(BotState.Idle);
        Log("Bot gestoppt");
    }

    private static byte[]? GetResource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream == null) return null;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private bool InitComponents()
    {
        var method = Config.DetectionMethod;
        _input = new InputController(Config.HumanizeDelays, Config.HumanizeRange);

        if (method is "screen" or "both")
        {
            if (Config.ScreenRegion == null && method == "screen")
            { Log("⚠ Keine Region gesetzt"); return false; }

            _screen = new ScreenDetector(Config.ScreenRegion, Config.MinWhitePixels);
            if (Config.ScreenRegion != null)
                Log($"Region gesetzt ({Config.ScreenRegion.W}x{Config.ScreenRegion.H})");

            var splash = GetResource("res.splash.png");
            if (splash != null && _screen.LoadReferenceImage(splash))
                Log("Bild-Referenz geladen → Weiß-Splash-Erkennung aktiv");

            var lure = GetResource("res.lure1.png") ?? GetResource("res.lure2.png");
            if (lure != null && _screen.LoadLureTemplate(lure))
                Log("Köder-Template geladen");
        }

        if (method is "audio" or "both")
        {
            _audio = new AudioDetector { Sensitivity = Config.AudioSensitivity };
            if (_audio.Start())
            {
                Log("Audio-Erkennung gestartet (WASAPI Loopback)");
                var bite   = GetResource("res.bite.wav");
                var nibble = GetResource("res.nibble.wav");
                if (bite != null && _audio.LoadReferenceSounds(bite, nibble))
                    Log("Audio-Referenz geladen → Fingerprint-Erkennung aktiv");
                else
                    Log("⚠ Audio-Ressource fehlt → RMS-Spike-Fallback");
            }
            else
            {
                Log("⚠ Audio-Erkennung fehlgeschlagen");
                _audio = null;
                if (method == "audio") return false;
            }
        }

        return true;
    }

    private void Sleep(double seconds)
    {
        double ms = seconds * 1000;
        if (Config.HumanizeDelays)
            ms += _rng.NextDouble() * Config.HumanizeRange * 2000 - Config.HumanizeRange * 1000;
        ms = Math.Max(100, ms);
        _cts.Token.WaitHandle.WaitOne((int)ms);
    }

    private bool ShouldStop() => !IsRunning || _cts.Token.IsCancellationRequested;

    private bool CastWithConfirmation()
    {
        double timeout = Config.CastConfirmTimeout;

        _input!.CastClick();
        Log("Angel ausgeworfen");

        if (_screen == null)
        {
            Sleep(Config.CastDelay);
            return !ShouldStop();
        }

        _screen.ResetCastDetection();
        Thread.Sleep(300);

        var deadline = DateTime.Now.AddSeconds(timeout);
        while (!ShouldStop() && DateTime.Now < deadline)
        {
            if (_screen.CheckCastSplash()) { Log("Wurf bestätigt"); return true; }
            Thread.Sleep(50);
        }
        Log("⚠ Kein Einwurf-Splash erkannt – weiter");
        return !ShouldStop();
    }

    private bool CalibrateAndVerify()
    {
        if (_screen == null) return true;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            _screen.StartCalibration();
            for (int i = 0; i < 20; i++)
            {
                if (ShouldStop()) return false;
                _screen.CheckForBite();
                Thread.Sleep(100);
            }
            _screen.FinishCalibration();

            if (_screen.IsSceneCalm()) return true;

            Log("⚠ Szene nicht ruhig – warte weiter...");
            Sleep(3.0);
            if (ShouldStop()) return false;
        }
        Log("⚠ Szene immer noch unruhig – Erkennung trotzdem gestartet");
        return true;
    }

    private (bool bite, string source) CheckBite()
    {
        if (_screen != null)
        {
            var (det, _) = _screen.CheckForBite();
            if (det) return (true, "screen");
        }
        if (_audio != null)
        {
            var (det, _) = _audio.CheckForBite();
            if (det) return (true, "audio");
        }
        return (false, "");
    }

    private void RunLoop()
    {
        if (Config.InitialDelay > 0)
        {
            Log($"Starte in {(int)Config.InitialDelay}s – jetzt ins Spiel wechseln!");
            Sleep(Config.InitialDelay);
            if (ShouldStop()) return;
        }

        while (!ShouldStop())
        {
            try
            {
                // CASTING — zuerst immer einholen um sauberen Zustand zu garantieren
                SetState(BotState.Casting);
                _input!.ReelIn(0);
                Thread.Sleep(600);
                if (ShouldStop()) break;

                if (!CastWithConfirmation()) break;
                CastCount++;
                UpdateStats();
                if (ShouldStop()) break;

                // WAITING
                SetState(BotState.Waiting);
                Log("Warte auf Biss...");

                // Splash settle
                if (_screen != null && Config.ScreenBlackoutAfterCast > 0)
                {
                    Sleep(Config.ScreenBlackoutAfterCast);
                    if (ShouldStop()) break;
                }

                // Calibrate
                if (!CalibrateAndVerify()) break;

                // Lure presence check
                if (_screen != null && _screen.HasLureTemplate)
                {
                    var lure = _screen.FindLureOnScreen(0.28);
                    if (lure == null)
                    {
                        Log("⚠ Köder nicht im Wasser – erneut auswerfen");
                        Sleep(1.0);
                        if (ShouldStop()) break;
                        continue;
                    }
                }

                // Debug loop
                double maxWait = Config.MaxWaitTime;
                int confirmNeeded = Config.BiteConfirmFrames;
                double minBiteWait = Config.MinBiteWait;
                var waitStart = DateTime.Now;
                bool biteFound = false;
                int confirmCount = 0;
                string lastSource = "";
                var lastDebug = DateTime.Now;

                while (!ShouldStop() && (DateTime.Now - waitStart).TotalSeconds < maxWait)
                {
                    double elapsed = (DateTime.Now - waitStart).TotalSeconds;
                    var (bite, source) = CheckBite();

                    if (bite && elapsed >= minBiteWait)
                    {
                        confirmCount++;
                        lastSource = source;
                        if (confirmCount >= confirmNeeded)
                        {
                            biteFound = true;
                            Log($"BISS ERKANNT! Einholen... [{lastSource}]");
                            break;
                        }
                    }
                    else confirmCount = 0;

                    // Debug every 5s
                    if ((DateTime.Now - lastDebug).TotalSeconds >= 5)
                    {
                        if (_audio != null)
                            Console.WriteLine($"[Audio-Debug] RMS={_audio.CurrentRms:F5}  Baseline={_audio.BaselineRms:F5}");
                        if (_screen != null && _screen.RefLoaded)
                            Console.WriteLine($"[Screen-Debug] Weiß={_screen.LastWhiteCount}  Baseline={_screen.WhiteBaseline:F0}  Threshold={_screen.WhiteThreshold:F0}");
                        lastDebug = DateTime.Now;
                    }

                    Thread.Sleep(20);
                }

                if (ShouldStop()) break;

                if (!biteFound)
                {
                    SetState(BotState.Idle);
                    TimeoutCount++;
                    _consecutiveTimeouts++;
                    UpdateStats();

                    if (_consecutiveTimeouts >= 3)
                    {
                        // Aggressives Reset: mehrfach einholen, dann lange Pause
                        Log($"⚠ {_consecutiveTimeouts}× Timeout – Hard-Reset...");
                        _input!.ReelIn(0);
                        Thread.Sleep(500);
                        _input.ReelIn(0);
                        Thread.Sleep(500);
                        _input.ReelIn(0);
                        _consecutiveTimeouts = 0;
                        Sleep(6.0);
                    }
                    else
                    {
                        Log("Timeout – neu auswerfen");
                        // Einholen vor dem nächsten Versuch
                        _input!.ReelIn(0);
                        Thread.Sleep(400);
                        Sleep(Config.BetweenCastsDelay);
                    }
                    if (ShouldStop()) break;
                    continue;
                }

                _consecutiveTimeouts = 0;

                // REELING
                SetState(BotState.Reeling);
                _input!.ReelIn(Config.ReelDelay);
                FishCaught++;
                Log($"✓ Fisch gefangen! (#{FishCaught})");
                UpdateStats();

                if (ShouldStop()) break;

                // FILLETING
                if (Config.AutoFillet) DoFillet();

                Sleep(Config.BetweenCastsDelay);
            }
            catch (Exception e)
            {
                Log($"⚠ Fehler: {e.Message}");
                Sleep(2.0);
            }
        }
    }

    private void DoFillet()
    {
        if (Config.FilletSlotX == null || Config.FilletSlotY == null)
        {
            Log("⚠ Fisch-Slot nicht konfiguriert");
            return;
        }
        SetState(BotState.Filleting);
        Log("Filetiere...");
        try
        {
            var key = Enum.TryParse<Keys>(Config.InventoryKey.ToUpper(), out var k) ? k : Keys.I;
            _input!.PressKey(key);
            Thread.Sleep(1000);
            _input.MoveMouseTo(Config.FilletSlotX.Value, Config.FilletSlotY.Value);
            Thread.Sleep(400);
            _input.RightClick();
            Thread.Sleep(600);
            _input.PressKey(key);
            Thread.Sleep(800);
            Log("Filetiert");
        }
        catch (Exception e) { Log($"⚠ Filetieren fehlgeschlagen: {e.Message}"); }
        SetState(BotState.Idle);
    }

    public TimeSpan GetRuntime()
        => StartTime.HasValue ? DateTime.Now - StartTime.Value : TimeSpan.Zero;

    public void Dispose() => Stop();
}
