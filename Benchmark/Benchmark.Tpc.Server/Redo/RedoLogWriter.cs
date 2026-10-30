namespace Benchmark.Tpc.Server;
/// <summary>
/// 再実行ログの書き手。<see cref="Append"/> は積むだけで、<see cref="Sync"/> が書き出して fsync する(複数の取引をまとめて 1 回で確定する)。
/// 応答は <see cref="Sync"/> の後に返す。
/// </summary>
public sealed class RedoLogWriter:IDisposable{
    private readonly FileStream stream;
    private readonly MemoryStream pending=new();
    private long lastAppendedSequence;
    public RedoLogWriter(string path,long validLength){
        this.stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough);
        // 電源断で書き終えなかった末尾は、読み手が見つけた有効な長さで切り捨ててから足す。
        this.stream.SetLength(validLength);
        this.stream.Position=validLength;
        this.DurableLength=validLength;
    }
    public long DurableLength{get;private set;}
    public long LastDurableSequence{get;private set;}
    public bool HasPending=>this.pending.Length>0;
    public void Append(RedoTransaction transaction){
        var frame=RedoLogFormat.Encode(transaction);
        this.pending.Write(frame);
        this.lastAppendedSequence=transaction.Sequence;
    }
    /// <summary>積んだ分を書いて、ストレージまで確実に届ける。</summary>
    public void Sync(){
        if(this.pending.Length==0){
            return;
        }
        this.pending.Position=0;
        this.pending.CopyTo(this.stream);
        this.pending.SetLength(0);
        this.stream.Flush(true);
        this.DurableLength=this.stream.Position;
        this.LastDurableSequence=this.lastAppendedSequence;
    }
    public void Dispose(){
        this.stream.Dispose();
        this.pending.Dispose();
    }
}
