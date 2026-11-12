using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Seamless.Execution;
namespace HistoryStoreModes;

// 実験用。返すspanは次のpage取得まで、外へ返す行はコピー。単一ownerの同期操作だけを扱う。
internal sealed class PagedArena:IDisposable{
    internal const int PageBytes=4096,HeaderBytes=40;
    private const ulong Magic=0x31564152414C5353;
    private readonly ResourceBudget budget;
    private readonly ResourceReservation memory,storage;
    private readonly Dictionary<long,Page> pages;
    private readonly int capacity;
    private readonly long logicalLimit;
    private SafeFileHandle? file;
    private long tick;
    private bool disposed,sealedForAppend;
    internal PagedArena(ResourceBudget budget,string? path,int rowBytes,int cacheBytes,long logicalLimit,long generation){
        if(rowBytes is<16 or>512||cacheBytes<PageBytes||cacheBytes%PageBytes!=0||logicalLimit<PageBytes){throw new ArgumentOutOfRangeException(nameof(rowBytes));}
        this.budget=budget;this.Path=path;this.RowBytes=rowBytes;this.Generation=generation;this.logicalLimit=logicalLimit;
        this.NodeBytes=checked((rowBytes+HeaderBytes+7)/8*8);this.capacity=cacheBytes/PageBytes;
        this.memory=budget.ReserveMemory(checked(256L+this.capacity*96L));this.storage=budget.ReserveTemporaryStorage(0);
        try{
            this.pages=new(this.capacity);
            if(path is not null){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);}
        }catch{this.memory.Dispose();this.storage.Dispose();throw;}
    }
    internal string? Path{get;}
    internal int RowBytes{get;}
    internal int NodeBytes{get;}
    internal long Generation{get;}
    internal long Tail{get;private set;}
    internal long BufferBytes=>checked((long)this.pages.Count*PageBytes);
    internal long ReadCalls{get;private set;}
    internal long ReadBytes{get;private set;}
    internal long WriteCalls{get;private set;}
    internal long WriteBytes{get;private set;}
    internal long Append(long id,long version,long previous,bool deleted,ReadOnlySpan<byte> row){
        this.Check();
        if(row.Length!=this.RowBytes||BinaryPrimitives.ReadInt64LittleEndian(row)!=id||BinaryPrimitives.ReadInt64LittleEndian(row[8..])!=version||version<1){throw new ArgumentException("Payload identity/version differs.");}
        var offset=this.Tail;
        // 確定済みpageを追記のread-modify-writeで再書込みしない。
        if(this.sealedForAppend||offset%PageBytes+this.NodeBytes>PageBytes){offset=AlignPage(offset);}
        var tail=checked(offset+this.NodeBytes);
        if(tail>this.logicalLimit){var kind=ResourceLimitKind.Memory;if(this.Path is not null){kind=ResourceLimitKind.TemporaryStorage;}throw new ResourceLimitException(kind,tail,this.logicalLimit);}
        if(previous>=offset||previous< -1){throw new InvalidDataException("Previous offset is not older than its node.");}
        if(this.Path is not null){this.storage.Resize(Math.Max(this.storage.Bytes,AlignPage(tail)));}
        var page=this.GetPage(offset/PageBytes,true);var node=page.Bytes.AsSpan((int)(offset%PageBytes),this.NodeBytes);node.Clear();
        BinaryPrimitives.WriteUInt64LittleEndian(node,Magic);BinaryPrimitives.WriteInt64LittleEndian(node[8..],version);BinaryPrimitives.WriteInt64LittleEndian(node[16..],previous);
        BinaryPrimitives.WriteInt32LittleEndian(node[24..],row.Length);if(deleted){BinaryPrimitives.WriteInt32LittleEndian(node[28..],1);}row.CopyTo(node[HeaderBytes..]);
        BinaryPrimitives.WriteUInt64LittleEndian(node[32..],Checksum(node));page.Dirty=true;this.Tail=tail;this.sealedForAppend=false;return offset;
    }
    internal ReadOnlySpan<byte> Read(long offset,long id,out long version,out long previous,out bool deleted){
        this.Check();
        if(offset<0||offset>this.Tail-this.NodeBytes||offset%PageBytes%this.NodeBytes!=0||offset%PageBytes+this.NodeBytes>PageBytes){throw new InvalidDataException("Invalid node boundary.");}
        var node=this.GetPage(offset/PageBytes,false).Bytes.AsSpan((int)(offset%PageBytes),this.NodeBytes);
        version=BinaryPrimitives.ReadInt64LittleEndian(node[8..]);previous=BinaryPrimitives.ReadInt64LittleEndian(node[16..]);var flags=BinaryPrimitives.ReadInt32LittleEndian(node[28..]);deleted=flags==1;
        if(BinaryPrimitives.ReadUInt64LittleEndian(node)!=Magic||version<1||previous< -1||previous>=offset||flags is<0 or>1||BinaryPrimitives.ReadInt32LittleEndian(node[24..])!=this.RowBytes||BinaryPrimitives.ReadUInt64LittleEndian(node[32..])!=Checksum(node)){throw new InvalidDataException("Invalid node header/checksum.");}
        var row=node.Slice(HeaderBytes,this.RowBytes);
        if(BinaryPrimitives.ReadInt64LittleEndian(row)!=id||BinaryPrimitives.ReadInt64LittleEndian(row[8..])!=version){throw new InvalidDataException("Node belongs to a different row/version.");}
        return row;
    }
    internal void Load(long tail){
        this.Check();this.EnsureFile();if(this.file is null||tail<0||tail>this.logicalLimit||RandomAccess.GetLength(this.file)<AlignPage(tail)){throw new InvalidDataException("Arena snapshot is truncated or exceeds its limit.");}
        this.storage.Resize(RandomAccess.GetLength(this.file));this.Tail=tail;this.sealedForAppend=true;
    }
    internal void Flush(bool durable){
        this.Check();this.EnsureFile();foreach(var page in this.pages.Values){this.FlushPage(page);}if(durable&&this.file is not null){RandomAccess.FlushToDisk(this.file);}this.sealedForAppend=true;
    }
    internal void Evict(){
        this.Flush(false);if(this.file is null){throw new InvalidOperationException("Memory arena cannot evict its only copy.");}
        this.pages.Clear();this.memory.Resize(checked(256L+this.capacity*96L));
    }
    private Page GetPage(long number,bool creating){
        this.EnsureFile();
        if(this.pages.TryGetValue(number,out var hit)){hit.Tick=++this.tick;return hit;}
        Page page;
        if(this.pages.Count==this.capacity){
            if(this.file is null){throw new ResourceLimitException(ResourceLimitKind.Memory,(this.pages.Count+1L)*PageBytes,this.capacity*(long)PageBytes);}
            page=this.pages.Values.MinBy(static page=>page.Tick)!;this.FlushPage(page);this.pages.Remove(page.Number);Array.Clear(page.Bytes);
        }else{
            this.memory.Resize(checked(this.memory.Bytes+PageBytes+128));page=new();
        }
        page.Number=number;page.Tick=++this.tick;page.Dirty=false;
        if(this.file is not null){
            var offset=checked(number*PageBytes);var available=RandomAccess.GetLength(this.file)-offset;
            if(available>=PageBytes){
                using var cpu=CpuScheduler.SuspendForIo();var read=0;while(read<PageBytes){var count=RandomAccess.Read(this.file,page.Bytes.AsSpan(read),offset+read);this.ReadCalls++;this.ReadBytes+=count;if(count==0){throw new EndOfStreamException("Short arena page read.");}read+=count;}
            }else if(!creating){throw new EndOfStreamException("Missing arena page.");}
        }else if(!creating){throw new InvalidDataException("Memory arena page is missing.");}
        this.pages.Add(number,page);return page;
    }
    private void FlushPage(Page page){
        if(!page.Dirty||this.file is null){return;}using var cpu=CpuScheduler.SuspendForIo();RandomAccess.Write(this.file,page.Bytes,page.Number*PageBytes);this.WriteCalls++;this.WriteBytes+=PageBytes;page.Dirty=false;
    }
    private static long AlignPage(long value)=>checked((value+PageBytes-1)/PageBytes*PageBytes);
    private static ulong Checksum(ReadOnlySpan<byte> node){ulong hash=14695981039346656037;foreach(var value in node[..32]){hash=unchecked((hash^value)*1099511628211);}foreach(var value in node[HeaderBytes..]){hash=unchecked((hash^value)*1099511628211);}return hash;}
    private void Check()=>ObjectDisposedException.ThrowIf(this.disposed,this);
    private void EnsureFile(){if(this.Path is{} path&&this.file is null){this.file=File.OpenHandle(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,FileOptions.RandomAccess);}}
    public void Dispose(){if(this.disposed){return;}this.disposed=true;this.file?.Dispose();this.pages.Clear();this.memory.Dispose();this.storage.Dispose();}
    private sealed class Page{internal long Number,Tick;internal bool Dirty;internal readonly byte[] Bytes=new byte[PageBytes];}
}

