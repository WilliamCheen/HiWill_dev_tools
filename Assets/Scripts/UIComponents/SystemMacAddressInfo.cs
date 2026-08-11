using System;
using System.Runtime.InteropServices;

public static class SystemMacAddressInfo
{
    [DllImport("WindowsHardwareInfo")]
    private static extern IntPtr GetMotherboardSerialNumber();
    
    [DllImport("WindowsHardwareInfo")]
    private static extern IntPtr GetCPUSerialNumber();
    
    [DllImport("WindowsHardwareInfo")]
    private static extern IntPtr GetDiskSerialNumber();
    
    private static string PtrToString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return string.Empty;
            
        string result = Marshal.PtrToStringAnsi(ptr);
        return result ?? string.Empty;
    }

    public static (string boardNum, string cpuNum, string diskNum) Numbers()
    {
        string boardStr = PtrToString(GetMotherboardSerialNumber());
        string cpuStr = PtrToString(GetCPUSerialNumber());
        string diskStr = PtrToString(GetDiskSerialNumber());
        return (boardStr, cpuStr, diskStr);
    }
}