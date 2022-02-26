using MiController.Properties;
using System;
using System.Drawing;
using System.Windows.Forms;
using MiController.Win32;
using Nefarius.ViGEm.Client.Exceptions;

namespace MiController
{
    public class MiApplicationContext : ApplicationContext
    {
        private readonly NotifyIcon _trayIcon;
        private ContextMenuStrip _contextMenuStrip;
        private ToolStripMenuItem _statusToolStripMenuItem;
        private const string XIAOMI_GAMEPAD_HARDWARE_FILTER = @"VID&00022717_PID&3144";
        private XInputManager _manager;
        private HidMonitor _monitor;
        public MiApplicationContext()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            InitContextMenu();
            InitGamepad();
            StartGamePad();
            // Initialize Tray Icon
            _trayIcon = new NotifyIcon   
            {
                Icon = Resources.MiLogoGrey,
                ContextMenuStrip = _contextMenuStrip,
                Visible = true
            };
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            var message = args.ExceptionObject is Exception exception ? exception.Message : args.ExceptionObject.ToString();
            _statusToolStripMenuItem.Text = $"Error: {message}";
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            _monitor.Stop();
            Application.Exit();
        }

        private void StartStopProcess(bool start)
        {
            _statusToolStripMenuItem.Image = start ? Resources.ConnectPlugged : Resources.ConnectUnplugged;
            _trayIcon.Icon = start ? Resources.MiLogo : Resources.MiLogoGrey;
        }

        private void InitContextMenu()
        {
            _statusToolStripMenuItem = new ToolStripMenuItem
            {
                Name = "statusToolStripMenuItem",
                Size = new Size(180, 22),
                Text = "ready",
                Enabled = true
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
            _contextMenuStrip.Items.Add(_statusToolStripMenuItem);
            _contextMenuStrip.Items.Add(toolStripSeparator2);
            _contextMenuStrip.Items.Add(exitToolStripMenuItem);
        }


        private void Manager_GamepadRunning(object sender, EventArgs eventArgs)
        {
            _statusToolStripMenuItem.Text = "Gamepad connected";
            StartStopProcess(true);
        }

        private void Manager_GamepadRemoved(object sender, EventArgs eventArgs)
        {
            _statusToolStripMenuItem.Text = "Gamepad disconnected";
            StartStopProcess(false);
        }

        private void Monitor_DeviceAttached(object sender, DeviceEventArgs e)
        {
            _manager.AddAndStart(e.Path, e.InstanceId);
        }

        private void Monitor_DeviceRemoved(object sender, DeviceEventArgs e)
        {
            _manager.StopAndRemove(e.Path);
        }

        private void InitGamepad()
        {
            try
            {
                _manager = new XInputManager();
                _manager.GamepadRunning += Manager_GamepadRunning;
                _manager.GamepadRemoved += Manager_GamepadRemoved;
            }
            catch (VigemBusNotFoundException ex)
            {
                throw new ApplicationException("ViGEm Bus not found. Please make sure that is installed correctly", ex);
            }

            _monitor = new HidMonitor(XIAOMI_GAMEPAD_HARDWARE_FILTER);
            _monitor.DeviceAttached += Monitor_DeviceAttached;
            _monitor.DeviceRemoved += Monitor_DeviceRemoved;
        }

        private void StartGamePad()
        {
            _monitor.Start();
        }
    }
}
