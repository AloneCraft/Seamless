using Seamless.Execution;
using Seamless.Execution.Codecs;
using Seamless.Collections.Serializers;
using ExecutionStore=Seamless.Execution.Store;

namespace Test.Tsavorite.Collections;

public sealed class ResourceSerializerBudgetTests{
    [Theory]
    [InlineData("Write")]
    [InlineData("Read")]
    [InlineData("Enumerate")]
    [InlineData("RemoveWithValue")]
    public void GraphOperations_UseTheirOwningStoreBudgetAndReleaseWork(string operation){
        var parent=new ResourceBudget(32<<20,0);
        using(var store=ExecutionStore.CreateMemory(new(){MemoryBytes=32<<20,DiskBytes=0},parentBudget:parent)){
            var data=store.CreateTsavorite<int,Graph>("rows",Tuning());
            using var rows=data.Create<Blittable<int>,RecordGraphSerializer<Graph>>();
            var graph=CreateGraph();
            if(operation!="Write"){rows[1]=graph;}
            var baseline=parent.MemoryBytes;
            using(var pressure=parent.ReserveMemory(parent.AvailableMemoryBytes-512)){
                var occupied=parent.MemoryBytes;
                Assert.Throws<ResourceLimitException>(()=>{
                    switch(operation){
                        case "Write":rows[1]=graph;break;
                        case "Read":_ = rows[1];break;
                        case "Enumerate":_ = rows.ToArray();break;
                        case "RemoveWithValue":rows.Remove(1,out _);break;
                        default:throw new InvalidOperationException(operation);
                    }
                });
                Assert.Equal(occupied,parent.MemoryBytes);
            }
            Assert.Equal(baseline,parent.MemoryBytes);
            if(operation=="Write"){Assert.Equal(0,rows.Count);rows[1]=graph;}
            Assert.Equal(1,rows.Count);
            var restored=rows[1];Assert.Same(restored.First,restored.Second);Assert.Same(restored.First,restored.First[1]);
            Assert.Equal(0,BitConverter.DoubleToInt64Bits((double)restored.First[0]));
            Assert.True(rows.Remove(1,out var removed));Assert.Same(removed.First,removed.Second);Assert.Equal(0,rows.Count);
        }
        Assert.Equal(0,parent.MemoryBytes);
    }

    private static TsavoriteTuning Tuning()=>new(){IndexSize=1024,PageSize=4096,SegmentSize=1<<16,LogMemorySize=1<<16};

    [Theory]
    [InlineData("ContainsKey")]
    [InlineData("Keys")]
    [InlineData("Values")]
    [InlineData("Memory")]
    [InlineData("RemovePair")]
    public void GraphKeysAndViews_UseTheStoreBudget(string operation){
        using var store=ExecutionStore.CreateMemory(new(){MemoryBytes=32<<20,DiskBytes=0});
        var data=store.CreateTsavorite<Graph,Graph>("graphs",Tuning());
        using var rows=data.Create<RecordGraphSerializer<Graph>,RecordGraphSerializer<Graph>>();
        var graph=CreateGraph();rows[graph]=graph;
        using(var pressure=store.Budget.ReserveMemory(store.Budget.AvailableMemoryBytes-512)){
            var occupied=store.Budget.MemoryBytes;
            Assert.Throws<ResourceLimitException>(()=>{
                switch(operation){
                    case "ContainsKey":_ = rows.ContainsKey(graph);break;
                    case "Keys":_ = rows.Keys.ToArray();break;
                    case "Values":_ = rows.Values.ToArray();break;
                    case "Memory":_ = ((IMemoryCollection<KeyValuePair<Graph,Graph>>)rows).ToArray();break;
                    case "RemovePair":rows.Remove(new KeyValuePair<Graph,Graph>(graph,graph));break;
                    default:throw new InvalidOperationException(operation);
                }
            });
            Assert.Equal(occupied,store.Budget.MemoryBytes);
        }
        Assert.Equal(1,rows.Count);Assert.True(rows.ContainsKey(graph));Assert.Single(rows);
    }

