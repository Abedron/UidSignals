using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;

namespace UidSignals
{
    /// <summary>
    /// Controller for Logitech MX Master devices to handle haptic feedback and raw input (Thumb Wheel).
    /// </summary>
    public static class LogitechController
    {
        // Event fired when the horizontal thumb wheel is rotated
        public static event EventHandler<int>? ThumbWheelScrolled;

        // Logitech HID++ Constants
        private const byte BOLT_DEVICE_SLOT = 0x02;      // Routing index for devices connected via Logi Bolt dongle
        private const byte DIRECT_DEVICE_SLOT = 0xFF;    // Routing index for Bluetooth or wired connections
        private const byte KNOWN_HAPTIC_INDEX = 0x0B;    // HID++ feature index for the haptic module

        // Caching mechanism to avoid expensive hardware enumeration on every click
        private static List<EndpointConfig>? _cachedEndpoints;
        private static readonly object _cacheLock = new object();

        private class EndpointConfig
        {
            public string? Path { get; set; }
            public byte DeviceIndex { get; set; }
        }

        // Win32 Message and Raw Input Constants
        private const int WM_INPUT = 0x00FF;             // Raw Input message
        private const int WM_DEVICECHANGE = 0x0219;      // Hardware change message (e.g., unplugged dongle)
        private const uint RID_INPUT = 0x10000003;       // Command to get raw input data
        private const uint RIM_TYPEMOUSE = 0;            // Identifier for mouse-type raw input
        private const ushort RI_MOUSE_HWHEEL = 0x0800;   // Flag for horizontal wheel (Thumb Wheel) movement
        private const uint RIDEV_INPUTSINK = 0x00000100; // Flag to receive input even when window lacks focus

