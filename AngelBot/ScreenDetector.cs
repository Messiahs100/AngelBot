using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Drawing;

namespace AngelBot;

public class ScreenDetector : IDisposable
{
    private ScreenRegion? _region;
    private readonly int _minWhitePixels;

    // White splash baseline
    private readonly List<double> _whiteSamples = new();
    private const int WhiteSamplesMax = 80;
    private double _whiteBaseline = 0;
    private bool _calibrating = false;
    private bool _refLoaded = false;

    // Lure template
    private Mat? _lureTemplate;

    // Cast detection
    private Mat? _castPrevFrame;

    public ScreenDetector(ScreenRegion? region, int minWhitePixels = 500)
    {
        _region = region;
        _minWhitePixels = minWhitePixels;
    }

    public void SetRegion(ScreenRegion region)
    {
        _region = region;
        _whiteSamples.Clear();
        _whiteBaseline = 0;
        _castPrevFrame?.Dispose();
        _castPrevFrame = null;
    }

    public bool LoadReferenceImage(byte[] bytes)
    {
        try
        {
            using var img = Cv2.ImDecode(bytes, ImreadModes.Color);
            if (img.Empty()) return false;
            _refLoaded = true;
            return true;
        }
        catch { return false; }
    }

    public bool LoadLureTemplate(byte[] bytes)
    {
        try
        {
            var img = Cv2.ImDecode(bytes, ImreadModes.Color);
            if (img.Empty()) { img.Dispose(); return false; }

            // Scale down to max 150px
            int maxDim = Math.Max(img.Width, img.Height);
            if (maxDim > 150)
            {
                double scale = 150.0 / maxDim;
                var scaled = new Mat();
                Cv2.Resize(img, scaled, new OpenCvSharp.Size(
                    (int)(img.Width * scale), (int)(img.Height * scale)));
                img.Dispose();
                _lureTemplate?.Dispose();
                _lureTemplate = scaled;
            }
            else
            {
                _lureTemplate?.Dispose();
                _lureTemplate = img;
            }
            return true;
        }
        catch { return false; }
    }

