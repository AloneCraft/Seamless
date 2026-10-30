namespace Benchmark.TpcE.Compliance.Tests;
public sealed class RedoLogWriter:IDisposable{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"tpce-redo-"+Guid.NewGuid().ToString("N"));
    public RedoLogWriter(){
        Directory.CreateDirectory(this.directory);
    }
    public void Dispose(){
        try{
            Directory.Delete(this.directory,true);
        }catch(IOException){
        }
    }
    private static RedoTransaction Sample(long sequence)=>new(sequence,[new RedoEntry(1,Core.WriteKind.Update,[1],[2])]);
    private static long Count(string path){
        long valid=0;
        long count=0;
        foreach(var _ in Compliance.RedoLogReader.Read(path,length=>valid=length)){
            count++;
        }
        return count;
    }
    [Fact,Trait("Category","Unit")]public void SyncMakesRecordsReadable(){
        {
            //積んだだけでは読めず、Sync の後で読める。応答は Sync の後に返す約束
            var path=Path.Combine(this.directory,"a.redo");
            using var writer=new Compliance.RedoLogWriter(path,0);
            writer.Append(Sample(1));
            writer.Append(Sample(2));
            Assert.True(writer.HasPending);
            Assert.Equal(0,Count(path));
            writer.Sync();
            Assert.False(writer.HasPending);
            Assert.Equal(2,Count(path));
            Assert.Equal(2,writer.LastDurableSequence);
            Assert.True(writer.DurableLength>0);
        }
    }
    [Fact,Trait("Category","Unit")]public void ReopenDropsTornTail(){
        {
            //電源断で書き終えなかった末尾は、有効な長さで切り捨てて続きを足せる
            var path=Path.Combine(this.directory,"b.redo");
            long valid;
            using(var writer=new Compliance.RedoLogWriter(path,0)){
                writer.Append(Sample(1));
                writer.Sync();
                valid=writer.DurableLength;
            }
            File.AppendAllText(path,"torn-tail-bytes");
            long found=0;
            Assert.Single(Compliance.RedoLogReader.Read(path,length=>found=length));
            Assert.Equal(valid,found);
            using(var writer=new Compliance.RedoLogWriter(path,found)){
                writer.Append(Sample(2));
                writer.Sync();
            }
            Assert.Equal(2,Count(path));
        }
    }
}
