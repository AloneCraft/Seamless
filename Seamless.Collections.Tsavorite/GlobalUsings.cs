global using System;
global using System.Buffers;
global using System.Collections;
global using System.Collections.Generic;
global using System.Collections.ObjectModel;
global using System.ComponentModel;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.IO;
global using System.Linq;
global using System.Reflection;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using Seamless.Collections;
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
