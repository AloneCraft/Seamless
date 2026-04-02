using System.Diagnostics;
namespace Seamless.ScanIteratorFunctions;

using Tsavorite.core;
public class CounterIterator:IScanIteratorFunctions<SpanByte,SpanByte> {
	public long Counter;
	private long Counter0;
	public bool SingleReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
		cursorRecordResult=CursorRecordResult.Accept;
		this.Counter0++;
		return true;
	}
	public bool ConcurrentReader(ref SpanByte key,ref SpanByte value,RecordMetadata recordMetadata,long numberOfRecords,out CursorRecordResult cursorRecordResult) {
		return this.SingleReader(ref key,ref value,recordMetadata,numberOfRecords,out cursorRecordResult);
	}
	public bool OnStart(long beginAddress,long endAddress) {
		return true;
	}
	public void OnException(Exception exception,long numberOfRecords) { }
	public void OnStop(bool completed,long numberOfRecords) {
		this.Counter=numberOfRecords;
        Debug.Assert(this.Counter0==numberOfRecords);
	}
}
