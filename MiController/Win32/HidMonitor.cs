using System;
using System.Linq;
using System.Threading;
using HidLibrary;

namespace MiController.Win32
{
    public class HidMonitor
    {
        public event EventHandler<DeviceEventArgs> DeviceAttached;
        public event EventHandler<DeviceEventArgs> DeviceRemoved;
        private readonly Timer _monitorTimer;
        private readonly string _filter;
        private HidDevices.DeviceInfo[] _seenDevices;


        public HidMonitor(string filter)
        {
            // Initializing HID device monitor with filter
            _filter = filter;
            _monitorTimer = new Timer(SearchForDevice);
            _seenDevices = Array.Empty<HidDevices.DeviceInfo>();
        }

        public void Start()
        {
            // Start monitoring for filter
            _monitorTimer.Change(0, 5000);
        }

        public void Stop()
        {
            // Stop monitoring for filter
            _monitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void SearchForDevice(object state)
        {
            var filter = _filter.ToLower();
            var devices = HidDevices
                .EnumerateDevices()
                .Where(p => p.Path.ToLower().Contains(filter))
                .ToArray();

            var comp = new DeviceInfoEqualityComparer();

            // Get all the devices that has connected since the last check
            var newDevices = devices.Except(_seenDevices, comp);

            // Get all the device that has disconnected since the last check
            var removedDevices = _seenDevices.Except(devices, comp);

            foreach (var device in newDevices)
            {
                // Detected attached HID devices matching filter
                DeviceAttached?.Invoke(this, new DeviceEventArgs(device));
            }

            foreach (var device in removedDevices)
            {
                // Detected removed HID devices matching filter
                DeviceRemoved?.Invoke(this, new DeviceEventArgs(device));
            }

            _seenDevices = devices;
        }

        public void Dispose()
        {
            // De-initilizing HID monitor
            _monitorTimer.Dispose();
        }
    }
}
