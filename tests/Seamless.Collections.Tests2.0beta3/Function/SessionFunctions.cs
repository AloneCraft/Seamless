namespace Seamless.Collections;
using System.Buffers;
using Tsavorite.core;
public sealed class SessionFunctions:SpanByteFunctions<Empty>{
    public SessionFunctions():base(MemoryPool<byte>.Shared){}
    public override bool InPlaceUpdater(ref LogRecord logRecord,in RecordSizeInfo sizeInfo,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return false;
    }
    public override bool NeedCopyUpdate<TSourceLogRecord>(in TSourceLogRecord srcLogRecord,ref PinnedSpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return false;
    }
}
