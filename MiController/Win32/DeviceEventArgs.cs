using System;
using HidLibrary;

namespace MiController.Win32
{
    public class DeviceEventArgs : EventArgs
    {
        internal DeviceEventArgs(HidDevices.DeviceInfo info)
        {
            Path = info.Path;
            Description = info.Description;
            InstanceId = info.InstanceID;
        }

        public string Path { get; set; }
        public string Description { get; set; }
        public string InstanceId { get; set; }
    }
}