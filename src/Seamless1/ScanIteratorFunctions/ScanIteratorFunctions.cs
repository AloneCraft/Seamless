using System.Diagnostics;
namespace Seamless.ScanIteratorFunctions;

// public abstract class ScanIteratorFunctions<T, TSessionKey, TSessionValue>:IScanIteratorFunctions<TSessionKey,TSessionValue> {
//     public readonly T[] Chunk = new T[Const.Chunk_Length];
//     public int Count;
//     private long LastAcceptedAddress;
//     private protected abstract T Deserialize(ref TSessionKey key,ref TSessionValue value);
//     public bool SingleReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
//         Debug.Assert(!recordMetadata.RecordInfo.Tombstone);
//         var index = Interlocked.Increment(ref this.Count)-1;
//         if(index>=Const.Chunk_Length) {
//             Volatile.Write(ref this.Count,Const.Chunk_Length);
//             var lastAcceptedAddress = Volatile.Read(ref this.LastAcceptedAddress);
//             cursorRecordResult=recordMetadata.Address==lastAcceptedAddress
//                 ? CursorRecordResult.Skip|CursorRecordResult.EndBatch
//                 : CursorRecordResult.Skip|CursorRecordResult.EndBatch|CursorRecordResult.RetryLastRecord;
//             return true;
//         }
//         this.Chunk[index]=this.Deserialize(ref key,ref value);
//         Volatile.Write(ref this.LastAcceptedAddress,recordMetadata.Address);
//         cursorRecordResult=CursorRecordResult.Accept;
//         return true;
//     }
//     public bool ConcurrentReader(ref TSessionKey key,ref TSessionValue value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
//         return this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
//     }
//     public bool OnStart(long beginAddress,long endAddress) {
//         Volatile.Write(ref this.Count,0);
//         Volatile.Write(ref this.LastAcceptedAddress,0);
//         return true;
//     }
//     public void OnStop(bool completed,long numberOfRecords) {
//     }
//     public void OnException(Exception exception,long numberOfRecords) {
//     }
// }
