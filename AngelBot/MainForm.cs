using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AngelBot;

public class MainForm : Form
{
    private BotConfig _config;
    private BotEngine _bot;
    private Form? _overlay;
    private readonly System.Windows.Forms.Timer _runtimeTimer = new();
    private bool _recordingHotkey;

    // Dynamic references (reassigned on language rebuild)
    private Label  _lblRegion    = new();
    private Label  _lblState     = new();
    private Label  _lblFish      = new();
    private Label  _lblRuntime   = new();
    private Label  _lblFails     = new();
    private Label  _lblHotkeyDisplay = new();
    private Button _btnChangeHotkey  = new();
    private Button _btnStartStop     = new();
    private RichTextBox _rtbLog  = new();
    private RadioButton _rbScreen = new(), _rbAudio = new();

    // Hidden fillet controls (config persistence only)
    private CheckBox _cbAutoFillet;

    private const int ClientW = 420;
    private const int Cw      = 400;

    // ── Colors ───────────────────────────────────────────────────────────────
    private static readonly Color BgDark   = Color.FromArgb(13, 17, 23);
    private static readonly Color BgPanel  = Color.FromArgb(22, 27, 34);
    private static readonly Color BgInput  = Color.FromArgb(33, 38, 45);
    private static readonly Color Gold     = Color.FromArgb(212, 168, 67);
    private static readonly Color Green    = Color.FromArgb(63, 185, 80);
    private static readonly Color Red      = Color.FromArgb(248, 81, 73);
    private static readonly Color Blue     = Color.FromArgb(88, 166, 255);
    private static readonly Color TextDim  = Color.FromArgb(139, 148, 158);
    private static readonly Color TextMain = Color.FromArgb(230, 237, 243);

    public MainForm()
    {
        _config = BotConfig.Load();
        L.Set(_config.Language);

        _cbAutoFillet = new CheckBox { Checked = _config.AutoFillet };

        _bot = new BotEngine(_config);
        _bot.OnLog         = msg => Invoke(() => AppendLog(msg));
        _bot.OnStateChange = s   => Invoke(() => UpdateState(s));
        _bot.OnStatsUpdate = (f, _, t) => Invoke(() =>
        {
            _lblFish.Text  = f.ToString();
            _lblFails.Text = t.ToString();
        });

        Icon = MakeAppIcon();
        BuildUI();
        SetupHotkey();

        _runtimeTimer.Interval = 1000;
        _runtimeTimer.Tick    += (_, _) => { if (_bot.IsRunning) _lblRuntime.Text = Fmt(_bot.GetRuntime()); };
        _runtimeTimer.Start();
    }

    // ── Build UI ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        Text      = "Windrose Angelbot";
        BackColor = BgDark;
        ForeColor = TextMain;
        Font      = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox     = false;