    [Fact]
    public void BoundCodec_ReceivesTheOwnerAndRetainsOutputReservation(){
        using var store=ExecutionStore.CreateMemory(new(){MemoryBytes=32<<20,DiskBytes=0});
        var data=store.CreateTsavorite<int,Graph>("bound",Tuning());
        using var rows=data.Create<Blittable<int>,ProbeSerializer>();
        rows[0]=CreateGraph();
        ProbeSerializer.Baseline=store.Budget.MemoryBytes;ProbeSerializer.SawOutputReservation=false;
        try{
            rows[1]=CreateGraph();Assert.True(ProbeSerializer.SawOutputReservation);
            Assert.Same(store.Budget,ProbeSerializer.LastBudget);Assert.Equal(ProbeSerializer.Baseline,store.Budget.MemoryBytes);
            Assert.Equal(2,rows.ToArray().Length);Assert.Equal(2,rows.Values.ToArray().Length);Assert.Equal(2,rows.Count);
            Assert.True(rows.Remove(1,out _));
        }finally{ProbeSerializer.Baseline=0;ProbeSerializer.LastBudget=null;}
    }

    [Theory,InlineData(false),InlineData(true)]
    public void EncodeFailure_ReleasesOutputAndPreservesRows(bool overwrite){
        using var store=ExecutionStore.CreateMemory(new(){MemoryBytes=32<<20,DiskBytes=0});
        var data=store.CreateTsavorite<int,Graph>("failure",Tuning());
        using var rows=data.Create<Blittable<int>,ProbeSerializer>();
        rows[0]=CreateGraph();var baseline=store.Budget.MemoryBytes;
        var key=1;if(overwrite){key=0;}
        ProbeSerializer.FailEncode=true;
        try{Assert.Throws<ResourceLimitException>(()=>rows[key]=CreateGraph());}
        finally{ProbeSerializer.FailEncode=false;ProbeSerializer.LastBudget=null;}
        Assert.Equal(baseline,store.Budget.MemoryBytes);Assert.Equal(1,rows.Count);
        Assert.True(rows.ContainsKey(0));Assert.False(rows.ContainsKey(1));
        var original=rows[0];Assert.Same(original.First,original.Second);
        rows[1]=CreateGraph();Assert.Equal(2,rows.Count);Assert.Equal(2,rows.ToArray().Length);
    }

    private readonly struct ProbeSerializer:IResourceSerializer<Graph>{
        internal static ResourceBudget? LastBudget;
        internal static long Baseline;
        internal static bool SawOutputReservation;
        internal static bool FailEncode;
        public static ReadOnlySpan<byte> Serialize(ref Graph value)=>throw new InvalidOperationException("Unbound serializer used.");
        public static Graph Deserialize(ReadOnlySpan<byte> bytes)=>throw new InvalidOperationException("Unbound serializer used.");
        public static IAdapter<Graph> CreateCodec(ResourceBudget budget){LastBudget=budget;return new ProbeCodec(budget);}
    }

    [Fact]
    public void ParallelAccessors_UseTheOwnerBudgetAndRecoverAfterRejection(){
        using var store=ExecutionStore.CreateMemory(new(){MemoryBytes=32<<20,DiskBytes=0});
        var data=store.CreateTsavorite<int,Graph>("parallel",Tuning());
        using(var warmup=data.Create<Blittable<int>,ProbeSerializer>()){warmup[99]=CreateGraph();Assert.True(warmup.Remove(99));}
        using(var region=data.BeginParallel<Blittable<int>,ProbeSerializer>(4)){
            var rejected=0;
            using(var pressure=store.Budget.ReserveMemory(store.Budget.AvailableMemoryBytes-512)){
                var occupied=store.Budget.MemoryBytes;
                Parallel.For(0,4,new ParallelOptions{MaxDegreeOfParallelism=4},index=>{
                    using var worker=region.Create();
                    try{worker[index]=CreateGraph();}catch(ResourceLimitException){Interlocked.Increment(ref rejected);}
                });
                Assert.Equal(4,rejected);Assert.Equal(occupied,store.Budget.MemoryBytes);
            }
            Parallel.For(0,4,new ParallelOptions{MaxDegreeOfParallelism=4},index=>{
                using var worker=region.Create();worker[index]=CreateGraph();
                var row=worker[index];Assert.Same(row.First,row.Second);
                Assert.Same(store.Budget,ProbeSerializer.LastBudget);
            });
        }
        using var rows=data.Create<Blittable<int>,ProbeSerializer>();Assert.Equal(4,rows.Count);Assert.Equal(4,rows.ToArray().Length);
        ProbeSerializer.LastBudget=null;
    }

