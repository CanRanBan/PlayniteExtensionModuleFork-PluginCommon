﻿using CommonPlayniteShared.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text;

namespace CommonPluginsShared
{
    public class LocalSystem
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private SystemConfiguration systemConfiguration;
        private int IdConfiguration = -1;
        private List<SystemConfiguration> Configurations = new List<SystemConfiguration>();


        public LocalSystem(string ConfigurationsPath, bool WithDiskInfos = true)
        {
            systemConfiguration = GetPcInfo(WithDiskInfos);

            if (File.Exists(ConfigurationsPath))
            {
                try
                {
                    string JsonStringData = FileSystem.ReadFileAsStringSafe(ConfigurationsPath);
                    Configurations = Serialization.FromJson<List<SystemConfiguration>>(JsonStringData);
                }
                catch (Exception ex)
                {
                    Common.LogError(ex, false, $"Failed to load {ConfigurationsPath}");
                }
            }

            IdConfiguration = Configurations?.FindIndex(x => 
                    x.Cpu == systemConfiguration.Cpu && 
                    x.Name == systemConfiguration.Name && 
                    x.GpuName == systemConfiguration.GpuName && 
                    x.RamUsage == systemConfiguration.RamUsage
            ) ?? -1;

            if (IdConfiguration == -1)
            {
                Configurations.Add(systemConfiguration);
                FileSystem.WriteStringToFileSafe(ConfigurationsPath, Serialization.ToJson(Configurations));

                IdConfiguration = Configurations.Count - 1;
            }
            // TODO Temp
            else
            {
                Configurations[IdConfiguration].CurrentHorizontalResolution = systemConfiguration.CurrentHorizontalResolution;
                Configurations[IdConfiguration].CurrentVerticalResolution = systemConfiguration.CurrentVerticalResolution;
                FileSystem.WriteStringToFileSafe(ConfigurationsPath, Serialization.ToJson(Configurations));
            }
        }


        private bool CallIsNvidia(string GpuName)
        {
            if (GpuName.IsNullOrEmpty())
            {
                return false;
            }
            return (GpuName.ToLower().IndexOf("nvidia") > -1 || GpuName.ToLower().IndexOf("geforce") > -1 || GpuName.ToLower().IndexOf("gtx") > -1 || GpuName.ToLower().IndexOf("rtx") > -1);
        }
        private bool CallIsAmd(string GpuName)
        {
            if (GpuName.IsNullOrEmpty())
            {
                return false;
            }
            return (GpuName.ToLower().IndexOf("amd") > -1 || GpuName.ToLower().IndexOf("radeon") > -1 || GpuName.ToLower().IndexOf("ati ") > -1);
        }
        private bool CallIsIntel(string GpuName)
        {
            if (GpuName.IsNullOrEmpty())
            {
                return false;
            }
            return GpuName.ToLower().IndexOf("intel") > -1;
        }


        /// <summary>
        /// Get actual system configuration
        /// </summary>
        /// <returns></returns>
        public SystemConfiguration GetSystemConfiguration()
        {
            return systemConfiguration;
        }

        /// <summary>
        /// Get configurations list saved
        /// </summary>
        /// <returns></returns>
        public List<SystemConfiguration> GetConfigurations()
        {
            return Configurations;
        }

        /// <summary>
        /// Get Id in configuration list for actual system configuration
        /// </summary>
        /// <returns></returns>
        public int GetIdConfiguration()
        {
            return IdConfiguration;
        }


