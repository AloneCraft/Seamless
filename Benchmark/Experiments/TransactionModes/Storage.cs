using Garnet.common;
using Seamless.Collections.Serializers;
using Seamless.Collections.Tsavorite;
using Tsavorite.core;
using Functions=Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordTriggers>;
using Allocator=Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordTriggers>>;
using Session=Tsavorite.core.ClientSession<Garnet.common.FixedSpanByteKey,TransactionModes.Row,TransactionModes.ReadResult,Tsavorite.core.Empty,TransactionModes.HistoryFunctions,Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordTriggers>,Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<Tsavorite.core.SpanByteComparer,Tsavorite.core.DefaultRecordTriggers>>>;

namespace TransactionModes;

public sealed class HistoryFunctions:SessionFunctionsBase<Row,ReadResult,Empty>{
    public override bool Reader<T>(in T source,ref Row input,ref ReadResult output,ref ReadInfo info){
        output=new(Auto<Row>.Deserialize(source.ValueSpan),info.Address,source.Info.PreviousAddress,true);return true;
    }
    public override RecordFieldInfo GetRMWInitialFieldInfo<TKey>(TKey key,ref Row input)=>new(){KeySize=key.KeyBytes.Length,ValueSize=System.Runtime.CompilerServices.Unsafe.SizeOf<Row>()};
    public override RecordFieldInfo GetRMWModifiedFieldInfo<T>(in T source,ref Row input)=>new(){KeySize=source.KeyBytes.Length,ValueSize=System.Runtime.CompilerServices.Unsafe.SizeOf<Row>()};
    public override bool InPlaceUpdater(ref LogRecord record,ref Row input,ref ReadResult output,ref RMWInfo info)=>false;
    public override bool InitialUpdater(ref LogRecord record,in RecordSizeInfo size,ref Row input,ref ReadResult output,ref RMWInfo info)=>Write(ref record,in size,ref input);
    public override bool CopyUpdater<T>(in T source,ref LogRecord record,in RecordSizeInfo size,ref Row input,ref ReadResult output,ref RMWInfo info){
        // Preserve はコールバックの後で native PreviousAddress を復元する。
        // 最終ヘッダーを先取りせず、native が渡した同一キーの SourceAddress を走査用のヒントにする。
        info.PreserveCopyUpdaterSourceRecord=true;input.Previous=info.SourceAddress;
        return record.TrySetValueSpanAndPrepareOptionals(Auto<Row>.Serialize(ref input),in size);
    }
    private static bool Write(ref LogRecord record,in RecordSizeInfo size,ref Row input){
        input.Previous=record.Info.PreviousAddress;
        return record.TrySetValueSpanAndPrepareOptionals(Auto<Row>.Serialize(ref input),in size);
    }
}

public sealed class Table:IDisposable{
    public readonly SetStore<int,Row> Data;
    public ParallelRegion<ParallelSet<int,Row>> Region{get;private set;}
    private readonly IDevice? log;
    private readonly IDevice? objects;
    private readonly long maximumTail;
    private readonly object allocation=new();
    public readonly ReaderWriterLockSlim Structure=new();
    public Table(string path,bool disk,int count,long budget){
        Directory.CreateDirectory(path);
        var logMemory=1L<<25;
        this.maximumTail=budget;
        var factory=Store.Create(IndexSize:IndexSize(count),PageSize:1L<<16,SegmentSize:1L<<24,MemorySize:logMemory);
        if(disk){
            this.log=Devices.CreateLogDevice(Path.Combine(path,"hlog"),preallocateFile:false,disableFileBuffering:false);
            this.objects=Devices.CreateLogDevice(Path.Combine(path,"olog"),preallocateFile:false,disableFileBuffering:false);
            this.Data=factory.CreateSet<int,Row>(this.log,this.objects);
        }else{this.Data=factory.CreateSet<int,Row>();}
        using(var rows=this.Data.Create()){
            for(var id=0;id<count;id++){rows.Add(new(){Id=id,Group=id%64,Value=100});}
        }
        this.Region=this.Data.BeginParallel(count+1024);
    }
    private static long IndexSize(int count){long size=1L<<14;while(size<count*16L){size*=2;}return size;}
    public Session Session()=>this.Data.Store.NewSession<FixedSpanByteKey,Row,ReadResult,Empty,HistoryFunctions>(new());
    public long Tail=>this.Data.Store.Log.TailAddress;
    public void Reserve(){lock(this.allocation){if(this.Tail-this.Data.Store.Log.BeginAddress+4096>=this.maximumTail){throw new HistoryBudgetException();}}}
    public void Evict()=>this.Data.Store.Log.FlushAndEvict(true);
    public long Compact(){
        this.Region.Dispose();var before=this.Tail-this.Data.Store.Log.BeginAddress;
        try{this.Data.Compact();}finally{this.Region=this.Data.BeginParallel();}
        return before-(this.Tail-this.Data.Store.Log.BeginAddress);
    }
    public void Dispose(){this.Region.Dispose();this.Data.Dispose();this.log?.Dispose();this.objects?.Dispose();this.Structure.Dispose();}
}