internal readonly record struct ArenaAddress(Guid Owner,long Generation,long Offset);
internal sealed class ArenaComponent:IStoreComponent{
    private readonly Guid owner=Guid.NewGuid();
    private readonly ResourceBudget budget;
    private readonly string? directory;
    private readonly int keyBytes,rowBytes,cacheBytes;
    private readonly long logicalLimit;
    private PagedArena current;
    private PagedArena? retired;
    internal ArenaComponent(ResourceBudget budget,string? directory,int keyBytes,int rowBytes,int cacheBytes,long logicalLimit){
        this.budget=budget;this.directory=directory;this.keyBytes=keyBytes;this.rowBytes=rowBytes;this.cacheBytes=cacheBytes;this.logicalLimit=logicalLimit;this.current=this.NewImage(1);
    }
    public string Name=>"arena";
    internal PagedArena Current=>this.current;
    public bool InvalidatesOlderSaves=>this.retired is not null;
    internal ArenaAddress Address(long offset)=>new(this.owner,this.current.Generation,offset);
    internal void Validate(ArenaAddress address){if(address.Owner!=this.owner||address.Generation!=this.current.Generation){throw new InvalidOperationException("Address belongs to another open or reclaim generation.");}}
    internal PagedArena CreateCandidate()=>this.NewImage(checked(this.current.Generation+1));
    internal void Switch(PagedArena candidate){if(this.retired is not null){throw new InvalidOperationException("Finish the previous checkpoint first.");}this.retired=this.current;this.current=candidate;}
    private PagedArena NewImage(long generation){string? path=null;if(this.directory is not null){path=System.IO.Path.Combine(this.directory,"arena-"+generation+".bin");}return new(this.budget,path,this.rowBytes,this.cacheBytes,this.logicalLimit,generation);}
    public string Save(){this.current.Flush(false);return JsonSerializer.Serialize(new State("dedicated-arena-prototype-v1",this.keyBytes,this.rowBytes,this.current.Generation,this.current.Tail));}
    public void Flush()=>this.current.Flush(true);
    private State Parse(string value){var state=JsonSerializer.Deserialize<State>(value)??throw new InvalidDataException("Missing arena state.");if(state.Format!="dedicated-arena-prototype-v1"||state.KeyBytes!=this.keyBytes||state.RowBytes!=this.rowBytes||state.Generation<1){throw new InvalidDataException("Arena schema/generation differs.");}return state;}
    public void ValidateLoad(string value){var state=this.Parse(value);if(this.directory is null||state.Tail<0||state.Tail>this.logicalLimit){throw new InvalidDataException("Invalid persistent arena length.");}var file=System.IO.Path.Combine(this.directory,"arena-"+state.Generation+".bin");if(!File.Exists(file)||new FileInfo(file).Length<(state.Tail+PagedArena.PageBytes-1)/PagedArena.PageBytes*PagedArena.PageBytes){throw new InvalidDataException("Arena file is truncated.");}}
    public void Load(string value){this.ValidateLoad(value);var state=this.Parse(value);this.current.Dispose();this.current=this.NewImage(state.Generation);this.current.Load(state.Tail);}
    public void SaveCommitted(){
        if(this.retired is not{} old){return;}old.Dispose();
        if(old.Path is{} path){try{File.Delete(path);}catch(IOException){return;}catch(UnauthorizedAccessException){return;}}
        this.retired=null;
    }
    public void Dispose(){try{this.current.Dispose();}finally{this.retired?.Dispose();}}
    private sealed record State(string Format,int KeyBytes,int RowBytes,long Generation,long Tail);
}
