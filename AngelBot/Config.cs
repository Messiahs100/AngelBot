using System.Text.Json;
using System.Text.Json.Serialization;

namespace AngelBot;

public class ScreenRegion
{
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
}

public class BotConfig
{
    public string DetectionMethod { get; set; } = "screen";
    public ScreenRegion? ScreenRegion { get; set; }
    public int ScreenSensitivity { get; set; } = 30;
    public int ScreenMinChangeArea { get; set; } = 500;
    public int MinWhitePixels { get; set; } = 500;
    public int? AudioDeviceIndex { get; set; } = null;
    public int AudioSensitivity { get; set; } = 70;
    public int AudioSampleRate { get; set; } = 44100;
    public string InfodatenPath { get; set; } = "";
    public bool UseReferenceImages { get; set; } = true;
    public bool UseReferenceAudio { get; set; } = true;
    public double CastDelay { get; set; } = 2.0;
    public double ReelDelay { get; set; } = 0.0;
    public double BetweenCastsDelay { get; set; } = 4.0;
    public double MaxWaitTime { get; set; } = 45.0;
    public bool HumanizeDelays { get; set; } = false;
    public double HumanizeRange { get; set; } = 0.2;
    public bool AutoFillet { get; set; } = false;
    public int FilletAfterNFish { get; set; } = 5;
    public string InventoryKey { get; set; } = "i";
    public int? FilletSlotX { get; set; } = null;
    public int? FilletSlotY { get; set; } = null;
    public double InitialDelay { get; set; } = 0.0;
    public double ScreenBlackoutAfterCast { get; set; } = 3.0;
    public int BiteConfirmFrames { get; set; } = 2;
    public double MinBiteWait { get; set; } = 1.5;
    public double CastConfirmTimeout { get; set; } = 3.0;
    public int CastMaxRetries { get; set; } = 3;
    public string StartStopHotkey { get; set; } = "F6";
    public string EmergencyStopHotkey { get; set; } = "F8";
    public string Language { get; set; } = "de";

    private static readonly string ConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static BotConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new BotConfig();
        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<BotConfig>(json, JsonOptions) ?? new BotConfig();
        }
        catch
        {
            return new BotConfig();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { }
    }

    public string GetInfodatenPath()
    {
        if (!string.IsNullOrEmpty(InfodatenPath) && Directory.Exists(InfodatenPath))
            return InfodatenPath;
        var next = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Infodaten");
        return next;
    }
}
