global using System;
global using System.Collections;
global using System.Collections.Generic;
global using System.IO;
global using System.Threading;
global using System.Threading.Tasks;

global using Tsavorite.core;
global using Garnet.common;
global using SpanByteSessionKey = Tsavorite.core.PinnedSpanByte;
global using SpanByteOutput = Tsavorite.core.SpanByteAndMemory;
global using SpanByteStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordDisposer
>;
global using SpanByteStoreAllocator = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordDisposer
    >
>;
