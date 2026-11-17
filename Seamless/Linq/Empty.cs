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
namespace Seamless.Linq {
    public static partial class Enumerable {
        public static ValueEnumerable<FromEmpty<T>,T> Empty<T>() {
            return new(default);
        }
    }
}

namespace Seamless.Linq {
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct FromEmpty<T>:IValueEnumerator<T> {
        public bool TryGetNonEnumeratedCount(out int count) {
            count=0;
            return true;
        }

        public bool TryGetSpan(out ReadOnlySpan<T> span) {
            span= [];
            return true;
        }

        public bool TryCopyTo(Span<T> destination,Index offset) => true;

        public bool TryGetNext(out T current) {
            Unsafe.SkipInit(out current);
            return false;
        }

        public void Dispose() {
        }
    }
}
