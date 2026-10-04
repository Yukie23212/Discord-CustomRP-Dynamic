using BlackSharp.Core.Extensions;
using LibreHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace CustomRPC
{
    /// <summary>
    /// Reads CPU, GPU, RAM and VRAM information using LibreHardwareMonitor.
    /// </summary>
    public sealed class HardwareMonitor : IDisposable
    {
        private readonly Computer computer;
        private readonly UpdateVisitor updateVisitor;

        private bool isOpen;

        public float? CpuUsage { get; private set; }
        public float? CpuTemperature { get; private set; }

        public float? GpuUsage { get; private set; }
        public float? GpuTemperature { get; private set; }

        public double RamUsedGB { get; private set; }
        public double RamTotalGB { get; private set; }
        public double RamUsage { get; private set; }

        public double VramUsedGB { get; private set; }
        public double VramTotalGB { get; private set; }
        public double VramUsage { get; private set; }

        public string LastError { get; private set; } = "";

        public bool IsAvailable => isOpen;

        public HardwareMonitor()
        {
            updateVisitor = new UpdateVisitor();

            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true
            };

            try
            {
                computer.Open();
                isOpen = true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                isOpen = false;
            }
        }

        /// <summary>
        /// Updates all monitored hardware values.
        /// </summary>
        public void Update()
        {
            if (!isOpen)
                return;

            try
            {
                computer.Accept(updateVisitor);

                UpdateCpu();
                UpdateGpu();
                UpdateRam();

                LastError = "";
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        /// <summary>
        /// Updates CPU usage and temperature.
        /// </summary>
        private void UpdateCpu()
        {
            IHardware cpu = computer.Hardware.FirstOrDefault(
                h => h.HardwareType == HardwareType.Cpu
            );

            if (cpu == null)
            {
                CpuUsage = null;
                CpuTemperature = null;
                return;
            }

            ISensor cpuUsageSensor = FindCpuLoadSensor(cpu);
            ISensor cpuTemperatureSensor = FindCpuTemperatureSensor(cpu);

            CpuUsage = cpuUsageSensor?.Value;
            CpuTemperature = cpuTemperatureSensor?.Value;
        }

        /// <summary>
        /// Updates GPU usage, temperature and VRAM.
        /// </summary>
        private void UpdateGpu()
        {
            IHardware gpu = computer.Hardware.FirstOrDefault(
                h =>
                    h.HardwareType == HardwareType.GpuNvidia ||
                    h.HardwareType == HardwareType.GpuAmd ||
                    h.HardwareType == HardwareType.GpuIntel
            );

            if (gpu == null)
            {
                GpuUsage = null;
                GpuTemperature = null;

                VramUsedGB = 0;
                VramTotalGB = 0;
                VramUsage = 0;

                return;
            }

            ISensor gpuUsageSensor = FindGpuLoadSensor(gpu);
            ISensor gpuTemperatureSensor = FindGpuTemperatureSensor(gpu);

            GpuUsage = gpuUsageSensor?.Value;
            GpuTemperature = gpuTemperatureSensor?.Value;

            ISensor vramUsedSensor =
                FindSensorByName(
                    gpu,
                    SensorType.SmallData,
                    "GPU Memory Used"
                );

            ISensor vramTotalSensor =
                FindSensorByName(
                    gpu,
                    SensorType.SmallData,
                    "GPU Memory Total"
                );

            ISensor vramFreeSensor =
                FindSensorByName(
                    gpu,
                    SensorType.SmallData,
                    "GPU Memory Free"
                );

            double usedMB =
                vramUsedSensor?.Value ?? 0;

            double totalMB =
                vramTotalSensor?.Value ?? 0;

            double freeMB =
                vramFreeSensor?.Value ?? 0;

            // If Total isn't exposed, calculate it from Used + Free.
            if (totalMB <= 0 && usedMB > 0 && freeMB > 0)
                totalMB = usedMB + freeMB;

            VramUsedGB =
                usedMB / 1024.0;

            VramTotalGB =
                totalMB / 1024.0;

            if (VramTotalGB > 0)
            {
                VramUsage =
                    (VramUsedGB / VramTotalGB) * 100.0;
            }
            else
            {
                VramUsage = 0;
            }
        }

        /// <summary>
        /// Updates physical RAM usage using Windows.
        /// </summary>
        private void UpdateRam()
        {
            MEMORYSTATUSEX memoryStatus = new MEMORYSTATUSEX();

            if (!GlobalMemoryStatusEx(memoryStatus))
            {
                RamUsedGB = 0;
                RamTotalGB = 0;
                RamUsage = 0;
                return;
            }

            RamTotalGB =
                BytesToGB(memoryStatus.ullTotalPhys);

            double availableGB =
                BytesToGB(memoryStatus.ullAvailPhys);

            RamUsedGB =
                Math.Max(0, RamTotalGB - availableGB);

            if (RamTotalGB > 0)
            {
                RamUsage =
                    (RamUsedGB / RamTotalGB) * 100.0;
            }
            else
            {
                RamUsage = 0;
            }
        }

        /// <summary>
        /// Finds the CPU total load sensor.
        /// </summary>
        private static ISensor FindCpuLoadSensor(
            IHardware cpu
        )
        {
            ISensor sensor =
                FindSensorByName(
                    cpu,
                    SensorType.Load,
                    "CPU Total"
                );

            if (sensor != null)
                return sensor;

            // Fallback: average all CPU load sensors except memory-like sensors.
            List<ISensor> loadSensors =
                GetSensors(cpu, SensorType.Load)
                    .Where(s =>
                        !s.Name.Contains(
                            "Memory",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .ToList();

            if (loadSensors.Count == 0)
                return null;

            float total = 0;
            int count = 0;

            foreach (ISensor loadSensor in loadSensors)
            {
                if (loadSensor.Value.HasValue)
                {
                    total += loadSensor.Value.Value;
                    count++;
                }
            }

            if (count == 0)
                return null;

            // We cannot return a calculated value as ISensor,
            // so use the exact CPU Total sensor whenever possible.
            return loadSensors.FirstOrDefault(
                s => s.Name.Contains(
                    "Total",
                    StringComparison.OrdinalIgnoreCase
                )
            ) ?? loadSensors.FirstOrDefault();
        }

        /// <summary>
        /// Finds a sensible CPU temperature sensor.
        /// </summary>
        private static ISensor FindCpuTemperatureSensor(
            IHardware cpu
        )
        {
            string[] preferredNames =
            {
        "Core (Tctl/Tdie)",
        "Core (Tdie)",
        "Core (Tctl)",
        "CPU Package",
        "Core Max",
        "Package",
        "Core Average"
    };

            foreach (string name in preferredNames)
            {
                ISensor sensor =
                    FindSensorByName(
                        cpu,
                        SensorType.Temperature,
                        name
                    );

                if (sensor != null &&
                    sensor.Value.HasValue &&
                    sensor.Value.Value > 0)
                {
                    return sensor;
                }
            }

            return GetSensors(
                cpu,
                SensorType.Temperature
            ).FirstOrDefault(
                sensor =>
                    sensor.Value.HasValue &&
                    sensor.Value.Value > 0
            );
        }

        /// <summary>
        /// Finds GPU utilization.
        /// </summary>
        private static ISensor FindGpuLoadSensor(
            IHardware gpu
        )
        {
            ISensor sensor =
                FindSensorByName(
                    gpu,
                    SensorType.Load,
                    "GPU Core"
                );

            if (sensor != null)
                return sensor;

            sensor =
                GetSensors(
                    gpu,
                    SensorType.Load
                ).FirstOrDefault(
                    s =>
                        s.Name.Contains(
                            "3D",
                            StringComparison.OrdinalIgnoreCase
                        )
                );

            if (sensor != null)
                return sensor;

            return GetSensors(
                gpu,
                SensorType.Load
            ).FirstOrDefault(
                s =>
                    !s.Name.Contains(
                        "Memory",
                        StringComparison.OrdinalIgnoreCase
                    )
            );
        }

        /// <summary>
        /// Finds GPU core temperature.
        /// </summary>
        private static ISensor FindGpuTemperatureSensor(
            IHardware gpu
        )
        {
            string[] preferredNames =
            {
                "GPU Core",
                "GPU Temperature",
                "Temperature",
                "GPU Hot Spot"
            };

            foreach (string name in preferredNames)
            {
                ISensor sensor =
                    FindSensorByName(
                        gpu,
                        SensorType.Temperature,
                        name
                    );

                if (sensor != null)
                    return sensor;
            }

            return GetSensors(
                gpu,
                SensorType.Temperature
            ).FirstOrDefault();
        }

        /// <summary>
        /// Finds a sensor by exact name.
        /// </summary>
        private static ISensor FindSensorByName(
            IHardware hardware,
            SensorType sensorType,
            string name
        )
        {
            return GetSensors(
                hardware,
                sensorType
            ).FirstOrDefault(
                sensor =>
                    string.Equals(
                        sensor.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase
                    )
            );
        }

        /// <summary>
        /// Gets sensors from hardware and its subhardware.
        /// </summary>
        private static IEnumerable<ISensor> GetSensors(
            IHardware hardware,
            SensorType sensorType
        )
        {
            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType == sensorType)
                    yield return sensor;
            }

            foreach (IHardware subHardware in hardware.SubHardware)
            {
                foreach (ISensor sensor in GetSensors(
                    subHardware,
                    sensorType
                ))
                {
                    yield return sensor;
                }
            }
        }

        private static double BytesToGB(double bytes)
        {
            return bytes /
                   (1024.0 * 1024.0 * 1024.0);
        }

        public void Dispose()
        {
            if (!isOpen)
                return;

            try
            {
                computer.Close();
            }
            catch
            {
            }

            isOpen = false;
        }

        /// <summary>
        /// Visitor used to refresh the LibreHardwareMonitor sensors.
        /// </summary>
        private sealed class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                computer.Traverse(this);
            }

            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
            }

            public void VisitSensor(ISensor sensor)
            {
            }

            public void VisitParameter(IParameter parameter)
            {
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private sealed class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength =
                    (uint)Marshal.SizeOf(
                        typeof(MEMORYSTATUSEX)
                    );
            }
        }

        [DllImport(
            "kernel32.dll",
            SetLastError = true
        )]
        private static extern bool GlobalMemoryStatusEx(
            [In, Out] MEMORYSTATUSEX lpBuffer
        );
    }
}