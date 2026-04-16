global using Tsavorite.core;
global using Garnet.common;
global using ManagedSessionKey=Tsavorite.core.PinnedSpanByte;
global using ManagedSessionValue=Tsavorite.core.SpanByteAndMemory;
global using ManagedOutput=Tsavorite.core.SpanByteAndMemory;
global using ManagedStoreFunctions=Tsavorite.core.StoreFunctions<




    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordDisposer
>;
global using ManagedAllocator=Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordDisposer
    >
>;
global using UnmanagedStoreFunctions=Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordDisposer
>;
global using UnmanagedAllocator=Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordDisposer
    >
>;
