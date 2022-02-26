using System;
using System.Threading;
using System.Threading.Tasks;
using HidLibrary;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace MiController
{
    public class XiaomiGamepad : IDisposable
    {
        private static readonly Xbox360Button[][] HatSwitches = {
            new [] { Xbox360Button.Up },
            new [] { Xbox360Button.Up, Xbox360Button.Right },
            new [] { Xbox360Button.Right },
            new [] { Xbox360Button.Right, Xbox360Button.Down },
            new [] { Xbox360Button.Down },
            new [] { Xbox360Button.Down, Xbox360Button.Left },
            new [] { Xbox360Button.Left },
            new [] { Xbox360Button.Left, Xbox360Button.Up },
        };

        public event EventHandler Started;
        public event EventHandler Ended;

        private readonly IXbox360Controller _target;
        private readonly Thread _inputThread;
        private readonly CancellationTokenSource _cts;
        private readonly Timer _vibrationTimer;
        private static readonly IHidEnumerator DeviceEnumerator = new HidFastReadEnumerator();

        public XiaomiGamepad(string device, string instance, ViGEmClient client)
        {
            Device = DeviceEnumerator.GetDevice(device) as HidFastReadDevice;
            if (Device != null)
            {
                Device.MonitorDeviceEvents = false;
            }

            _target = client.CreateXbox360Controller();
            _target.AutoSubmitReport = false;
            _target.FeedbackReceived += Target_OnFeedbackReceived;

            // TODO mark the threads as background?
            _inputThread = new Thread(DeviceWorker);

            _cts = new CancellationTokenSource();
            _vibrationTimer = new Timer(VibrationTimer_Trigger);

            LedNumber = 0xFF;
            InstanceId = instance;
        }

        public HidFastReadDevice Device { get; }

        public ushort LedNumber { get;  set; }

        public ushort BatteryLevel { get; private set; }

        public bool IsActive => _inputThread.IsAlive;
        public bool CleanEnd => _cts.IsCancellationRequested;

        public bool ExclusiveMode { get; private set; }

        public string InstanceId { get; }

        public void Dispose()
        {
            // De-initializing XiaomiGamepad handler for device
            if (_inputThread.IsAlive)
                Stop();

            Device.Dispose();
            _cts.Dispose();
        }

        public void Start()
        {
            _inputThread.Start();
        }

        public void Stop()
        {
            if (_cts.IsCancellationRequested)
            {
                // Thread stop for device already requested
                return;
            }

            // Requesting thread stop for device
            _cts.Cancel();
            _inputThread.Join();
        }

        private void DeviceWorker()
        {
            // Starting worker thread for device

            // Open HID device to read input from the gamepad
            Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
            ExclusiveMode = true;

            // If exclusive mode is not available, retry in shared mode.
            if (!Device.IsOpen)
            {
                // Cannot access HID device in exclusive mode, retrying in shared mode

                Device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
                ExclusiveMode = false;

                if (!Device.IsOpen)
                {
                    // Cannot open HID device
                    Device.CloseDevice();
                    Ended?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }

            // Init Xiaomi Gamepad vibration
            Device.WriteFeatureData(new byte[] { 0x20, 0x00, 0x00 });

            // Connect the virtual Xbox360 gamepad
            try
            {
                // Connecting to ViGEm client
                _target.Connect();
            }
            catch (VigemAlreadyConnectedException)
            {
                // ViGEm client was already opened, closing and reopening it
                _target.Disconnect();
                _target.Connect();
            }

            Started?.Invoke(this, EventArgs.Empty);

            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                // Is device has been closed, exit the loop
                if (!Device.IsOpen)
                    break;

                // Otherwise read a report
                var hidReport = Device.FastReadReport(1000);

                if (hidReport.ReadStatus == HidDeviceData.ReadStatus.WaitTimedOut)
                    continue;
                if (hidReport.ReadStatus != HidDeviceData.ReadStatus.Success)
                {
                    // Cannot read HID report for device
                    break;
                }

                var data = hidReport.Data;

                /*
                [0]  Buttons state, 1 bit per button
                [1]  Buttons state, 1 bit per button
                [2]  0x00
                [3]  D-Pad
                [4]  Left thumb, X axis
                [5]  Left thumb, Y axis
                [6]  Right thumb, X axis
                [7]  Right thumb, Y axis
                [8]  0x00
                [9]  0x00
                [10] L trigger
                [11] R trigger
                [12] Accelerometer axis 1
                [13] Accelerometer axis 1
                [14] Accelerometer axis 2
                [15] Accelerometer axis 2
                [16] Accelerometer axis 3
                [17] Accelerometer axis 3
                [18] Battery level
                [19] MI button
                    */

                lock (_target)
                {
                    _target.SetButtonState(Xbox360Button.A, GetBit(data[0], 0));
                    _target.SetButtonState(Xbox360Button.B, GetBit(data[0], 1));
                    _target.SetButtonState(Xbox360Button.X, GetBit(data[0], 3));
                    _target.SetButtonState(Xbox360Button.Y, GetBit(data[0], 4));
                    _target.SetButtonState(Xbox360Button.LeftShoulder, GetBit(data[0], 6));
                    _target.SetButtonState(Xbox360Button.RightShoulder, GetBit(data[0], 7));

                    _target.SetButtonState(Xbox360Button.Back, GetBit(data[1], 2));
                    _target.SetButtonState(Xbox360Button.Start, GetBit(data[1], 3));
                    _target.SetButtonState(Xbox360Button.LeftThumb, GetBit(data[1], 5));
                    _target.SetButtonState(Xbox360Button.RightThumb, GetBit(data[1], 6));

                    // Reset Hat switch status, as is set to 15 (all directions set, impossible state)
                    _target.SetButtonState(Xbox360Button.Up, false);
                    _target.SetButtonState(Xbox360Button.Left, false);
                    _target.SetButtonState(Xbox360Button.Down, false);
                    _target.SetButtonState(Xbox360Button.Right, false);

                    if (data[3] < 8)
                    {
                        var btns = HatSwitches[data[3]];
                        // Hat Switch is a number from 0 to 7, where 0 is Up, 1 is Up-Left, etc.
                        foreach (var b in btns)
                            _target.SetButtonState(b, true);
                    }

                    // Analog axis
                    _target.SetAxisValue(Xbox360Axis.LeftThumbX, MapAnalog(data[4]));
                    _target.SetAxisValue(Xbox360Axis.LeftThumbY, MapAnalog(data[5], true));
                    _target.SetAxisValue(Xbox360Axis.RightThumbX, MapAnalog(data[6]));
                    _target.SetAxisValue(Xbox360Axis.RightThumbY, MapAnalog(data[7], true));

                    // Triggers
                    _target.SetSliderValue(Xbox360Slider.LeftTrigger, data[10]);
                    _target.SetSliderValue(Xbox360Slider.RightTrigger, data[11]);

                    // Logo ("home") button
                    if (GetBit(data[19], 0))
                    {
                        _target.SetButtonState(Xbox360Button.Guide, true);
                        Task.Delay(200, token).ContinueWith(DelayedReleaseGuideButton);
                    }

                    // Update battery level
                    BatteryLevel = data[18];

                    _target.SubmitReport();
                }

            }

            // Disconnect the virtual Xbox360 gamepad
            // Let Dispose handle that, otherwise it will rise a NotPluggedIn exception
            // Disconnecting ViGEm client
            _target.Disconnect();

            // Close the HID device
            // Closing HID device
            Device.CloseDevice();

            // Exiting worker thread for device
            Ended?.Invoke(this, EventArgs.Empty);
        }

        private static bool GetBit(byte b, int bit)
        {
            return ((b >> bit) & 1) != 0;
        }

        private static short MapAnalog(byte value, bool invert = false)
        {
            return (short)(value * 257 * (invert ? -1 : 1) + short.MinValue);
        }

        private void DelayedReleaseGuideButton(Task t)
        {
            lock (_target)
            {
                _target.SetButtonState(Xbox360Button.Guide, false);
                _target.SubmitReport();
            }
        }

        private void VibrationTimer_Trigger(object o)
        {
            Task.Run(() => {
                lock (_vibrationTimer)
                {
                    if (Device.IsOpen)
                        Device.WriteFeatureData(new byte[] { 0x20, 0x00, 0x00 });

                    // Vibration feedback reset after 3 seconds for device
                }
            });
        }

        private void Target_OnFeedbackReceived(object sender, Xbox360FeedbackReceivedEventArgs e)
        {
            byte[] data = { 0x20, e.SmallMotor, e.LargeMotor };

            Task.Run(() => {

                lock (_vibrationTimer)
                {
                    if (!Device.IsOpen)
                        return;

                    Device.WriteFeatureData(data);
                }

                var timeout = e.SmallMotor > 0 || e.LargeMotor > 0 ? 3000 : Timeout.Infinite;
                _vibrationTimer.Change(timeout, Timeout.Infinite);

            });

            if (LedNumber != e.LedNumber)
            {
                LedNumber = e.LedNumber;
                // TODO raise event here
            }
        }

    }
}
