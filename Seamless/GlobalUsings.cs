global using System;
global using global::System.Buffers;
global using global::System.Collections;
global using global::System.Collections.Frozen;
global using global::System.Collections.Generic;
global using global::System.Collections.Immutable;
global using System.ComponentModel;
global using global::System.Diagnostics;
global using global::System.Diagnostics.CodeAnalysis;
global using global::System.IO;
global using System.Linq;
global using global::System.Numerics;
global using System.Reflection;
global using global::System.Runtime.CompilerServices;
global using global::System.Runtime.InteropServices;
global using global::System.Runtime.Intrinsics;
global using System.Security.Cryptography;
global using global::System.Text;
global using global::System.Threading;
global using global::System.Threading.Tasks;

global using Seamless.Collections;
global using Seamless.Linq;
global using Seamless.System.Carrier;
global using Seamless.System.Carrier.Internal;
global using Seamless.Persisted.Collections;
global using Seamless.Serialization;
global using Seamless.Tsavorite;
global using Seamless.Tsavorite.Collections;
global using Seamless.Tsavorite.Stores;
global using Seamless.Tsavorite.SessionFunctions;
global using Garnet.common;
global using Tsavorite.core;
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
