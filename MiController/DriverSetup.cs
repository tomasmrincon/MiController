using System;
using System.IO;
using System.Reflection;
using MiController.DriverUtils;

namespace MiController
{
    public class DriverSetup
    {
        private static readonly Guid Ds3BusClassGuid = new Guid("f679f562-3164-42ce-a4db-e7ddbe723909");
        public static void Install()
        {
            string driverDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "ScpDriver");
            string devPath = string.Empty;
            string instanceId = string.Empty;

            var flags = DifxFlags.DRIVER_PACKAGE_ONLY_IF_DEVICE_PRESENT;

            //if (force)
            //    flags |= DifxFlags.DRIVER_PACKAGE_FORCE;


            if (!Devcon.Find(Ds3BusClassGuid, ref devPath, ref instanceId))
            {
                bool virtualBusCreated = Devcon.Create("System", new Guid("{4D36E97D-E325-11CE-BFC1-08002BE10318}"),
                    "root\\ScpVBus\0\0");
            }

            var installer = Difx.Factory(driverDir);
            uint result = installer.Install(Path.Combine(driverDir, @"ScpVBus.inf"), flags, out var rebootRequired);


            if (result != 0)
            {
                Uninstall();
                throw new Exception("Driver installation failed. Error code: " + result);
            }
        }

        public static void Uninstall()
        {
            string driverDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "ScpDriver");

            string devPath = string.Empty;
            string instanceId = string.Empty;

            if (!Devcon.Find(Ds3BusClassGuid, ref devPath, ref instanceId))
            {
                Devcon.Remove(Ds3BusClassGuid, devPath, instanceId);
            }

            var installer = Difx.Factory(driverDir);
            installer.Uninstall(Path.Combine(driverDir, @"ScpVBus.inf"),
                DifxFlags.DRIVER_PACKAGE_DELETE_FILES,
                out var rebootRequired);
        }
    }
}
