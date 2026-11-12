using Microsoft.Win32.SafeHandles;
using NativeStream=global::Seamless.Collections.NativeUnbufferedStream;
namespace Test.Seamless.Persisted;

public class NativeUnbufferedStream {
    [Fact,Trait("Category","Unit")]
    public void Constructor(){
        if(!OperatingSystem.IsWindows()){
            return;
        }
        WithFile(Enumerable.Range(0,17).Select(i=>(byte)i).ToArray(),path=>{
            // Append の開始位置取得が失敗しても、取得済みハンドルと整列バッファを解放する。
            var operations=new FaultOperations{ThrowGetLength=true};
            var error=Assert.Throws<IOException>(()=>{
                using var rejected=new NativeStream(path,FileMode.Append,FileAccess.Write,FileShare.Read,DirectTuning(),operations);
            });
            Assert.Same(operations.Failure,error);
            Assert.Equal(1,operations.CloseCalls);
            Assert.Equal(1,operations.FreeCalls);
            Assert.NotEqual((nint)0,operations.FreedBuffer);
            Assert.NotNull(operations.ClosedHandle);
            Assert.True(operations.ClosedHandle!.IsClosed);
            using(var exclusive=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
                Assert.Equal(17,exclusive.Length);
            }
            Assert.Equal(Enumerable.Range(0,17).Select(i=>(byte)i).ToArray(),File.ReadAllBytes(path));
        });
    }

