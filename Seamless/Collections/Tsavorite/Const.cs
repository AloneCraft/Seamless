using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpanByteStoreAllocator = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordTriggers
    >
>;
using SpanByteStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordTriggers
>;
using EqualityEmpty = Seamless.Collections.Tsavorite.EqualityEmpty;
using Garnet.common;
using Seamless.Collections;
using Tsavorite.core;

namespace Seamless.Collections.Tsavorite;

public static class Const {
    public const int Chunk_Length = 2;
    public const int IndexSizeBits = 6;
    public const int PageSizeBits = 12;
    public const int MemorySizeBits = PageSizeBits+1;
    public const long IndexSize = 1L<<IndexSizeBits;
    public const long LogMemorySize = 1L<<MemorySizeBits;
    public const long PageSize = 1L<<PageSizeBits;
    public const long SegmentSize = PageSize*1;
    //public const long SegmentSize=PageSize*2;
    public const long MinLogCapacity = SegmentSize*2;
    public const long MinObjectLogCapacity = 1L<<22;
    public const long MaxObjectLogCapacity = 1L<<62;
}