        var main = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents  = false,
            AutoSize      = true,
            AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            Width         = ClientW,
            BackColor     = BgDark,
            Padding       = new Padding(10),
            Location      = new Point(0, 0),
        };
        Controls.Add(main);

        void Put(Control c, int top = 0, int bottom = 6)
        { c.Margin = new Padding(0, top, 0, bottom); main.Controls.Add(c); }

        Label Sep() => new() { Width = Cw, Height = 1, BackColor = Color.FromArgb(48, 54, 61) };

        void Section(string title, int topGap = 8)
        {
            Put(new Label
            {
                Text = $"  {title}",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Gold, BackColor = BgInput,
                Width = Cw, Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
            }, topGap, 6);
        }

        Button SmallBtn(string text, Color back, Color fore, int w = 46)
            => new()
            {
                Text = text, BackColor = back, ForeColor = fore,
                FlatStyle = FlatStyle.Flat, Width = w, Height = 24,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Cursor = Cursors.Hand, Margin = new Padding(3, 0, 0, 0),
            };

        // ── Header ───────────────────────────────────────────────────────────
        var headerTable = new TableLayoutPanel
        {
            Width = Cw, Height = 44,
            ColumnCount = 2, RowCount = 1,
            BackColor = BgDark,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
        };
        headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        headerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        // Left: ⚓ badge + title
        var titleFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            BackColor = BgDark,
            Padding = new Padding(0, 8, 0, 0),
        };
        var anchorBadge = new Label
        {
            Text = "⚓",
            Font = new Font("Segoe UI Symbol", 11f, FontStyle.Bold),
            BackColor = Gold, ForeColor = BgDark,
            Width = 26, Height = 26,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 6, 0),
        };
        var titleLbl = new Label
        {
            Text = "Windrose Angelbot",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = Gold, BackColor = BgDark,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
        };
        titleFlow.Controls.Add(anchorBadge);
        titleFlow.Controls.Add(titleLbl);
        headerTable.Controls.Add(titleFlow, 0, 0);

        // Right: Info, Hilfe, DE, EN
        var rightFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            BackColor = BgDark,
            Padding = new Padding(0, 10, 0, 0),
        };
        var btnInfo  = SmallBtn("Info",       BgPanel, TextMain, 42);
        var btnHelp  = SmallBtn(L.HelpBtn,    BgPanel, TextMain, 46);
        var btnDE    = SmallBtn("DE", _config.Language == "de" ? Gold : BgPanel,
                                      _config.Language == "de" ? BgDark : TextDim, 30);
        var btnEN    = SmallBtn("EN", _config.Language == "en" ? Gold : BgPanel,
                                      _config.Language == "en" ? BgDark : TextDim, 30);

        btnInfo.Click += (_, _) => ShowInfo();
        btnHelp.Click += (_, _) => ShowHelp();
        btnDE.Click   += (_, _) => SetLanguage("de");
        btnEN.Click   += (_, _) => SetLanguage("en");

        rightFlow.Controls.Add(btnInfo);
        rightFlow.Controls.Add(btnHelp);
        rightFlow.Controls.Add(btnDE);
        rightFlow.Controls.Add(btnEN);
        headerTable.Controls.Add(rightFlow, 1, 0);

        Put(headerTable, 0, 4);
        Put(Sep(), 0, 6);

        // ── Erkennung ────────────────────────────────────────────────────────
        Section(L.Detection, 2);

        // Methode
        _rbScreen = new RadioButton
        {
            Text = L.ScreenRb, ForeColor = TextMain, BackColor = BgPanel,
            AutoSize = true, Checked = _config.DetectionMethod != "audio",
            Margin = new Padding(0, 1, 14, 0),
        };
        _rbAudio = new RadioButton
        {
            Text = "Audio", ForeColor = TextMain, BackColor = BgPanel,
            AutoSize = true, Checked = _config.DetectionMethod == "audio",
            Margin = new Padding(0, 1, 0, 0),
        };
        var methodRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            AutoSize = false, Width = Cw, Height = 34,
            BackColor = BgPanel, Padding = new Padding(10, 7, 0, 0),
        };
        var mLbl = new Label { Text = L.Method, ForeColor = TextDim, BackColor = BgPanel, AutoSize = true };
        mLbl.Margin = new Padding(0, 1, 10, 0);
        methodRow.Controls.Add(mLbl);
        methodRow.Controls.Add(_rbScreen);
        methodRow.Controls.Add(_rbAudio);
        Put(methodRow, 0, 4);

        // Hotkey
        _lblHotkeyDisplay = new Label
        {
            Text = _config.StartStopHotkey.ToUpper(),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Gold, BackColor = BgPanel,
            AutoSize = true, Margin = new Padding(8, 1, 6, 0),
        };
        _btnChangeHotkey = new Button
        {
            Text = L.ChangeKey, BackColor = BgInput, ForeColor = TextMain,
            FlatStyle = FlatStyle.Flat, AutoSize = true, Height = 22,
            Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 0),
        };
        _btnChangeHotkey.Click += (_, _) => StartHotkeyRecording();

        var hotkeyRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
            AutoSize = false, Width = Cw, Height = 34,
            BackColor = BgPanel, Padding = new Padding(10, 6, 0, 0),
        };
        var hkLbl = new Label { Text = L.StartKey, ForeColor = TextDim, BackColor = BgPanel, AutoSize = true };
        hkLbl.Margin = new Padding(0, 1, 4, 0);
        hotkeyRow.Controls.Add(hkLbl);
        hotkeyRow.Controls.Add(_lblHotkeyDisplay);
        hotkeyRow.Controls.Add(_btnChangeHotkey);
        Put(hotkeyRow, 0, 6);

        // Region button
        var btnRegion = new Button
        {
            Text = L.DrawRegion, BackColor = BgInput, ForeColor = Gold,
            FlatStyle = FlatStyle.Flat, Width = Cw, Height = 34,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleCenter,
        };
        btnRegion.FlatAppearance.BorderColor = Gold;
        btnRegion.FlatAppearance.BorderSize  = 1;
        btnRegion.Click += (_, _) => SelectRegion();
        Put(btnRegion, 0, 4);

        _lblRegion = new Label
        {
            Text = GetRegionText(), ForeColor = TextDim, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f), Width = Cw, Height = 20,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Put(_lblRegion, 0, 0);

        // ── Status ───────────────────────────────────────────────────────────
        Section("Status");

        _lblState = new Label
        {
            Text = L.Ready, Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            ForeColor = TextDim, BackColor = Color.Transparent,
            Width = Cw, Height = 30, TextAlign = ContentAlignment.MiddleCenter,
        };
        Put(_lblState, 0, 6);

        // Stats TableLayoutPanel
        var statsTable = new TableLayoutPanel
        {
            Width = Cw, Height = 62, BackColor = BgPanel,
            ColumnCount = 3, RowCount = 2,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
        };
        statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        statsTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        statsTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        (string cap, string val, float fs)[] statDefs =
        [
            (L.FishCap,    "0",        16f),
            (L.RuntimeCap, "00:00:00", 13f),
            (L.ErrorsCap,  "0",        16f),
        ];
        var statVals = new Label[3];
        for (int i = 0; i < 3; i++)
        {
            statsTable.Controls.Add(new Label
            {
                Text = statDefs[i].cap, ForeColor = TextDim, BackColor = BgPanel,
                Font = new Font("Segoe UI", 9f),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomCenter,
            }, i, 0);

            statVals[i] = new Label
            {
                Text = statDefs[i].val, ForeColor = Gold, BackColor = BgPanel,
                Font = new Font("Segoe UI", statDefs[i].fs, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopCenter,
                Padding = new Padding(0, 2, 0, 0),
            };
            statsTable.Controls.Add(statVals[i], i, 1);
        }
        _lblFish    = statVals[0];
        _lblRuntime = statVals[1];
        _lblFails   = statVals[2];
        Put(statsTable, 0, 0);

        // ── Log ──────────────────────────────────────────────────────────────
        Section("Log");
        _rtbLog = new RichTextBox
        {
            BackColor = BgInput, ForeColor = TextMain,
            Font = new Font("Consolas", 9f),
            ReadOnly = true, Width = Cw, Height = 190,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
        };
        Put(_rtbLog, 0, 0);

        // ── Buttons ──────────────────────────────────────────────────────────
        Put(Sep(), 8, 6);

        var btnTable = new TableLayoutPanel
        {
            Width = Cw, Height = 44,
            ColumnCount = 2, RowCount = 1,
            BackColor = BgDark,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
        };
        btnTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68));
        btnTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

        _btnStartStop = new Button
        {
            Text = $"Start  ({_config.StartStopHotkey.ToUpper()})",
            BackColor = Green, ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Dock = DockStyle.Fill, Cursor = Cursors.Hand,
        };
        _btnStartStop.Click += (_, _) => ToggleBot();
        btnTable.Controls.Add(_btnStartStop, 0, 0);

        var btnSave = new Button
        {
            Text = L.Save, BackColor = BgInput, ForeColor = TextMain,
            FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10f),
            Dock = DockStyle.Fill, Cursor = Cursors.Hand,
            Margin = new Padding(5, 0, 0, 0),
        };
        btnSave.Click += (_, _) => SaveConfig();
        btnTable.Controls.Add(btnSave, 1, 0);
        Put(btnTable, 0, 4);

        Put(new Label
        {
            Text = $"{_config.StartStopHotkey.ToUpper()}: Start/Stop",
            ForeColor = TextDim, BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f),
            Width = Cw, Height = 20,
            TextAlign = ContentAlignment.MiddleCenter,
        }, 0, 0);

        AutoSize     = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    // ── Hotkey recording ─────────────────────────────────────────────────────

    private void StartHotkeyRecording()
    {
        if (_recordingHotkey) return;
        _recordingHotkey = true;
        _btnChangeHotkey.Text = L.PressKey;
        _lblHotkeyDisplay.Text = "…";
        KeyPreview = true;
        KeyDown += CaptureHotkey;
    }

    private void CaptureHotkey(object? sender, KeyEventArgs e)
    {
        if (!_recordingHotkey) return;
        _recordingHotkey = false;
        KeyDown -= CaptureHotkey;
        KeyPreview = false;
        _btnChangeHotkey.Text = L.ChangeKey;

        // Ignore system/control keys
        if (e.KeyCode is Keys.Escape or Keys.Return or Keys.LWin or Keys.RWin
                       or Keys.Apps or Keys.LMenu or Keys.RMenu)
        {
            _lblHotkeyDisplay.Text = _config.StartStopHotkey.ToUpper();
            return;
        }

        var keyStr = e.KeyCode.ToString();
        _config.StartStopHotkey = keyStr;
        _lblHotkeyDisplay.Text = keyStr.ToUpper();
        _btnStartStop.Text = _bot.IsRunning
            ? $"Stop  ({keyStr.ToUpper()})"
            : $"Start  ({keyStr.ToUpper()})";

        UnregisterHotKey(Handle, 1);
        RegisterHotKey(Handle, 1, 0, (uint)(int)e.KeyCode);

        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    // ── Language switching ────────────────────────────────────────────────────

    private void SetLanguage(string lang)
    {
        if (_config.Language == lang) return;

        if (_recordingHotkey)
        {
            _recordingHotkey = false;
            KeyDown -= CaptureHotkey;
            KeyPreview = false;
        }

        _config.Language = lang;
        L.Set(lang);

        UnregisterHotKey(Handle, 1);
        _runtimeTimer.Stop();
        Controls.Clear();
        BuildUI();
        SetupHotkey();
        _runtimeTimer.Start();

        UpdateState(_bot.State);
        _lblRegion.Text = GetRegionText();

        if (_bot.IsRunning)
        {
            _btnStartStop.Text     = $"Stop  ({_config.StartStopHotkey.ToUpper()})";
            _btnStartStop.BackColor = Red;
        }
    }

    // ── Bot control ───────────────────────────────────────────────────────────

    private void SelectRegion()
    {
        Hide();
        var sel = new RegionSelector();
        sel.RegionSelected += region =>
        {
            _config.ScreenRegion = region;
            Invoke(() =>
            {
                Show(); Activate();
                _lblRegion.Text = GetRegionText();
                if (region != null) ShowOverlay(region.X, region.Y, region.W, region.H);
            });
        };
        sel.Start();
    }

    private void ToggleBot()
    {
        if (_bot.IsRunning)
        {
            // UI sofort aktualisieren — kein Warten auf Cleanup
            _btnStartStop.Text      = $"Start  ({_config.StartStopHotkey.ToUpper()})";
            _btnStartStop.BackColor = Green;
            HideOverlay();
            Task.Run(() => _bot.Stop());
        }
        else
        {
            ApplyConfig(); _bot.Config = _config; _bot.Start();
            // Button nur auf Stop setzen wenn der Bot wirklich gestartet hat
            if (_bot.IsRunning)
            {
                _btnStartStop.Text      = $"Stop  ({_config.StartStopHotkey.ToUpper()})";
                _btnStartStop.BackColor = Red;
                // Overlay nur bei Bildschirm-Erkennung anzeigen
                if (_config.DetectionMethod != "audio")
                {
                    var r = _config.ScreenRegion;
                    if (r != null) ShowOverlay(r.X, r.Y, r.W, r.H);
                }
            }
        }
    }

    private void SaveConfig()
    {
        ApplyConfig(); _config.Save();
        AppendLog($"[{DateTime.Now:HH:mm:ss}] Config gespeichert");
    }

    private void ApplyConfig()
    {
        _config.DetectionMethod = _rbAudio.Checked ? "audio" : "screen";
        _config.AutoFillet = _cbAutoFillet.Checked;
    }

    // ── Log / State ───────────────────────────────────────────────────────────

    private void AppendLog(string message)
    {
        if (InvokeRequired) { Invoke(() => AppendLog(message)); return; }
        _rtbLog.SuspendLayout();
        _rtbLog.SelectionStart = _rtbLog.TextLength;
        _rtbLog.SelectionColor = message.Contains("BISS") || message.Contains("BITE") ? Red
            : message.Contains("✓") ? Green
            : message.Contains("⚠") ? Color.FromArgb(210, 153, 34)
            : TextMain;
        _rtbLog.AppendText(message + "\n");
        _rtbLog.ScrollToCaret();
        if (_rtbLog.Lines.Length > 200)
        {
            _rtbLog.Select(0, _rtbLog.GetFirstCharIndexFromLine(_rtbLog.Lines.Length - 200));
            _rtbLog.SelectedText = "";
        }
        _rtbLog.ResumeLayout();
    }

    private void UpdateState(BotState state)
    {
        (_lblState.Text, _lblState.ForeColor) = state switch
        {
            BotState.Casting      => (L.Casting,   Blue),
            BotState.Waiting      => (L.Waiting,   Color.FromArgb(210, 153, 34)),
            BotState.BiteDetected => (L.Bite,      Red),
            BotState.Reeling      => (L.Reeling,   Green),
            BotState.Filleting    => (L.Filleting, Color.FromArgb(188, 140, 255)),
            _                     => (L.Ready,     TextDim),
        };
    }

    // ── Overlay ───────────────────────────────────────────────────────────────

    private void ShowOverlay(int x, int y, int w, int h)
    {
        HideOverlay();
        _overlay = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition   = FormStartPosition.Manual,
            Location = new Point(x, y), Size = new Size(w, h),
            BackColor = Color.Black, TransparencyKey = Color.Black,
            TopMost = true, ShowInTaskbar = false,
        };
        _overlay.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.LimeGreen, 3);
            e.Graphics.DrawRectangle(pen, 2, 2, w - 4, h - 4);
            using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            e.Graphics.DrawString("Erkennung", font, Brushes.LimeGreen, w / 2 - 38, 5);
        };
        _overlay.Show();
    }

    private void HideOverlay() { _overlay?.Close(); _overlay?.Dispose(); _overlay = null; }

    // ── Info dialog ───────────────────────────────────────────────────────────

    private void ShowInfo()
    {
        var dlg = MakeDialog(L.InfoTitle, 320, 360);

        var anchorLbl = new Label
        {
            Text = "⚓", Font = new Font("Segoe UI Symbol", 32f),
            ForeColor = Gold, BackColor = Color.Transparent,
            AutoSize = true, Margin = new Padding(0, 10, 0, 6),
        };
        dlg.main.Controls.Add(anchorLbl);

        var body = new Label
        {
            Text = L.InfoText,
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = TextMain, BackColor = Color.Transparent,
            AutoSize = false, Width = 280,
            TextAlign = ContentAlignment.TopCenter,
            Margin = new Padding(0, 0, 0, 16),
        };
        body.Height = body.GetPreferredSize(new Size(280, 0)).Height + 8;
        dlg.main.Controls.Add(body);

        var ok = MakeDialogBtn(L.IsDE ? "Schließen" : "Close");
        ok.Click += (_, _) => dlg.form.Close();
        dlg.main.Controls.Add(ok);

        dlg.form.ShowDialog(this);
    }

    // ── Help dialog ───────────────────────────────────────────────────────────

    private void ShowHelp()
    {
        var dlg = MakeDialog(L.HelpTitle, 360, 500);

        foreach (var (title, body, recommended) in L.HelpSections)
        {
            if (recommended)
            {
                var recBadge = new Label
                {
                    Text = L.IsDE ? "⭐  Empfohlen" : "⭐  Recommended",
                    Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                    ForeColor = BgDark, BackColor = Gold,
                    AutoSize = false, Width = 320, Height = 20,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(0, 8, 0, 0),
                };
                dlg.main.Controls.Add(recBadge);
            }
            else
            {
                dlg.main.Controls.Add(new Label
                {
                    Height = 8, Width = 320, BackColor = Color.Transparent,
                    Margin = new Padding(0),
                });
            }

            var titleLbl = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Gold, BackColor = Color.FromArgb(33, 38, 45),
                AutoSize = false, Width = 320, Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 0, 0),
                Margin = new Padding(0, 0, 0, 4),
            };
            dlg.main.Controls.Add(titleLbl);

            var bodyLbl = new Label
            {
                Text = body,
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextMain, BackColor = Color.Transparent,
                AutoSize = false, Width = 320,
                TextAlign = ContentAlignment.TopLeft,
                Margin = new Padding(0, 0, 0, 6),
            };
            bodyLbl.Height = bodyLbl.GetPreferredSize(new Size(320, 0)).Height + 4;
            dlg.main.Controls.Add(bodyLbl);
        }

        var ok = MakeDialogBtn(L.IsDE ? "Schließen" : "Close");
        ok.Margin = new Padding(0, 10, 0, 0);
        ok.Click += (_, _) => dlg.form.Close();
        dlg.main.Controls.Add(ok);

        dlg.form.ShowDialog(this);
    }

    // ── Dialog helpers ────────────────────────────────────────────────────────

    private (Form form, FlowLayoutPanel main) MakeDialog(string title, int w, int h)
    {
        var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = BgDark, ForeColor = TextMain,
            Font = new Font("Segoe UI", 10f),
            ClientSize = new Size(w, h),
            Icon = Icon,
        };

        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = w,
            BackColor = BgDark,
            Padding = new Padding(20, 16, 20, 16),
        };
        form.Controls.Add(flow);
        form.AutoSize = true;
        form.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        return (form, flow);
    }

    private static Button MakeDialogBtn(string text)
        => new()
        {
            Text = text, BackColor = Color.FromArgb(33, 38, 45), ForeColor = TextMain,
            FlatStyle = FlatStyle.Flat, Width = 120, Height = 34,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold), Cursor = Cursors.Hand,
        };

    // ── Window icon ───────────────────────────────────────────────────────────

    private static Icon MakeAppIcon()
    {
        try
        {
            using var bmp = new Bitmap(32, 32);
            using var g   = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(13, 17, 23));
            using var f = new Font("Segoe UI Symbol", 17f);
            g.DrawString("⚓", f, new SolidBrush(Color.FromArgb(212, 168, 67)), 2, 5);
            var hIcon = bmp.GetHicon();
            var icon  = (Icon)Icon.FromHandle(hIcon).Clone();
            DestroyIcon(hIcon);
            return icon;
        }
        catch { return SystemIcons.Application; }
    }

    // ── Hotkey P/Invoke ───────────────────────────────────────────────────────

    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    private void SetupHotkey()
        => RegisterHotKey(Handle, 1, 0, (uint)ParseKey(_config.StartStopHotkey));

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1)
            Invoke(ToggleBot);
        base.WndProc(ref m);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int ParseKey(string key)
        => Enum.TryParse<Keys>(key, true, out var k) ? (int)k : (int)Keys.F6;

    private string GetRegionText()
    {
        var r = _config.ScreenRegion;
        return r != null ? L.RegionSet(r.X, r.Y, r.W, r.H) : L.NoRegion;
    }

    private static string Fmt(TimeSpan t)
        => $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (_recordingHotkey) { KeyDown -= CaptureHotkey; }
        UnregisterHotKey(Handle, 1);
        _bot.Stop(); HideOverlay(); _runtimeTimer.Stop();
        base.OnFormClosed(e);
    }
}
