using System.Diagnostics;
namespace Seamless.ScanIteratorFunctions;
//todo 将来struct
public abstract class ScanIteratorFunctions<T>:IScanIteratorFunctions {
    public readonly T[] Chunk = new T[Const.Chunk_Length];
    public int Count;
    private long LastAcceptedAddress;
    private protected virtual bool ShouldRead<TSourceLogRecord>(in TSourceLogRecord logRecord)
        where TSourceLogRecord : ISourceLogRecord => true;
    private protected abstract T Deserialize<TSourceLogRecord>(in TSourceLogRecord logRecord) where TSourceLogRecord : ISourceLogRecord;
    public bool Reader<TSourceLogRecord>(in TSourceLogRecord logRecord,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) where TSourceLogRecord : ISourceLogRecord {
        if(!this.ShouldRead(logRecord)) {
            cursorRecordResult=CursorRecordResult.Skip;
            return true;
        }
        Debug.Assert(!logRecord.Info.Tombstone);
        var Index = Interlocked.Increment(ref this.Count)-1;
        if(Index>=Const.Chunk_Length) {
            Volatile.Write(ref this.Count,Const.Chunk_Length);
            var lastAcceptedAddress = Volatile.Read(ref this.LastAcceptedAddress);
            cursorRecordResult=recordMetadata.Address==lastAcceptedAddress
                ? CursorRecordResult.Skip|CursorRecordResult.EndBatch
                : CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
            return true;
        }
        this.Chunk[Index]=this.Deserialize(logRecord);
        Volatile.Write(ref this.LastAcceptedAddress,recordMetadata.Address);
        cursorRecordResult=CursorRecordResult.Accept;
        return true;
    }
    public bool OnStart(long beginAddress,long endAddress) {
        Volatile.Write(ref this.Count,0);
        Volatile.Write(ref this.LastAcceptedAddress,0);
        return true;
    }
    public void OnStop(bool completed,long numberOfRecords) {
    }
    public void OnException(Exception exception,long numberOfRecords) {
    }
}

public abstract class MemoryScanIteratorFunctions<T>:ScanIteratorFunctions<T> {
    private protected sealed override bool ShouldRead<TSourceLogRecord>(in TSourceLogRecord logRecord) =>
        typeof(TSourceLogRecord)!=typeof(DiskLogRecord);
}
