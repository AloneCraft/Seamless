using Index=global::Seamless.Collections.PersistedHashIndex;
using Matcher=global::Seamless.Collections.IPersistedKeyMatcher;

namespace Test.Seamless.Persisted;

public class PersistedHashIndex{
    [Fact,Trait("Category","Unit")]
    public void Constructor(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-index-format-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try{
            foreach(var mode in new[]{StorageBufferingMode.Buffered,StorageBufferingMode.Unbuffered}){
                if(mode==StorageBufferingMode.Unbuffered&&!OperatingSystem.IsWindows()){
                    continue;
                }
                var tuning=new StorageTuning(FileAccess.ReadWrite,StorageAccessPattern.Random,mode);
                Assert.Throws<ArgumentOutOfRangeException>(()=>{using var rejected=new Index(Path.Combine(directory,"too-large.hindex"),tuning,long.MaxValue);});
                // 未反映の索引を再構築する既存の印 -1 は正常な保存状態。
                var stalePath=Path.Combine(directory,Guid.NewGuid().ToString("N")+".hindex");
                File.WriteAllBytes(stalePath,Header(-16,0,-1,1));
                using(var stale=new Index(stalePath,tuning,0)){
                    Assert.Equal(-1,stale.ListCountAtFlush);
                }
                // 旧版、未知版、壊れた容量/件数、途中で切れたヘッダーを変更せず拒否する。
                var invalid=new[]{Header(16,0,0,0),Header(-16,0,0,2),Header(long.MinValue,0,0,1),
                    Header(-24,0,0,1),Header(-16,16,0,1),Header(-16,-1,0,1),Header(-16,0,-2,1),
                    Header(-(1L<<60),0,0,1),new byte[7]};
                foreach(var bytes in invalid){
                    var path=Path.Combine(directory,Guid.NewGuid().ToString("N")+".hindex");
                    File.WriteAllBytes(path,bytes);
                    Assert.Throws<InvalidDataException>(()=>{using var rejected=new Index(path,tuning,0);});
                    using var exclusive=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None);
                    var actual=new byte[bytes.Length];exclusive.ReadExactly(actual);
                    Assert.Equal(bytes,actual);
                }
            }
        }finally{
            Directory.Delete(directory,true);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Upsert(){
        var directory=Path.Combine(Path.GetTempPath(),"seamless-index-distribution-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"rows.hindex");
        var matcher=new KeyMatcher();
        const int count=1024;
        try{
            using(var index=new Index(path,StorageTuning.Default,0)){
                // 下位32bitが同じでも、異なる上位bitを持つキーを一つの長い探索列にしない。
                for(var i=0;i<count;i++){
                    index.Upsert(Hash(i),i,matcher,BitConverter.GetBytes(i));
                }
                Assert.Equal(count,index.EntryCount);
                index.SetListCount(count);
                index.FlushToDisk();
            }
            using(var file=File.OpenRead(path)){
                using var reader=new BinaryReader(file);
                var encodedCapacity=reader.ReadInt64();
                var capacity=Math.Abs(encodedCapacity);
                var longest=0;var current=0;
                for(var slot=0L;slot<2*capacity;slot++){
                    file.Position=32+(slot%capacity)*16+8;
                    if(reader.ReadInt64()==-1){current=0;}else{current++;longest=Math.Max(longest,current);}
                }
                Assert.True(longest<64,$"A half-full index contains an occupied run of {longest} slots.");
                Assert.True(encodedCapacity<0,"Old readers must reject the new index format.");
            }
            using(var reopened=new Index(path,StorageTuning.Default,0)){
                Assert.Equal(count,reopened.EntryCount);
                Assert.Equal(count,reopened.ListCountAtFlush);
                for(var i=0;i<count;i++){
                    Assert.True(reopened.TryGetOffset(Hash(i),matcher,BitConverter.GetBytes(i),out var offset));
                    Assert.Equal(i,offset);
                }
                reopened.Upsert(Hash(5),100005,matcher,BitConverter.GetBytes(5));
                Assert.Equal(count,reopened.EntryCount);
                Assert.True(reopened.TryGetOffset(Hash(5),matcher,BitConverter.GetBytes(5),out var updated));
                Assert.Equal(100005,updated);
                reopened.Clear();
                Assert.Equal(0,reopened.EntryCount);
                Assert.False(reopened.TryGetOffset(Hash(5),matcher,BitConverter.GetBytes(5),out _));
                reopened.Upsert(Hash(7),7,matcher,BitConverter.GetBytes(7));
                reopened.SetListCount(1);
                if(OperatingSystem.IsWindows()){
                    reopened.SetTuning(new StorageTuning(FileAccess.ReadWrite,StorageAccessPattern.Random,StorageBufferingMode.Unbuffered));
                    Assert.True(reopened.TryGetOffset(Hash(7),matcher,BitConverter.GetBytes(7),out var direct));
                    Assert.Equal(7,direct);
                }
            }
            using var final=new Index(path,StorageTuning.Default,0);
            Assert.Equal(1,final.EntryCount);
            Assert.Equal(1,final.ListCountAtFlush);
            Assert.True(final.TryGetOffset(Hash(7),matcher,BitConverter.GetBytes(7),out _));
        }finally{
            Directory.Delete(directory,true);
        }
    }

    private static ulong Hash(int value)=>((ulong)unchecked((uint)value*2654435761U)<<32)|17UL;
    private static byte[]Header(long capacity,long count,long flushed,long version){
        var bytes=new byte[32+16*16];
        BitConverter.GetBytes(capacity).CopyTo(bytes,0);BitConverter.GetBytes(count).CopyTo(bytes,8);
        BitConverter.GetBytes(flushed).CopyTo(bytes,16);BitConverter.GetBytes(version).CopyTo(bytes,24);
        for(var slot=0;slot<16;slot++){BitConverter.GetBytes(-1L).CopyTo(bytes,32+slot*16+8);}
        return bytes;
    }
    private sealed class KeyMatcher:Matcher{
        public bool KeyEqualsAt(long offset,ReadOnlySpan<byte>key)=>offset%100000==BitConverter.ToInt32(key);
    }
}
