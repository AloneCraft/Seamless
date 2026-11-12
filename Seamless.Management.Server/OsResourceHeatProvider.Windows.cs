using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Seamless.Management.Server;

internal sealed partial class OsResourceHeatProvider{
    private sealed class WindowsCounters:IDisposable{
        private nint query;
        private readonly List<(nint Handle,string Kind,string Metric,string Unit)> counters=[];
        private long previous;
        private readonly List<string> unavailable=[];
        internal WindowsCounters(){
            if(PdhOpenQueryW(null,0,out this.query)!=0){this.unavailable.Add("Windows performance counters are unavailable.");return;}
            this.Add(@"\Processor Information(*)\% Processor Time","CPU","CpuTime","Percent");
            this.Add(@"\PhysicalDisk(*)\Disk Read Bytes/sec","Storage","ReadBytesPerSecond","BytesPerSecond");
            this.Add(@"\PhysicalDisk(*)\Disk Write Bytes/sec","Storage","WriteBytesPerSecond","BytesPerSecond");
        }
        private void Add(string path,string kind,string metric,string unit){
            var status=PdhAddEnglishCounterW(this.query,path,0,out var handle);
            if(status==0){this.counters.Add((handle,kind,metric,unit));}else{this.unavailable.Add(path+" unavailable: "+status.ToString("X8",System.Globalization.CultureInfo.InvariantCulture));}
        }
        internal void Read(List<ResourceLane> lanes,List<ResourceSample> samples,List<string> diagnostics,CancellationToken cancellation){
            diagnostics.AddRange(this.unavailable);
            if(this.query==0){return;}
            var status=PdhCollectQueryData(this.query);var at=Stopwatch.GetTimestamp();
            if(status!=0){throw new InvalidOperationException("PdhCollectQueryData: "+status.ToString("X8",System.Globalization.CultureInfo.InvariantCulture));}
            foreach(var counter in this.counters){
                cancellation.ThrowIfCancellationRequested();uint bytes=0,count=0;
                status=PdhGetFormattedCounterArrayW(counter.Handle,0x200|0x8000,ref bytes,ref count,0);
                if(status!=0x800007D2||bytes is 0 or >1048576||count>8192){diagnostics.Add(counter.Metric+" counter array unavailable: "+status.ToString("X8",System.Globalization.CultureInfo.InvariantCulture));continue;}
                var buffer=Marshal.AllocHGlobal(checked((int)bytes));
                try{
                    status=PdhGetFormattedCounterArrayW(counter.Handle,0x200|0x8000,ref bytes,ref count,buffer);
                    if(status!=0){diagnostics.Add(counter.Metric+" counter values unavailable.");continue;}
                    for(var index=0;index<count&&lanes.Count<MaximumLanes;index++){
                        var item=Marshal.PtrToStructure<CounterItem>(buffer+checked(index*Marshal.SizeOf<CounterItem>()));var name=Marshal.PtrToStringUni(item.Name);
                        if(name is null||name.Contains("_Total",StringComparison.Ordinal)){continue;}
                        var id="windows:"+counter.Kind+":"+name+":"+counter.Metric;ResourcePercentBasis? basis=null;Dictionary<string,string>? topology=null;
                        if(counter.Kind=="CPU"){
                            basis=new("One logical CPU = 100%",1,"LogicalCPU","PDH Processor Information/% Processor Time");
                            topology=new(){["ProcessorInstance"]=name};var parts=name.Split(',');
                            if(parts.Length==2&&ushort.TryParse(parts[0],out var group)&&byte.TryParse(parts[1],out var number)){
                                var processor=new ProcessorNumber{Group=group,Number=number};if(GetNumaProcessorNodeEx(ref processor,out var node)&&node!=ushort.MaxValue){topology["NUMA"]=node.ToString(System.Globalization.CultureInfo.InvariantCulture);}
                            }
                        }
                        topology??=new(){["OsDiskCounterInstance"]=name};topology["Source"]="Windows PDH";
                        var lane=new ResourceLane(id,name+" "+counter.Metric,counter.Kind,"Host",counter.Metric,counter.Unit,PercentBasis:basis,Topology:topology);lanes.Add(lane);
                        if(this.previous==0||item.Status>1||!double.IsFinite(item.Value)||item.Value<0){samples.Add(Missing(id,at,"NoData","PDH requires two valid observations for this counter."));}
                        else{double? percent=null;if(basis is not null){percent=item.Value;}samples.Add(new(id,this.previous,at,item.Value,percent));}
                    }
                }finally{Marshal.FreeHGlobal(buffer);}
            }
            this.previous=at;
            var memory=new MemoryStatus{Length=(uint)Marshal.SizeOf<MemoryStatus>()};
            if(GlobalMemoryStatusEx(ref memory)&&memory.TotalPhysical>0){
                at=Stopwatch.GetTimestamp();
                var lane=new ResourceLane("host:memory","Host physical memory in use","Memory","Host","UsedMemory","Bytes",PercentBasis:new("Total physical minus available physical memory",memory.TotalPhysical,"Bytes","GlobalMemoryStatusEx"));lanes.Add(lane);samples.Add(new(lane.Id,at,at,memory.TotalPhysical-memory.AvailablePhysical,100d*(memory.TotalPhysical-memory.AvailablePhysical)/memory.TotalPhysical,Aggregation:"Instant"));
            }else{diagnostics.Add("GlobalMemoryStatusEx unavailable.");}
            if(GetNumaHighestNodeNumber(out var highest)){
                for(ushort node=0;node<=highest&&node<256&&lanes.Count<MaximumLanes;node++){
                    if(!GetNumaAvailableMemoryNodeEx(node,out var available)){continue;}
                    at=Stopwatch.GetTimestamp();
                    var lane=new ResourceLane("numa:"+node+":free","NUMA "+node+" available memory","NUMA","Host","AvailableMemory","Bytes",Topology:new(){["OsMemoryDomain"]=node.ToString(System.Globalization.CultureInfo.InvariantCulture),["Source"]="GetNumaAvailableMemoryNodeEx"});lanes.Add(lane);samples.Add(new(lane.Id,at,at,available,null,Aggregation:"Instant"));
                }
            }
        }
        public void Dispose(){if(this.query==0){return;}_=PdhCloseQuery(this.query);this.query=0;}
        [StructLayout(LayoutKind.Sequential)]private struct CounterItem{internal nint Name;internal uint Status;internal double Value;}
        [StructLayout(LayoutKind.Sequential)]private struct ProcessorNumber{internal ushort Group;internal byte Number;internal byte Reserved;}
        [StructLayout(LayoutKind.Sequential)]private struct MemoryStatus{internal uint Length,Load;internal ulong TotalPhysical,AvailablePhysical,TotalPageFile,AvailablePageFile,TotalVirtual,AvailableVirtual,AvailableExtendedVirtual;}
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]private static extern uint PdhOpenQueryW(string? source,nuint user,out nint query);
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]private static extern uint PdhAddEnglishCounterW(nint query,string path,nuint user,out nint counter);
        [DllImport("pdh.dll")]private static extern uint PdhCollectQueryData(nint query);
        [DllImport("pdh.dll",CharSet=CharSet.Unicode)]private static extern uint PdhGetFormattedCounterArrayW(nint counter,uint format,ref uint bytes,ref uint count,nint items);
        [DllImport("pdh.dll")]private static extern uint PdhCloseQuery(nint query);
        [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNumaProcessorNodeEx(ref ProcessorNumber processor,out ushort node);
        [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNumaHighestNodeNumber(out uint highest);
        [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNumaAvailableMemoryNodeEx(ushort node,out ulong available);
    }
}
