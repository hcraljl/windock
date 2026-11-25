using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
namespace SuperDock;
internal record PerformanceSample(double? Cpu,double Memory,double? Gpu,double Upload,double Download);
internal sealed class PerformanceMonitor : IDisposable
{
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out ulong idle,out ulong kernel,out ulong user);
    [StructLayout(LayoutKind.Sequential)] struct Memory { public uint Length,Load; public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,Extended; }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref Memory memory);
    [DllImport("pdh.dll",CharSet=CharSet.Unicode)] static extern uint PdhOpenQuery(string? source,IntPtr user,out IntPtr query);
    [DllImport("pdh.dll",CharSet=CharSet.Unicode)] static extern uint PdhAddEnglishCounter(IntPtr query,string path,IntPtr user,out IntPtr counter);
    [DllImport("pdh.dll")] static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll",CharSet=CharSet.Unicode)] static extern uint PdhGetFormattedCounterArray(IntPtr counter,uint format,ref uint bytes,out uint count,IntPtr items);
    [DllImport("pdh.dll")] static extern uint PdhCloseQuery(IntPtr query);
    [StructLayout(LayoutKind.Sequential)] struct Value { public uint Status; public double Number; }
    [StructLayout(LayoutKind.Sequential)] struct Item { public IntPtr Name; public Value Value; }
    ulong oldIdle,oldTotal; bool initialized;
    readonly Dictionary<string,(long Sent,long Received)> network = new();
    long previousTime=Stopwatch.GetTimestamp();
    IntPtr query,counter;
    public PerformanceMonitor()
    {
        if(PdhOpenQuery(null,IntPtr.Zero,out query)==0)
        {
            if(PdhAddEnglishCounter(query,@"\GPU Engine(*)\Utilization Percentage",IntPtr.Zero,out counter)!=0) counter=IntPtr.Zero;
            PdhCollectQueryData(query);
        }
    }
    public PerformanceSample Read()
    {
        double? cpu=null;
        if(GetSystemTimes(out var idle,out var kernel,out var user))
        {
            ulong total=kernel+user;
            if(initialized && total>oldTotal)cpu=Math.Clamp(100.0*(1-(double)(idle-oldIdle)/(total-oldTotal)),0,100);
            oldIdle=idle;oldTotal=total;initialized=true;
        }
        var memory=new Memory {Length=(uint)Marshal.SizeOf<Memory>()};GlobalMemoryStatusEx(ref memory);
        long now=Stopwatch.GetTimestamp();double seconds=Math.Max(.001,(double)(now-previousTime)/Stopwatch.Frequency);previousTime=now;
        double upload=0,download=0;var active=new HashSet<string>();
        foreach(var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if(adapter.OperationalStatus!=OperationalStatus.Up || adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)continue;
            try
            {
                var data=adapter.GetIPStatistics();active.Add(adapter.Id);
                if(network.TryGetValue(adapter.Id,out var old)) {upload+=Math.Max(0,data.BytesSent-old.Sent)/seconds;download+=Math.Max(0,data.BytesReceived-old.Received)/seconds;}
                network[adapter.Id]=(data.BytesSent,data.BytesReceived);
            }
            catch(NetworkInformationException) { }
        }
        foreach(var key in network.Keys.Where(key=>!active.Contains(key)).ToArray())network.Remove(key);
        return new(cpu,memory.Load,Gpu(),upload,download);
    }
    double? Gpu()
    {
        if(counter==IntPtr.Zero || PdhCollectQueryData(query)!=0)return null;
        uint bytes=0;PdhGetFormattedCounterArray(counter,0x200,ref bytes,out _,IntPtr.Zero);
        if(bytes==0 || bytes>16_000_000)return null;
        var buffer=Marshal.AllocHGlobal((int)bytes);
        try
        {
            if(PdhGetFormattedCounterArray(counter,0x200,ref bytes,out var count,buffer)!=0)return null;
            var engines=new Dictionary<string,double>();
            for(int i=0;i<count;i++)
            {
                var item=Marshal.PtrToStructure<Item>(IntPtr.Add(buffer,i*Marshal.SizeOf<Item>()));
                if(item.Value.Status>1 || !double.IsFinite(item.Value.Number))continue;
                var name=Marshal.PtrToStringUni(item.Name)??"";
                var key=Regex.Match(name,@"luid_.*?_phys_\d+_eng_\d+").Value;
                if(key.Length==0)continue;
                engines[key]=engines.GetValueOrDefault(key)+Math.Max(0,item.Value.Number);
            }
            return engines.Count>0?Math.Clamp(engines.Values.Max(),0,100):null;
        }
        finally {Marshal.FreeHGlobal(buffer);}
    }
    public void Dispose(){if(query!=IntPtr.Zero){PdhCloseQuery(query);query=IntPtr.Zero;counter=IntPtr.Zero;}}
}

