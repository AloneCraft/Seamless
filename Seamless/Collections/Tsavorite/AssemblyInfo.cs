using EqualityEmpty = Seamless.Collections.Tsavorite.EqualityEmpty;
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
using Garnet.common;
using Seamless.Collections;
using Tsavorite.core;
[assembly: InternalsVisibleTo("Execution.Tests")]
[assembly: InternalsVisibleTo("Seamless.TestKit")]
[assembly: InternalsVisibleTo("Database")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Seamless.Core.Tests")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Dynamic.RegressionCases")]

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Benchmark.Dynamic.Tests")]
