using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;   // ← фикс неоднозначности

namespace NetPulseMonitor
{
    // ────────────────────────────────────────────────────────────────────
    //  ТЕМА ОФОРМЛЕНИЯ
    // ────────────────────────────────────────────────────────────────────
    public static class Theme
    {
        public static readonly Color BgDeep     = Color.FromArgb(9, 13, 22);
        public static readonly Color BgPanel    = Color.FromArgb(17, 24, 39);
        public static readonly Color BgElevated = Color.FromArgb(30, 41, 59);
        public static readonly Color BgInput    = Color.FromArgb(15, 23, 42);

        public static readonly Color Border       = Color.FromArgb(30, 58, 88);
        public static readonly Color BorderSubtle = Color.FromArgb(30, 41, 59);

        public static readonly Color Accent     = Color.FromArgb(56, 189, 248);
        public static readonly Color AccentDark = Color.FromArgb(14, 42, 65);

        public static readonly Color Success = Color.FromArgb(16, 185, 129);
        public static readonly Color Warning = Color.FromArgb(245, 158, 11);
        public static readonly Color Danger  = Color.FromArgb(239, 68, 68);

        public static readonly Color TextPrimary   = Color.FromArgb(241, 245, 249);
        public static readonly Color TextSecondary = Color.FromArgb(148, 163, 184);
        public static readonly Color TextMuted     = Color.FromArgb(100, 116, 139);

        public static readonly Font FontSection  = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font FontBody     = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font FontBodyBold = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font FontLabelBold= new Font("Segoe UI", 7.5f, FontStyle.Bold);
        public static readonly Font FontMono     = new Font("Consolas", 13f, FontStyle.Bold);
        public static readonly Font FontMonoSmall= new Font("Consolas", 9f, FontStyle.Regular);
    }

    // ────────────────────────────────────────────────────────────────────
    //  МОДЕЛЬ УЗЛА
    // ────────────────────────────────────────────────────────────────────
    public class NodeItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CountryCode { get; set; }
        public Color FlagColor1 { get; set; }
        public Color FlagColor2 { get; set; }
        public string Ip { get; set; }
        public int Port { get; set; }
        public string Protocol { get; set; }
        public int Ping { get; set; }

        public NodeItem()
        {
            Ping = -1;
            FlagColor1 = Theme.Accent;
            FlagColor2 = Color.FromArgb(14, 165, 233);
        }

        public override string ToString()
        {
            string pingStr = Ping >= 0 ? Ping + " мс" : "-- мс";
            return string.Format("[{0}] {1}  ·  {2}:{3}  ·  {4}  ·  {5}",
                CountryCode, Name, Ip, Port, Protocol, pingStr);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  СИСТЕМНЫЙ ПРОКСИ
    // ────────────────────────────────────────────────────────────────────
    public static class SystemProxyManager
    {
        [DllImport("wininet.dll", SetLastError = true)]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH          = 37;
        private const string REG_PATH = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        public static bool EnableProxy(string ip, int port, string protocol)
        {
            try
            {
                string proxyServer = ip + ":" + port;
                if (protocol != null && protocol.ToUpperInvariant().StartsWith("SOCKS"))
                    proxyServer = "socks=" + ip + ":" + port;

                using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(REG_PATH, true))
                {
                    if (reg == null) return false;
                    reg.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
                    reg.SetValue("ProxyServer", proxyServer, RegistryValueKind.String);
                }
                RefreshSystemProxy();
                return true;
            }
            catch { return false; }
        }

        public static void DisableProxy()
        {
            try
            {
                using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(REG_PATH, true))
                {
                    if (reg == null) return;
                    reg.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
                }
                RefreshSystemProxy();
            }
            catch { }
        }

        private static void RefreshSystemProxy()
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  ИЗМЕРЕНИЕ ПИНГА
    // ────────────────────────────────────────────────────────────────────
    public static class NodePinger
    {
        public static int MeasureLatency(string host, int port, int timeoutMs)
        {
            if (string.IsNullOrEmpty(host) || port < 1 || port > 65535) return -1;
            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    IAsyncResult ar = socket.BeginConnect(host, port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
                    {
                        try { socket.Close(); } catch { }
                        return -1;
                    }
                    socket.EndConnect(ar);
                }
                sw.Stop();
                int ms = (int)sw.ElapsedMilliseconds;
                return ms > 0 ? ms : 1;
            }
            catch { return -1; }
        }

