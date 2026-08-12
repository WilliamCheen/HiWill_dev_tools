using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;
using UniRx;
using AClockworkBerry;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
#endif

#if UNITY_STANDALONE_WIN
using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;
#endif

public class DeviceSerialNumberItem
{
    public readonly string name;
    public readonly string number;
    public readonly bool isSelected;

    public DeviceSerialNumberItem(string name, string number, bool isSelected)
    {
        this.name = name;
        this.number = number;
        this.isSelected = isSelected;
    }
}

public static class Utils
{
    // 唯一标识符
    private static string _currentDevicePhysicalAddress;
    public static DeviceSerialNumberItem[] DebugNumberItems;

    public static async UniTask<string> DeviceUniqueIdentifier()
    {
        if (!string.IsNullOrEmpty(_currentDevicePhysicalAddress))
        {
            return _currentDevicePhysicalAddress;
        }

        // 此处对于移动端可能会有特殊处理
        const bool shouldWait = false;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (shouldWait) await UniTask.NextFrame();

#if UNITY_STANDALONE_WIN
        // ReSharper disable once StringLiteralTypo
        //string boardNum = RunCommand("wmic baseboard get serialnumber");
        // ReSharper disable once StringLiteralTypo
        //string cpuNum = RunCommand("wmic cpu get processorid");
        // ReSharper disable once StringLiteralTypo
        //string diskNum = RunCommand("wmic diskdrive get serialnumber");

        try
        {
            // 获取 C++ dll 库里硬件信息，超时时间为 5s
            // 正常情况下此函数调用在毫秒级别
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var hardwareNumbers = await UniTask.RunOnThreadPool(SystemMacAddressInfo.Numbers, cancellationToken: cts.Token)
                .SuppressCancellationThrow();
            if (hardwareNumbers.IsCanceled || hardwareNumbers.Result == default)
            {
                Debug.LogWarning("[HardwareInfo] C++ 硬件查询超时或被取消，启动网络 MAC 降级方案");
                _currentDevicePhysicalAddress = GetNetworkMacAddress();
            }
            else
            {
                var (boardNum, cpuNum, diskNum) = hardwareNumbers.Result;

                string[] numberList = { boardNum, cpuNum, diskNum };
                var list = numberList.Where(IsValidSerialNumber).ToList();
                string macAddress = string.Join("_", list);
                string address = !string.IsNullOrEmpty(macAddress) ? macAddress : GetNetworkMacAddress();
                _currentDevicePhysicalAddress = address;
                
                // For Debug
                DeviceSerialNumberItem[] items =
                {
                    new("主板", boardNum, true),
                    new("CPU", cpuNum, true),
                    new("硬盘", diskNum, true),
                };
                DebugNumberItems = items.Where(e => IsValidSerialNumber(e.number)).ToArray();
                // End Debug
            }
        }
        catch (DllNotFoundException ex)
        {
            Debug.LogWarning($"[HardwareInfo] 缺失 C++ 运行库或 DLL 文件不存在: {ex.Message}");
            _currentDevicePhysicalAddress = GetNetworkMacAddress();
        }
        catch (Exception ex)
        {
            Debug.Log($"[HardwareInfo] 获取硬件信息时发生非致命异常：{ex.Message}");
            _currentDevicePhysicalAddress = GetNetworkMacAddress();
        }
        
        return _currentDevicePhysicalAddress;
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        return GetNetworkMacAddress();
#endif

        return SystemInfo.deviceUniqueIdentifier;
    }

    private static string GetNetworkMacAddress()
    {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
        // 获取物理地址
        var physicalAdapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni =>
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                !IsVirtualAdapter(ni) &&
                ni.GetPhysicalAddress().GetAddressBytes().Length > 0
            )
            .OrderBy(ni =>
            {
                var bytes = ni.GetPhysicalAddress().GetAddressBytes();
                return BitConverter.ToString(bytes).Replace("-", "").ToLower();
            })
            .ToList();

