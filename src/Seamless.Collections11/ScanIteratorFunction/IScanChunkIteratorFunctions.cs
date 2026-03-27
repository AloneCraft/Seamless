namespace Seamless.Collections;
using Tsavorite.core;

internal interface IScanChunkIteratorFunctions<T>:IScanIteratorFunctions<SpanByte,SpanByte>{
    T[] Chunk{get;}
    int Count{get;}
}