    [Fact,Trait("Category","Unit")]
    public void ReadWrite() {
        var path=Path.Combine(Path.GetTempPath(),"seamless-direct-"+Guid.NewGuid().ToString("N")+".dat");
        var tuning=new StorageTuning(FileAccess.ReadWrite,StorageAccessPattern.Random,StorageBufferingMode.Unbuffered);
        if(!OperatingSystem.IsWindows()) {
            Assert.Throws<PlatformNotSupportedException>(()=>new NativeStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read,tuning));
            return;
        }
        try {
            // 非整列のファイル末尾、セクターをまたぐ部分更新、先頭と末尾の保持。
            var expected=Enumerable.Range(0,8199).Select(i=>(byte)(i%251)).ToArray();
            using(var stream=new NativeStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read,tuning)) {
                stream.Write(expected);
                Assert.Equal(expected.Length,stream.Length);
                var replacement=Enumerable.Repeat((byte)0xE7,25).ToArray();
                stream.Position=4090;
                stream.Write(replacement);
                replacement.CopyTo(expected,4090);
                Assert.Equal(expected.Length,stream.Length);
                stream.Position=0;
                var actual=new byte[expected.Length];
                stream.ReadExactly(actual);
                Assert.Equal(expected,actual);
                Assert.Equal(0,stream.Read(new byte[1]));
                stream.Flush(flushToDisk:true);
                Assert.Equal(expected.Length,new FileInfo(path).Length);
            }
            Assert.Equal(expected,File.ReadAllBytes(path));
            // 読み取り専用で開き直しても、padding をレコードとして見せない。
            using(var stream=new NativeStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite,tuning)) {
                stream.Position=8190;
                var tail=new byte[100];
                Assert.Equal(9,stream.Read(tail));
                Assert.Equal(expected.AsSpan(8190).ToArray(),tail.AsSpan(0,9).ToArray());
                Assert.Throws<NotSupportedException>(()=>stream.Write(new byte[]{1}));
            }
            // 切り詰め後の伸長はゼロになり、以前の末尾を復活させない。
            using(var stream=new NativeStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read,tuning)) {
                stream.SetLength(13);
                stream.SetLength(5001);
                stream.Position=0;
                var actual=new byte[5001];
                stream.ReadExactly(actual);
                Assert.Equal(expected.AsSpan(0,13).ToArray(),actual.AsSpan(0,13).ToArray());
                Assert.All(actual.Skip(13),value=>Assert.Equal((byte)0,value));
                stream.Position=6000;
                stream.Write(new byte[]{0x42});
                Assert.Equal(6001,stream.Length);
                stream.Position=5001;
                var gap=new byte[1000];
                stream.ReadExactly(gap);
                Assert.All(gap.Take(999),value=>Assert.Equal((byte)0,value));
                Assert.Equal((byte)0x42,gap[999]);
            }
            Assert.Equal(6001,new FileInfo(path).Length);
        } finally {
            File.Delete(path);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Read(){
        if(!OperatingSystem.IsWindows()){
            return;
        }
        var expected=Enumerable.Range(0,33).Select(i=>(byte)i).ToArray();
        WithFile(expected,path=>{
            var operations=new FaultOperations{ReadLimit=11};
            using(var stream=new NativeStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read,DirectTuning(),operations)){
                stream.Position=3;
                var actual=Enumerable.Repeat((byte)0xCC,20).ToArray();
                // 整列読み取りが11バイトで終了した場合、要求開始位置3からの8バイトだけを返す。
                Assert.Equal(8,stream.Read(actual));
                Assert.Equal(11,stream.Position);
                Assert.Equal(expected.Length,stream.Length);
                Assert.Equal(expected.AsSpan(3,8).ToArray(),actual.AsSpan(0,8).ToArray());
                Assert.All(actual.Skip(8),value=>Assert.Equal((byte)0xCC,value));
                Assert.Equal(0,operations.WriteCalls);
            }
            Assert.Equal(expected,File.ReadAllBytes(path));
        });
    }

    [Fact,Trait("Category","Unit")]
    public void Write(){
        if(!OperatingSystem.IsWindows()){
            return;
        }
        {
            // RMW に必要な既存バイトを読めない場合、書込み・長さ・Position を変えない。
            var expected=Enumerable.Range(0,33).Select(i=>(byte)i).ToArray();
            WithFile(expected,path=>{
                var operations=new FaultOperations{ReadLimit=7};
                using(var stream=new NativeStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read,DirectTuning(),operations)){
                    stream.Position=5;
                    Assert.Throws<IOException>(()=>stream.Write(new byte[]{0xE7,0xE8,0xE9}));
                    Assert.Equal(5,stream.Position);
                    Assert.Equal(expected.Length,stream.Length);
                    Assert.Equal(0,operations.WriteCalls);
                    Assert.Equal(0,operations.SetLengthCalls);
                }
                Assert.Equal(expected,File.ReadAllBytes(path));
            });
        }
        {
            WithFile(new byte[]{1},path=>{
                // 実ファイルの整列量を取得し、1回の転送に2整列単位を使う。
                var probeOperations=new FaultOperations();
                using(var probe=new NativeStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,DirectTuning(),probeOperations)){
                    Assert.Equal(1,probe.Read(new byte[1]));
                }
                var alignment=probeOperations.LastReadSize;
                foreach(var originalLength in new[]{checked(alignment*6),checked(alignment*2+13)}){
                    var expected=Enumerable.Range(0,originalLength).Select(i=>(byte)(i%251)).ToArray();
                    File.WriteAllBytes(path,expected);
                    var operations=new FaultOperations{FailWriteCall=2,PartialWriteLength=alignment};
                    using(var stream=new NativeStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read,DirectTuning(checked(alignment*2)),operations)){
                        var replacement=Enumerable.Repeat((byte)0xE7,checked(alignment*6)).ToArray();
                        // 最初の転送は成功し、次の転送は1整列単位を実際に書き込んでから失敗する。
                        var error=Assert.Throws<IOException>(()=>stream.Write(replacement));
                        Assert.Same(operations.Failure,error);
                        Assert.Equal(0,stream.Position);
                        Assert.Equal(originalLength,stream.Length);
                        Assert.Equal(2,operations.WriteCalls);
                        Assert.Equal(3L*alignment,operations.ActualWrittenBytes);
                        if(originalLength<alignment*3){
                            // 失敗した転送が延ばした整列余白だけ、転送前の長さへ戻す。
                            Assert.Equal(1,operations.SetLengthCalls);
                        }else{
                            Assert.Equal(0,operations.SetLengthCalls);
                        }
                    }
                    Array.Fill(expected,(byte)0xE7,0,Math.Min(originalLength,checked(alignment*3)));
                    Assert.Equal(expected,File.ReadAllBytes(path));
                }
            });
        }
        {
            // 成功した整列書込み後に余白の切戻しが失敗したら、例外を返し Position は進めない。
            var original=Enumerable.Range(0,17).Select(i=>(byte)i).ToArray();
            WithFile(original,path=>{
                var operations=new FaultOperations{ThrowSetLength=true};
                using(var stream=new NativeStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read,DirectTuning(),operations)){
                    stream.Position=original.Length;
                    var error=Assert.Throws<IOException>(()=>stream.Write(new byte[]{0xE7,0xE8,0xE9}));
                    Assert.Same(operations.Failure,error);
                    Assert.Equal(original.Length,stream.Position);
                    Assert.Equal(1,operations.WriteCalls);
                    Assert.Equal(1,operations.SetLengthCalls);
                    Assert.Equal(operations.LastWriteSize,stream.Length);
                }
                var actual=File.ReadAllBytes(path);
                Assert.Equal(original,actual.AsSpan(0,original.Length).ToArray());
                Assert.Equal(new byte[]{0xE7,0xE8,0xE9},actual.AsSpan(original.Length,3).ToArray());
                Assert.All(actual.Skip(original.Length+3),value=>Assert.Equal((byte)0,value));
            });
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Flush(){
        if(!OperatingSystem.IsWindows()){
            return;
        }
        var expected=new byte[]{1,2,3};
        WithFile(expected,path=>{
            var operations=new FaultOperations{ThrowFlush=true};
            using(var stream=new NativeStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read,DirectTuning(),operations)){
                stream.Position=2;
                Assert.Same(operations.Failure,Assert.Throws<IOException>(()=>stream.Flush(flushToDisk:true)));
                Assert.Equal(2,stream.Position);
                Assert.Equal(expected.Length,stream.Length);
                Assert.Equal(1,operations.FlushCalls);
            }
            var readOnlyOperations=new FaultOperations{ThrowFlush=true};
            using(var stream=new NativeStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,DirectTuning(),readOnlyOperations)){
                stream.Flush(flushToDisk:true);
                Assert.Equal(0,readOnlyOperations.FlushCalls);
            }
            Assert.Equal(expected,File.ReadAllBytes(path));
        });
    }

    [Fact,Trait("Category","Unit")]
    public void Dispose(){
        if(!OperatingSystem.IsWindows()){
            return;
        }
        WithFile(new byte[]{1,2,3},path=>{
            var operations=new FaultOperations();
            var stream=new NativeStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.Read,DirectTuning(),operations);
            try{
                stream.Dispose();
                stream.Dispose();
                Assert.Equal(1,operations.CloseCalls);
                Assert.Equal(1,operations.FreeCalls);
                Assert.NotEqual((nint)0,operations.FreedBuffer);
                Assert.NotNull(operations.ClosedHandle);
                Assert.True(operations.ClosedHandle!.IsClosed);
                Assert.False(stream.CanRead);
                Assert.False(stream.CanWrite);
                Assert.False(stream.CanSeek);
                Assert.Throws<ObjectDisposedException>(()=>stream.Read(new byte[1]));
                using var exclusive=File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
                Assert.Equal(3,exclusive.Length);
            }finally{
                stream.Dispose();
            }
            Assert.Equal(1,operations.CloseCalls);
            Assert.Equal(1,operations.FreeCalls);
        });
    }

    private static StorageTuning DirectTuning(int bufferSize=1)=>
        new(FileAccess.ReadWrite,StorageAccessPattern.Random,StorageBufferingMode.Unbuffered,StorageBufferSizeStrategy.Explicit,bufferSize);

    private static void WithFile(byte[] bytes,Action<string> action){
        var path=Path.Combine(Path.GetTempPath(),"seamless-direct-fault-"+Guid.NewGuid().ToString("N")+".dat");
        try{
            File.WriteAllBytes(path,bytes);
            action(path);
        }finally{
            File.Delete(path);
        }
    }

    private sealed class FaultOperations:NativeStream.FileOperations{
        public IOException Failure{get;}=new("Injected file I/O failure.");
        public int ReadLimit{get;init;}=-1;
        public int FailWriteCall{get;init;}
        public int PartialWriteLength{get;init;}
        public bool ThrowGetLength{get;init;}
        public bool ThrowSetLength{get;init;}
        public bool ThrowFlush{get;init;}
        public int LastReadSize{get;private set;}
        public int LastWriteSize{get;private set;}
        public int WriteCalls{get;private set;}
        public long ActualWrittenBytes{get;private set;}
        public int SetLengthCalls{get;private set;}
        public int FlushCalls{get;private set;}
        public int CloseCalls{get;private set;}
        public int FreeCalls{get;private set;}
        public nint FreedBuffer{get;private set;}
        public SafeFileHandle? ClosedHandle{get;private set;}

        public override int Read(SafeFileHandle handle,Span<byte> destination,long fileOffset){
            this.LastReadSize=destination.Length;
            var read=base.Read(handle,destination,fileOffset);
            if(this.ReadLimit>=0){
                return Math.Min(read,this.ReadLimit);
            }
            return read;
        }
        public override void Write(SafeFileHandle handle,ReadOnlySpan<byte> source,long fileOffset){
            this.WriteCalls++;
            this.LastWriteSize=source.Length;
            if(this.WriteCalls==this.FailWriteCall){
                if(this.PartialWriteLength>0){
                    base.Write(handle,source[..this.PartialWriteLength],fileOffset);
                    this.ActualWrittenBytes+=this.PartialWriteLength;
                }
                throw this.Failure;
            }
            base.Write(handle,source,fileOffset);
            this.ActualWrittenBytes+=source.Length;
        }
        public override long GetLength(SafeFileHandle handle){
            if(this.ThrowGetLength){
                throw this.Failure;
            }
            return base.GetLength(handle);
        }
        public override void SetLength(SafeFileHandle handle,long length){
            this.SetLengthCalls++;
            if(this.ThrowSetLength){
                throw this.Failure;
            }
            base.SetLength(handle,length);
        }
        public override void FlushToDisk(SafeFileHandle handle){
            this.FlushCalls++;
            if(this.ThrowFlush){
                throw this.Failure;
            }
            base.FlushToDisk(handle);
        }
        public override void Close(SafeFileHandle handle){
            this.CloseCalls++;
            this.ClosedHandle=handle;
            base.Close(handle);
        }
        public override void AlignedFree(nint address){
            this.FreeCalls++;
            this.FreedBuffer=address;
            base.AlignedFree(address);
        }
    }
}