        if (physicalAdapters.Any())
        {
            PhysicalAddress address = physicalAdapters.First().GetPhysicalAddress();
            string macAddress = string.Join("-", address.GetAddressBytes().Select(b => b.ToString("X2")));
            _currentDevicePhysicalAddress = macAddress;
            Debug.Log($"Current physical address: {macAddress}");

            // For Debug
            var first = physicalAdapters.First();
            Debug.Log($"找到 {physicalAdapters.Count} 个物理适配器，按MAC地址排序:");
            List<DeviceSerialNumberItem> items = new();
            foreach (var adapter in physicalAdapters)
            {
                string mac = BitConverter.ToString(adapter.GetPhysicalAddress().GetAddressBytes());
                Debug.Log($"  -===>>: {mac} : {adapter.Name} ({adapter.NetworkInterfaceType})");
                items.Add(new DeviceSerialNumberItem(adapter.Name, mac, first == adapter));
            }
            DebugNumberItems = items.ToArray();
            
            // End Debug

            return macAddress;
        }
#endif
        return SystemInfo.deviceUniqueIdentifier;
    }

    

    
#if UNITY_STANDALONE_WIN
    /// <summary>
    /// 是否是有效的串号
    /// </summary>
    /// <param name="value">串号</param>
    /// <returns></returns>
    private static bool IsValidSerialNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string normalized = value.Trim().ToLowerInvariant();
        // 1. 关键字过滤
        string[] invalidKeywords =
        {
            "o.e.m", "oem", "default", "base board", "system serial", "serial number",
            "n/a", "none", "unknown", "undefined", "not applicable", "to be filled"
        };
        foreach (var keyword in invalidKeywords)
        {
            if (normalized.Contains(keyword)) return false;
        }

        // 2. 数字模式过滤
        if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^(0+|1+|2+|3+|4+|5+|6+|7+|8+|9+|123456789|0123456789)$"))
        {
            return false;
        }

        // 3. 长度过滤
        if (normalized.Length < 4 || normalized.Length > 64) return false;

        return true;
    }
    
    // 是否是虚拟网卡设备
    private static bool IsVirtualAdapter(NetworkInterface ni)
    {
        if (ni == null) return true;

        string name = ni.Name.ToLower();
        string description = ni.Description.ToLower();

        // 虚拟适配器关键词
        string[] virtualKeywords =
        {
            "virtual", "vmware", "virtualbox", "vpn", "hyper-v",
            "tap-", "vethernet", "veth", "docker", "wsl",
            "pseudo", "mullvad", "zerotier", "hamachi",
            "microsoft wi-fi", "microsoft kernal", "bluetooth"
        };

        // 虚拟适配器制造商
        string[] virtualVendors =
        {
            "vmware", "virtualbox", "microsoft", "parallels"
        };

        bool isVirtualByName = virtualKeywords.Any(keyword =>
            name.Contains(keyword) || description.Contains(keyword));

        bool isVirtualByVendor = virtualVendors.Any(vendor =>
            description.Contains(vendor));

        return isVirtualByName || isVirtualByVendor;
    }
#endif
    
    public static void AddScreenLoggerListener(MonoBehaviour target)
    {
        if (!ScreenLogger.Instance) return;
        const string localKey = "show_log_debug"; 
        if (PlayerPrefs.GetInt(localKey, 0) == 1)
        {
            ScreenLogger.Instance.ShowLog = true;
        }
        
        var clickStream = Observable.EveryUpdate()
            .Where(_ => Input.GetMouseButtonDown(0) && IsClickInHotZone(Input.mousePosition));
        var timeoutSingle = clickStream.Throttle(TimeSpan.FromSeconds(0.5f));
        clickStream
            .Buffer(timeoutSingle)
            .Where(clicks => ScreenLogger.Instance && clicks.Count >= 8)
            .Subscribe(_ =>
            {
                bool current = ScreenLogger.Instance.ShowLog;
                bool shouldShow = !current;
                ScreenLogger.Instance.ShowLog = shouldShow;
                PlayerPrefs.SetInt(localKey, shouldShow ? 1 : 0);
            })
            .AddTo(target);
    }
    
    private static bool IsClickInHotZone(Vector3 mousePosition)
    {
        bool isXInZone = mousePosition.x >= (Screen.width - 200);
        bool isYInZone = mousePosition.y <= 200;
        return isXInZone && isYInZone;
    }
}