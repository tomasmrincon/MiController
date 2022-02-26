using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MiController.Win32;
using Nefarius.ViGEm.Client;


namespace MiController
{
    public class XInputManager : IDisposable
    {
        public event EventHandler GamepadRunning;
        public event EventHandler GamepadRemoved;

        private readonly Dictionary<string, XiaomiGamepad> _gamepads;
        private readonly ViGEmClient _viGEmClient;

        private readonly SynchronizationContext _syncContext;

        public XInputManager()
        {
            //Initializing ViGEm client
            _viGEmClient = new ViGEmClient();
            _gamepads = new Dictionary<string, XiaomiGamepad>();
            _syncContext = SynchronizationContext.Current;
        }

        public void Dispose()
        {
            // Cleaning up running gamepads

            // When calling Stop() the device will get removed from the dictionary, do this to avoid exceptions in enumeration
            var devices = _gamepads.Values.ToArray();

            foreach (var device in devices)
            {
                StopAndRemove(device);
                device.Dispose();
            }

            // Deinitializing ViGEm client
            _viGEmClient.Dispose();
        }


        public bool AddAndStart(string device, string instance)
        {
            if (Contains(device))
            {
                // Requested addition of already existing device
                return false;
            }

            // Adding device
            var gamepad = new XiaomiGamepad(device, instance, _viGEmClient);
            if (_gamepads.ContainsKey(device))
            {
                _gamepads[device].Dispose();
                _gamepads.Remove(device);
            }
            _gamepads.Add(device, gamepad);

            gamepad.Started += Gamepad_Started;
            gamepad.Ended += Gamepad_Ended;

            // Hiding device instance
            using (var hh = new HidHide())
            {
                hh.SetDeviceHideStatus(gamepad.InstanceId, true);
            }

            // Starting device
            gamepad.Start();

            return true;
        }

        public void StopAndRemove(string device)
        {
            if (!Contains(device))
            {
                // Requested removal of non-existing device
                return;
            }
            var gamepad = _gamepads[device];
            StopAndRemove(gamepad);
        }

        private void StopAndRemove(XiaomiGamepad gamepad)
        {
            // Stopping device
            if (gamepad.IsActive)
            {
                gamepad.Stop();
            }

            gamepad.Started -= Gamepad_Started;
            gamepad.Ended -= Gamepad_Ended;

            // Un-hiding device instance
            using (var hh = new HidHide())
            {
                hh.SetDeviceHideStatus(gamepad.InstanceId, false);
            }

            // De-initializing and removing device
            _gamepads.Remove(gamepad.Device.DevicePath);
            gamepad.Dispose();
        }

        public bool Contains(string device) => _gamepads.ContainsKey(device);

        private void Gamepad_Ended(object sender, EventArgs eventArgs)
        {
            _syncContext.Post(o =>
            {
                if (sender is XiaomiGamepad gamepad && !gamepad.CleanEnd)
                {
                    StopAndRemove(gamepad);
                }

                GamepadRemoved?.Invoke(this, EventArgs.Empty);
            }, null);
        }

        private void Gamepad_Started(object sender, EventArgs eventArgs)
        {
            _syncContext.Post(o => GamepadRunning?.Invoke(this, EventArgs.Empty), null);
        }
    }
}
