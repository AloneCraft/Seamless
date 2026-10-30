namespace Seamless.Transactions;
public enum WalMutation:byte{Insert=1,Update=2,Delete=3,Upsert=4}
public sealed record WalEntry(int Partition,int TableId,WalMutation Mutation,string Codec,byte[] Key,byte[]? Row);
public sealed record WalDraft(Guid Id,int[] Partitions,IReadOnlyList<WalEntry> Entries,byte[] Metadata);
public sealed record WalTransaction(long Lsn,WalDraft Draft);
public readonly record struct WalReceipt(long Lsn,Task Durable);
public interface IWalDevice:IDisposable{
    void Write(byte[] frame);
    void Sync();
}
public sealed record WalOptions{
    public int QueueCapacity{get;init;}=256;
    public long QueueBytes{get;init;}=64L<<20;
    public int MaxBatch{get;init;}=64;
    public int MaxFrameBytes{get;init;}=4<<20;
    public long SegmentBytes{get;init;}=64L<<20;
    public int SectorBytes{get;init;}=4096;
    public string CatalogIdentity{get;init;}="seamless-transactions-v2";
    public void Validate(){
        if(this.QueueCapacity<=0||this.QueueCapacity>256||this.MaxBatch<=0||this.MaxBatch>256||this.MaxFrameBytes<64||this.QueueBytes<this.MaxFrameBytes||this.SectorBytes<512||(this.SectorBytes&(this.SectorBytes-1))!=0||this.SegmentBytes<this.MaxFrameBytes+2L*this.SectorBytes||this.SegmentBytes%this.SectorBytes!=0){
            throw new ArgumentOutOfRangeException(nameof(WalOptions));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(this.CatalogIdentity);
    }
}
/// <summary>最初の障害を保持し、受付・応答許可・保存の共通境界で検査する。</summary>
public sealed class TransactionFailure{
    private readonly object gate=new();
    private Exception? error;
    public Exception? Error{get{lock(this.gate){return this.error;}}}
    public void Fail(Exception error){lock(this.gate){this.error??=error;}}
    public void ThrowIfFailed(){lock(this.gate){if(this.error is not null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(this.error).Throw();}}}
    public void Permit(Action action){lock(this.gate){this.ThrowIfFailed();action();}}
}
