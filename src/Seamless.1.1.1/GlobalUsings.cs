global using Tsavorite.core;
//global using Seamless.Collections;
global using ManagedSessionKey     = Tsavorite.core.SpanByte;
global using ManagedSessionValue   = Tsavorite.core.SpanByte;
global using ManagedOutput         = Tsavorite.core.SpanByteAndMemory;
global using ManagedStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.
    SpanByte, 
    Tsavorite.core.
    SpanByte, 
    Tsavorite.core.SpanByteComparer, 
    Tsavorite.core.SpanByteRecordDisposer
>;
global using ManagedAllocator      = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByte,
        Tsavorite.core.SpanByte,
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.SpanByteRecordDisposer
    >
>;