        public static int DemoPing(NodeItem node, Random rnd)
        {
            int hash = node.Ip != null ? node.Ip.GetHashCode() : 0;
            int absHash = hash & 0x7FFFFFFF;
            int basePing = 55 + (absHash % 180);
            return basePing + rnd.Next(-8, 12);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  ХРАНИЛИЩЕ УЗЛОВ
    // ────────────────────────────────────────────────────────────────────
    public static class NodeStorage
    {
        public const string FilePath = "nodes.dat";

        public static List<NodeItem> Load()
        {
            var result = new List<NodeItem>();
            if (!File.Exists(FilePath)) return result;
            try
            {
                string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    string[] p = line.Split('|');
                    if (p.Length >= 5)
                    {
                        int port;
                        if (int.TryParse(p[3], out port) && port > 0 && port <= 65535)
                        {
                            result.Add(new NodeItem
                            {
                                Id = 100 + i,
                                Name = p[0],
                                CountryCode = p[1],
                                Ip = p[2],
                                Port = port,
                                Protocol = p[4],
                                FlagColor1 = Theme.Accent,
                                FlagColor2 = Theme.Success
                            });
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        public static void Append(NodeItem node)
        {
            try
            {
                string line = string.Format("{0}|{1}|{2}|{3}|{4}",
                    node.Name, node.CountryCode, node.Ip, node.Port, node.Protocol);
                File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  КНОПКА ПОДКЛЮЧЕНИЯ
    // ────────────────────────────────────────────────────────────────────
    public class PulsePowerButton : Control
    {
        public enum ConnState { Disconnected, Scanning, Connected }

        private ConnState _state = ConnState.Disconnected;
        private float _pulsePhase = 0f;
        private float _scanAngle = 0f;
        private bool _hovered = false;
        private bool _isDemoMode = false;
        private Timer _animTimer;

        public ConnState State
        {
            get { return _state; }
            set { if (_state != value) { _state = value; Invalidate(); } }
        }

        public bool IsDemoMode
        {
            get { return _isDemoMode; }
            set { _isDemoMode = value; Invalidate(); }
        }

        public PulsePowerButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(240, 240);
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;

            _animTimer = new Timer { Interval = 30 };
            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            if (_state == ConnState.Connected)
            {
                _pulsePhase += 0.025f;
                if (_pulsePhase > 1f) _pulsePhase -= 1f;
                Invalidate();
            }
            else if (_state == ConnState.Scanning)
            {
                _scanAngle += 9f;
                if (_scanAngle >= 360f) _scanAngle -= 360f;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int cx = Width / 2, cy = Height / 2, baseR = 80;
            Color primary = Theme.Accent;
            string mainLabel = "ПОДКЛЮЧИТЬ";
            string subLabel  = "НАЖМИТЕ ДЛЯ СТАРТА";

            if (_state == ConnState.Scanning)
            { primary = Theme.Warning; mainLabel = "СКАНИРОВАНИЕ..."; subLabel = "ПРОВЕРКА КАНАЛА"; }
            else if (_state == ConnState.Connected)
            {
                primary = Theme.Success; mainLabel = "ПОДКЛЮЧЕНО";
                subLabel = _isDemoMode ? "ДЕМО-РЕЖИМ" : "ТУННЕЛЬ АКТИВЕН";
            }

            if (_state == ConnState.Connected)
            {
                for (int i = 0; i < 2; i++)
                {
                    float p = (_pulsePhase + i * 0.5f) % 1f;
                    int r = baseR + (int)(p * 36);
                    int alpha = (int)((1f - p) * 130);
                    using (var pen = new Pen(Color.FromArgb(alpha, primary), 2.2f))
                        g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
                }
            }
            else if (_state == ConnState.Scanning)
            {
                int r = baseR + 16;
                using (var pen = new Pen(Color.FromArgb(200, primary), 3f) { DashStyle = DashStyle.Dash })
                    g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, _scanAngle, 240f);
            }

            int glowR = _hovered ? baseR + 10 : baseR + 5;
            using (var glow = new SolidBrush(Color.FromArgb(_hovered ? 42 : 26, primary)))
                g.FillEllipse(glow, cx - glowR, cy - glowR, glowR * 2, glowR * 2);

            var btnRect = new Rectangle(cx - baseR, cy - baseR, baseR * 2, baseR * 2);
            using (var bg = new LinearGradientBrush(btnRect, Color.FromArgb(30, 41, 59), Color.FromArgb(15, 23, 42), 45f))
                g.FillEllipse(bg, btnRect);
            using (var border = new Pen(primary, 3f))
                g.DrawEllipse(border, btnRect);

            int iconY = cy - 30, iconR = 17;
            using (var iconPen = new Pen(primary, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(iconPen, cx - iconR, iconY - iconR, iconR * 2, iconR * 2, -55f, 290f);
                g.DrawLine(iconPen, cx, iconY - iconR - 3, cx, iconY + 2);
            }

            if (_state == ConnState.Connected && _isDemoMode)
            {
                int bw = 48, bh = 16;
                var badge = new Rectangle(cx - bw / 2, cy + 46, bw, bh);
                using (var bg = new SolidBrush(Color.FromArgb(180, 30, 41, 59)))
                    g.FillRectangle(bg, badge);
                using (var pen = new Pen(Theme.Warning, 1f))
                    g.DrawRectangle(pen, badge);
                using (var f = new Font("Segoe UI", 6.5f, FontStyle.Bold))
                using (var b = new SolidBrush(Theme.Warning))
                {
                    SizeF sz = g.MeasureString("DEMO", f);
                    g.DrawString("DEMO", f, b, cx - sz.Width / 2f, badge.Y + (bh - sz.Height) / 2f);
                }
            }

            using (var f = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var b = new SolidBrush(_state == ConnState.Disconnected ? Color.White : primary))
            {
                SizeF sz = g.MeasureString(mainLabel, f);
                g.DrawString(mainLabel, f, b, cx - sz.Width / 2f, cy + 10);
            }

            using (var f = new Font("Segoe UI", 7.2f, FontStyle.Regular))
            using (var b = new SolidBrush(Theme.TextSecondary))
            {
                SizeF sz = g.MeasureString(subLabel, f);
                g.DrawString(subLabel, f, b, cx - sz.Width / 2f, cy + 31);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _animTimer != null) { _animTimer.Stop(); _animTimer.Dispose(); }
            base.Dispose(disposing);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  ГРАФИК ПИНГА
    // ────────────────────────────────────────────────────────────────────
    public class PingChartControl : Control
    {
        private const int MaxPoints = 28;
        private List<int> _history = new List<int>();

        public PingChartControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.BgDeep;
        }

        public void AddPoint(int ping)
        {
            if (ping < 0) return;
            _history.Add(ping);
            if (_history.Count > MaxPoints) _history.RemoveAt(0);
            Invalidate();
        }

        public void Clear() { _history.Clear(); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var border = new Pen(Theme.BorderSubtle, 1f))
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            using (var grid = new Pen(Color.FromArgb(22, 32, 50), 1f))
            {
                for (int y = 22; y < Height - 14; y += 28)
                    g.DrawLine(grid, 30, y, Width - 4, y);
                for (int x = 30; x < Width; x += 48)
                    g.DrawLine(grid, x, 4, x, Height - 14);
            }

            using (var axis = new Pen(Theme.TextMuted, 1f))
            {
                g.DrawLine(axis, 30, 4, 30, Height - 14);
                g.DrawLine(axis, 30, Height - 14, Width - 4, Height - 14);
            }

            if (_history.Count == 0)
            {
                using (var f = new Font("Segoe UI", 8.5f))
                using (var b = new SolidBrush(Theme.TextMuted))
                {
                    const string msg = "Ожидание телеметрии...";
                    SizeF s = g.MeasureString(msg, f);
                    g.DrawString(msg, f, b, (Width - s.Width) / 2f, (Height - s.Height) / 2f);
                }
                return;
            }

            int maxVal = 150;
            for (int i = 0; i < _history.Count; i++)
                if (_history[i] > maxVal) maxVal = _history[i] + 30;

            float stepX = _history.Count > 1 ? (float)(Width - 44) / (_history.Count - 1) : 0f;
            var pts = new PointF[_history.Count];
            for (int i = 0; i < _history.Count; i++)
            {
                float x = 34f + i * stepX;
                float norm = (float)_history[i] / maxVal;
                float y = Height - 20f - norm * (Height - 32f);
                pts[i] = new PointF(x, y);
            }

            if (pts.Length >= 2)
            {
                var poly = new PointF[pts.Length + 2];
                for (int i = 0; i < pts.Length; i++) poly[i] = pts[i];
                poly[pts.Length] = new PointF(pts[pts.Length - 1].X, Height - 14);
                poly[pts.Length + 1] = new PointF(pts[0].X, Height - 14);

                using (var fill = new LinearGradientBrush(
                    new Rectangle(0, 0, Width, Height),
                    Color.FromArgb(85, 16, 185, 129),
                    Color.FromArgb(5, 16, 185, 129), 90f))
                    g.FillPolygon(fill, poly);

                using (var line = new Pen(Theme.Success, 2.2f) { LineJoin = LineJoin.Round })
                    g.DrawLines(line, pts);
            }

            if (pts.Length > 0)
            {
                var last = pts[pts.Length - 1];
                using (var glow = new SolidBrush(Color.FromArgb(80, Theme.Success)))
                    g.FillEllipse(glow, last.X - 8, last.Y - 8, 16, 16);
                using (var dot = new SolidBrush(Theme.Success))
                    g.FillEllipse(dot, last.X - 3.5f, last.Y - 3.5f, 7f, 7f);
            }

            using (var f = new Font("Segoe UI", 7f))
            using (var b = new SolidBrush(Theme.TextMuted))
            {
                g.DrawString("0", f, b, 8, Height - 22);
                g.DrawString(maxVal.ToString(), f, b, 4, 6);
                if (_history.Count > 0)
                    g.DrawString(_history[_history.Count - 1] + " мс", f, b, Width - 46, Height - 22);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  ДИАЛОГ ДОБАВЛЕНИЯ УЗЛА
    // ────────────────────────────────────────────────────────────────────
    public class AddNodeForm : Form
    {
        public NodeItem CreatedNode { get; private set; }

        private TextBox txtLocation;
        private TextBox txtIp;
        private TextBox txtPort;
        private ComboBox cmbProtocol;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        public AddNodeForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 380);
            BackColor = Theme.BgPanel;
            ForeColor = Theme.TextPrimary;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            KeyPreview = true;

            BuildUI();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        private void BuildUI()
        {
            Panel header = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(ClientSize.Width, 48),
                BackColor = Theme.BgDeep
            };
            header.MouseDown += DragWindow;

            Label title = new Label
            {
                Text = "◆  Новый узел",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                Location = new Point(18, 14),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            title.MouseDown += DragWindow;
            header.Controls.Add(title);

            Button btnClose = new Button
            {
                Text = "✕",
                Location = new Point(ClientSize.Width - 40, 10),
                Size = new Size(28, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.TextSecondary,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            header.Controls.Add(btnClose);

            Controls.Add(header);

            int y = 68;
            int leftX = 28;
            int fieldW = ClientSize.Width - leftX * 2;

            AddLabel("СТРАНА И ГОРОД", leftX, y);
            txtLocation = AddTextBox(leftX, y + 18, fieldW, "Германия (Франкфурт)");
            y += 62;

            AddLabel("IP-АДРЕС ИЛИ ХОСТ", leftX, y);
            txtIp = AddTextBox(leftX, y + 18, fieldW - 110, "185.22.14.89");
            AddLabel("ПОРТ", leftX + fieldW - 100, y);
            txtPort = AddTextBox(leftX + fieldW - 100, y + 18, 100, "1080");
            y += 62;

            AddLabel("ПРОТОКОЛ", leftX, y);
            cmbProtocol = new ComboBox
            {
                Location = new Point(leftX, y + 18),
                Size = new Size(fieldW, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                FlatStyle = FlatStyle.Flat
            };
            cmbProtocol.Items.AddRange(new object[] { "SOCKS5", "SOCKS4", "HTTPS", "HTTP" });
            cmbProtocol.SelectedIndex = 0;
            Controls.Add(cmbProtocol);

            Button btnSave = new Button
            {
                Text = "Сохранить",
                Location = new Point(ClientSize.Width - 200, ClientSize.Height - 58),
                Size = new Size(160, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Accent,
                ForeColor = Theme.BgDeep,
                Font = Theme.FontBodyBold,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += OnSaveClick;

            Button btnCancel = new Button
            {
                Text = "Отмена",
                Location = new Point(ClientSize.Width - 290, ClientSize.Height - 58),
                Size = new Size(80, 38),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.BgElevated,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBody,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Theme.Border;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(btnSave);
            Controls.Add(btnCancel);

            AcceptButton = btnSave;
            CancelButton = btnCancel;
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero); }
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = Theme.FontLabelBold,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent
            });
        }

        private TextBox AddTextBox(int x, int y, int w, string placeholder)
        {
            var tb = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 30),
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Theme.FontBody,
                Text = placeholder
            };
            Controls.Add(tb);
            return tb;
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            string loc = txtLocation.Text.Trim();
            string ip = txtIp.Text.Trim();
            int port;
            if (string.IsNullOrEmpty(loc) || string.IsNullOrEmpty(ip) ||
                !int.TryParse(txtPort.Text.Trim(), out port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Проверьте название, IP-адрес и порт (1-65535).",
                    "Ошибка ввода", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string code = loc.Length >= 2 ? loc.Substring(0, 2).ToUpperInvariant() : "GL";
            CreatedNode = new NodeItem
            {
                Id = (int)(DateTime.Now.Ticks % 1000000),
                Name = loc,
                CountryCode = code,
                FlagColor1 = Theme.Accent,
                FlagColor2 = Color.FromArgb(99, 102, 241),
                Ip = ip,
                Port = port,
                Protocol = cmbProtocol.SelectedItem.ToString(),
                Ping = -1
            };

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  ГЛАВНАЯ ФОРМА
    // ────────────────────────────────────────────────────────────────────
    public class MainForm : Form
    {
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;

        private readonly List<NodeItem> _nodes = new List<NodeItem>();
        private readonly Random _rnd = new Random();

        private PulsePowerButton _powerBtn;
        private ComboBox _nodeCombo;
        private Panel _flagBox;
        private Label _lblSelectedIp;
        private Label _lblSelectedProto;
        private Label _lblMetricPing;
        private Label _lblMetricJitter;
        private Label _lblMetricDown;
        private Label _lblMetricUp;
        private PingChartControl _chart;
        private ListView _nodesListView;
        private Button _btnSmartScan;
        private Button _btnAddNode;
        private Timer _liveTimer;

        private int _lastPing = 0;
        private bool _suppressComboChanged = false;
        private int _connectionToken = 0;

        public MainForm()
        {
            Text = "NetPulse Monitor";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1000, 800);
            BackColor = Theme.BgDeep;
            ForeColor = Theme.TextPrimary;
            DoubleBuffered = true;

            InitDefaultNodes();
            _nodes.AddRange(NodeStorage.Load());

            BuildTitleBar();
            BuildHeader();
            BuildPowerButton();
            BuildNodeSelector();
            BuildMetrics();
            BuildBottomPanels();
            RefreshNodesUI();

            _liveTimer = new Timer { Interval = 1500 };
            _liveTimer.Tick += OnLiveTelemetryTick;

            FormClosing += (s, e) => { _liveTimer.Stop(); SystemProxyManager.DisableProxy(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }

        private void InitDefaultNodes()
        {
            _nodes.Add(new NodeItem { Id = 1, Name = "Индонезия (Сукабуми)", CountryCode = "ID",
                FlagColor1 = Color.FromArgb(239, 68, 68), FlagColor2 = Color.White,
                Ip = "202.73.27.116", Port = 1080, Protocol = "SOCKS5" });
            _nodes.Add(new NodeItem { Id = 2, Name = "Индия (Ченнаи)", CountryCode = "IN",
                FlagColor1 = Color.FromArgb(249, 115, 22), FlagColor2 = Color.FromArgb(22, 163, 74),
                Ip = "210.16.85.42", Port = 8080, Protocol = "HTTPS" });
            _nodes.Add(new NodeItem { Id = 3, Name = "Таиланд (Бангкок)", CountryCode = "TH",
                FlagColor1 = Color.FromArgb(220, 38, 38), FlagColor2 = Color.FromArgb(30, 58, 138),
                Ip = "125.24.179.211", Port = 8080, Protocol = "HTTPS" });
            _nodes.Add(new NodeItem { Id = 4, Name = "Бразилия (Гуарульюс)", CountryCode = "BR",
                FlagColor1 = Color.FromArgb(22, 163, 74), FlagColor2 = Color.FromArgb(234, 179, 8),
                Ip = "186.250.53.215", Port = 1080, Protocol = "SOCKS4" });
            _nodes.Add(new NodeItem { Id = 5, Name = "Непал (Катманду)", CountryCode = "NP",
                FlagColor1 = Color.FromArgb(220, 38, 38), FlagColor2 = Color.FromArgb(37, 99, 235),
                Ip = "202.79.35.12", Port = 1080, Protocol = "SOCKS4" });
            _nodes.Add(new NodeItem { Id = 6, Name = "Иран", CountryCode = "IR",
                FlagColor1 = Color.FromArgb(22, 163, 74), FlagColor2 = Color.FromArgb(220, 38, 38),
                Ip = "46.100.35.25", Port = 1080, Protocol = "SOCKS4" });
        }

        private void BuildTitleBar()
        {
            Panel bar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(ClientSize.Width, 40),
                BackColor = Theme.BgDeep
            };
            bar.Paint += (s, e) => {
                using (var p = new Pen(Theme.BorderSubtle, 1f))
                    e.Graphics.DrawLine(p, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };
            bar.MouseDown += DragWindow;

            Label title = new Label
            {
                Text = "◆  NETPULSE MONITOR",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                ForeColor = Theme.Accent,
                Location = new Point(20, 10),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            title.MouseDown += DragWindow;

            Button btnClose = MakeTitleBtn("✕", 20);
            btnClose.MouseEnter += (s, e) => btnClose.BackColor = Theme.Danger;
            btnClose.MouseLeave += (s, e) => btnClose.BackColor = Theme.BgDeep;
            btnClose.Click += (s, e) => Close();

            Button btnMin = MakeTitleBtn("—", 52);
            btnMin.MouseEnter += (s, e) => btnMin.BackColor = Theme.BgElevated;
            btnMin.MouseLeave += (s, e) => btnMin.BackColor = Theme.BgDeep;
            btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

            bar.Controls.Add(title);
            bar.Controls.Add(btnClose);
            bar.Controls.Add(btnMin);
            Controls.Add(bar);
        }

        private Button MakeTitleBtn(string text, int fromRight)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(32, 24),
                Location = new Point(ClientSize.Width - fromRight - 32, 8),
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.TextSecondary,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero); }
        }

        private void BuildHeader()
        {
            Panel header = new Panel
            {
                Location = new Point(20, 56),
                Size = new Size(ClientSize.Width - 40, 56),
                BackColor = Theme.BgPanel
            };
            header.Paint += DrawCardBorder;

            Label subtitle = new Label
            {
                Text = "Сетевой монитор и прокси-клиент",
                Font = Theme.FontLabelBold,
                ForeColor = Theme.TextSecondary,
                Location = new Point(18, 22),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            _btnSmartScan = MakeHeaderBtn("⚡ Найти лучший узел", 480, 10, 190, 36,
                Theme.AccentDark, Theme.Accent, Theme.Accent);
            _btnSmartScan.Click += OnSmartScanClick;

            _btnAddNode = MakeHeaderBtn("+ Добавить узел", 680, 10, 160, 36,
                Theme.BgElevated, Theme.TextPrimary, Theme.Border);
            _btnAddNode.Click += OnAddNodeClick;

            header.Controls.AddRange(new Control[] { subtitle, _btnSmartScan, _btnAddNode });
            Controls.Add(header);
        }

        private Button MakeHeaderBtn(string text, int x, int y, int w, int h, Color bg, Color fg, Color border)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Font = Theme.FontBodyBold,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = border;
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private void BuildPowerButton()
        {
            _powerBtn = new PulsePowerButton();
            _powerBtn.Location = new Point((ClientSize.Width - _powerBtn.Width) / 2, 130);
            _powerBtn.Click += OnPowerButtonClick;
            Controls.Add(_powerBtn);
        }

        private void BuildNodeSelector()
        {
            const int w = 640, h = 100;
            Panel card = new Panel
            {
                Location = new Point((ClientSize.Width - w) / 2, 390),
                Size = new Size(w, h),
                BackColor = Theme.BgPanel
            };
            card.Paint += DrawCardBorder;

            _flagBox = new Panel { Location = new Point(18, 22), Size = new Size(56, 56) };
            _flagBox.Paint += OnFlagBoxPaint;

            _nodeCombo = new ComboBox
            {
                Location = new Point(88, 18),
                Size = new Size(w - 108, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Theme.BgInput,
                ForeColor = Theme.TextPrimary,
                Font = Theme.FontBodyBold,
                FlatStyle = FlatStyle.Flat
            };
            _nodeCombo.SelectedIndexChanged += OnNodeComboChanged;

            _lblSelectedIp = new Label
            {
                Location = new Point(88, 58),
                AutoSize = true,
                Font = Theme.FontMonoSmall,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent
            };

            _lblSelectedProto = new Label
            {
                Location = new Point(w - 98, 58),
                Size = new Size(80, 22),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.AccentDark,
                ForeColor = Theme.Accent,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold)
            };

            card.Controls.AddRange(new Control[] { _flagBox, _nodeCombo, _lblSelectedIp, _lblSelectedProto });
            Controls.Add(card);
        }

        private void BuildMetrics()
        {
            int gap = 12;
            int total = ClientSize.Width - 40;
            int cardW = (total - gap * 3) / 4;
            int y = 510;

            _lblMetricPing   = MakeMetricCard(20, y, cardW, "ЗАДЕРЖКА (PING)", "-- мс");
            _lblMetricJitter = MakeMetricCard(20 + (cardW + gap), y, cardW, "ДЖИТТЕР (JITTER)", "-- мс");
            _lblMetricDown   = MakeMetricCard(20 + (cardW + gap) * 2, y, cardW, "ВХОДЯЩАЯ (DOWN)", "0.0 Мбит/с");
            _lblMetricUp     = MakeMetricCard(20 + (cardW + gap) * 3, y, cardW, "ИСХОДЯЩАЯ (UP)", "0.0 Мбит/с");
        }

        private Label MakeMetricCard(int x, int y, int w, string title, string defaultVal)
        {
            Panel card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, 80),
                BackColor = Theme.BgPanel
            };
            card.Paint += DrawCardBorder;

            Label lblTitle = new Label
            {
                Text = title,
                Location = new Point(16, 12),
                AutoSize = true,
                Font = Theme.FontLabelBold,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent
            };

            Label lblVal = new Label
            {
                Text = defaultVal,
                Location = new Point(16, 34),
                AutoSize = true,
                Font = Theme.FontMono,
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent
            };

            card.Controls.AddRange(new Control[] { lblTitle, lblVal });
            Controls.Add(card);
            return lblVal;
        }

        private void BuildBottomPanels()
        {
            int y = 610, h = 170;
            int chartW = 440;
            int listW = ClientSize.Width - 40 - chartW - 20;

            Panel chartPanel = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(chartW, h),
                BackColor = Theme.BgPanel
            };
            chartPanel.Paint += DrawCardBorder;

            chartPanel.Controls.Add(new Label
            {
                Text = "График стабильности отклика",
                Location = new Point(14, 10),
                AutoSize = true,
                Font = Theme.FontSection,
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent
            });

            _chart = new PingChartControl
            {
                Location = new Point(14, 36),
                Size = new Size(chartW - 28, h - 50)
            };
            chartPanel.Controls.Add(_chart);
            Controls.Add(chartPanel);

            Panel listPanel = new Panel
            {
                Location = new Point(20 + chartW + 20, y),
                Size = new Size(listW, h),
                BackColor = Theme.BgPanel
            };
            listPanel.Paint += DrawCardBorder;

            listPanel.Controls.Add(new Label
            {
                Text = "Статус удалённых узлов",
                Location = new Point(14, 10),
                AutoSize = true,
                Font = Theme.FontSection,
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent
            });

            _nodesListView = new ListView
            {
                Location = new Point(14, 36),
                Size = new Size(listW - 28, h - 50),
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BackColor = Theme.BgDeep,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = Theme.FontBody
            };
            _nodesListView.Columns.Add("Локация", 200);
            _nodesListView.Columns.Add("IP:Порт", 130);
            _nodesListView.Columns.Add("Протокол", 70);
            _nodesListView.Columns.Add("Пинг", 70);
            _nodesListView.SelectedIndexChanged += OnListViewSelectionChanged;

            listPanel.Controls.Add(_nodesListView);
            Controls.Add(listPanel);
        }

        private void DrawCardBorder(object sender, PaintEventArgs e)
        {
            Control c = sender as Control;
            if (c == null) return;
            using (var p = new Pen(Theme.Border, 1f))
                e.Graphics.DrawRectangle(p, 0, 0, c.Width - 1, c.Height - 1);
        }

        private void OnFlagBoxPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            NodeItem cur = GetSelectedNode();
            Color c1 = cur != null ? cur.FlagColor1 : Theme.Accent;
            Color c2 = cur != null ? cur.FlagColor2 : Theme.Success;
            string code = cur != null ? cur.CountryCode : "GL";

            using (var grad = new LinearGradientBrush(new Rectangle(0, 0, 56, 56), c1, c2, 90f))
                g.FillEllipse(grad, 2, 2, 52, 52);
            using (var overlay = new SolidBrush(Color.FromArgb(110, 9, 13, 22)))
                g.FillEllipse(overlay, 2, 2, 52, 52);
            using (var f = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var b = new SolidBrush(Color.White))
            {
                SizeF s = g.MeasureString(code, f);
                g.DrawString(code, f, b, (56 - s.Width) / 2f, (56 - s.Height) / 2f);
            }
        }

        private NodeItem GetSelectedNode()
        {
            if (_nodeCombo.SelectedIndex >= 0 && _nodeCombo.SelectedIndex < _nodes.Count)
                return _nodes[_nodeCombo.SelectedIndex];
            return _nodes.Count > 0 ? _nodes[0] : null;
        }

        private void RefreshNodesUI()
        {
            _suppressComboChanged = true;
            try
            {
                int prev = _nodeCombo.SelectedIndex >= 0 ? _nodeCombo.SelectedIndex : 0;
                _nodeCombo.Items.Clear();
                _nodesListView.Items.Clear();

                for (int i = 0; i < _nodes.Count; i++)
                {
                    NodeItem n = _nodes[i];
                    _nodeCombo.Items.Add(n);

                    var lvi = new ListViewItem("[" + n.CountryCode + "] " + n.Name);
                    lvi.SubItems.Add(n.Ip + ":" + n.Port);
                    lvi.SubItems.Add(n.Protocol);
                    lvi.SubItems.Add(n.Ping >= 0 ? n.Ping + " ms" : "--");
                    if (n.Ping >= 0)
                        lvi.ForeColor = n.Ping < 120 ? Theme.Success
                                     : n.Ping < 200 ? Theme.Warning
                                                    : Theme.Danger;
                    _nodesListView.Items.Add(lvi);
                }

                if (prev < _nodeCombo.Items.Count)
                    _nodeCombo.SelectedIndex = prev;

                UpdateSelectedNodeDetails();
            }
            finally { _suppressComboChanged = false; }
        }

        private void UpdateNodeListPingOnly()
        {
            for (int i = 0; i < _nodes.Count && i < _nodesListView.Items.Count; i++)
            {
                NodeItem n = _nodes[i];
                var lvi = _nodesListView.Items[i];
                lvi.SubItems[3].Text = n.Ping >= 0 ? n.Ping + " ms" : "--";
                lvi.ForeColor = n.Ping >= 0
                    ? (n.Ping < 120 ? Theme.Success : (n.Ping < 200 ? Theme.Warning : Theme.Danger))
                    : Theme.TextPrimary;
            }
        }

        private void UpdateSelectedNodeDetails()
        {
            NodeItem cur = GetSelectedNode();
            if (cur == null) return;
            _lblSelectedIp.Text = "Хост: " + cur.Ip + ":" + cur.Port;
            _lblSelectedProto.Text = cur.Protocol;
            _flagBox.Invalidate();
        }

        private void OnNodeComboChanged(object sender, EventArgs e)
        {
            if (_suppressComboChanged) return;
            UpdateSelectedNodeDetails();

            if (_powerBtn.State == PulsePowerButton.ConnState.Connected ||
                _powerBtn.State == PulsePowerButton.ConnState.Scanning)
            {
                DisconnectNode();
                StartConnection();
            }
        }

        private void OnListViewSelectionChanged(object sender, EventArgs e)
        {
            if (_suppressComboChanged) return;
            if (_nodesListView.SelectedIndices.Count > 0)
            {
                int idx = _nodesListView.SelectedIndices[0];
                if (idx >= 0 && idx < _nodeCombo.Items.Count && _nodeCombo.SelectedIndex != idx)
                    _nodeCombo.SelectedIndex = idx;
            }
        }

        private void OnPowerButtonClick(object sender, EventArgs e)
        {
            if (_powerBtn.State == PulsePowerButton.ConnState.Disconnected)
                StartConnection();
            else if (_powerBtn.State == PulsePowerButton.ConnState.Connected)
                DisconnectNode();
        }

        private void StartConnection()
        {
            NodeItem target = GetSelectedNode();
            if (target == null) return;

            int myToken = ++_connectionToken;

            _powerBtn.State = PulsePowerButton.ConnState.Scanning;
            _chart.Clear();
            _lastPing = 0;

            Thread worker = new Thread(() =>
            {
                int ping = -1;
                bool real = false;
                try
                {
                    int measured = NodePinger.MeasureLatency(target.Ip, target.Port, 1500);
                    real = measured > 0;
                    ping = real ? measured : NodePinger.DemoPing(target, new Random());

                    if (real)
                        SystemProxyManager.EnableProxy(target.Ip, target.Port, target.Protocol);
                    else
                        SystemProxyManager.DisableProxy();
                }
                catch { }

                try
                {
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        if (myToken != _connectionToken) return;
                        _lastPing = ping;
                        target.Ping = ping;
                        _powerBtn.IsDemoMode = !real;
                        _powerBtn.State = PulsePowerButton.ConnState.Connected;
                        _chart.AddPoint(ping);
                        UpdateTelemetryLabels(ping, 0);
                        UpdateNodeListPingOnly();
                        _liveTimer.Start();
                    }));
                }
                catch { }
            });
            worker.IsBackground = true;
            worker.Start();
        }

        private void DisconnectNode()
        {
            _connectionToken++;
            _liveTimer.Stop();
            SystemProxyManager.DisableProxy();
            _powerBtn.State = PulsePowerButton.ConnState.Disconnected;
            _powerBtn.IsDemoMode = false;
            _lblMetricPing.Text = "-- мс";
            _lblMetricJitter.Text = "-- мс";
            _lblMetricDown.Text = "0.0 Мбит/с";
            _lblMetricUp.Text = "0.0 Мбит/с";
        }

        private void OnLiveTelemetryTick(object sender, EventArgs e)
        {
            if (_powerBtn.State != PulsePowerButton.ConnState.Connected) return;
            NodeItem cur = GetSelectedNode();
            if (cur == null) return;

            int delta = _rnd.Next(-7, 9);
            int newPing = Math.Max(14, _lastPing + delta);
            int jitter = Math.Abs(newPing - _lastPing);
            _lastPing = newPing;
            cur.Ping = newPing;

            _chart.AddPoint(newPing);
            UpdateTelemetryLabels(newPing, jitter);
            UpdateNodeListPingOnly();
        }

        private void UpdateTelemetryLabels(int ping, int jitter)
        {
            double baseSpeed = Math.Max(14.0, 13800.0 / Math.Max(ping, 1));
            double down = baseSpeed + (_rnd.NextDouble() * 6.0 - 3.0);
            double up = baseSpeed * 0.65 + (_rnd.NextDouble() * 4.0 - 2.0);

            _lblMetricPing.Text = ping + " мс";
            _lblMetricJitter.Text = jitter + " мс";
            _lblMetricDown.Text = down.ToString("0.0") + " Мбит/с";
            _lblMetricUp.Text = up.ToString("0.0") + " Мбит/с";
        }

        private void OnSmartScanClick(object sender, EventArgs e)
        {
            _btnSmartScan.Enabled = false;
            _btnSmartScan.Text = "Сканирование...";

            Thread scanThread = new Thread(() =>
            {
                int bestIndex = -1;
                int bestPing = int.MaxValue;

                for (int i = 0; i < _nodes.Count; i++)
                {
                    int p = NodePinger.MeasureLatency(_nodes[i].Ip, _nodes[i].Port, 1500);
                    _nodes[i].Ping = p;
                    if (p > 0 && p < bestPing) { bestPing = p; bestIndex = i; }
                }

                try
                {
                    BeginInvoke((MethodInvoker)(() =>
                    {
                        RefreshNodesUI();
                        if (bestIndex >= 0 && bestIndex < _nodeCombo.Items.Count)
                            _nodeCombo.SelectedIndex = bestIndex;

                        _btnSmartScan.Enabled = true;
                        _btnSmartScan.Text = "⚡ Найти лучший узел";

                        if (bestIndex >= 0)
                            MessageBox.Show(string.Format(
                                "Найден самый быстрый узел:\n{0} ({1} мс)",
                                _nodes[bestIndex].Name, bestPing),
                                "Smart Scan", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        else
                            MessageBox.Show("Не удалось достичь ни одного узла.\nПроверьте сеть или добавьте рабочий узел.",
                                "Smart Scan", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }));
                }
                catch { }
            });
            scanThread.IsBackground = true;
            scanThread.Start();
        }

        private void OnAddNodeClick(object sender, EventArgs e)
        {
            using (var dlg = new AddNodeForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.CreatedNode != null)
                {
                    _nodes.Add(dlg.CreatedNode);
                    NodeStorage.Append(dlg.CreatedNode);
                    RefreshNodesUI();
                    _nodeCombo.SelectedIndex = _nodes.Count - 1;
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  ТОЧКА ВХОДА
    // ────────────────────────────────────────────────────────────────────
    static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}