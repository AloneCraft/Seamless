using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;

namespace Seamless.Management.Server;

/// <summary>ホストと現在のプロセスの観測。クエリやノードへの帰属は推定しない。</summary>
internal sealed partial class OsResourceHeatProvider:IResourceHeatProvider{
    private readonly Process process=Process.GetCurrentProcess();
    private readonly Dictionary<string,(double Value,long At)> counters=new(StringComparer.Ordinal);
    private readonly Dictionary<string,(ulong Busy,ulong Total,long At)> processors=new(StringComparer.Ordinal);
    private readonly Dictionary<string,string> numa=new(StringComparer.Ordinal);
    private WindowsCounters? windows;
    private bool disposed;
    private const int MaximumLanes=512;
    internal OsResourceHeatProvider(){
        if(OperatingSystem.IsWindows()){this.windows=new();}
        if(OperatingSystem.IsLinux()){
            try{
                foreach(var path in Directory.EnumerateDirectories("/sys/devices/system/node","node*").Take(256)){
                    foreach(var cpu in Directory.EnumerateDirectories(path,"cpu*").Take(4096)){this.numa[Path.GetFileName(cpu)]=Path.GetFileName(path);}
                }
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    public ResourceHeatReading Read(CancellationToken cancellationToken=default){
        ObjectDisposedException.ThrowIf(this.disposed,this);cancellationToken.ThrowIfCancellationRequested();
        var lanes=new List<ResourceLane>();var samples=new List<ResourceSample>();var diagnostics=new List<string>{"NotImplemented: PCIe utilization, NUMA bandwidth and hardware performance counters are not collected.","Topology describes this OS instance; virtual resources and OS memory domains are not physical-host identification."};
        void Category(string name,Action read){
            cancellationToken.ThrowIfCancellationRequested();
            try{read();}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or NetworkInformationException or FormatException or OverflowException){
                diagnostics.Add(name+": "+error.GetType().Name);
                var id="unavailable:"+name;lanes.Add(new(id,name,"Capability","Host","Availability","Unknown"));
                var at=Stopwatch.GetTimestamp();samples.Add(new(id,at,at,null,null,"Missing","Unknown",MissingReason:error.GetType().Name,Aggregation:"Instant"));
            }
        }
        Category("Process",()=>{
            this.process.Refresh();var cpuTime=this.process.TotalProcessorTime.TotalSeconds;var at=Stopwatch.GetTimestamp();
            var cpu=new ResourceLane("process:cpu","Process CPU","CPU","Process","CpuTime","CpuSecondsPerSecond",PercentBasis:new("One logical CPU = 100%",1,"LogicalCPU","Process.TotalProcessorTime"));
            lanes.Add(cpu);samples.Add(this.Rate(cpu,cpuTime,at,100));
            var memory=new ResourceLane("process:working-set","Process working set","Memory","Process","WorkingSet","Bytes");
            var workingSet=this.process.WorkingSet64;at=Stopwatch.GetTimestamp();lanes.Add(memory);samples.Add(new(memory.Id,at,at,workingSet,null,Aggregation:"Instant"));
        });
        if(OperatingSystem.IsLinux()){
            Category("CPU",()=>this.ReadLinuxCpu(lanes,samples,cancellationToken));
            Category("Memory",()=>this.ReadLinuxMemory(lanes,samples));
            Category("Disk",()=>this.ReadLinuxDisk(lanes,samples,cancellationToken));
            Category("NUMA",()=>ReadLinuxNuma(lanes,samples,cancellationToken));
        }else if(this.windows is{} windows){
            Category("WindowsCounters",()=>windows.Read(lanes,samples,diagnostics,cancellationToken));
        }else{diagnostics.Add("Host CPU, disk and NUMA counters are unsupported on this OS.");}
        Category("NIC",()=>{
            foreach(var nic in NetworkInterface.GetAllNetworkInterfaces().Take(256)){
                cancellationToken.ThrowIfCancellationRequested();if(lanes.Count>=MaximumLanes){break;}
                try{
                    var stats=nic.GetIPStatistics();var at=Stopwatch.GetTimestamp();var speed=nic.Speed;ResourcePercentBasis? basis=null;double? percentScale=null;
                    if(speed>0){basis=new("OS-reported interface link speed; each direction independently",speed,"BitsPerSecond","NetworkInterface.Speed");percentScale=800d/speed;}
                    foreach(var value in new[]{("rx",stats.BytesReceived),("tx",stats.BytesSent)}){
                        var lane=new ResourceLane("nic:"+nic.Id+":"+value.Item1,nic.Name+" "+value.Item1,"NIC","Host","BytesPerSecond","BytesPerSecond",PercentBasis:basis,Topology:new(){["OsInterfaceId"]=nic.Id,["InterfaceType"]=nic.NetworkInterfaceType.ToString(),["Source"]="NetworkInterface"});
                        lanes.Add(lane);samples.Add(this.Rate(lane,value.Item2,at,percentScale));
                    }
                }catch(NetworkInformationException){diagnostics.Add("NIC counter unavailable: "+nic.Id);}
            }
        });
        if(lanes.Count>=MaximumLanes){diagnostics.Add("Resource lane collection limit reached.");lanes=lanes.Take(MaximumLanes).ToList();var ids=lanes.Select(lane=>lane.Id).ToHashSet(StringComparer.Ordinal);samples.RemoveAll(sample=>!ids.Contains(sample.LaneId));}
        return new(lanes.ToArray(),samples.ToArray(),diagnostics.Distinct(StringComparer.Ordinal).Take(128).ToArray());
    }
    private ResourceSample Rate(ResourceLane lane,double value,long at,double? percentScale=null){
        this.counters.TryGetValue(lane.Id,out var previous);this.counters[lane.Id]=(value,at);
        if(previous.At==0){return Missing(lane.Id,at,"NoData","A second counter observation is required.");}
        if(value<previous.Value||at<=previous.At){return Missing(lane.Id,at,"Gap","The counter reset or its time window is empty.");}
        var seconds=Stopwatch.GetElapsedTime(previous.At,at).TotalSeconds;if(seconds<=0){return Missing(lane.Id,at,"NoData","The observation window is below the timer resolution.");}var rate=(value-previous.Value)/seconds;
        double? percent=null;if(percentScale is{} scale){percent=rate*scale;}
        return new(lane.Id,previous.At,at,rate,percent);
    }
    private static ResourceSample Missing(string id,long at,string state,string reason)=>new(id,at,at,null,null,state,"Unknown",MissingReason:reason,Aggregation:"Instant");
    private void ReadLinuxCpu(List<ResourceLane> lanes,List<ResourceSample> samples,CancellationToken cancellation){
        foreach(var line in File.ReadLines("/proc/stat").Take(8192)){
            cancellation.ThrowIfCancellationRequested();if(lanes.Count>=MaximumLanes){break;}
            var fields=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(fields.Length<9||!fields[0].StartsWith("cpu",StringComparison.Ordinal)||fields[0]=="cpu"){continue;}
            ulong total=0;for(var index=1;index<=8;index++){total=checked(total+ulong.Parse(fields[index],CultureInfo.InvariantCulture));}
            var idle=ulong.Parse(fields[4],CultureInfo.InvariantCulture)+ulong.Parse(fields[5],CultureInfo.InvariantCulture);var busy=total-idle;var at=Stopwatch.GetTimestamp();
            var id="cpu:"+fields[0];Dictionary<string,string> topology=new(){["LogicalProcessor"]=fields[0]};if(this.numa.TryGetValue(fields[0],out var node)){topology["NUMA"]=node;}
            topology["Source"]="/proc/stat";
            var lane=new ResourceLane(id,fields[0],"CPU","Host","NonIdleCpuTime","Percent",PercentBasis:new("One logical CPU; idle and iowait excluded",1,"LogicalCPU","/proc/stat"),Topology:topology);lanes.Add(lane);
            this.processors.TryGetValue(id,out var previous);this.processors[id]=(busy,total,at);
            if(previous.At==0){samples.Add(Missing(id,at,"NoData","A second CPU counter observation is required."));}
            else if(total<=previous.Total||busy<previous.Busy){samples.Add(Missing(id,at,"Gap","CPU counters reset or have insufficient resolution."));}
            else{var percent=100d*(busy-previous.Busy)/(total-previous.Total);samples.Add(new(id,previous.At,at,percent,percent));}
        }
    }
    private void ReadLinuxMemory(List<ResourceLane> lanes,List<ResourceSample> samples){
        var values=File.ReadLines("/proc/meminfo").Take(256).Select(line=>line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries)).Where(fields=>fields.Length>=2).ToDictionary(fields=>fields[0],fields=>double.Parse(fields[1],CultureInfo.InvariantCulture)*1024,StringComparer.Ordinal);
        if(!values.TryGetValue("MemTotal:",out var total)||!values.TryGetValue("MemAvailable:",out var available)){throw new IOException("MemAvailable is unavailable.");}
        var lane=new ResourceLane("host:memory","Host memory in use","Memory","Host","UsedMemory","Bytes",PercentBasis:new("MemTotal minus MemAvailable",total,"Bytes","/proc/meminfo"));var at=Stopwatch.GetTimestamp();lanes.Add(lane);samples.Add(new(lane.Id,at,at,total-available,100*(total-available)/total,Aggregation:"Instant"));
    }
    private void ReadLinuxDisk(List<ResourceLane> lanes,List<ResourceSample> samples,CancellationToken cancellation){
        foreach(var line in File.ReadLines("/proc/diskstats").Take(4096)){
            cancellation.ThrowIfCancellationRequested();if(lanes.Count>=MaximumLanes){break;}
            var fields=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);if(fields.Length<14){continue;}
            // 個別partitionと親deviceの二重加算をせず、/sys/blockに存在するデバイスを独立表示する。
            var name=fields[2];if(!Directory.Exists("/sys/block/"+name)){continue;}var at=Stopwatch.GetTimestamp();
            foreach(var field in new[]{("read",5),("write",9)}){
                var lane=new ResourceLane("disk:"+name+":"+field.Item1,name+" "+field.Item1,"Storage","Host","BytesPerSecond","BytesPerSecond",Topology:new(){["OsBlockDevice"]=name,["Source"]="/proc/diskstats; /sys/block"});lanes.Add(lane);
                samples.Add(this.Rate(lane,double.Parse(fields[field.Item2],CultureInfo.InvariantCulture)*512,at));
            }
        }
    }
    private static void ReadLinuxNuma(List<ResourceLane> lanes,List<ResourceSample> samples,CancellationToken cancellation){
        foreach(var path in Directory.EnumerateDirectories("/sys/devices/system/node","node*").Take(256)){
            cancellation.ThrowIfCancellationRequested();if(lanes.Count>=MaximumLanes){break;}
            var line=File.ReadLines(Path.Combine(path,"meminfo")).FirstOrDefault(line=>line.Contains("MemFree:",StringComparison.Ordinal));if(line is null){continue;}
            var fields=line.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);var free=double.Parse(fields[^2],CultureInfo.InvariantCulture)*1024;
            var node=Path.GetFileName(path);var lane=new ResourceLane("numa:"+node+":free",node+" free memory","NUMA","Host","FreeMemory","Bytes",Topology:new(){["OsMemoryDomain"]=node,["Source"]="/sys/devices/system/node"});var at=Stopwatch.GetTimestamp();lanes.Add(lane);samples.Add(new(lane.Id,at,at,free,null,Aggregation:"Instant"));
        }
    }
    public void Dispose(){if(this.disposed){return;}this.disposed=true;this.windows?.Dispose();this.process.Dispose();}
}
