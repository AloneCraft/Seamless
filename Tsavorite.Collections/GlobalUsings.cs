global using System;
global using System.Buffers;
global using System.Collections;
global using System.Collections.Frozen;
global using System.Collections.Generic;
global using System.Collections.Immutable;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.IO;
global using System.Linq;
global using System.Numerics;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Runtime.Intrinsics;
global using System.Security.Cryptography;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;

global using Seamless.Collections.Tsavorite.Carrier;
global using Seamless.Collections.Tsavorite.Carrier.Internal;
global using Seamless.Collections.Tsavorite.Carrier.Linq;
global using Seamless.Collections.Persisted;
global using Seamless.Collections.Serialization;
#if NET9_0_OR_GREATER
#endif

global using Tsavorite.core;
global using Garnet.common;
global using SpanByteStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordTriggers
>;
global using SpanByteStoreAllocator = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordTriggers
    >
>;

global using Seamless.Collections.Tsavorite;
