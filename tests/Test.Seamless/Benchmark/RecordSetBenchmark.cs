using MemoryPack;
using Seamless.Collections;
using Seamless.Stores;

namespace Test.Seamless.Benchmarks;

[MemoryPackable]
public partial record struct ManagedBenchmarkSetRecord(int Key,string Name):IRecord<int>;

public readonly record struct UnmanagedBenchmarkSetRecord(int Key,long Value):IRecord<int>;

public abstract class RecordSetBenchmark<TRecord>(ITestOutputHelper output):Benchmark(output)
    where TRecord : IRecord<int>, IEquatable<TRecord> {
    public static TheoryData<int,double,long,long,int,int> MatrixPairs {
        get {
            var data = new TheoryData<int,double,long,long,int,int>();
            data.Add(1,0.5,64,1L<<12,14,12);
            if(BenchmarkRunSettings.RunFullMatrix) {
                data.Add(1,1.0,64,1L<<12,14,12);
                data.Add(4,0.5,64,1L<<12,14,12);
                data.Add(1,0.5,1L<<10,1L<<20,16,12);
            }
            return data;
        }
    }

    protected abstract Handle CreateHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits);
    protected abstract TRecord CreateRecord(int seed);

    protected abstract class Handle:IDisposable {
        public abstract ISeamlessSet<int,TRecord> Set { get; }
        public abstract Handle CreateSibling();
        public abstract void Dispose();
    }

    private void Drive(
        int parallel,
        double mutableFraction,
        long indexSize,
        long segmentSize,
        int memorySizeBits,
        int pageSizeBits,
        long opCount,
        bool prefill,
        Action<Handle> measured,
        string label
    ) {
        var handles = new Handle?[parallel];
        try {
            this.RunMeasured(
                parallel,
                mutableFraction,
                indexSize,
                segmentSize,
                memorySizeBits,
                pageSizeBits,
                opCount,
                setup: id => {
                    var handle = this.CreateHandle(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);
                    handles[id]=handle;
                    if(prefill) {
                        var n = Scaled;
                        for(var i = 0; i<n; i++)
                            handle.Set.Add(this.CreateRecord(i));
                    }
                },
                measured: id => measured(handles[id]!),
                label: label);
        }
        finally {
            foreach(var handle in handles)
                handle?.Dispose();
        }
    }

    private void Drive2(
        int parallel,
        double mutableFraction,
        long indexSize,
        long segmentSize,
        int memorySizeBits,
        int pageSizeBits,
        long opCount,
        bool prefillPrimary,
        bool prefillSibling,
        Action<Handle,Handle> measured,
        string label
    ) {
        var primary = new Handle?[parallel];
        var sibling = new Handle?[parallel];
        try {
            this.RunMeasured(
                parallel,
                mutableFraction,
                indexSize,
                segmentSize,
                memorySizeBits,
                pageSizeBits,
                opCount,
                setup: id => {
                    var handle = this.CreateHandle(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);
                    primary[id]=handle;
                    if(prefillPrimary) {
                        var n = Scaled;
                        for(var i = 0; i<n; i++)
                            handle.Set.Add(this.CreateRecord(i));
                    }
                    var other = handle.CreateSibling();
                    sibling[id]=other;
                    if(prefillSibling) {
                        var n = Scaled;
                        for(var i = 0; i<n; i++)
                            other.Set.Add(this.CreateRecord(i+(int)Scaled/2));
                    }
                },
                measured: id => measured(primary[id]!,sibling[id]!),
                label: label);
        }
        finally {
            foreach(var handle in primary)
                handle?.Dispose();
            foreach(var handle in sibling)
                handle?.Dispose();
        }
    }

    [Theory, MemberData(nameof(Matrix))]
    public void Set_Add(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: false,
            handle => {
                var n = Scaled;
                for(var i = 0; i<n; i++)
                    handle.Set.Add(this.CreateRecord(i));
            },
            nameof(Set_Add));

    [Theory, MemberData(nameof(Matrix))]
    public void Set_TryGetValue(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: true,
            handle => {
                var n = Scaled;
                for(var i = 0; i<n; i++)
                    _=handle.Set.TryGetValue(i,out _);
            },
            nameof(Set_TryGetValue));

    [Theory, MemberData(nameof(Matrix))]
    public void Set_Remove(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,Scaled,prefill: true,
            handle => {
                var n = Scaled;
                for(var i = 0; i<n; i++)
                    handle.Set.Remove(i);
            },
            nameof(Set_Remove));

    [Theory, MemberData(nameof(MatrixPairs))]
    public void Set_UnionWith(int parallel,double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => this.Drive2(parallel,mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits,1,prefillPrimary: true,prefillSibling: true,
            (handle,other) => handle.Set.UnionWith(other.Set),
            nameof(Set_UnionWith));
}

