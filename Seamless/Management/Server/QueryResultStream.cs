using System.Buffers.Binary;
using System.Collections;
using System.Text.Json;
using Seamless.Execution;

namespace Seamless.Management.Server;

/// <summary>確定済みDB結果の独立したコピー。一度だけ列挙でき、列挙子またはusingで所有資源を解放する。</summary>
public sealed class QueryResultStream<T>:IEnumerable<T>,IDisposable{
    private readonly object gate=new();
    private readonly ExecutionScope scope;
    private readonly ResultStorage storage;
    private readonly Action<IDisposable> release;
    private bool claimed;
    private bool disposed;
    private int read;
    public int Count{get;private set;}
    internal ResourceBudget Budget=>this.scope.Budget;
    internal bool Spilled=>this.storage.Spilled;

    internal QueryResultStream(ExecutionOptions options,Action<IDisposable> release){
        this.scope=new(options);this.release=release;
        try{this.storage=new(this.scope);}catch{this.scope.Dispose();throw;}
    }
    internal void Add(JsonElement value){
        lock(this.gate){
            this.Check();
            var start=this.storage.Position;
            Span<byte> prefix=stackalloc byte[4];prefix.Clear();this.storage.Write(prefix);
            using(var writer=new Utf8JsonWriter(this.storage)){value.WriteTo(writer);}
            var end=this.storage.Position;var length=checked((int)(end-start-4));
            BinaryPrimitives.WriteInt32LittleEndian(prefix,length);
            this.storage.Position=start;this.storage.Write(prefix);this.storage.Position=end;
            // 呼出元へ返す型と一行分の読取枠を、書込取引の確定前に検査する。
            using(var memory=this.ReserveRead(length)){_=RuntimeValueJson.Deserialize(value,typeof(T));}
            this.Count++;
        }
    }
    internal void Seal(){lock(this.gate){this.Check();this.storage.Flush();this.storage.Position=0;}}
    public IEnumerator<T> GetEnumerator(){
        lock(this.gate){
            try{this.Check();}catch{this.Dispose();throw;}
            if(this.claimed){throw new InvalidOperationException("結果ストリームは一度だけ列挙できます。");}
            this.claimed=true;return new Enumerator(this);
        }
    }
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    private bool Next(out T value){
        lock(this.gate){
            try{
                this.Check();value=default!;
                if(this.read==this.Count){this.Dispose();return false;}
                Span<byte> prefix=stackalloc byte[4];this.storage.ReadExactly(prefix);
                var length=BinaryPrimitives.ReadInt32LittleEndian(prefix);
                using var memory=this.ReserveRead(length);
                // 上で符号化領域と復元領域をまとめて予約したため、二重に会計しない。
                var bytes=new byte[length];this.storage.ReadExactly(bytes);
                using var document=JsonDocument.Parse(bytes);
                value=(T)RuntimeValueJson.Deserialize(document.RootElement,typeof(T))!;
                this.read++;return true;
            }catch{this.Dispose();throw;}
        }
    }
    private ResourceReservation ReserveRead(int length){
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        var bytes=checked(5L*length+128);
        try{return this.scope.Budget.ReserveMemory(bytes);}
        catch(ResourceLimitException error)when(error.Kind==ResourceLimitKind.Memory&&!this.storage.Spilled){
            this.storage.Spill();return this.scope.Budget.ReserveMemory(bytes);
        }
    }
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);this.scope.ThrowIfUnavailable();}
    public void Dispose(){
        lock(this.gate){
            if(this.disposed){return;}
            this.disposed=true;
            try{try{this.storage.Dispose();}finally{this.scope.Dispose();}}
            finally{this.release(this);}
        }
    }
    private sealed class Enumerator(QueryResultStream<T> owner):IEnumerator<T>{
        private bool completed;
        private bool hasCurrent;
        private T current=default!;
        public T Current{get{if(!this.hasCurrent){throw new InvalidOperationException();}return this.current;}}
        object? IEnumerator.Current=>this.Current;
        public bool MoveNext(){
            if(this.completed){return false;}
            this.hasCurrent=false;this.current=default!;
            try{this.hasCurrent=owner.Next(out this.current);if(!this.hasCurrent){this.completed=true;}return this.hasCurrent;}
            catch{this.completed=true;throw;}
        }
        public void Reset()=>throw new NotSupportedException();
        public void Dispose(){this.completed=true;this.hasCurrent=false;this.current=default!;owner.Dispose();}
    }

    private sealed class ResultStorage:Stream{
        private readonly ExecutionScope scope;
        private readonly ResourceReservation memory;
        private Stream stream=new MemoryStream();
        internal bool Spilled{get;private set;}
        internal ResultStorage(ExecutionScope scope){this.scope=scope;this.memory=scope.Budget.ReserveMemory(0);}
        public override bool CanRead=>true;
        public override bool CanSeek=>true;
        public override bool CanWrite=>true;
        public override long Length=>this.stream.Length;
        public override long Position{get=>this.stream.Position;set=>this.stream.Position=value;}
        public override void Flush()=>this.stream.Flush();
        public override long Seek(long offset,SeekOrigin origin)=>this.stream.Seek(offset,origin);
        public override int Read(byte[] buffer,int offset,int count)=>this.stream.Read(buffer,offset,count);
        public override int Read(Span<byte> buffer)=>this.stream.Read(buffer);
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>this.Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> buffer){
            this.scope.ThrowIfUnavailable();
            if(this.stream is MemoryStream retained){
                var required=checked(retained.Position+buffer.Length);
                if(required>retained.Capacity){
                    var capacity=Math.Max(256L,Math.Max(required,2L*retained.Capacity));
                    try{
                        if(capacity>int.MaxValue){this.Spill();}
                        else{this.memory.Resize(checked(capacity+64));retained.Capacity=(int)capacity;}
                    }catch(ResourceLimitException error)when(error.Kind==ResourceLimitKind.Memory){this.Spill();}
                }
            }
            this.stream.Write(buffer);
        }
        internal void Spill(){
            if(this.Spilled){return;}
            var position=this.stream.Position;var file=this.scope.CreateTemporaryFile();
            try{this.stream.Position=0;this.stream.CopyTo(file);file.Position=position;}
            catch{file.Dispose();throw;}
            this.stream.Dispose();this.stream=file;this.Spilled=true;this.memory.Resize(0);
        }
        protected override void Dispose(bool disposing){
            if(disposing){var previous=this.stream;this.stream=Stream.Null;try{previous.Dispose();}finally{this.memory.Dispose();}}
            base.Dispose(disposing);
        }
    }
}