public sealed class Access:IDisposable{
    private readonly Table table;
    private readonly Session session;
    private bool disposed;
    public ParallelSet<int,Row> Rows{get;}
    public Access(Table table){this.table=table;this.session=table.Session();this.Rows=table.Region.Create();}
    private ReadResult Complete(Status status,ReadResult output){
        if(status.IsPending){
            this.session.BasicContext.CompletePendingWithOutputs(out var completed,wait:true);
            using(completed){while(completed.Next()){output=completed.Current.Output;}}
        }
        return output;
    }
    public unsafe ReadResult Latest(int id){
        var bytes=Auto<int>.Serialize(ref id);var input=default(Row);var output=default(ReadResult);
        var options=new ReadOptions();
        fixed(byte* pointer=bytes){var status=this.session.BasicContext.Read((FixedSpanByteKey)bytes,ref input,ref output,ref options,out _);return this.Complete(status,output);}
    }
    public ReadResult At(long address){
        var input=default(Row);var output=default(ReadResult);var options=new ReadOptions{CopyOptions=ReadCopyOptions.None};
        var status=this.session.BasicContext.ReadAtAddress(address,ref input,ref output,ref options,out _);
        output=this.Complete(status,output);
        if(!output.Captured){throw new InvalidDataException("Retained history is missing: "+address);}
        return output;
    }
    public unsafe void Put(Row row){
        this.table.Structure.EnterWriteLock();
        try{this.table.Reserve();var id=row.Id;var key=Auto<int>.Serialize(ref id);var output=default(ReadResult);
            var options=new RMWOptions();
            fixed(byte* pointer=key){var status=this.session.BasicContext.RMW((FixedSpanByteKey)key,ref row,ref output,ref options);_ =this.Complete(status,output);}
        }finally{this.table.Structure.ExitWriteLock();}
    }
    public void Dispose(){if(this.disposed){return;}this.disposed=true;this.Rows.Dispose();this.session.Dispose();}
}

public sealed class PlainTable:IDisposable{
    public readonly SetStore<int,PlainRow> Data;
    public readonly Set<int,PlainRow> Rows;
    private readonly IDevice? log;
    private readonly IDevice? objects;
    public PlainTable(string path,bool disk,int count){
        Directory.CreateDirectory(path);long index=1L<<14;while(index<count*16L){index*=2;}
        var factory=Store.Create(IndexSize:index,PageSize:1L<<16,SegmentSize:1L<<24,MemorySize:1L<<25);
        if(disk){this.log=Devices.CreateLogDevice(Path.Combine(path,"hlog"),preallocateFile:false,disableFileBuffering:false);this.objects=Devices.CreateLogDevice(Path.Combine(path,"olog"),preallocateFile:false,disableFileBuffering:false);this.Data=factory.CreateSet<int,PlainRow>(this.log,this.objects);}else{this.Data=factory.CreateSet<int,PlainRow>();}
        this.Rows=this.Data.Create();for(var id=0;id<count;id++){this.Rows.Add(new(){Id=id,Group=id%64,Value=100});}
    }
    public void Put(Row row){this.Rows.Remove(row.Id);if(row.Deleted==0){this.Rows.Add(new(){Id=row.Id,Group=row.Group,Value=row.Value});}}
    public void Dispose(){this.Rows.Dispose();this.Data.Dispose();this.log?.Dispose();this.objects?.Dispose();}
}
