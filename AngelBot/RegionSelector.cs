using System.Drawing;
using System.Windows.Forms;

namespace AngelBot;

public class RegionSelector
{
    public event Action<ScreenRegion?>? RegionSelected;

    private Form? _form;
    private Point _start;
    private Rectangle _rect;
    private bool _drawing;

    public void Start()
    {
        _form = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Maximized,
            BackColor = Color.Black,
            Opacity = 0.4,
            TopMost = true,
            Cursor = Cursors.Cross,
            ShowInTaskbar = false,
        };

        _form.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { _form.Close(); RegionSelected?.Invoke(null); }
        };

        _form.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) { _start = e.Location; _drawing = true; }
        };

        _form.MouseMove += (_, e) =>
        {
            if (!_drawing) return;
            _rect = new Rectangle(
                Math.Min(_start.X, e.X), Math.Min(_start.Y, e.Y),
                Math.Abs(e.X - _start.X), Math.Abs(e.Y - _start.Y));
            _form.Invalidate();
        };

        _form.MouseUp += (_, e) =>
        {
            if (!_drawing || e.Button != MouseButtons.Left) return;
            _drawing = false;
            _form.Close();

            if (_rect.Width > 10 && _rect.Height > 10)
            {
                // Convert from form coordinates to screen coordinates
                var screenPoint = _form!.PointToScreen(_rect.Location);
                RegionSelected?.Invoke(new ScreenRegion
                {
                    X = screenPoint.X,
                    Y = screenPoint.Y,
                    W = _rect.Width,
                    H = _rect.Height,
                });
            }
            else RegionSelected?.Invoke(null);
        };

        _form.Paint += (_, e) =>
        {
            if (_drawing && _rect.Width > 0 && _rect.Height > 0)
            {
                using var pen = new Pen(Color.LimeGreen, 2);
                e.Graphics.DrawRectangle(pen, _rect);
                using var brush = new SolidBrush(Color.FromArgb(30, 0, 255, 0));
                e.Graphics.FillRectangle(brush, _rect);
            }
        };

        var hint = new Label
        {
            Text = "Bereich aufziehen  |  ESC = Abbrechen",
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            AutoSize = true,
        };
        _form.Controls.Add(hint);
        hint.Location = new Point((_form.Width - hint.PreferredWidth) / 2, 30);

        _form.ShowDialog();
    }
}
