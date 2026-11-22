using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WinProgrammBlocker
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private readonly TextBox _txtProcess;
        private readonly Button _btnToggle;
        private readonly Label _lblStatus;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly NotifyIcon _trayIcon;

        private string[] _targetNames = new string[0];
        private int _killedCount;
        private bool _isActive;

        public MainForm()
        {
            Text = "Process Blocker";
            Size = new Size(300, 155);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var lblHint = new Label
            {
                Text = "Process name (e.g., ff.exe):",
                Location = new Point(12, 12),
                AutoSize = true
            };

            _txtProcess = new TextBox
            {
                Location = new Point(12, 32),
                Width = 260,
                Text = "ff.exe"
            };

            _btnToggle = new Button
            {
                Text = "Start Blocking",
                Location = new Point(12, 62),
                Width = 260,
                Height = 28
            };
            _btnToggle.Click += BtnToggle_Click;

            _lblStatus = new Label
            {
                Text = "Status: Stopped",
                Location = new Point(12, 96),
                AutoSize = true,
                ForeColor = Color.Gray
            };

            Controls.AddRange(new Control[] { lblHint, _txtProcess, _btnToggle, _lblStatus });

            _timer = new System.Windows.Forms.Timer { Interval = 500 };
            _timer.Tick += (s, e) => KillTargets();

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Shield,
                Text = "Process Blocker",
                Visible = false
            };
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();

            Resize += (s, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    Hide();
                    _trayIcon.Visible = true;
                }
            };

            FormClosing += (s, e) =>
            {
                _timer.Stop();
                _timer.Dispose();
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            };
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            _trayIcon.Visible = false;
            Activate();
        }

        private void BtnToggle_Click(object sender, EventArgs e)
        {
            if (!_isActive)
            {
                _targetNames = _txtProcess.Text
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Select(s => s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? s.Substring(0, s.Length - 4) : s)
                    .Where(s => s.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (_targetNames.Length == 0)
                {
                    MessageBox.Show("Please enter a process name.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _isActive = true;
                _killedCount = 0;
                _txtProcess.Enabled = false;
                _btnToggle.Text = "Stop";
                _lblStatus.ForeColor = Color.DarkGreen;
                _lblStatus.Text = "Active | Killed: 0";

                var trayText = "Blocking: " + string.Join(", ", _targetNames);
                _trayIcon.Text = trayText.Length > 63 ? trayText.Substring(0, 60) + "..." : trayText;

                KillTargets();
                _timer.Start();
            }
            else
            {
                _isActive = false;
                _timer.Stop();
                _txtProcess.Enabled = true;
                _btnToggle.Text = "Start Blocking";
                _lblStatus.ForeColor = Color.Gray;
                _lblStatus.Text = "Stopped | Total killed: " + _killedCount;
                _trayIcon.Text = "Process Blocker (stopped)";
            }
        }

        private void KillTargets()
        {
            foreach (var name in _targetNames)
            {
                Process[] processes;
                try
                {
                    processes = Process.GetProcessesByName(name);
                }
                catch
                {
                    continue;
                }

                foreach (var proc in processes)
                {
                    using (proc)
                    {
                        try
                        {
                            proc.Kill();
                            _killedCount++;
                            _lblStatus.Text = "Active | Killed: " + _killedCount;
                        }
                        catch
                        {
                            // Process already exited or insufficient permissions
                        }
                    }
                }
            }
        }
    }
}