using System;
using System.ComponentModel;
using System.Linq;
using HidLibrary;
using ScpDriverInterface;
using System.Threading;
using System.Runtime.InteropServices;

namespace MiController
{
    public class ControllerHelper
    {
        private static ScpBus _globalScpBus;
        static bool ConsoleEventCallback(int eventType)
        {
            if (eventType == 2)
            {
                _globalScpBus.UnplugAll();
            }
            return false;
        }
        static ConsoleEventDelegate _handler;   // Keeps it from getting garbage collected
                                               // Pinvoke
        private delegate bool ConsoleEventDelegate(int eventType);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);


        public static void Run(BackgroundWorker bw)
        {
            ScpBus scpBus = new ScpBus();
            scpBus.UnplugAll();
            _globalScpBus = scpBus;

            _handler = ConsoleEventCallback;
            SetConsoleCtrlHandler(_handler, true);

            Thread.Sleep(400);

            XiaomiGamepad[] gamepads = new XiaomiGamepad[4];
            int index = 1;
            var compatibleDevices = HidDevices.Enumerate(0x2717, 0x3144).ToList();
            foreach (var deviceInstance in compatibleDevices)
            {
                Console.WriteLine(deviceInstance);
                HidDevice device = deviceInstance;
                try
                {
                    device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
                }
                catch
                {
                    bw.ReportProgress(0, "Could not open gamepad in exclusive mode. Try re-enable device.");
                    var instanceId = DevicePathToInstanceId(deviceInstance.DevicePath);
                    if (TryReEnableDevice(instanceId))
                    {
                        try
                        {
                            device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.Exclusive);
                            bw.ReportProgress(0, "Opened in exclusive mode.");
                        }
                        catch
                        {
                            device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
                            bw.ReportProgress(0, "Opened in shared mode.");
                        }
                    }
                    else
                    {
                        device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead | ShareMode.ShareWrite);
                        bw.ReportProgress(0, "Opened in shared mode.");
                    }
                }

                byte[] vibration = { 0x20, 0x00, 0x00 };
                if (device.WriteFeatureData(vibration) == false)
                {
                    bw.ReportProgress(0, "Could not write to gamepad (is it closed?), skipping");
                    device.CloseDevice();
                    continue;
                }

                device.ReadSerialNumber(out _);
                device.ReadProduct(out _);


                gamepads[index - 1] = new XiaomiGamepad(device, scpBus, index);
                ++index;

                if (index >= 5)
                {
                    break;
                }
            }
            bw.ReportProgress(0, $"{index - 1} controllers connected");

            while (!bw.CancellationPending)
            {
                Thread.Sleep(1000);
            }
        }

        private static bool TryReEnableDevice(string deviceInstanceId)
        {
            try
            {
                bool success;
                Guid hidGuid = new Guid();
                HidLibrary.NativeMethods.HidD_GetHidGuid(ref hidGuid);
                IntPtr deviceInfoSet = HidLibrary.NativeMethods.SetupDiGetClassDevs(ref hidGuid, deviceInstanceId, 0, HidLibrary.NativeMethods.DIGCF_PRESENT | HidLibrary.NativeMethods.DIGCF_DEVICEINTERFACE);
                HidLibrary.NativeMethods.SP_DEVINFO_DATA deviceInfoData = new HidLibrary.NativeMethods.SP_DEVINFO_DATA();
                deviceInfoData.cbSize = Marshal.SizeOf(deviceInfoData);
                success = HidLibrary.NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 0, ref deviceInfoData);
                if (!success)
                {
                    Console.WriteLine("Error getting device info data, error code = " + Marshal.GetLastWin32Error());
                }
                success = HidLibrary.NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, 1, ref deviceInfoData); // Checks that we have a unique device
                if (success)
                {
                    Console.WriteLine("Can't find unique device");
                }

                HidLibrary.NativeMethods.SP_PROPCHANGE_PARAMS propChangeParams = new HidLibrary.NativeMethods.SP_PROPCHANGE_PARAMS();
                propChangeParams.classInstallHeader.cbSize = Marshal.SizeOf(propChangeParams.classInstallHeader);
                propChangeParams.classInstallHeader.installFunction = HidLibrary.NativeMethods.DIF_PROPERTYCHANGE;
                propChangeParams.stateChange = HidLibrary.NativeMethods.DICS_DISABLE;
                propChangeParams.scope = HidLibrary.NativeMethods.DICS_FLAG_GLOBAL;
                propChangeParams.hwProfile = 0;
                success = HidLibrary.NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData, ref propChangeParams, Marshal.SizeOf(propChangeParams));
                if (!success)
                {
                    Console.WriteLine("Error setting class install params, error code = " + Marshal.GetLastWin32Error());
                    return false;
                }
                success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet, ref deviceInfoData);
                if (!success)
                {
                    Console.WriteLine("Error disabling device, error code = " + Marshal.GetLastWin32Error());
                    return false;

                }
                propChangeParams.stateChange = HidLibrary.NativeMethods.DICS_ENABLE;
                success = HidLibrary.NativeMethods.SetupDiSetClassInstallParams(deviceInfoSet, ref deviceInfoData, ref propChangeParams, Marshal.SizeOf(propChangeParams));
                if (!success)
                {
                    Console.WriteLine("Error setting class install params, error code = " + Marshal.GetLastWin32Error());
                    return false;
                }
                success = HidLibrary.NativeMethods.SetupDiCallClassInstaller(HidLibrary.NativeMethods.DIF_PROPERTYCHANGE, deviceInfoSet, ref deviceInfoData);
                if (!success)
                {
                    Console.WriteLine("Error enabling device, error code = " + Marshal.GetLastWin32Error());
                    return false;
                }

                HidLibrary.NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);

                return true;
            }
            catch
            {
                Console.WriteLine("Can't reenable device");
                return false;
            }
        }

        private static string DevicePathToInstanceId(string devicePath)
        {
            string deviceInstanceId = devicePath;
            deviceInstanceId = deviceInstanceId.Remove(0, deviceInstanceId.LastIndexOf('\\') + 1);
            deviceInstanceId = deviceInstanceId.Remove(deviceInstanceId.LastIndexOf('{'));
            deviceInstanceId = deviceInstanceId.Replace('#', '\\');
            if (deviceInstanceId.EndsWith("\\"))
            {
                deviceInstanceId = deviceInstanceId.Remove(deviceInstanceId.Length - 1);
            }
            return deviceInstanceId;
        }
    }
}
