using Garnet.common;
using Seamless.Collections.Serializers;
using Tsavorite.core;
using Functions=Tsavorite.core.StoreFunctions<TransactionModes.CollisionComparer,Tsavorite.core.DefaultRecordTriggers>;
using Allocator=Tsavorite.core.SpanByteAllocator<Tsavorite.core.StoreFunctions<TransactionModes.CollisionComparer,Tsavorite.core.DefaultRecordTriggers>>;

namespace TransactionModes;

public struct CollisionComparer:IKeyComparer{
    public readonly long GetHashCode64<TKey>(TKey key) where TKey:IKey,allows ref struct=>123456789;
    public readonly bool Equals<TFirst,TSecond>(TFirst first,TSecond second) where TFirst:IKey,allows ref struct where TSecond:IKey,allows ref struct=>first.KeyBytes.SequenceEqual(second.KeyBytes);
}
public static class CollisionProbe{
    public static void Run(string path,bool disk){
        Directory.CreateDirectory(path);IDevice selected;
        if(disk){selected=Devices.CreateLogDevice(Path.Combine(path,"hlog"),preallocateFile:false,disableFileBuffering:false);}else{selected=new NullDevice();}
        using var device=selected;
        var settings=new KVSettings{IndexSize=1L<<12,PageSize=1L<<12,SegmentSize=1L<<20,LogMemorySize=1L<<20,MutableFraction=1,LogDevice=device,ReadCacheEnabled=false,ReadCopyOptions=ReadCopyOptions.None,RevivificationSettings=RevivificationSettings.None};
        using var store=new TsavoriteKV<Functions,Allocator>(settings,new(new CollisionComparer(),static()=>null,DefaultRecordTriggers.Instance),static(s,f)=>new Allocator(s,f));
        using var session=store.NewSession<FixedSpanByteKey,Row,ReadResult,Empty,HistoryFunctions>(new());
        Put(1,100);var old=Read(1);Put(2,900);Put(1,200);var latest=Read(1);
        Acceptance.Check(At(latest.Previous).Row.Id==2,"Collision chain did not contain another key");
        Acceptance.Check(At(At(latest.Previous).Previous).Row.Value==100,"Collision chain lost the old revision");
        Acceptance.Check(At(old.Address).Row.Value==100,"Old address was overwritten");
        if(disk){store.Log.FlushAndEvict(true);Acceptance.Check(At(old.Address).Row.Value==100,"Eviction lost the old revision");Acceptance.Check(At(latest.Previous).Row.Id==2,"Eviction changed the collision chain");}

        unsafe void Put(int id,long value){
            var bytes=Auto<int>.Serialize(ref id);var input=new Row{Id=id,Value=value};var output=default(ReadResult);
            fixed(byte* pointer=bytes){var status=session.BasicContext.RMW((FixedSpanByteKey)bytes,ref input,ref output);_ =Complete(status,output);}
        }
        unsafe ReadResult Read(int id){
            var bytes=Auto<int>.Serialize(ref id);var input=default(Row);var output=default(ReadResult);
            fixed(byte* pointer=bytes){return Complete(session.BasicContext.Read((FixedSpanByteKey)bytes,ref input,ref output),output);}
        }
        ReadResult At(long address){
            var input=default(Row);var output=default(ReadResult);var options=new ReadOptions{CopyOptions=ReadCopyOptions.None};
            var status=session.BasicContext.ReadAtAddress(address,ref input,ref output,ref options,out _);output=Complete(status,output);
            Acceptance.Check(output.Captured,"Address read did not capture "+address);return output;
        }
        ReadResult Complete(Status status,ReadResult output){
            if(status.IsPending){session.BasicContext.CompletePendingWithOutputs(out var completed,wait:true);using(completed){while(completed.Next()){Acceptance.Check(completed.Current.Status.Found,"Pending read not found");output=completed.Current.Output;}}}
            return output;
        }
    }
}