        #region Win32 API Imports
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern int HidP_GetCaps(IntPtr preparsedData, ref HIDP_CAPS capabilities);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);
        #endregion

        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint GENERIC_READ = 0x80000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const int HIDP_STATUS_SUCCESS = 0x00110000;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct RAWMOUSE
        {
            [FieldOffset(0)] public ushort usFlags;
            [FieldOffset(4)] public uint ulButtons;
            [FieldOffset(4)] public ushort usButtonFlags;
            [FieldOffset(6)] public short usButtonData;
            [FieldOffset(8)] public uint ulRawButtons;
            [FieldOffset(12)] public int lLastX;
            [FieldOffset(16)] public int lLastY;
            [FieldOffset(20)] public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;
            public ushort Res1, Res2, Res3, Res4, Res5, Res6, Res7, Res8, Res9, Res10, Res11, Res12, Res13, Res14, Res15, Res16, Res17;
            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        // HID++ 2.0 Long Report Buffer (20 bytes)
        // [0] ReportID (0x11), [1] DeviceIndex, [2] FeatureIndex, [3] Command (HapticPlay), [4] PatternID
        private static readonly byte[] _hapticBuffer = new byte[] { 0x11, 0x00, KNOWN_HAPTIC_INDEX, 0x4C, 0x00, 0,0,0,0,0,0,0,0,0,0,0,0,0,0,0 };

        /// <summary>
        /// Registers the window to receive raw input from mice and hooks the WndProc.
        /// </summary>
        public static void Initialize(Window window)
        {
            HwndSource? source = PresentationSource.FromVisual(window) as HwndSource;
            if (source != null)
            {
                source.AddHook(WndProc);

                // Setup Raw Input registration for the Generic Desktop Mouse
                RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
                rid[0].usUsagePage = 0x01;       // Generic Desktop Controls
                rid[0].usUsage = 0x02;           // Mouse
                rid[0].dwFlags = RIDEV_INPUTSINK; // Background input support
                rid[0].hwndTarget = source.Handle;

                RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0]));
            }
        }

        /// <summary>
        /// Message processing loop to handle hardware changes and raw mouse input.
        /// </summary>
        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_DEVICECHANGE)
            {
                // Invalidate cache if a device is plugged/unplugged
                ResetCache();
            }
            else if (msg == WM_INPUT)
            {
                uint dwSize = 0;
                uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));

                // Determine buffer size for the incoming raw input
                GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, headerSize);

                if (dwSize > 0)
                {
                    IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                    try
                    {
                        if (GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, headerSize) == dwSize)
                        {
                            RAWINPUTHEADER header = Marshal.PtrToStructure<RAWINPUTHEADER>(buffer);

                            if (header.dwType == RIM_TYPEMOUSE)
                            {
                                // Jump past header to get the RAWMOUSE structure
                                IntPtr mouseDataPtr = new IntPtr(buffer.ToInt64() + headerSize);
                                RAWMOUSE mouse = Marshal.PtrToStructure<RAWMOUSE>(mouseDataPtr);

                                // Check if the horizontal wheel flag is set
                                if ((mouse.usButtonFlags & RI_MOUSE_HWHEEL) == RI_MOUSE_HWHEEL)
                                {
                                    short wheelDelta = mouse.usButtonData;

                                    // Trigger soft haptic feedback on rotation (Pattern 0x01)
                                    _ = TriggerFeedbackAsync(0x04);

                                    ThumbWheelScrolled?.Invoke(null, wheelDelta);
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            return IntPtr.Zero;
        }

        public static void ResetCache()
        {
            lock (_cacheLock)
            {
                _cachedEndpoints = null;
            }
        }

        /// <summary>
        /// Sends a haptic feedback command asynchronously to all detected Logitech devices.
        /// </summary>
        /// <param name="patternId">The haptic pattern ID (e.g., 0x01 for SoftClick).</param>
        public static Task TriggerFeedbackAsync(byte patternId = 0x00)
        {
            return Task.Run(() => 
            {
                // Initialize/Retrieve device paths from cache
                lock (_cacheLock)
                {
                    if (_cachedEndpoints == null || _cachedEndpoints.Count == 0)
                    {
                        _cachedEndpoints = ScanForLogitechEndpoints();
                        if (_cachedEndpoints.Count == 0) return; 
                    }
                }

                bool anyWriteSuccessful = false;

                foreach (var ep in _cachedEndpoints)
                {
                    if (string.IsNullOrEmpty(ep.Path)) continue;

                    // Open handle to the HID device (Vendor-Defined interface)
                    using (SafeFileHandle handle = CreateFile(ep.Path, GENERIC_WRITE | GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                    {
                        if (handle.IsInvalid) continue;

                        // Update buffer with specific routing and pattern
                        _hapticBuffer[1] = ep.DeviceIndex;
                        _hapticBuffer[4] = patternId;

                        // Send the HID++ report
                        if (WriteFile(handle, _hapticBuffer, (uint)_hapticBuffer.Length, out _, IntPtr.Zero))
                        {
                            anyWriteSuccessful = true;
                        }
                    }
                }

                // If communication fails (e.g., device sleeping), reset cache for next attempt
                if (!anyWriteSuccessful)
                {
                    ResetCache();
                }
            });
        }

        /// <summary>
        /// Scans the system for Logitech HID devices (VID 046D) and detects if they are Bluetooth or Bolt.
        /// </summary>
        private static List<EndpointConfig> ScanForLogitechEndpoints()
        {
            var validConfigs = new List<EndpointConfig>();
            HidD_GetHidGuid(out Guid hidGuid);

            // Get handle to the list of all connected HID devices
            IntPtr deviceInfoSet = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == IntPtr.Zero) return validConfigs;

            SP_DEVICE_INTERFACE_DATA deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA();
            deviceInterfaceData.cbSize = Marshal.SizeOf(deviceInterfaceData);
            uint i = 0;

            try
            {
                // Iterate through all HID interfaces
                while (SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, i, ref deviceInterfaceData))
                {
                    // Query for the size of the Detail Data structure
                    SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, IntPtr.Zero, 0, out uint requiredSize, IntPtr.Zero);
                    IntPtr detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    Marshal.WriteInt32(detailDataBuffer, (IntPtr.Size == 8) ? 8 : 5); // cbSize varies by architecture

                    if (SetupDiGetDeviceInterfaceDetail(deviceInfoSet, ref deviceInterfaceData, detailDataBuffer, requiredSize, out _, IntPtr.Zero))
                    {
                        // Extract device path (e.g., "\\?\hid#vid_046d&pid_c548...")
                        string? path = Marshal.PtrToStringAuto(detailDataBuffer + 4);
                        
                        // Filter for Logitech Vendor ID (046D)
                        if (!string.IsNullOrEmpty(path) && path.ToLower().Contains("046d"))
                        {
                            using (SafeFileHandle tempHandle = CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero))
                            {
                                if (!tempHandle.IsInvalid)
                                {
                                    // Verify that this interface supports HID++ 2.0 (20-byte reports)
                                    if (HidD_GetPreparsedData(tempHandle, out IntPtr preparsedData))
                                    {
                                        HIDP_CAPS caps = new HIDP_CAPS();
                                        if (HidP_GetCaps(preparsedData, ref caps) == HIDP_STATUS_SUCCESS)
                                        {
                                            // UsagePage >= 0xFF00 indicates a Vendor-Defined communication channel
                                            if (caps.UsagePage >= 0xFF00 && caps.OutputReportByteLength == 20)
                                            {
                                                // Determine routing index: pid_c548 is the Logi Bolt receiver
                                                byte targetIndex = path.ToLower().Contains("pid_c548") ? BOLT_DEVICE_SLOT : DIRECT_DEVICE_SLOT;
                                                
                                                validConfigs.Add(new EndpointConfig 
                                                { 
                                                    Path = path, 
                                                    DeviceIndex = targetIndex 
                                                });
                                            }
                                        }
                                        HidD_FreePreparsedData(preparsedData);
                                    }
                                }
                            }
                        }
                    }
                    Marshal.FreeHGlobal(detailDataBuffer);
                    i++;
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return validConfigs;
        }
    }
}