namespace Seamless.Collections;
using System.Buffers;
using Tsavorite.core;
public sealed class SessionFunctions:SpanByteFunctions<Empty>{
    public SessionFunctions():base(MemoryPool<byte>.Shared){}
    public override bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo){
        return false;
    }
    public override bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        return false;
    }
}

