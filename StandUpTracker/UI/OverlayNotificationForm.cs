using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using StandUpTracker.Config;

namespace StandUpTracker.UI
{
    internal sealed class OverlayNotificationForm : Form
    {
        private readonly System.Windows.Forms.Timer _closeTimer;

        public OverlayNotificationForm(string title, string text, int timeoutMs)
        {
            var background = ParseColorOrFallback(AppSettings.CustomNotificationBackgroundColorHex, Color.FromArgb(176, 0, 32));
            var foreground = ParseColorOrFallback(AppSettings.CustomNotificationTextColorHex, Color.White);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = background;
            Opacity = Math.Clamp(AppSettings.CustomNotificationOpacity, 0.15, 0.95);
            Width = AppSettings.CustomNotificationWidth;
            Height = AppSettings.CustomNotificationHeight;

            var titleLabel = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 30,
                Text = title,
                ForeColor = foreground,
                BackColor = background,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Padding = new Padding(12, 8, 12, 0)
            };

            var (mainText, detailText) = SplitMainAndDetail(text);

            var bodyTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = string.IsNullOrWhiteSpace(detailText) ? 1 : 3,
                BackColor = background,
                Padding = new Padding(12, 4, 12, 8)
            };

            bodyTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var mainLabel = new Label
            {
                AutoSize = true,
                Text = mainText,
                ForeColor = foreground,
                BackColor = background,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                Margin = new Padding(0)
            };
            bodyTable.Controls.Add(mainLabel, 0, 0);

            if (!string.IsNullOrWhiteSpace(detailText))
            {
                bodyTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                bodyTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 8));
                bodyTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                var spacer = new Panel
                {
                    Dock = DockStyle.Fill,
                    Height = 8,
                    BackColor = background,
                    Margin = new Padding(0)
                };

                var detailLabel = new Label
                {
                    AutoSize = true,
                    Text = detailText,
                    ForeColor = foreground,
                    BackColor = background,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    Margin = new Padding(0)
                };

                bodyTable.Controls.Add(spacer, 0, 1);
                bodyTable.Controls.Add(detailLabel, 0, 2);
            }
            else
            {
                bodyTable.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }

            Controls.Add(bodyTable);
            Controls.Add(titleLabel);

            PositionBottomRight();

            _closeTimer = new System.Windows.Forms.Timer { Interval = Math.Max(1200, timeoutMs) };
            _closeTimer.Tick += (s, e) =>
            {
                _closeTimer.Stop();
                Close();
            };
        }

        protected override bool ShowWithoutActivation => true;

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _closeTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _closeTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private static (string MainText, string DetailText) SplitMainAndDetail(string fullText)
        {
            if (string.IsNullOrWhiteSpace(fullText))
            {
                return (string.Empty, string.Empty);
            }

            var parts = fullText.Split(new[] { "\n" }, 2, StringSplitOptions.None);
            var main = parts[0].Trim();
            var detail = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            return (main, detail);
        }

        private static Color ParseColorOrFallback(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return fallback;

            try
            {
                return ColorTranslator.FromHtml(hex);
            }
            catch
            {
                return fallback;
            }
        }

        private void PositionBottomRight()
        {
            var area = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
            var x = area.Right - Width - AppSettings.CustomNotificationMarginRight;
            var y = area.Bottom - Height - AppSettings.CustomNotificationMarginBottom;
            Location = new Point(Math.Max(area.Left, x), Math.Max(area.Top, y));
        }
    }
}
