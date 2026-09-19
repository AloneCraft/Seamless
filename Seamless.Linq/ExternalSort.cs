using System.IO;
using System.Buffers.Binary;
using Seamless.Execution;
namespace Seamless.Linq;
internal static class ExternalSort<T,TKey>{
    private readonly record struct Row(long Position,TKey Key,T Value);
    internal static IEnumerable<T> Sort(ExecutionScope scope,IEnumerable<T> source,Func<T,TKey> selector,IComparer<TKey> keyComparer){
        using var metadata=scope.Budget.ReserveMemory(2048);
        using var memory=scope.Budget.ReserveMemory(0);
        var runs=new TemporaryFile?[64];
        var rows=new List<Row>();
        var comparer=Comparer<Row>.Create((x,y)=>{
            var comparison=keyComparer.Compare(x.Key,y.Key);
            if(comparison!=0){return comparison;}
            return x.Position.CompareTo(y.Position);
        });
        var threshold=Math.Max(1,(scope.Budget.MemoryLimitBytes-scope.Budget.MemoryBytes)/3);
        var canEncode=scope.Codecs.CanEncode<(long,TKey,T)>();
        IBinaryCodec<(long,TKey,T)>? codec=null;
        if(canEncode){codec=scope.Codecs.Get<(long,TKey,T)>();}
        long position=0,values=0;
        TemporaryFile? final=null;
        try{
            foreach(var value in source){
                scope.ThrowIfUnavailable();
                var row=new Row(position,selector(value),value);
                position=checked(position+1);
                long size=96;
                if(codec is not null){size=checked(size+4L*codec.GetEncodedLength((row.Position,row.Key,row.Value)));}
                if(rows.Count>0&&memory.Bytes+size+Unsafe.SizeOf<Row>()*2L>threshold){
                    AddRun(WriteRun(),runs,scope,codec!,comparer);
                }
                var capacity=rows.Capacity;
                if(rows.Count==capacity){capacity=Math.Max(4,checked(capacity*2));}
                var required=checked(values+size+capacity*(long)(Unsafe.SizeOf<Row>()+16));
                if(required>scope.Budget.MemoryLimitBytes){throw new ResourceLimitException(ResourceLimitKind.SingleElement,required,scope.Budget.MemoryLimitBytes);}
                memory.Resize(required);
                if(capacity!=rows.Capacity){rows.Capacity=capacity;}
                rows.Add(row);values+=size;
            }
            if(runs.All(static x=>x is null)){
                rows.Sort(comparer);
                foreach(var row in rows){scope.ThrowIfUnavailable();yield return row.Value;}
                yield break;
            }
            if(rows.Count>0){AddRun(WriteRun(),runs,scope,codec!,comparer);}
            foreach(var run in runs){
                if(run is null){continue;}
                if(final is null){final=run;}
                else{final=Merge(scope,final,run,codec!,comparer);}
            }
            if(final is not null){
                using var reader=new Cursor(scope,final,codec!);
                while(reader.MoveNext()){yield return reader.Current.Value;}
            }
        }finally{
            final?.Dispose();
            foreach(var run in runs){run?.Dispose();}
        }
        TemporaryFile WriteRun(){
            if(codec is null){throw new CodecUnavailableException(typeof((long,TKey,T)));}
            rows.Sort(comparer);
            var file=scope.CreateTemporaryFile();
            try{
                foreach(var row in rows){Write(scope,file,codec,row);}
                rows.Clear();rows.Capacity=0;values=0;memory.Resize(0);
                file.Position=0;return file;
            }catch{file.Dispose();throw;}
        }
    }
    private static void AddRun(TemporaryFile file,TemporaryFile?[] runs,ExecutionScope scope,IBinaryCodec<(long,TKey,T)> codec,IComparer<Row> comparer){
        try{
            for(var level=0;level<runs.Length;level++){
                if(runs[level] is null){runs[level]=file;return;}
                var previous=runs[level]!;
                runs[level]=null;
                file=Merge(scope,previous,file,codec,comparer);
            }
            throw new OverflowException("The number of sort runs exceeds Int64 capacity.");
        }catch{file.Dispose();throw;}
    }
    private static TemporaryFile Merge(ExecutionScope scope,TemporaryFile first,TemporaryFile second,IBinaryCodec<(long,TKey,T)> codec,IComparer<Row> comparer){
        var output=scope.CreateTemporaryFile();
        try{
            using var left=new Cursor(scope,first,codec);
            using var right=new Cursor(scope,second,codec);
            var hasLeft=left.MoveNext();var hasRight=right.MoveNext();
            while(hasLeft||hasRight){
                scope.ThrowIfUnavailable();
                if(hasLeft&&(!hasRight||comparer.Compare(left.Current,right.Current)<=0)){
                    Write(scope,output,codec,left.Current);hasLeft=left.MoveNext();
                }else{
                    Write(scope,output,codec,right.Current);hasRight=right.MoveNext();
                }
            }
            output.Position=0;
            return output;
        }catch{output.Dispose();throw;}
        finally{first.Dispose();second.Dispose();}
    }
    private static void Write(ExecutionScope scope,TemporaryFile file,IBinaryCodec<(long,TKey,T)> codec,Row row){
        var tuple=(row.Position,row.Key,row.Value);
        using var buffer=scope.Rent(codec.GetEncodedLength(tuple));
        codec.Encode(tuple,buffer.Memory.Span);
        Span<byte> length=stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length,buffer.Memory.Length);
        file.Write(length);file.Write(buffer.Memory.Span);
    }
    private sealed class Cursor:IDisposable{
        private readonly ExecutionScope scope;
        private readonly TemporaryFile file;
        private readonly IBinaryCodec<(long,TKey,T)> codec;
        private readonly ResourceReservation memory;
        public Cursor(ExecutionScope scope,TemporaryFile file,IBinaryCodec<(long,TKey,T)> codec){
            this.scope=scope;this.file=file;this.codec=codec;
            this.memory=scope.Budget.ReserveMemory(0);
            file.Position=0;
        }
        public Row Current{get;private set;}
        public bool MoveNext(){
            this.Current=default;this.memory.Resize(0);
            if(this.file.Position==this.file.Length){return false;}
            Span<byte> prefix=stackalloc byte[4];this.file.ReadExactly(prefix);
            var length=BinaryPrimitives.ReadInt32LittleEndian(prefix);
            if(length<0||length>this.file.Length-this.file.Position){throw new InvalidDataException("Invalid sort record length.");}
            this.memory.Resize(checked(96+4L*length));
            using var buffer=this.scope.Rent(length);
            this.file.ReadExactly(buffer.Memory.Span);
            var decoded=this.codec.Decode(buffer.Memory.Span);
            this.Current=new(decoded.Item1,decoded.Item2,decoded.Item3);
            return true;
        }
        public void Dispose(){this.Current=default;this.memory.Dispose();}
    }
}
