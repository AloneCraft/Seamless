using Seamless.Execution;
namespace HistoryStoreModes;

internal static partial class Program{
    private static readonly List<string> checks=[];
    private static void Throws<T>(Action action,string name) where T:Exception{
        try{action();}catch(T){checks.Add(name);return;}throw new InvalidDataException("Missing rejection: "+name);
    }
    private static void Verify(string root){
        VerifyBackend<long>(root);VerifyBackend<WideKey>(root);VerifyBackend<long>(root,true);VerifyBackend<WideKey>(root,true);VerifyLargeReclaim<long>(root);VerifyLargeReclaim<WideKey>(root);
        var budget=new ResourceBudget(2<<20,2<<20);var row=new byte[512];
        using(var memory=new PagedArena(budget,null,512,8192,8192,1)){
            Fill(row,1,1);var first=memory.Append(1,1,-1,false,row);
            Throws<InvalidDataException>(()=>{memory.Read(first,2,out _,out _,out _);},"node refuses another row identity");
            Throws<InvalidDataException>(()=>{memory.Read(first+8,1,out _,out _,out _);},"node refuses an interior offset");
            Throws<ResourceLimitException>(()=>{for(var version=2;version<100;version++){Fill(row,1,version);memory.Append(1,version,first,false,row);}},"bounded arena refuses overflow");
            var stored=memory.Read(first,1,out var version,out _,out _);Assert(version==1&&stored[16]==unchecked((byte)(31+17+16)),"Overflow damaged an existing node");
        }
        Assert(budget.MemoryBytes==0&&budget.TemporaryStorageBytes==0,"Arena overflow test leaked budget");checks.Add("overflow preserves existing data and releases budget");
        var file=Path.Combine(root,"corruption","arena.bin");
        using(var image=new PagedArena(budget,file,512,8192,1<<20,1)){Fill(row,1,1);image.Append(1,1,-1,false,row);image.Flush(true);}
        using(var corrupt=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){corrupt.Position=PagedArena.HeaderBytes+20;var old=corrupt.ReadByte();corrupt.Position--;corrupt.WriteByte((byte)(old^1));corrupt.Flush(true);}
        using(var image=new PagedArena(budget,file,512,8192,1<<20,1)){image.Load(PagedArena.HeaderBytes+512);Throws<InvalidDataException>(()=>{image.Read(0,1,out _,out _,out _);},"checksum rejects changed payload");}
        using(var truncate=new FileStream(file,FileMode.Open,FileAccess.Write,FileShare.None)){truncate.SetLength(10);}
        using(var image=new PagedArena(budget,file,512,8192,1<<20,1)){Throws<InvalidDataException>(()=>image.Load(PagedArena.HeaderBytes+512),"snapshot refuses truncated page");}
        Assert(budget.MemoryBytes==0&&budget.TemporaryStorageBytes==0,"Corruption test leaked budget");checks.Add("corruption cleanup releases budget");
        var sealPath=Path.Combine(root,"sealed","arena.bin");
        using(var image=new PagedArena(budget,sealPath,512,8192,1<<20,1)){
            Fill(row,1,1);var first=image.Append(1,1,-1,false,row);image.Flush(true);
            var before=FirstPage(sealPath);Fill(row,1,2);var second=image.Append(1,2,first,false,row);image.Flush(true);
            Assert(second==PagedArena.PageBytes&&FirstPage(sealPath).AsSpan().SequenceEqual(before),"Append overwrote a sealed checkpoint page");
        }
        checks.Add("checkpoint pages stay immutable across later append");
        var componentPath=Path.Combine(root,"cleanup");
        using(var store=StoreForVerification(componentPath)){
            var component=store.Register(new ArenaComponent(store.Budget,Path.Combine(componentPath,"arena"),8,512,8192,1<<20));Fill(row,1,1);component.Current.Append(1,1,-1,false,row);store.Save();
            var oldPath=component.Current.Path!;var candidate=component.CreateCandidate();Fill(row,1,2);candidate.Append(1,2,-1,false,row);component.Switch(candidate);
            using(var hold=new FileStream(oldPath,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){store.Save();Assert(File.Exists(oldPath)&&component.InvalidatesOlderSaves,"Cleanup must be deferred while the old file is held");}
            component.SaveCommitted();Assert(!File.Exists(oldPath)&&!component.InvalidatesOlderSaves,"Deferred cleanup did not retry");
        }
        checks.Add("committed checkpoint survives deferred file cleanup");
    }
    private static Store StoreForVerification(string directory)=>Store.Open(directory,new(){MemoryBytes=2<<20,DiskBytes=2<<20});
    private static void VerifyLargeReclaim<TKey>(string root) where TKey:unmanaged{
        foreach(var dedicated in new[]{true,false}){
            var settings=new Settings(false,64,8<<20,32,1024,8);
            using var backend=Open<TKey>(Path.Combine(root,"large-"+typeof(TKey).Name+"-"+dedicated),settings,dedicated);
            var row=new byte[32];for(var version=1;version<=8;version++){for(var id=0;id<settings.Keys;id++){Fill(row,id,version);backend.Append(id,version,Deleted(id,version,8),row);}}
            ValidateAll(backend,settings);Console.WriteLine("Reclaim regression: "+backend.GetType().Name+"/"+typeof(TKey).Name);backend.Reclaim();ValidateAll(backend,settings,true);
            checks.Add("1024-row reclaim "+typeof(TKey).Name+"/"+dedicated);
        }
    }
    private static byte[] FirstPage(string path){var bytes=new byte[PagedArena.PageBytes];using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);input.ReadExactly(bytes);return bytes;}
    private static void VerifyBackend<TKey>(string root,bool buffered=false) where TKey:unmanaged{
        var storages=new[]{false,true};if(buffered){storages=[true];}
        foreach(var persistent in storages){
            foreach(var dedicated in new[]{false,true}){
                var tag=typeof(TKey).Name+"-"+persistent+"-"+dedicated+"-buffered"+buffered;
                var directory=Path.Combine(root,tag);var settings=new Settings(persistent,64,65536,512,8,8,buffered);var row=new byte[512];ArenaAddress? oldAddress=null;
                using(var backend=Open<TKey>(directory,settings,dedicated)){
                    Assert(backend.Read(999,8) is null,"Absent key was found");
                    for(var version=1;version<=8;version++){for(var id=0;id<8;id++){Fill(row,id,version);backend.Append(id,version,Deleted(id,version,8),row);}}
                    for(var id=0;id<8;id++){Assert(backend.Read(id,0) is null,"Row existed before its first version");for(var version=1;version<=8;version++){Validate(backend.Read(id,version),id,version,settings);}}
                    if(backend is DedicatedHistory<TKey> arena){oldAddress=arena.HeadAddress(1);}
                    backend.Save();if(persistent){backend.Evict();ValidateAll(backend,settings);}
                    backend.Reclaim();ValidateAll(backend,settings,true);
                    if(backend is DedicatedHistory<TKey> current){Throws<InvalidOperationException>(()=>current.ValidateAddress(oldAddress!.Value),"old reclaim generation "+tag);oldAddress=current.HeadAddress(1);}
                    backend.Dispose();Assert(backend.MemoryBytes==0&&backend.StorageBytes==0,"Backend leaked budget");
                }
                if(persistent){
                    using(var reopened=Open<TKey>(directory,settings,dedicated)){ValidateAll(reopened,settings,true);if(reopened is DedicatedHistory<TKey> arena){Throws<InvalidOperationException>(()=>arena.ValidateAddress(oldAddress!.Value),"old open identity "+tag);}}
                    if(dedicated){Throws<InvalidDataException>(()=>{using var wrong=Open<TKey>(directory,settings with{RowBytes=32},true);},"snapshot refuses another row schema "+tag);Assert(Directory.EnumerateFiles(Path.Combine(directory,"arena")).Count()==1,"Reopen recreated an obsolete arena file");}
                }
                checks.Add("versions/deletion/reinsertion/boundaries/reclaim/reopen "+tag);
            }
        }
    }
}
