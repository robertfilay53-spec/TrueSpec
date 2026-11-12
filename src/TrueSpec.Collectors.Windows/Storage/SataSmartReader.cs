namespace TrueSpec.Collectors.Windows.Storage;

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal sealed record SataSmartData(
    double? WearPercent,
    ulong? PowerOnHours,
    uint? PowerCycles,
    double? TemperatureC,
    ulong? ReallocatedSectors,
    ulong? PendingSectors,
    ulong? UncorrectedErrors,
    ulong? DataWrittenBytes,
    ulong? DataReadBytes);

/// <summary>
/// Raw ATA SMART passthrough for SATA drives. Windows reliability counters cover
/// many drives, but SATA attributes are the fallback that CrystalDiskInfo-style
/// tools use. Requires an elevated process.
/// </summary>
internal static class SataSmartReader
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const uint SmartRcvDriveData = 0x0007C088;
    private const byte SmartCommand = 0xB0;
    private const byte SmartReadData = 0xD0;
    private const byte SmartCylLow = 0x4F;
    private const byte SmartCylHigh = 0xC2;
    private const int SmartDataSize = 512;

    public static SataSmartData? TryRead(int driveNumber)
    {
        // The drive number comes from a WMI DeviceId string parsed by the
        // caller, and it lands in two places that must agree: the
        // "\\.\PhysicalDriveN" device path and the single DriveNumber byte
        // of the ATA command. A negative or >255 value can never be a real
        // drive, and (byte)256 would alias drive 0, so reject it here at
        // the P/Invoke boundary instead of trusting the caller.
        if (driveNumber is < 0 or > 255)
        {
            return null;
        }

        SafeFileHandle? handle = null;
        try
        {
            handle = CreateFileW(
                $@"\\.\PhysicalDrive{driveNumber}",
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                0,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                handle.Dispose();
                handle = CreateFileW(
                    $@"\\.\PhysicalDrive{driveNumber}",
                    GenericRead,
                    FileShareRead | FileShareWrite,
                    IntPtr.Zero,
                    OpenExisting,
                    0,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    handle.Dispose();
                    return null;
                }
            }

            var input = new SendCmdInParams
            {
                BufferSize = SmartDataSize,
                IdeRegs = new IdeRegs
                {
                    Features = SmartReadData,
                    SectorCount = 1,
                    SectorNumber = 1,
                    CylLow = SmartCylLow,
                    CylHigh = SmartCylHigh,
                    DriveHead = (byte)(0xA0 | ((driveNumber & 1) << 4)),
                    Command = SmartCommand,
                },
                DriveNumber = new byte[4],
                Reserved = new uint[3],
                Buffer = new byte[1],
            };
            input.DriveNumber[0] = (byte)driveNumber;

            var inputSize = Marshal.SizeOf<SendCmdInParams>();
            var outputSize = Marshal.SizeOf<SendCmdOutParams>();
            var inputPtr = Marshal.AllocHGlobal(inputSize);
            var outputPtr = Marshal.AllocHGlobal(outputSize);

            try
            {
                Marshal.StructureToPtr(input, inputPtr, false);
                var ok = DeviceIoControl(
                    handle,
                    SmartRcvDriveData,
                    inputPtr,
                    (uint)inputSize,
                    outputPtr,
                    (uint)outputSize,
                    out _,
                    IntPtr.Zero);

                if (!ok)
                {
                    return null;
                }

                var output = Marshal.PtrToStructure<SendCmdOutParams>(outputPtr);
                if (output.DriverStatus.DriverError != 0 || output.Buffer is null || output.Buffer.Length < SmartDataSize)
                {
                    return null;
                }

                return Parse(output.Buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(inputPtr);
                Marshal.FreeHGlobal(outputPtr);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            handle?.Dispose();
        }
    }

    private static SataSmartData Parse(byte[] data)
    {
        var attributes = new Dictionary<byte, byte[]>();
        for (var offset = 2; offset + 12 <= data.Length && attributes.Count < 30; offset += 12)
        {
            var id = data[offset];
            if (id == 0)
            {
                break;
            }

            var raw = new byte[6];
            Array.Copy(data, offset + 5, raw, 0, raw.Length);
            attributes[id] = raw;
        }

        double? wear = null;
        if (attributes.TryGetValue(231, out var lifeLeft))
        {
            // 0 means the life is exhausted: wear is 100%, not unknown.
            if (lifeLeft[0] <= 100)
            {
                wear = 100d - lifeLeft[0];
            }
        }
        else if (attributes.TryGetValue(233, out var wearout) && wearout[0] <= 100)
        {
            wear = wearout[0];
        }

        double? temperature = null;
        if (attributes.TryGetValue(194, out var temp))
        {
            var current = temp[0] > 0 ? temp[0] : temp[1];
            if (current is > 0 and < 150)
            {
                temperature = current;
            }
        }
        else if (attributes.TryGetValue(190, out var temp190))
        {
            var current = temp190[0] > 0 ? temp190[0] : temp190[1];
            if (current is > 0 and < 150)
            {
                temperature = current;
            }
        }

        return new SataSmartData(
            wear,
            attributes.TryGetValue(9, out var hours) ? RawToUInt64(hours) : null,
            attributes.TryGetValue(12, out var cycles) ? (uint?)RawToUInt64(cycles) : null,
            temperature,
            attributes.TryGetValue(5, out var reallocated) ? RawToUInt64(reallocated) : null,
            attributes.TryGetValue(197, out var pending) ? RawToUInt64(pending) : null,
            attributes.TryGetValue(198, out var uncorrected) ? RawToUInt64(uncorrected) : null,
            attributes.TryGetValue(241, out var written) ? RawToUInt64(written) * 512UL : null,
            attributes.TryGetValue(242, out var read) ? RawToUInt64(read) * 512UL : null);
    }

    private static ulong RawToUInt64(byte[] raw)
    {
        ulong value = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            value |= (ulong)raw[i] << (8 * i);
        }

        return value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IdeRegs
    {
        public byte Features;
        public byte SectorCount;
        public byte SectorNumber;
        public byte CylLow;
        public byte CylHigh;
        public byte DriveHead;
        public byte Command;
        public byte Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SendCmdInParams
    {
        public uint BufferSize;
        public IdeRegs IdeRegs;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] DriveNumber;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public uint[] Reserved;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public byte[] Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DriverStatus
    {
        public byte DriverError;
        public byte IdeError;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public byte[] Reserved;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
        public uint[] DwReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SendCmdOutParams
    {
        public uint BufferSize;
        public DriverStatus DriverStatus;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = SmartDataSize)]
        public byte[] Buffer;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);
}
