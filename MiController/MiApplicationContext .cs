using MiController.Properties;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace MiController
{
    public class MiApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private BackgroundWorker _backgroundWorker;
        private ContextMenuStrip _contextMenuStrip;
        private ToolStripMenuItem _connectToolStripMenuItem;
        private ToolStripMenuItem _disconnectToolStripMenuItem;
        private ToolStripMenuItem _statusToolStripMenuItem;
        public MiApplicationContext()
        {
            InitBackgroundWorker();
            InitContextMenu();

            // Initialize Tray Icon
            _trayIcon = new NotifyIcon   
            {
                Icon = Resources.MiLogoGrey,
                ContextMenuStrip = _contextMenuStrip,
                Visible = true
            };
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _backgroundWorker.CancelAsync();
            _trayIcon.Visible = false;

            Application.Exit();
        }

        private void connectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StartStopProcess(true);
            _backgroundWorker.RunWorkerAsync();
        }
        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _backgroundWorker.CancelAsync();
        }

        private void InitBackgroundWorker()
        {
            _backgroundWorker = new BackgroundWorker {WorkerReportsProgress = true, WorkerSupportsCancellation = true};
            _backgroundWorker.DoWork += backgroundWorker_DoWork;
            _backgroundWorker.ProgressChanged += backgroundWorker_ProgressChanged;
            _backgroundWorker.RunWorkerCompleted += backgroundWorker_RunWorkerCompleted;
        }

        private void StartStopProcess(bool start)
        {
            _connectToolStripMenuItem.Enabled = !start;
            _disconnectToolStripMenuItem.Enabled = start;
            _trayIcon.Icon = start ? Resources.MiLogo : Resources.MiLogoGrey;
        }

        private void InitContextMenu()
        {
            _connectToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "connectToolStripMenuItem",
                Size = new Size(180, 22),
                Text = "Connect",
                Image = Resources.ConnectPlugged,
                ImageTransparentColor = Color.Magenta
            };
            _connectToolStripMenuItem.Click += connectToolStripMenuItem_Click;

            _disconnectToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "disconnectToolStripMenuItem",
                Size = new Size(180, 22),
                Text = "Disconnect",
                Image = Resources.ConnectUnplugged,
                ImageTransparentColor = Color.Magenta,
                Enabled = false
            };
            _disconnectToolStripMenuItem.Click += disconnectToolStripMenuItem_Click;
            var toolStripSeparator1 = new ToolStripSeparator { Name = "toolStripSeparator1", Size = new Size(322, 6) };

            _statusToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "statusToolStripMenuItem",
                Size = new Size(180, 22),
                Text = "ready",
                Enabled = false
            };

            var toolStripSeparator2 = new ToolStripSeparator { Name = "toolStripSeparator2", Size = new Size(322, 6) };

            var exitToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "exitToolStripMenuItem",
                Size = new Size(180, 22),
                Text = "E&xit",
                Image = Resources.Close,
                ImageTransparentColor = Color.Magenta
            };
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;

            _contextMenuStrip = new ContextMenuStrip { Renderer = new NoHighlightRenderer() };
            _contextMenuStrip.Items.Add(_connectToolStripMenuItem);
            _contextMenuStrip.Items.Add(_disconnectToolStripMenuItem);
            _contextMenuStrip.Items.Add(toolStripSeparator1);
            _contextMenuStrip.Items.Add(_statusToolStripMenuItem);
            _contextMenuStrip.Items.Add(toolStripSeparator2);
            _contextMenuStrip.Items.Add(exitToolStripMenuItem);
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var bw = sender as BackgroundWorker;

            ControllerHelper.Run(bw);

            if (bw != null)
            {
                if (bw.CancellationPending)
                {
                    e.Cancel = true;
                }
            }
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                _statusToolStripMenuItem.Text = "disconnected";
            }
            else if (e.Error != null)
            {
                string msg = String.Format(CultureInfo.InvariantCulture, "An error occurred: {0}", e.Error.Message);
                _statusToolStripMenuItem.Text = msg;
            }
            StartStopProcess(false);
        }

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            _statusToolStripMenuItem.Text = (string)e.UserState;
        }

    }

    internal class NoHighlightRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Enabled)
            {
                base.OnRenderMenuItemBackground(e);
            }
        }
    }
}
