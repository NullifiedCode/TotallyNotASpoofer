using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using MelonLoader;
using UnhollowerBaseLib;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace TotallyNotASpoofer
{
    public class Main : MelonPlugin
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        private static bool hasLoadedSteam = true;
        private static int beforeMemory = 0, beforeGraphics = 0, beforeProcessor = 0, beforeMHz = 0;
        private static string beforeHwid = "";
        private static ulong SteamId = 0;

        private static long Long()
        {
            long min = 10000000000001;
            long max = 99999999999999;
            System.Random random = new System.Random();
            long randomNumber = min + random.Next() % (max - min);
            return randomNumber;
        }

        private static string RandomString(int length)
        {
            var chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOP";
            var output = new StringBuilder();
            var random = new System.Random();

            for (int i = 0; i < length; i++)
                output.Append(chars[random.Next(chars.Length)]);
            return output.ToString();
        }

        public static void FindPatchSteamID(string path)
        {
            var library = LoadLibrary(path);
            if (library == IntPtr.Zero)
            {
                MelonLogger.Error($"Library load failed; used path: {path}");
                hasLoadedSteam = false;
                return;
            }
            // Generates random ulong for a SteamID
            SteamId = (ulong)Long();

            // Methods to intercept to prevent steam from setting/getting steamid
            var names = new[]
            {
                "Breakpad_SteamSetSteamID",
                "SteamAPI_ISteamGameServer_GetSteamID",
                "SteamAPI_ISteamInventory_CheckResultSteamID",
                "SteamAPI_ISteamRemotePlay_GetSessionSteamID",
                "SteamAPI_ISteamUser_GetSteamID",
                "SteamAPI_SteamNetworkingIdentity_GetSteamID",
                "SteamAPI_SteamNetworkingIdentity_GetSteamID64",
                "SteamAPI_SteamNetworkingIdentity_SetSteamID",
                "SteamAPI_SteamNetworkingIdentity_SetSteamID64",
                "SteamGameServer_GetSteamID",
                "ISteamUser_GetSteamID",
                "GetSteamID64_Public_UInt64_0",
                "GetSteamID_Public_CSteamID_0",
                "SteamAPI_ISteamUser_GetHSteamUser",
                "SteamGameServer_GetHSteamUser",
               
            };

            // Method to intercept to prevent steam authentication tickets. This will break some games requiring steam auth (https://partner.steamgames.com/doc/features/auth)
            unsafe
            {
                var address = GetProcAddress(library, "SteamAPI_ISteamUser_GetAuthSessionTicket");
                if (address != IntPtr.Zero)
                {

                    MelonUtils.NativeHookAttach((IntPtr)(&address),
                        AccessTools.Method(typeof(Main), nameof(NullifyString)).MethodHandle
                            .GetFunctionPointer());
                }
            }


            // Lets foreach all names and intercept them. This is a major WIP dont laugh at me
            foreach (var name in names)
            {
                unsafe
                {
                    var address = GetProcAddress(library, name);
                    if (address == IntPtr.Zero)
                    {
                        continue;
                    }
                    MelonUtils.NativeHookAttach((IntPtr)(&address),
                        AccessTools.Method(typeof(Main), nameof(ReturnID)).MethodHandle
                            .GetFunctionPointer());
                    MelonLogger.Msg(name + " Patched.");
                }
                unsafe
                {
                    var address = GetProcAddress(library, "NativeMethodInfoPtr_" + name);
                    if (address == IntPtr.Zero)
                    {
                        continue;
                    }
                    MelonUtils.NativeHookAttach((IntPtr)(&address),
                        AccessTools.Method(typeof(Main), nameof(ReturnID)).MethodHandle
                            .GetFunctionPointer());
                    MelonLogger.Msg(name + " Patched.");
                }
                unsafe
                {
                    var address = GetProcAddress(library, name.Replace("Steam_", ""));
                    if (address == IntPtr.Zero)
                    {
                        continue;
                    }
                    MelonUtils.NativeHookAttach((IntPtr)(&address),
                        AccessTools.Method(typeof(Main), nameof(ReturnID)).MethodHandle
                            .GetFunctionPointer());
                    MelonLogger.Msg(name + " Patched.");
                }
            }
        }
      
        public static ulong ReturnID()
        {
            return SteamId;
        }

        private static string[] names =
        {
            "GetDeviceUniqueIdentifier",
            "GetDeviceModel",
            "GetDeviceName",
            "GetGraphicsDeviceName",
            "GetGraphicsDeviceID",
            "GetProcessorType",
            "GetOperatingSystem",
            "GetProcessorCount",
            "GetGraphicsMemorySize",
            "GetPhysicalMemoryMB",
            "GetGraphicsDeviceVersion",
            "GetProcessorFrequencyMHz",
            "GetOperatingSystemFamily",
            "GetGraphicsDeviceType",
            "GetDeviceType"
        };

        public static List<string> foundFunctions = new List<string>();
        public static unsafe void FindPatchHWIDDetails()
        {
            // Grab old details store them for further use.
            beforeHwid = SystemInfo.deviceUniqueIdentifier;
            beforeMemory = SystemInfo.systemMemorySize;
            beforeGraphics = SystemInfo.GetGraphicsMemorySize();
            beforeProcessor = SystemInfo.GetProcessorCount();
            beforeMHz = SystemInfo.GetProcessorFrequencyMHz();
            foundFunctions = new List<string>();

            // This is probably a shit way but it was easy and just works
            var resolveFunctionName = "";
            foreach(var name in names)
            {
                switch (name)
                {
                    case "GetDeviceUniqueIdentifier": resolveFunctionName = "GetRandomDeviceID"; break;
                    case "GetDeviceModel": resolveFunctionName = "GetRandomModel"; break;
                    case "GetDeviceName": resolveFunctionName = "GetRandomName"; break;
                    case "GetGraphicsDeviceName": resolveFunctionName = "GetRandomGPU"; break;
                    case "GetGraphicsDeviceID": resolveFunctionName = "GetRandomGPUID"; break;
                    case "GetProcessorType": resolveFunctionName = "GetRandomProcessor"; break;
                    case "GetOperatingSystem": resolveFunctionName = "GetRandomOS"; break;
                    case "GetProcessorCount": resolveFunctionName = "GetRandomNumberProcessor"; break;
                    case "GetGraphicsMemorySize": resolveFunctionName = "GetRandomGraphicsSize"; break;
                    case "GetPhysicalMemoryMB": resolveFunctionName = "GetRandomMemoryRam"; break;
                    case "GetGraphicsDeviceVersion": resolveFunctionName = "GetRandomDirectXVersion"; break;
                    case "GetProcessorFrequencyMHz": resolveFunctionName = "GetRandomProcessorMHz"; break;
                    case "GetOperatingSystemFamily": resolveFunctionName = "GetRandomOperatingFamily"; break;
                    case "GetGraphicsDeviceType": resolveFunctionName = "GetRandomGraphicsType"; break;
                    case "GetDeviceType": resolveFunctionName = "GetRandomDeviceType"; break;
                }

                IntPtr ptr = IL2CPP.il2cpp_resolve_icall("UnityEngine.SystemInfo::"+name);
                if(ptr == IntPtr.Zero)
                {
                    MelonLogger.Msg("Cannot find ptr for UnityEngine.SystemInfo::" + name);
                    return;
                }

                if(!foundFunctions.Contains(resolveFunctionName))
                    foundFunctions.Add(resolveFunctionName);
                MelonUtils.NativeHookAttach((IntPtr)((void*)(&ptr)), AccessTools.Method(typeof(Main), resolveFunctionName, null, null).MethodHandle.GetFunctionPointer());
            }

            // Give me the information it has spoofed.
            // Keep in mind some of these functions do not exist in older versions of unity. Meaning you will have to modify the code yourself
            // Edit fixed some stufff this works, So far atleast. Ik its a crap way but works.
            if (foundFunctions.Contains("GetRandomDeviceID")) MelonLogger.Msg("HWID New: " + SystemInfo.deviceUniqueIdentifier); else MelonLogger.Error("Failed to spoof HWID");
            if (foundFunctions.Contains("GetRandomName")) MelonLogger.Msg("Name New: " + SystemInfo.deviceName); else MelonLogger.Error("Failed to spoof Name");
            if (foundFunctions.Contains("GetRandomModel")) MelonLogger.Msg("Model New: " + SystemInfo.deviceModel); else MelonLogger.Error("Failed to spoof Model");
            if (foundFunctions.Contains("GetRandomGPU")) MelonLogger.Msg("GPU New: " + SystemInfo.graphicsDeviceName); else MelonLogger.Error("Failed to spoof GPU");
            if (foundFunctions.Contains("GetRandomProcessor")) MelonLogger.Msg("CPU New: " + SystemInfo.processorType); else MelonLogger.Error("Failed to spoof Processor");
            if (foundFunctions.Contains("GetRandomGPUID")) MelonLogger.Msg("GPU ID New: " + SystemInfo.graphicsDeviceID.ToString()); else MelonLogger.Error("Failed to spoof GPU ID");
            if (foundFunctions.Contains("GetRandomOS")) MelonLogger.Msg("OS New: " + SystemInfo.operatingSystem); else MelonLogger.Error("Failed to spoof OS");
            if (foundFunctions.Contains("GetRandomNumberProcessor")) MelonLogger.Msg("Processer Count New: " + SystemInfo.processorCount); else MelonLogger.Error("Failed to spoof Processor Count");
            if (foundFunctions.Contains("GetRandomProcessorMHz")) MelonLogger.Msg("Processer MHz New: " + SystemInfo.GetProcessorFrequencyMHz()); else MelonLogger.Error("Failed to spoof Processor MHz");
            if (foundFunctions.Contains("GetRandomMemoryRam")) MelonLogger.Msg("Ram New: " + SystemInfo.systemMemorySize); else MelonLogger.Error("Failed to spoof Ram size");
            if (foundFunctions.Contains("GetRandomDirectXVersion")) MelonLogger.Msg("DirectX Version New: " + SystemInfo.GetGraphicsDeviceVersion()); else MelonLogger.Error("Failed to spoof Direct X Version");
            if (foundFunctions.Contains("GetRandomOperatingFamily")) MelonLogger.Msg("Operating System Family New: " + SystemInfo.GetOperatingSystemFamily()); else MelonLogger.Error("Failed to spoof Operating System Family");
            if (foundFunctions.Contains("GetRandomGraphicsType")) MelonLogger.Msg("Graphics Type New: " + SystemInfo.GetGraphicsDeviceType()); else MelonLogger.Error("Failed to spoof Graphics Type");
            if (foundFunctions.Contains("GetRandomDeviceType")) MelonLogger.Msg("Device Type New: " + SystemInfo.GetDeviceType()); else MelonLogger.Error("Failed to spoof Device Type");
            if (hasLoadedSteam)
            {
                if (SteamId > 0)
                    MelonLogger.Msg("SteamID New: " + SteamId);
                else
                    MelonLogger.Error("SteamID Failed to spoof.");
            }
            MelonLogger.Warning("Unity Version: " + Application.unityVersion);
            if(hasLoadedSteam) MelonLogger.Warning("Broke steam auth ticket sessions. Some functions will not work.");
            MelonLogger.Warning("Hardware Details Spoofed!");
            foundFunctions.Clear();
        }

        public static IntPtr GetRandomDeviceID()
		{
			return IL2CPP.ManagedStringToIl2Cpp(RandomString(beforeHwid.Length));
		}

        public static int GetRandomMemoryRam()
        {
            return (int)new System.Random().Next(0, beforeMemory);
        }

        public static int GetRandomGraphicsSize()
        {
            return (int)new System.Random().Next(0, beforeGraphics);
        }

        public static int GetRandomNumberProcessor()
        {
            return (int)new System.Random().Next(0, beforeProcessor);
        }

        public static int GetRandomProcessorMHz()
        {
            return (int)new System.Random().Next(0, beforeMHz);
        }

        public static DeviceType GetRandomDeviceType()
        {
                return (DeviceType)Random.Range(0f, Enum.GetValues(typeof(DeviceType)).Length);
        }
        
        public static GraphicsDeviceType GetRandomGraphicsType()
        {
            return (GraphicsDeviceType)Random.Range(0f, Enum.GetValues(typeof(GraphicsDeviceType)).Length);
        }

        public static IntPtr GetRandomDirectXVersion()
        {
            return IL2CPP.ManagedStringToIl2Cpp("Direct3D "+ Math.Round(Random.Range(0f, 99f)).ToString() + ".0"+ $" [level {Math.Round(Random.Range(0f, 99f))}.{Math.Round(Random.Range(0f, 99f))}]");
        }
        
        public static OperatingSystemFamily GetRandomOperatingFamily()
        {
            return (OperatingSystemFamily)Random.Range(0f, Enum.GetValues(typeof(OperatingSystemFamily)).Length);
        }
       
        public static IntPtr GetRandomModel()
		{
			return IL2CPP.il2cpp_string_new(Main.Motherboards[new System.Random().Next(0, Main.Motherboards.Length)]);
		}

		public static IntPtr GetRandomName()
		{
			return IL2CPP.ManagedStringToIl2Cpp("DESKTOP-" + RandomString(10));
		}
		public static IntPtr GetRandomGPU()
		{
			return IL2CPP.ManagedStringToIl2Cpp(Main.PBU[new System.Random().Next(0, Main.PBU.Length)]);
		}

		public static IntPtr GetRandomGPUID()
		{
			return IL2CPP.ManagedStringToIl2Cpp(RandomString(30));
		}

		public static IntPtr GetRandomProcessor()
		{
			return IL2CPP.ManagedStringToIl2Cpp(Main.CPU[new System.Random().Next(0, Main.CPU.Length)]);
		}

		public static IntPtr GetRandomOS()
		{
			return IL2CPP.ManagedStringToIl2Cpp(Main.OS[new System.Random().Next(0, Main.OS.Length)]);
		}

        public static IntPtr NullifyString()
        {
            return IL2CPP.il2cpp_string_new(Math.Round(Random.Range(18, (float)99999999999999999)).ToString());
        }

        private static string[] PBU = new string[] { "GeForce GTX 1630", "GeForce MX550", "GeForce MX570", "GeForce MX570", "GeForce MX570 A", "GeForce RTX 2050 Mobile", "GeForce RTX 2060 12 GB", "GeForce RTX 3050 4 GB", "GeForce RTX 3050 8 GB", "GeForce RTX 3050 8 GB GA107", "GeForce RTX 3050 Max-Q", "GeForce RTX 3050 Max-Q Refresh", "GeForce RTX 3050 Mobile", "GeForce RTX 3050 Mobile Refresh", "GeForce RTX 3050 OEM", "GeForce RTX 3050 Ti Mobile", "GeForce RTX 3060 8 GB", "GeForce RTX 3060 12 GB GA104", "GeForce RTX 3060 Ti GA103", "GeForce RTX 3060 Ti GDDR6X", "GeForce RTX 3070 Ti", "GeForce RTX 3070 Ti 8 GB GA102", "GeForce RTX 3070 Ti 16 GB", "GeForce RTX 3070 Ti Max-Q", "GeForce RTX 3070 Ti Mobile", "GeForce RTX 3070 TiM", "GeForce RTX 3080 12 GB", "GeForce RTX 3080 Ti", "GeForce RTX 3080 Ti 20 GB", "GeForce RTX 3080 Ti Max-Q", "GeForce RTX 3080 Ti Mobile", "GeForce RTX 3090 Ti", "GeForce RTX 4050", "GeForce RTX 4050 Mobile", "GeForce RTX 4060", "GeForce RTX 4060 Mobile", "GeForce RTX 4060 Ti", "GeForce RTX 4060 Ti Mobile", "GeForce RTX 4070", "GeForce RTX 4070 Max-Q", "GeForce RTX 4070 Mobile", "GeForce RTX 4070 Ti", "GeForce RTX 4080 12 GB", "GeForce RTX 4080", "GeForce RTX 4080 Ti", "GeForce RTX 4080 Ti Max-Q", "GeForce RTX 4080 Ti Mobile", "GeForce RTX 4090", "GeForce RTX 4090 Mobile", "GeForce RTX 4090 Ti", "Radeon 660M", "Radeon 680M", "Radeon Graphics 448SP", "Radeon Instinct MI210", "Radeon Instinct MI250", "Radeon Instinct MI250X", "Radeon Pro V620", "Radeon PRO W6300", "Radeon PRO W6300M", "Radeon PRO W6400", "Radeon PRO W6500M", "Radeon PRO W6600", "Radeon PRO W6600M", "Radeon PRO W6800", "Radeon PRO W6800X", "Radeon PRO W6800X Duo", "Radeon PRO W6900X", "Radeon RX 6300M", "Radeon RX 6400", "Radeon RX 6500 XT", "Radeon RX 6500M", "Radeon RX 6550S", "Radeon RX 6600", "Radeon RX 6600 XT", "Radeon RX 6600M", "Radeon RX 6600S", "Radeon RX 6650 XT", "Radeon RX 6650M", "Radeon RX 6650M XT", "Radeon RX 6700", "Radeon RX 6700 XT", "Radeon RX 6700M", "Radeon RX 6700S", "Radeon RX 6750 XT", "Radeon RX 6800M", "Radeon RX 6800S", "Radeon RX 6850M XT", "Radeon RX 6950 XT", "Radeon RX 7700 XT", "Radeon RX 7800 XT", "Radeon RX 7900 XT", "Radeon RX 7900 XTX", "Radeon RX 7950 XT", "Radeon RX 7950 XTX", "Radeon RX 7990 XTX", "Radeon Vega 6", "Radeon Vega 6 Mobile", "Radeon Vega 7", "Radeon Vega 7 Mobile", "Radeon Vega 8 Mobile" };
        private static string[] CPU = new string[] { "A4 PRO-7300B", "A4 PRO-7350B", "A4-5300B", "A4-6300B", "A4-6320B", "A6 PRO-7400B", "A6-5400B", "A6-6400B", "A6-9120C SoC", "A6-9220C SoC", "A8 PRO-7600B", "A8-5500B", "A8-6500B", "A9-9400 SoC", "A9-9410 SoC", "A9-9420 SoC", "A9-9425 SoC", "A10 PRO-7800B", "A10 PRO-7850B", "A10-5800B", "A10-6790B", "Athlon 3000G", "Athlon Gold 3150G", "Athlon Gold 3150GE", "Athlon Gold PRO 3150G", "Athlon Gold PRO 3150GE", "Athlon PRO 300U", "Athlon Silver 3050GE", "Athlon Silver PRO 3125GE", "Atom S1220", "Atom S1240", "Atom S1260", "Celeron 7300", "Celeron 7305", "Celeron B720", "Celeron B730", "Celeron B820", "Celeron B830", "Celeron G6900", "Celeron G6900E", "Celeron G6900T", "Celeron G6900TE", "Centaur CHA", "Core 2 Duo E4400", "Core 2 Duo E4500", "Core 2 Duo E4600", "Core 2 Duo E4700", "Core 2 Duo E6320", "Core 2 Duo E6550", "Core 2 Duo E6750", "Core 2 Duo E6850", "Core 2 Duo E7200", "Core 2 Duo E7300", "Core 2 Duo E7400", "Core 2 Duo E7500", "Core 2 Duo E7600", "Core 2 Duo E8135", "Core 2 Duo E8190", "Core 2 Duo E8200", "Core 2 Duo E8235", "Core 2 Duo E8300", "Core 2 Duo E8335", "Core 2 Duo E8400", "Core 2 Duo E8435", "Core 2 Duo E8500", "Core 2 Duo E8600", "Core 2 Duo P8400", "Core 2 Duo P8600", "Core 2 Duo P8700", "Core 2 Duo P8800", "Core 2 Duo P9500", "Core 2 Duo P9600", "Core 2 Duo P9700", "Core 2 Duo T7100", "Core 2 Duo T7250", "Core 2 Duo T7500", "Core 2 Duo T7700", "Core 2 Duo T7800", "Core 2 Duo T8100", "Core 2 Duo T8300", "Core 2 Duo T9300", "Core 2 Duo T9400", "Core 2 Duo T9500", "Core 2 Duo T9550", "Core 2 Duo T9600", "Core 2 Duo T9800", "Core 2 Duo T9900", "Core 2 Duo U7500", "Core 2 Duo U7500", "Core 2 Duo U7600", "Core 2 Duo U7600", "Core 2 Duo U7700", "Core 2 Duo U7700", "Core i3-1000G1", "Core i3-1000G4", "Core i3-1005G1", "Core i3-1110G4", "Core i3-1115G4", "Core i3-1120G4", "Core i3-1125G4", "Core i3-1210U", "Core i3-1215U", "Core i3-1215U (IPU)", "Core i3-1220P", "Core i3-8000", "Core i3-8020", "Core i3-8100", "Core i3-8100T", "Core i3-8109U", "Core i3-8120", "Core i3-8121U", "Core i3-8130U", "Core i3-8145U", "Core i3-8300", "Core i3-8300T", "Core i3-8350K", "Core i3-9000", "Core i3-9100", "Core i3-9100F", "Core i3-9300", "Core i3-9320", "Core i3-9350K", "Core i3-9350KF", "Core i3-10100", "Core i3-10105", "Core i3-10105F", "Core i3-10105T", "Core i3-10110U", "Core i3-10110Y", "Core i3-10300", "Core i3-10305", "Core i3-10305T", "Core i3-10320", "Core i3-10325", "Core i3-10350K", "Core i3-12100", "Core i3-12100E", "Core i3-12100F", "Core i3-12100T", "Core i3-12100TE", "Core i3-12300", "Core i3-12300HE", "Core i3-12300T", "Core i5-1038NG7", "Core i5-1130G7", "Core i5-1135G7", "Core i5-1145G7", "Core i5-1155G7", "Core i5-1230U", "Core i5-1235U", "Core i5-1235U (IPU)", "Core i5-1240P", "Core i5-1240U", "Core i5-1245U", "Core i5-1250P", "Core i5-4430S", "Core i5-4440S", "Core i5-4570S", "Core i5-4590S", "Core i5-4670S", "Core i5-6350HQ", "Core i5-7300HQ", "Core i5-7600K", "Core i5-8500B", "Core i5-8600K", "Core i5-8650K", "Core i5-9400F", "Core i5-9500F", "Core i5-9600K", "Core i5-9600KF", "Core i5-10200H", "Core i5-10300H", "Core i5-10400", "Core i5-10400F", "Core i5-10600K", "Core i5-10600KF", "Core i5-11260H", "Core i5-11300H", "Core i5-11320H", "Core i5-11400", "Core i5-11400F", "Core i5-11400H", "Core i5-11400T", "Core i5-11500", "Core i5-11500H", "Core i5-11500T", "Core i5-11600", "Core i5-11600K", "Core i5-11600KF", "Core i5-11600T", "Core i5-12400", "Core i5-12400F", "Core i5-12400T", "Core i5-12450H", "Core i5-12450HX", "Core i5-12490F", "Core i5-12500", "Core i5-12500E", "Core i5-12500H", "Core i5-12500T", "Core i5-12500TE", "Core i5-12600", "Core i5-12600H", "Core i5-12600HE", "Core i5-12600HX", "Core i5-12600K", "Core i5-12600KF", "Core i5-12600T", "Core i5-13500", "Core i5-13600K", "Core i5-13600KF", "Core i7-1160G7", "Core i7-1165G7", "Core i7-1185G7", "Core i7-1195G7", "Core i7-1250U", "Core i7-1255U", "Core i7-1260P", "Core i7-1260U", "Core i7-1265U", "Core i7-1270P", "Core i7-1280P", "Core i7-3610QE", "Core i7-3612QE", "Core i7-3615QE", "Core i7-3615QM", "Core i7-3630QM", "Core i7-3632QM", "Core i7-3632QM", "Core i7-3635QM", "Core i7-3720QM", "Core i7-3720QM", "Core i7-3740QM", "Core i7-3740QM", "Core i7-3820QM", "Core i7-3820QM", "Core i7-3840QM", "Core i7-3840QM", "Core i7-4700EQ", "Core i7-4700HQ", "Core i7-4700MQ", "Core i7-4702HQ", "Core i7-4702MQ", "Core i7-4710HQ", "Core i7-4710MQ", "Core i7-4712HQ", "Core i7-4712MQ", "Core i7-4750HQ", "Core i7-4760HQ", "Core i7-4770HQ", "Core i7-4770S", "Core i7-4790S", "Core i7-4800MQ", "Core i7-4810MQ", "Core i7-4850EQ", "Core i7-4850HQ", "Core i7-4860EQ", "Core i7-4860HQ", "Core i7-4870HQ", "Core i7-4900MQ", "Core i7-4910MQ", "Core i7-4950HQ", "Core i7-4960HQ", "Core i7-4980HQ", "Core i7-5700HQ", "Core i7-5750HQ", "Core i7-5850HQ", "Core i7-5950HQ", "Core i7-7700K", "Core i7-8086K", "Core i7-8700B", "Core i7-8700K", "Core i7-9700F", "Core i7-9700K", "Core i7-9700KF", "Core i7-9750HF", "Core i7-10510Y", "Core i7-10610U", "Core i7-10700", "Core i7-10700F", "Core i7-10700K", "Core i7-10700KF", "Core i7-10750H", "Core i7-10810U", "Core i7-10850H", "Core i7-10870H", "Core i7-10875H", "Core i7-11370H", "Core i7-11375H", "Core i7-11390H", "Core i7-11600H", "Core i7-11700", "Core i7-11700F", "Core i7-11700K", "Core i7-11700KF", "Core i7-11700T", "Core i7-11800H", "Core i7-11850H", "Core i7-12650H", "Core i7-12650HX", "Core i7-12700", "Core i7-12700E", "Core i7-12700F", "Core i7-12700H", "Core i7-12700K", "Core i7-12700KF", "Core i7-12700T", "Core i7-12700TE", "Core i7-12800H", "Core i7-12800HE", "Core i7-12800HX", "Core i7-12850HX", "Core i7-13700", "Core i7-13700K", "Core i7-13700KF", "Core i9-8950HK", "Core i9-9820X", "Core i9-9880H", "Core i9-9900", "Core i9-9900K", "Core i9-9900KF", "Core i9-9900KS", "Core i9-9900X", "Core i9-9920X", "Core i9-9940X", "Core i9-9960X", "Core i9-9980HK", "Core i9-9980XE", "Core i9-10800F", "Core i9-10850K", "Core i9-10885H", "Core i9-10900", "Core i9-10900F", "Core i9-10900K", "Core i9-10900KF", "Core i9-10900X", "Core i9-10920X", "Core i9-10940X", "Core i9-10980HK", "Core i9-10980XE", "Core i9-10990XE", "Core i9-11900", "Core i9-11900F", "Core i9-11900H", "Core i9-11900K", "Core i9-11900KB", "Core i9-11900KF", "Core i9-11900T", "Core i9-11950H", "Core i9-11980HK", "Core i9-12900", "Core i9-12900E", "Core i9-12900F", "Core i9-12900H", "Core i9-12900HK", "Core i9-12900HX", "Core i9-12900K", "Core i9-12900KF", "Core i9-12900KS", "Core i9-12900T", "Core i9-12900TE", "Core i9-12950HX", "Core i9-13900", "Core i9-13900K", "Core i9-13900KF", "Core i9-13900KS", "EPYC 7F32", "EPYC 7F52", "EPYC 7F72", "EPYC 72F3", "EPYC 73F3", "EPYC 74F3", "EPYC 75F3", "EPYC 7373X", "EPYC 7473X", "EPYC 7573X", "EPYC 7773X", "EPYC 9124", "EPYC 9174F", "EPYC 9224", "EPYC 9254", "EPYC 9274F", "EPYC 9334", "EPYC 9354", "EPYC 9354P", "EPYC 9374F", "EPYC 9454", "EPYC 9454P", "EPYC 9474F", "EPYC 9534", "EPYC 9554", "EPYC 9554P", "EPYC 9634", "EPYC 9654", "EPYC 9654P", "EPYC Embedded 3251", "Nano QuadCore C4650", "Nano QuadCore C4650 ES", "Nano QuadCore C4700", "Opteron 6386 SE", "Pentium 4 505J", "Pentium 987", "Pentium 3550M", "Pentium 3556U", "Pentium 3560Y", "Pentium 3805U", "Pentium 3825U", "Pentium 8500", "Pentium 8505", "Pentium 8505 (IPU)", "Pentium B915C", "Pentium B970", "Pentium B980", "Pentium G2010", "Pentium G2020", "Pentium G2020T", "Pentium G2130", "Pentium G3220", "Pentium G3220T", "Pentium G3250", "Pentium G3250T", "Pentium G3258", "Pentium G3420", "Pentium G3420T", "Pentium G3430", "Pentium G3440", "Pentium G3450", "Pentium G3450T", "Pentium G3460", "Pentium G4400", "Pentium G4500", "Pentium G4520", "Pentium G4560", "Pentium G4560T", "Pentium G4600", "Pentium G4600T", "Pentium G4620", "Pentium Gold G5400", "Pentium Gold G5500", "Pentium Gold G5600", "Pentium Gold G5620", "Pentium Gold G6400", "Pentium Gold G6405", "Pentium Gold G6405T", "Pentium Gold G6500", "Pentium Gold G6505", "Pentium Gold G6505T", "Pentium Gold G6600", "Pentium Gold G6605", "Pentium Gold G7400", "Pentium Gold G7400E", "Pentium Gold G7400T", "Pentium Gold G7400TE", "Pentium Silver J5005", "Phenom II X4 965 BE (125W)", "Phenom II X4 965 BE (140W)", "Phenom II X6 1055T (95W)", "Phenom II X6 1055T (125W)", "PRO A4-8350B", "PRO A6-8550B", "PRO A8-8650B", "PRO A10-8750B", "PRO A10-8850B", "Ryzen 3 4100", "Ryzen 3 5125C", "Ryzen 3 5300G", "Ryzen 3 5425C", "Ryzen 3 5425U", "Ryzen 3 PRO 5350G", "Ryzen 3 PRO 5350GE", "Ryzen 3 PRO 5475U", "Ryzen 5 1600AF", "Ryzen 5 4500", "Ryzen 5 4600HS", "Ryzen 5 5500", "Ryzen 5 5600", "Ryzen 5 5600G", "Ryzen 5 5625C", "Ryzen 5 5625U", "Ryzen 5 6600H", "Ryzen 5 6600HS", "Ryzen 5 6600U", "Ryzen 5 7600X", "Ryzen 5 PRO 5650G", "Ryzen 5 PRO 5650GE", "Ryzen 5 PRO 5675U", "Ryzen 5 PRO 6650H", "Ryzen 5 PRO 6650HS", "Ryzen 5 PRO 6650U", "Ryzen 7 2700X 50th Anniversary", "Ryzen 7 4800HS", "Ryzen 7 5700G", "Ryzen 7 5700X", "Ryzen 7 5800G", "Ryzen 7 5800HS", "Ryzen 7 5800X3D", "Ryzen 7 5825C", "Ryzen 7 5825U", "Ryzen 7 6800H", "Ryzen 7 6800HS", "Ryzen 7 6800U", "Ryzen 7 7700X", "Ryzen 7 PRO 5750G", "Ryzen 7 PRO 5750GE", "Ryzen 7 PRO 5875U", "Ryzen 7 PRO 6850H", "Ryzen 7 PRO 6850HS", "Ryzen 7 PRO 6850U", "Ryzen 9 4900HS", "Ryzen 9 5980HS", "Ryzen 9 6900HS", "Ryzen 9 6900HX", "Ryzen 9 6980HS", "Ryzen 9 6980HX", "Ryzen 9 7900X", "Ryzen 9 7950X", "Ryzen 9 PRO 6950H", "Ryzen 9 PRO 6950HS", "Ryzen Embedded R1102G", "Ryzen Embedded R1305G", "Ryzen Embedded R1505G", "Ryzen Embedded R1600", "Ryzen Embedded R1606G", "Ryzen Embedded V1202B", "Ryzen Embedded V1605B", "Ryzen Embedded V1756B", "Ryzen Embedded V1807B", "Ryzen Embedded V2516", "Ryzen Embedded V2546", "Ryzen Embedded V2718", "Ryzen Embedded V2748", "Ryzen Threadripper 2970WX", "Ryzen Threadripper 2990WX", "Ryzen Threadripper 3960X", "Ryzen Threadripper 3970X", "Ryzen Threadripper 3980X", "Ryzen Threadripper 3990X", "Ryzen Threadripper 5990X", "Ryzen Threadripper PRO 3945WX", "Ryzen Threadripper PRO 3955WX", "Ryzen Threadripper PRO 3975WX", "Ryzen Threadripper PRO 3995WX", "Ryzen Threadripper PRO 5945WX", "Ryzen Threadripper PRO 5955WX", "Ryzen Threadripper PRO 5965WX", "Ryzen Threadripper PRO 5975WX", "Ryzen Threadripper PRO 5995WX", "Sempron 2650", "Sempron 3850", "Xeon Bronze 3106", "Xeon E3-1258L v4", "Xeon E3-1265L v4", "Xeon E3-1270L v4", "Xeon E3-1278L v4", "Xeon E3-1280 v5", "Xeon E3-1283L v4", "Xeon E3-1284L v4", "Xeon E3-1285 v4", "Xeon E3-1285 v6", "Xeon E3-1285L v4", "Xeon E5-2676 V3", "Xeon E5-2678 V3", "Xeon E5-2687W", "Xeon E5-2687W v2", "Xeon E5-2687W V3", "Xeon E5-2698B V3", "Xeon E5-4610 V3", "Xeon E5-4620 V3", "Xeon E5-4627 V3", "Xeon E5-4640 V3", "Xeon E5-4648 V3", "Xeon E5-4650 V3", "Xeon E5-4655 V3", "Xeon E7-4809 v3", "Xeon E7-4820 v3", "Xeon E7-4830 v3", "Xeon E7-4850 v3", "Xeon E7-8860 v3", "Xeon E7-8867 v3", "Xeon E7-8870 v3", "Xeon E7-8880 v3", "Xeon E7-8880L v3", "Xeon E7-8890 v3", "Xeon E7-8891 v3", "Xeon E7-8893 v3", "Xeon E-2314", "Xeon E-2324G", "Xeon E-2334", "Xeon E-2336", "Xeon E-2356G", "Xeon E-2374G", "Xeon E-2378", "Xeon E-2378G", "Xeon E-2386G", "Xeon E-2388G", "Xeon Gold 5315Y", "Xeon Gold 5317", "Xeon Gold 5318H", "Xeon Gold 5318N", "Xeon Gold 5318S", "Xeon Gold 5318Y", "Xeon Gold 5320", "Xeon Gold 5320H", "Xeon Gold 5320T", "Xeon Gold 6312U", "Xeon Gold 6314U", "Xeon Gold 6326", "Xeon Gold 6328H", "Xeon Gold 6328HL", "Xeon Gold 6330", "Xeon Gold 6330H", "Xeon Gold 6334", "Xeon Gold 6336Y", "Xeon Gold 6338N", "Xeon Gold 6338T", "Xeon Gold 6342", "Xeon Gold 6346", "Xeon Gold 6348", "Xeon Gold 6348H", "Xeon Gold 6354", "Xeon Phi 31S1P", "Xeon Phi 7210F", "Xeon Phi 7230F", "Xeon Phi 7250F", "Xeon Phi 7290F", "Xeon Phi SE10P", "Xeon Phi SE10X", "Xeon Platinum 8351N", "Xeon Platinum 8352M", "Xeon Platinum 8352S", "Xeon Platinum 8352V", "Xeon Platinum 8353H", "Xeon Platinum 8354H", "Xeon Platinum 8356H", "Xeon Platinum 8358", "Xeon Platinum 8358P", "Xeon Platinum 8360H", "Xeon Platinum 8360HL", "Xeon Platinum 8360Y", "Xeon Platinum 8362", "Xeon Platinum 8368", "Xeon Platinum 8368Q", "Xeon Platinum 8376H", "Xeon Platinum 8376HL", "Xeon Platinum 8380", "Xeon Platinum 8380H", "Xeon Platinum 8380HL", "Xeon Platinum 8490H", "Xeon Platinum 9221", "Xeon Platinum 9222", "Xeon Platinum 9242", "Xeon Platinum 9282", "Xeon Silver 4116", "Xeon Silver 4309Y", "Xeon Silver 4310", "Xeon Silver 4310T", "Xeon Silver 4314", "Xeon Silver 4316", "Xeon W3530", "Xeon W3550", "Xeon W3565", "Xeon W3580", "Xeon W3670", "Xeon W3680", "Xeon W3690", "Xeon W5590", "Xeon W-1290", "Xeon W-1290P", "Xeon W-2102", "Xeon W-2104", "Xeon W-2123", "Xeon W-2125", "Xeon W-2133", "Xeon W-2135", "Xeon W-2140B", "Xeon W-2145", "Xeon W-2150B", "Xeon W-2155", "Xeon W-2170B", "Xeon W-2175", "Xeon W-2191B", "Xeon W-2195", "Xeon W-3175X", "Xeon W-3323", "Xeon W-3335", "Xeon W-3345", "Xeon W-3365", "Xeon W-3375", "Xeon W-11855M", "Xeon W-11955M" };
        private static string[] Motherboards = new string[] { "Asrock 970 Pro3 R2.0", "Asrock 990FX Extreme4", "Asrock 990FX Extreme6", "Asrock A320M", "Asrock A320M-HDV", "Asrock A320M-HDV R4.0", "Asrock A320M-ITX", "Asrock AB350 Pro4", "Asrock AB350M Pro4", "Asrock B150M Pro4/D3", "Asrock B150M Pro4S/D3", "Asrock B150M Pro4V", "Asrock B150M-ITX", "Asrock B250M Pro4", "Asrock B250M-HDV", "Asrock B450 Pro4", "Asrock B450 Steel Legend", "Asrock B450M Steel Legend", "Asrock B450M-HDV", "Asrock B450M-HDV R4.0", "Asrock B460 Phantom Gaming 4", "Asrock B460 Pro4", "Asrock B550 Pro4", "Asrock B550 Steel Legend", "Asrock B550M Pro4", "Asrock B550M Steel Legend", "Asrock Fatal1ty B250 Gaming K4", "Asrock Fatal1ty H170 Performance", "Asrock Fatal1ty X299 Professional Gaming i9", "Asrock Fatal1ty X299 Professional Gaming i9 XE", "Asrock Fatal1ty X99 Professional/3.1", "Asrock Fatal1ty Z170 Gaming K4", "Asrock Fatal1ty Z170 Gaming K6", "Asrock FM2A88X Extreme4+", "Asrock H110 Pro BTC+", "Asrock H110M Combo-G", "Asrock H110M-DVS R3.0", "Asrock H110M-GL/D3", "Asrock H110M-HDS", "Asrock H110M-HDV R3.0", "Asrock H110M-HDVP", "Asrock H110TM-ITX", "Asrock H170 Combo", "Asrock H170 Pro4/D3", "Asrock H170M-ITX/DL", "Asrock H270M Pro4", "Asrock H270M-ITX/ac", "Asrock H310M-STX/COM", "Asrock H97M-ITX/ac", "Asrock IMB-390-L", "Asrock J3455-ITX", "Asrock X299 Steel Legend", "Asrock X99 Extreme11", "Asrock X99 Taichi", "Asrock Z170 Extreme3", "Asrock Z170 Pro4", "Asrock Z170M-ITX/ac", "Asrock Z370 Extreme4", "Asrock Z370 Pro4", "Asrock Z390 Phantom Gaming 4S", "Asus B150 Pro Gaming", "Asus B150 Pro Gaming D3", "Asus B150 Pro Gaming/Aura", "Asus B150-Pro D3", "Asus B150I Pro Gaming/Aura", "Asus B150I Pro Gaming/WiFi/Aura", "Asus B150M Pro Gaming", "Asus B150M-A", "Asus B150M-A D3", "Asus B150M-K D3", "Asus B150M-Plus", "Asus B150M-Plus D3", "Asus B250 Mining Expert", "Asus B365M-Kylin", "Asus B365M-Pixiu", "Asus E3 Pro Gaming V5", "Asus EX-A320M-Gaming", "Asus EX-B150M-V5 D3", "Asus EX-B250-V7", "Asus EX-H110M-V", "Asus EX-H310M-V3", "Asus H110I-Plus", "Asus H170 Pro Gaming", "Asus H170-Plus D3", "Asus H170-Pro/USB 3.1", "Asus H170I-Plus D3", "Asus H170M-Plus", "Asus H370 Mining Master", "Asus M5A99FX PRO R2.0", "Asus P6X58D Premium", "Asus Prime A320M-A", "Asus Prime A320M-C R2.0", "Asus Prime A320M-E", "Asus Prime A320M-K", "Asus Prime B250-Plus", "Asus Prime B250-Pro", "Asus Prime B250M-A", "Asus Prime B250M-D", "Asus Prime B250M-J", "Asus Prime B250M-K", "Asus Prime B250M-Plus", "Asus Prime B350-Plus", "Asus Prime B350M-A", "Asus Prime B350M-E", "Asus Prime B350M-K", "Asus Prime B360M-K", "Asus Prime B365M-A", "Asus Prime B365M-K", "Asus Prime B450M-A", "Asus Prime H270-Plus", "Asus Prime H270-Pro", "Asus Prime H310M-A R2.0", "Asus Prime H310M-CS R2.0", "Asus Prime H310M-D R2.0", "Asus Prime H370M-Plus", "Asus Prime Q270M-C", "Asus Prime X299-A", "Asus Prime X299-Deluxe", "Asus Prime X370-A", "Asus Prime X370-Pro", "Asus Prime X399-A", "Asus Prime X470-Pro", "Asus Prime Z270-A", "Asus Prime Z270-AR", "Asus Prime Z270-K", "Asus Prime Z270-P", "Asus Prime Z270M-Plus", "Asus Prime Z370-A", "Asus Prime Z370-P", "Asus Prime Z390-A", "Asus Prime Z390-P", "Asus Prime Z390M-Plus", "Asus Pro WS X570-Ace", "Asus ROG Blitz Extreme", "Asus ROG Commando", "Asus ROG Crossblade Ranger", "Asus ROG Crosshair", "Asus ROG Crosshair II Formula", "Asus ROG Crosshair III Formula", "Asus ROG Crosshair IV Extreme", "Asus ROG Crosshair IV Formula", "Asus ROG Crosshair V Formula-Z", "Asus ROG Crosshair VI Extreme", "Asus ROG Crosshair VI Hero", "Asus ROG Dominus Extreme", "Asus ROG Maximus Extreme", "Asus ROG Maximus Formula", "Asus ROG Maximus Formula Special Edition", "Asus ROG Maximus II Formula", "Asus ROG Maximus II Gene", "Asus ROG Maximus III Extreme", "Asus ROG Maximus III Formula", "Asus ROG Maximus III Gene", "Asus ROG Maximus IV Extreme", "Asus ROG Maximus IV Extreme-Z", "Asus ROG Maximus IV Gene-Z/Gen3", "Asus ROG Maximus IX Apex", "Asus ROG Maximus IX Code", "Asus ROG Maximus IX Extreme", "Asus ROG Maximus IX Formula", "Asus ROG Maximus IX Hero", "Asus ROG Maximus V Extreme", "Asus ROG Maximus V Formula", "Asus ROG Maximus V Gene", "Asus ROG Maximus VI Extreme", "Asus ROG Maximus VI Gene", "Asus ROG Maximus VI Hero", "Asus ROG Maximus VI Impact", "Asus ROG Maximus VII Formula", "Asus ROG Maximus VII Gene", "Asus ROG Maximus VII Hero", "Asus ROG Maximus VII Impact", "Asus ROG Maximus VII Ranger", "Asus ROG Maximus VIII Gene", "Asus ROG Maximus VIII Hero", "Asus ROG Maximus VIII Hero Alpha", "Asus ROG Maximus VIII Impact", "Asus ROG Maximus VIII Ranger", "Asus ROG Maximus X Apex", "Asus ROG Maximus X Code", "Asus ROG Maximus X Formula", "Asus ROG Maximus X Hero", "Asus ROG Maximus X Hero (Wi-Fi AC)", "Asus ROG Maximus XI Extreme", "Asus ROG Maximus XI Formula", "Asus ROG Maximus XI Gene", "Asus ROG Maximus XI Hero Wi-Fi", "Asus ROG Rampage Extreme", "Asus ROG Rampage Formula", "Asus ROG Rampage II Extreme", "Asus ROG Rampage II Gene", "Asus ROG Rampage III Black Edition", "Asus ROG Rampage III Extreme", "Asus ROG Rampage III Formula", "Asus ROG Rampage III Gene", "Asus ROG Rampage IV Black Edition", "Asus ROG Rampage IV Extreme", "Asus ROG Rampage IV Formula", "Asus ROG Rampage IV Gene", "Asus ROG Rampage V Edition 10", "Asus ROG Rampage V Extreme", "Asus ROG Rampage V Extreme/U3.1", "Asus ROG Rampage VI Apex", "Asus ROG Rampage VI Extreme", "Asus ROG Striker Extreme", "Asus ROG Striker II Extreme", "Asus ROG Striker II Formula", "Asus ROG Striker II NSE", "Asus ROG Strix B250F Gaming", "Asus ROG Strix B250G Gaming", "Asus ROG Strix B250H Gaming", "Asus ROG Strix B350-F Gaming", "Asus ROG Strix B360-H Gaming", "Asus ROG Strix B450-F Gaming", "Asus ROG Strix H270F Gaming", "Asus ROG Strix H270I Gaming", "Asus ROG Strix X299-XE Gaming", "Asus ROG Strix X370-F Gaming", "Asus ROG Strix X399-E Gaming", "Asus ROG Strix X470-F Gaming", "Asus ROG Strix X570-E Gaming", "Asus ROG Strix X570-F Gaming", "Asus ROG Strix X99 Gaming", "Asus ROG Strix Z270E Gaming", "Asus ROG Strix Z270F Gaming", "Asus ROG Strix Z270H Gaming", "Asus ROG Strix Z270I Gaming", "Asus ROG Strix Z370-E Gaming", "Asus ROG Strix Z370-F Gaming", "Asus ROG Strix Z370-G Gaming", "Asus ROG Strix Z370-G Gaming (Wi-Fi AC)", "Asus ROG Strix Z370-H Gaming", "Asus ROG Strix Z390-E Gaming", "Asus ROG Strix Z390-F Gaming", "Asus ROG Strix Z390-H Gaming", "Asus ROG Strix Z390-I Gaming", "Asus ROG Zenith Extreme", "Asus ROG Zenith Extreme Alpha", "Asus Sabertooth 990FX R2.0", "Asus Sabertooth 990FX R3.0", "Asus Sabertooth 990FX/GEN3 R2.0", "Asus Sabertooth Z170 Mark 1", "Asus Sabertooth Z170 S", "Asus Trooper B150 D3", "Asus TUF B350M-Plus Gaming", "Asus TUF B360-Plus Gaming", "Asus TUF B360-Pro Gaming", "Asus TUF B360M-E Gaming", "Asus TUF B360M-Plus Gaming", "Asus TUF B450M-Plus Gaming", "Asus TUF B450M-Pro Gaming", "Asus TUF H370-Pro Gaming", "Asus TUF X299 Mark 1", "Asus TUF X299 Mark 2", "Asus TUF X470-Plus Gaming", "Asus TUF Z270 Mark 1", "Asus TUF Z270 Mark 2", "Asus TUF Z370-Plus Gaming", "Asus TUF Z390-Plus Gaming", "Asus WS C422 PRO/SE", "Asus WS C621E Sage", "Asus WS X299 Sage", "Asus X99-A II", "Asus X99-Deluxe", "Asus X99-Deluxe II", "Asus Z170 Pro Gaming", "Asus Z170 Pro Gaming/Aura", "Asus Z170-A", "Asus Z170-AR", "Asus Z170-Deluxe", "Asus Z170-E", "Asus Z170-K", "Asus Z170-P", "Asus Z170-P D3", "Asus Z170-Premium", "Asus Z170-WS", "Asus Z170I Pro Gaming", "Asus Z170M-E D3", "Asus Z170M-Plus", "Asus Z270-WS", "Biostar A320MH", "Biostar TB250-BTC Pro", "Colorful C.A320M-K Pro V14", "ECS H110H4-M19", "Gigabyte B450 Aorus Elite", "Gigabyte B450 Aorus M", "Gigabyte B450 Aorus Pro WiFi", "Gigabyte B450M DS3H", "Gigabyte B450M Gaming", "Gigabyte G1.Sniper B7", "Gigabyte GA-970A-DS3P Rev. 2.0", "Gigabyte GA-A320M-DS2", "Gigabyte GA-A320M-S2H", "Gigabyte GA-AB350-Gaming 3", "Gigabyte GA-AB350M-DS3H", "Gigabyte GA-AX370M-DS3H", "Gigabyte GA-B150M-D3H", "Gigabyte GA-B250M-D2V", "Gigabyte GA-B250M-D3H", "Gigabyte GA-B250M-DS3H", "Gigabyte GA-B250M-HD3", "Gigabyte GA-Gaming B8", "Gigabyte GA-H110-D3A", "Gigabyte GA-H110M-DS2", "Gigabyte GA-H110M-H", "Gigabyte GA-H110M-S2", "Gigabyte GA-H170-Gaming 3 Rev. 1.1", "Gigabyte GA-H270-HD3", "Gigabyte GA-X58A-UD3R Rev. 2.0", "Gigabyte GA-Z170-HD3P", "Gigabyte GA-Z270-HD3", "Gigabyte H310M S2", "Gigabyte X570 Aorus Elite", "Gigabyte X570I Aorus Pro Wifi", "Gigabyte Z370 Aorus Gaming 5", "Gigabyte Z370 HD3", "Gigabyte Z370M D3H", "Gigabyte Z370P D3", "Gigabyte Z390 Aorus Elite", "Gigabyte Z390 Aorus Pro Wifi", "Gigabyte Z390 Aorus Ultra", "Gigabyte Z390 Gaming X", "Gigabyte Z390 UD", "Maxsun MS-B350FX Gaming Pro", "MSI 970 Gaming", "MSI 970A-G43", "MSI 970A-G43 Plus", "MSI 970A-G46", "MSI 990FXA Gaming", "MSI 990FXA-GD65", "MSI 990FXA-GD80", "MSI A320M Bazooka", "MSI A320M Gaming Pro", "MSI A320M Grenade", "MSI A320M Pro-M2", "MSI A320M Pro-VD", "MSI A320M Pro-VD/S", "MSI A320M Pro-VH Plus", "MSI B150 Gaming M3", "MSI B150 PC Mate", "MSI B150A Gaming Pro", "MSI B150I Gaming Pro", "MSI B150M Bazooka", "MSI B150M Bazooka Plus", "MSI B150M Mortar", "MSI B150M Night Elf", "MSI B150M PRO-DH", "MSI B150M PRO-VDH", "MSI B250 Gaming M3", "MSI B250 Gaming Pro Carbon", "MSI B250 PC Mate", "MSI B250I Gaming Pro AC", "MSI B250M Bazooka", "MSI B250M Bazooka Plus", "MSI B250M Gaming Pro", "MSI B250M Mortar", "MSI B250M Mortar Arctic", "MSI B250M Pro-VD", "MSI B250M Pro-VDH", "MSI B250M Pro-VH", "MSI B350 Gaming Plus", "MSI B350 Gaming Pro Carbon", "MSI B350 PC Mate", "MSI B350 Tomahawk", "MSI B350 Tomahawk Arctic", "MSI B350 Tomahawk Plus", "MSI B350M Bazooka", "MSI B350M Gaming Pro", "MSI B350M Mortar", "MSI B350M Mortar Arctic", "MSI B350M Pro-VD Plus", "MSI B350M Pro-VDH", "MSI B360M Pro-VDH", "MSI B360M Pro-VH", "MSI B450 Gaming Plus", "MSI B450 Gaming Pro Carbon AC", "MSI B450 Tomahawk", "MSI B450M Bazooka Plus", "MSI B450M Mortar", "MSI B450M Pro-M2", "MSI B450M Pro-VDH", "MSI E3 Krait Gaming V5", "MSI H110M ECO", "MSI H110M PRO-D", "MSI H110M PRO-VD", "MSI H110M PRO-VD D3", "MSI H110M PRO-VD Plus", "MSI H110M PRO-VDP", "MSI H110M PRO-VH", "MSI H110M PRO-VH Plus", "MSI H110M-A PRO M2", "MSI H170A Gaming Pro", "MSI H170M PRO-DH", "MSI H170M PRO-VDH", "MSI H270 Gaming M3", "MSI H270 Gaming Pro Carbon", "MSI H270 Tomahawk Arctic", "MSI H270-A Pro", "MSI H270M Bazooka", "MSI H270M Mortar Arctic", "MSI H310M Pro-M2 Plus", "MSI H310M Pro-VD Plus", "MSI H370 Gaming Pro Carbon", "MSI MEG X570 Godlike", "MSI MEG Z390 Godlike", "MSI MPG X570 Gaming Edge Wifi", "MSI MPG X570 Gaming Plus", "MSI MPG X570 Gaming Pro Carbon Wifi", "MSI MPG Z390 Gaming Edge AC", "MSI MPG Z390 Gaming Plus", "MSI MPG Z390 Gaming Pro Carbon AC", "MSI X299 Gaming Pro Carbon AC", "MSI X299 Raider", "MSI X299 SLI Plus", "MSI X299 Tomahawk AC", "MSI X299M Gaming Pro Carbon AC", "MSI X299M-A Pro", "MSI X370 Gaming M7 ACK", "MSI X370 Gaming Plus", "MSI X370 Gaming Pro Carbon", "MSI X370 Gaming Pro Carbon AC", "MSI X370 Krait Gaming", "MSI X370 SLI Plus", "MSI X370 XPower Gaming Titanium", "MSI X399 Gaming Pro Carbon AC", "MSI X399 SLI Plus", "MSI X470 Gaming Plus", "MSI X470 Gaming Pro", "MSI X79MA-GD45", "MSI X99A Godlike Gaming", "MSI X99A Raider", "MSI X99S SLI Plus", "MSI Z170 Krait Gaming", "MSI Z170-A Pro", "MSI Z170A Gaming M3", "MSI Z170A Gaming M5", "MSI Z170A Gaming M6", "MSI Z170A Gaming Pro", "MSI Z170A Gaming Pro Carbon", "MSI Z170A Krait Gaming", "MSI Z170A Krait Gaming 3X", "MSI Z170A PC Mate", "MSI Z170A Tomahawk", "MSI Z170A Tomahawk AC", "MSI Z270 Gaming Plus", "MSI Z270 Gaming Pro Carbon", "MSI Z270 PC Mate", "MSI Z270 Tomahawk Arctic", "MSI Z270 XPower Gaming Titanium", "MSI Z270-A Pro", "MSI Z270I Gaming Pro Carbon AC", "MSI Z270M Mortar", "MSI Z370 Gaming M5", "MSI Z370 Gaming Plus", "MSI Z370 Gaming Pro Carbon AC", "MSI Z370 Godlike Gaming", "MSI Z370 Krait Gaming", "MSI Z370 PC Pro", "MSI Z370 Tomahawk", "MSI Z370-A Pro", "MSI Z370M Mortar", "MSI Z390-A Pro", "MSI Z97-S02", "MSI Z97A SLI Krait Edition" };
        private static string[] OS = new string[] { "Windows 10  (10.0.0) 64bit", "Windows 10  (10.0.0) 32bit", "Windows 8  (10.0.0) 64bit", "Windows 8  (10.0.0) 32bit", "Windows 7  (10.0.0) 64bit", "Windows 7  (10.0.0) 32bit", "Windows Vista 64bit", "Windows Vista 32bit", "Windows XP 64bit", "Windows XP 32bit", "Windows 9 64bit", "Windows 9 32bit" };
        
        public override void OnPreInitialization()
        {
            // Thanks knah. Credit goes to him for this simplistic way but ive done some major editing for steamid stuff.
            var path = "";
            if (!File.Exists(path)) path = MelonUtils.GetGameDataDirectory() + "\\Plugins\\steam_api64.dll";
            if (!File.Exists(path)) path = MelonUtils.GetGameDataDirectory() + "\\Plugins\\x86_64\\steam_api64.dll";
            if (!File.Exists(path)) path = MelonUtils.GetGameDataDirectory() + "\\Plugins\\x86\\steam_api64.dll";
            FindPatchSteamID(path);
        }
        
        [Obsolete]
        public override void OnApplicationStart()
        {
            FindPatchHWIDDetails();
        }
    }
}