        private SystemConfiguration GetPcInfo(bool WithDiskInfos = true)
        {
            string Name = Environment.MachineName;

            SystemConfiguration systemConfiguration = new SystemConfiguration();


            #region Disks infos
            List <SystemDisk> Disks = new List<SystemDisk>();
            if (WithDiskInfos)
            {
                Disks = GetInfoDisks();
            }
            #endregion


            #region System informations
            string Os = string.Empty;
            string Cpu = string.Empty;
            uint CpuMaxClockSpeed = 0;
            string GpuName = string.Empty;
            long GpuRam = 0;
            uint CurrentHorizontalResolution = 0;
            uint CurrentVerticalResolution = 0;
            long Ram = 0;


            // OS
            try
            {
                ManagementObjectSearcher myOperativeSystemObject = new ManagementObjectSearcher("select * from Win32_OperatingSystem");
                foreach (ManagementObject obj in myOperativeSystemObject.Get())
                {
                    Os = obj["Name"]?.ToString().Split('|')[0];
                    Common.LogDebug(true, $"Os: {Os}");
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error on Win32_OperatingSystem");
            }

            // CPU
            try
            {
                ManagementObjectSearcher myProcessorObject = new ManagementObjectSearcher("select * from Win32_Processor");
                foreach (ManagementObject obj in myProcessorObject.Get())
                {
                    Cpu = obj["Name"]?.ToString();
                    Common.LogDebug(true, $"Cpu: {Cpu}");

                    if (obj["MaxClockSpeed"] != null)
                    {
                        uint.TryParse(obj["MaxClockSpeed"].ToString(), out CpuMaxClockSpeed);
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error on Win32_Processor");
            }

            // GPU
            try
            {
                ManagementObjectSearcher myVideoObject = new ManagementObjectSearcher("select * from Win32_VideoController");
                foreach (ManagementObject obj in myVideoObject.Get())
                {
                    GpuName = obj["Name"]?.ToString();
                    Common.LogDebug(true, $"Gpu: {GpuName}");

                    if (CallIsNvidia(GpuName) || CallIsAmd(GpuName) || CallIsIntel(GpuName))
                    { 
                        if (obj["AdapterRAM"] != null)
                        {
                            long.TryParse(obj["AdapterRAM"].ToString(), out GpuRam);
                        }

                        if (obj["CurrentHorizontalResolution"] != null)
                        {
                            uint.TryParse(obj["CurrentHorizontalResolution"].ToString(), out CurrentHorizontalResolution);
                        }

                        if (obj["CurrentVerticalResolution"] != null)
                        {
                            uint.TryParse(obj["CurrentVerticalResolution"].ToString(), out CurrentVerticalResolution);
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error on Win32_VideoController");
            }

            // RAM
            try
            {
                ManagementObjectSearcher myComputerSystemObject = new ManagementObjectSearcher("select * from Win32_ComputerSystem");
                foreach (ManagementObject obj in myComputerSystemObject.Get())
                {
                    double TempRam = 0;

                    if (obj["TotalPhysicalMemory"] != null)
                    {
                        double.TryParse(obj["TotalPhysicalMemory"].ToString(), out TempRam);
                    }

                    TempRam = Math.Ceiling(TempRam / 1024 / 1024 / 1024);
                    Ram = (long)(TempRam * 1024 * 1024 * 1024);
                }
            }
            catch (Exception ex)
            {
                Common.LogError(ex, false, "Error on Win32_ComputerSystem");
            }
            #endregion


            systemConfiguration.Name = Name?.Trim();
            systemConfiguration.Os = Os?.Trim();
            systemConfiguration.Cpu = Cpu?.Trim();
            systemConfiguration.CpuMaxClockSpeed = CpuMaxClockSpeed;
            systemConfiguration.GpuName = GpuName?.Trim();
            systemConfiguration.GpuRam = GpuRam;
            systemConfiguration.CurrentHorizontalResolution = CurrentHorizontalResolution;
            systemConfiguration.CurrentVerticalResolution = CurrentVerticalResolution;
            systemConfiguration.Ram = Ram;
            systemConfiguration.RamUsage = Tools.SizeSuffix(Ram, true);
            systemConfiguration.Disks = Disks;

            return systemConfiguration;
        }

        private List<SystemDisk> GetInfoDisks()
        {
            List<SystemDisk> Disks = new List<SystemDisk>();
            DriveInfo[] allDrives = DriveInfo.GetDrives();

            foreach (DriveInfo d in allDrives)
            {
                if (d.DriveType == DriveType.Fixed)
                {
                    string VolumeLabel = string.Empty;
                    try
                    {
                        VolumeLabel = d.VolumeLabel;
                    }
                    catch (Exception ex)
                    {
                        logger.Warn($"Error on VolumeLabel - {ex.Message.Trim()}");
                    }

                    string Name = string.Empty;
                    try
                    {
                        Name = d.Name;
                    }
                    catch (Exception ex)
                    {
                        logger.Warn($"Error on Name - {ex.Message.Trim()}");
                    }

                    long FreeSpace = 0;
                    try
                    {
                        FreeSpace = d.TotalFreeSpace;
                    }
                    catch (Exception ex)
                    {
                        logger.Warn($"Error on TotalFreeSpace - {ex.Message.Trim()}");
                    }

                    string FreeSpaceUsage = string.Empty;
                    try
                    {
                        FreeSpaceUsage = Tools.SizeSuffix(d.TotalFreeSpace);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn($"Error on FreeSpaceUsage - {ex.Message.Trim()}");
                    }

                    Disks.Add(new SystemDisk
                    {
                        Name = VolumeLabel,
                        Drive = Name,
                        FreeSpace = FreeSpace,
                        FreeSpaceUsage = FreeSpaceUsage
                    });
                }
            }

            return Disks;
        }
    }


    public class SystemConfiguration
    {
        public string Name { get; set; }
        public string Os { get; set; }
        public string Cpu { get; set; }
        public uint CpuMaxClockSpeed { get; set; }
        public string GpuName { get; set; }
        public long GpuRam { get; set; }
        public uint CurrentVerticalResolution { get; set; }
        public uint CurrentHorizontalResolution { get; set; }
        public long Ram { get; set; }
        public string RamUsage { get; set; }
        public List<SystemDisk> Disks { get; set; }
    }


    public class SystemDisk
    {
        public string Name { get; set; }
        public string Drive { get; set; }
        public long FreeSpace { get; set; }
        public string FreeSpaceUsage { get; set; }
    }
}