    private Bitmap? CaptureScreen(int x, int y, int w, int h)
    {
        try
        {
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h));
            return bmp;
        }
        catch { return null; }
    }

    private Mat? CaptureRegionColor()
    {
        if (_region == null) return null;
        using var bmp = CaptureScreen(_region.X, _region.Y, _region.W, _region.H);
        if (bmp == null) return null;
        var mat = BitmapConverter.ToMat(bmp);
        Cv2.CvtColor(mat, mat, ColorConversionCodes.BGRA2BGR);
        return mat;
    }

    private Mat? CaptureRegionGray()
    {
        if (_region == null) return null;
        using var bmp = CaptureScreen(_region.X, _region.Y, _region.W, _region.H);
        if (bmp == null) return null;
        using var mat = BitmapConverter.ToMat(bmp);
        var gray = new Mat();
        Cv2.CvtColor(mat, gray, ColorConversionCodes.BGRA2GRAY);
        return gray;
    }

    private (bool detected, int whiteCount) DetectWhiteSplash(Mat frameBgr)
    {
        using var hsv = new Mat();
        Cv2.CvtColor(frameBgr, hsv, ColorConversionCodes.BGR2HSV);
        using var mask = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 0, 200), new Scalar(180, 60, 255), mask);
        int whiteCount = Cv2.CountNonZero(mask);

        _whiteSamples.Add(whiteCount);
        if (_whiteSamples.Count > WhiteSamplesMax)
            _whiteSamples.RemoveAt(0);

        if (_whiteSamples.Count >= 10)
        {
            var sorted = _whiteSamples.Take(_whiteSamples.Count - 3).OrderBy(x => x).ToList();
            _whiteBaseline = sorted[sorted.Count / 2];
        }

        double threshold = Math.Max(_whiteBaseline * 2.0, _minWhitePixels);
        bool detected = whiteCount > threshold;

        if (detected)
            Console.WriteLine($"[Screen-Splash] Weiß={whiteCount}  Threshold={threshold:F0}  Baseline={_whiteBaseline:F0}");

        return (detected, whiteCount);
    }

    public (bool detected, double value) CheckForBite()
    {
        if (_region == null) return (false, 0);

        if (_refLoaded)
        {
            using var frame = CaptureRegionColor();
            if (frame == null) return (false, 0);
            var (det, count) = DetectWhiteSplash(frame);
            if (_calibrating) return (false, count);
            return (det, count);
        }
        return (false, 0);
    }

    public void StartCalibration()
    {
        _calibrating = true;
        _whiteSamples.Clear();
        _whiteBaseline = 0;
    }

    public void FinishCalibration()
    {
        _calibrating = false;
        if (_whiteSamples.Count > 0)
        {
            var sorted = _whiteSamples.OrderBy(x => x).ToList();
            _whiteBaseline = sorted[sorted.Count / 2];
        }
    }

    public bool IsSceneCalm()
    {
        if (!_refLoaded || _region == null) return true;
        using var frame = CaptureRegionColor();
        if (frame == null) return true;
        using var hsv = new Mat();
        Cv2.CvtColor(frame, hsv, ColorConversionCodes.BGR2HSV);
        using var mask = new Mat();
        Cv2.InRange(hsv, new Scalar(0, 0, 200), new Scalar(180, 60, 255), mask);
        int count = Cv2.CountNonZero(mask);
        double threshold = Math.Max(_whiteBaseline * 2.0, _minWhitePixels);
        return count < threshold;
    }

    public void ResetCastDetection()
    {
        _castPrevFrame?.Dispose();
        _castPrevFrame = null;
    }

    public bool CheckCastSplash(double changeThreshold = 3.0)
    {
        using var current = CaptureRegionGray();
        if (current == null) return false;

        if (_castPrevFrame == null)
        {
            _castPrevFrame = current.Clone();
            return false;
        }

        using var diff = new Mat();
        Cv2.Absdiff(_castPrevFrame, current, diff);
        using var thresh = new Mat();
        Cv2.Threshold(diff, thresh, 15, 255, ThresholdTypes.Binary);
        int changed = Cv2.CountNonZero(thresh);
        int total = Math.Max(current.Rows * current.Cols, 1);

        _castPrevFrame.Dispose();
        _castPrevFrame = current.Clone();

        return (changed / (double)total * 100) >= changeThreshold;
    }

    public (int cx, int cy, double confidence)? FindLureOnScreen(double minConfidence = 0.28)
    {
        if (_lureTemplate == null) return null;
        try
        {
            var screen = Screen.PrimaryScreen!;
            using var bmp = CaptureScreen(screen.Bounds.X, screen.Bounds.Y,
                screen.Bounds.Width, screen.Bounds.Height);
            if (bmp == null) return null;

            using var full = BitmapConverter.ToMat(bmp);
            using var fullGray = new Mat();
            Cv2.CvtColor(full, fullGray, ColorConversionCodes.BGRA2GRAY);

            using var templateGray = new Mat();
            Cv2.CvtColor(_lureTemplate, templateGray, ColorConversionCodes.BGR2GRAY);

            using var result = new Mat();
            Cv2.MatchTemplate(fullGray, templateGray, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out OpenCvSharp.Point maxLoc);

            Console.WriteLine($"[Screen] Template-Matching: confidence={maxVal:F3}");

            if (maxVal < minConfidence) return null;

            int cx = maxLoc.X + templateGray.Width / 2;
            int cy = maxLoc.Y + templateGray.Height / 2;
            return (cx, cy, maxVal);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Screen] Template-Matching Fehler: {e.Message}");
            return null;
        }
    }

    public ScreenRegion SetLureRegion(int cx, int cy, int padding = 250)
    {
        var screen = Screen.PrimaryScreen!;
        int x = Math.Max(0, cx - padding);
        int y = Math.Max(0, cy - padding);
        int w = Math.Min(padding * 2, screen.Bounds.Width - x);
        int h = Math.Min(padding * 2, screen.Bounds.Height - y);
        var region = new ScreenRegion { X = x, Y = y, W = w, H = h };
        SetRegion(region);
        Console.WriteLine($"[Screen] Region gesetzt: {x},{y}  {w}×{h}px");
        return region;
    }

    public double WhiteBaseline => _whiteBaseline;
    public int LastWhiteCount => _whiteSamples.Count > 0 ? (int)_whiteSamples[^1] : 0;
    public double WhiteThreshold => Math.Max(_whiteBaseline * 2.0, _minWhitePixels);
    public bool RefLoaded => _refLoaded;
    public bool HasLureTemplate => _lureTemplate != null;

    public void Dispose()
    {
        _lureTemplate?.Dispose();
        _castPrevFrame?.Dispose();
    }
}
