using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AngelBot;

public class InputController
{
    private static readonly Random Rng = new();

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    private const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP    = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP   = 0x0010;
    private const uint KEYEVENTF_KEYUP       = 0x0002;

    public bool HumanizeDelays { get; set; }
    public double HumanizeRange { get; set; }

    public InputController(bool humanize = false, double humanizeRange = 0.2)
    {
        HumanizeDelays = humanize;
        HumanizeRange = humanizeRange;
    }

    private void HumanizedDelay(double baseMs = 0)
    {
        double delay = baseMs;
        if (HumanizeDelays)
            delay += Rng.NextDouble() * HumanizeRange * 2000 - HumanizeRange * 1000;
        delay = Math.Max(50, delay);
        Thread.Sleep((int)delay);
    }

    public void CastClick()
    {
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
    }

    public void CastLine(double delaySeconds = 2.0)
    {
        CastClick();
        HumanizedDelay(delaySeconds * 1000);
    }

    public void ReelIn(double delaySeconds = 0.0)
    {
        if (delaySeconds > 0)
            HumanizedDelay(delaySeconds * 1000);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
    }

    public void RightClick()
    {
        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
    }

    public void MoveMouseTo(int x, int y)
    {
        GetCursorPos(out var current);
        int steps = Rng.Next(8, 16);
        for (int i = 1; i <= steps; i++)
        {
            double progress = (double)i / steps;
            progress = 1 - Math.Pow(1 - progress, 2); // ease-out
            int nx = (int)(current.X + (x - current.X) * progress);
            int ny = (int)(current.Y + (y - current.Y) * progress);
            SetCursorPos(nx, ny);
            Thread.Sleep(Rng.Next(5, 20));
        }
    }

    public void PressKey(Keys key)
    {
        byte vk = (byte)key;
        keybd_event(vk, 0, 0, IntPtr.Zero);
        Thread.Sleep(Rng.Next(50, 150));
        keybd_event(vk, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
        Thread.Sleep(Rng.Next(100, 300));
    }

    public void Wait(double seconds)
    {
        HumanizedDelay(seconds * 1000);
    }
}