    [Theory,InlineData(false),InlineData(true)]
    public void DiskCompletion_UsesTheStoreBudgetAndLeavesTheSessionUsable(bool remove){
        using var store=ExecutionStore.CreateTemporary(new(){MemoryBytes=32<<20,DiskBytes=32<<20},temporaryDirectory:Path.GetTempPath());
        var data=store.CreateTsavorite<int,Graph>("disk",Tuning());
        using var rows=data.Create<Blittable<int>,RecordGraphSerializer<Graph>>();
        rows[1]=CreateGraph();data.Store.Log.FlushAndEvict(wait:true);rows.Refresh();
        Assert.True(data.GetStatistics().DiskLogBytes>0);
        using(var pressure=store.Budget.ReserveMemory(store.Budget.AvailableMemoryBytes-512)){
            var occupied=store.Budget.MemoryBytes;
            Assert.Throws<ResourceLimitException>(()=>{if(remove){rows.Remove(1,out _);}else{_ = rows[1];}});
            Assert.Equal(occupied,store.Budget.MemoryBytes);
        }
        Assert.Equal(1,rows.Count);var restored=rows[1];Assert.Same(restored.First,restored.Second);
        Assert.True(rows.Remove(1,out _));Assert.Equal(0,rows.Count);
    }

    [Fact]
    public void TwoStores_KeepIndependentLimitsUnderTheirSharedParent(){
        var parent=new ResourceBudget(64<<20,0);
        using(var first=ExecutionStore.CreateMemory(new(){MemoryBytes=32<<20,DiskBytes=0},parentBudget:parent))
        using(var second=ExecutionStore.CreateMemory(new(){MemoryBytes=32<<20,DiskBytes=0},parentBudget:parent)){
            var firstData=first.CreateTsavorite<int,Graph>("rows",Tuning());var secondData=second.CreateTsavorite<int,Graph>("rows",Tuning());
            using var firstRows=firstData.Create<Blittable<int>,RecordGraphSerializer<Graph>>();
            using var secondRows=secondData.Create<Blittable<int>,RecordGraphSerializer<Graph>>();
            var graph=CreateGraph();firstRows[0]=graph;secondRows[0]=graph;
            using(var pressure=first.Budget.ReserveMemory(first.Budget.AvailableMemoryBytes-512)){
                Assert.Throws<ResourceLimitException>(()=>_ = firstRows[0]);
                var other=secondRows[0];Assert.Same(other.First,other.Second);
                secondRows[1]=graph;Assert.Equal(2,secondRows.Count);
            }
            Assert.Equal(1,firstRows.Count);Assert.NotSame(firstRows[0].First,secondRows[0].First);
        }
        Assert.Equal(0,parent.MemoryBytes);
    }
    private sealed class ProbeCodec(ResourceBudget budget):IAdapter<Graph>{
        private readonly RecordGraphCodec<Graph> inner=new(budget);
        public string FormatId=>this.inner.FormatId;
        public int GetEncodedLength(Graph value)=>this.inner.GetEncodedLength(value);
        public void Encode(Graph value,Span<byte> destination){
            if(ProbeSerializer.Baseline!=0&&budget.MemoryBytes>=ProbeSerializer.Baseline+destination.Length+64){ProbeSerializer.SawOutputReservation=true;}
            if(ProbeSerializer.FailEncode){using var rejected=budget.ReserveMemory(budget.AvailableMemoryBytes+1);}
            this.inner.Encode(value,destination);
        }
        public Graph Decode(ReadOnlySpan<byte> source)=>this.inner.Decode(source);
    }

    private static Graph CreateGraph(){object[] values=[-0.0,null!];values[1]=values;return new(values,values);}
    private readonly record struct Graph(object[] First,object[] Second);
}
