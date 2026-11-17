using System;
using System.Buffers;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Seamless.Linq;
namespace Seamless.Linq;

partial class Enumerable {
    public static bool TryGetNonEnumeratedCount<TEnumerator, TSource>(this ValueEnumerable<TEnumerator,TSource> source,out int count)
        where TEnumerator : struct, IValueEnumerator<TSource> {
            using var enumerator = source.Enumerator;
            return enumerator.TryGetNonEnumeratedCount(out count);
        }
}
