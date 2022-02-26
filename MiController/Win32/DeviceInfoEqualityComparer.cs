using System.Collections.Generic;
using HidLibrary;

namespace MiController.Win32
{
    public class DeviceInfoEqualityComparer : IEqualityComparer<HidDevices.DeviceInfo>
    {
        public bool Equals(HidDevices.DeviceInfo x, HidDevices.DeviceInfo y) => y != null && x != null && x.Path == y.Path;
        public int GetHashCode(HidDevices.DeviceInfo di) => di.Path.GetHashCode();
    }
}