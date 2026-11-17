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

internal static class SingleSpan {
    internal static Span<T> Create<T>(
#if !NETSTANDARD2_0
        ref T reference
#endif
    ) {
#if NETSTANDARD2_0
        var array = SingleArray<T>.Array;
        if (array == null)
        {
            array = SingleArray<T>.Array = new T[1];
        }
        return array.AsSpan(); // need to Clear after used to prevent memory leak
#elif NETSTANDARD2_1
        return MemoryMarshal.CreateSpan(ref reference, 1);
#else // NET8_0_OR_GREATER
        return new Span<T>(ref reference);
#endif
    }

#if NETSTANDARD2_0
    internal static class SingleArray<T>
    {
        [ThreadStatic]
        public static T[]? Array;
    }
#endif
}
