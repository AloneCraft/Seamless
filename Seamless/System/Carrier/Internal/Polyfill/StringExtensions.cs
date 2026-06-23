#if NETSTANDARD2_0 || NETSTANDARD2_1

using System;
using global::System.Collections.Generic;
using global::System.Text;

namespace Seamless.System.Carrier.Internal;

public static class StringExtensions
{
    public static void CopyTo(this string s, Span<char> destination)
    {
        s.AsSpan().CopyTo(destination);
    }
}

#endif