[Collection("Benchmark")]
public sealed class ManagedRecordSetBenchmark(ITestOutputHelper output):RecordSetBenchmark<ManagedBenchmarkSetRecord>(output) {
    protected override ManagedBenchmarkSetRecord CreateRecord(int seed) => new(seed,"value-"+seed);

    protected override Handle CreateHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => new ManagedHandle(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);

    private sealed class ManagedHandle:Handle {
        private readonly double mutableFraction;
        private readonly long indexSize;
        private readonly long segmentSize;
        private readonly int memorySizeBits;
        private readonly int pageSizeBits;
        private readonly SetStore<int,ManagedBenchmarkSetRecord> store;
        private readonly Set<int,ManagedBenchmarkSetRecord> set;

        public ManagedHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
            this.mutableFraction=mutableFraction;
            this.indexSize=indexSize;
            this.segmentSize=segmentSize;
            this.memorySizeBits=memorySizeBits;
            this.pageSizeBits=pageSizeBits;
            this.store=HashStore.Create(
                IndexSize: indexSize,
                PageSize: 1L<<pageSizeBits,
                SegmentSize: segmentSize,
                MemorySize: 1L<<memorySizeBits,
                MutableFraction: mutableFraction
            ).CreateSet<int,ManagedBenchmarkSetRecord>();
            this.set=this.store.Create();
        }

        public override ISeamlessSet<int,ManagedBenchmarkSetRecord> Set => this.set;

        public override Handle CreateSibling() => new ManagedHandle(this.mutableFraction,this.indexSize,this.segmentSize,this.memorySizeBits,this.pageSizeBits);

        public override void Dispose() {
            this.set.Dispose();
            this.store.Dispose();
        }
    }
}

[Collection("Benchmark")]
public sealed class UnmanagedRecordSetBenchmark(ITestOutputHelper output):RecordSetBenchmark<UnmanagedBenchmarkSetRecord>(output) {
    protected override UnmanagedBenchmarkSetRecord CreateRecord(int seed) => new(seed,seed*17L);

    protected override Handle CreateHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits)
        => new UnmanagedHandle(mutableFraction,indexSize,segmentSize,memorySizeBits,pageSizeBits);

    private sealed class UnmanagedHandle:Handle {
        private readonly double mutableFraction;
        private readonly long indexSize;
        private readonly long segmentSize;
        private readonly int memorySizeBits;
        private readonly int pageSizeBits;
        private readonly SetStore<int,UnmanagedBenchmarkSetRecord> store;
        private readonly Set<int,UnmanagedBenchmarkSetRecord> set;

        public UnmanagedHandle(double mutableFraction,long indexSize,long segmentSize,int memorySizeBits,int pageSizeBits) {
            this.mutableFraction=mutableFraction;
            this.indexSize=indexSize;
            this.segmentSize=segmentSize;
            this.memorySizeBits=memorySizeBits;
            this.pageSizeBits=pageSizeBits;
            this.store=HashStore.Create(
                IndexSize: indexSize,
                PageSize: 1L<<pageSizeBits,
                SegmentSize: segmentSize,
                MemorySize: 1L<<memorySizeBits,
                MutableFraction: mutableFraction
            ).CreateSet<int,UnmanagedBenchmarkSetRecord>();
            this.set=this.store.Create();
        }

        public override ISeamlessSet<int,UnmanagedBenchmarkSetRecord> Set => this.set;

        public override Handle CreateSibling() => new UnmanagedHandle(this.mutableFraction,this.indexSize,this.segmentSize,this.memorySizeBits,this.pageSizeBits);

        public override void Dispose() {
            this.set.Dispose();
            this.store.Dispose();
        }
    }
}
