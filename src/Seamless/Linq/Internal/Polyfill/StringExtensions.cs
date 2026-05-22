#if NETSTANDARD2_0 || NETSTANDARD2_1

using System;
using System.Collections.Generic;
using System.Text;

namespace Seamless.Linq.Carrier.Internal;

public static class StringExtensions
{
    public static void CopyTo(this string s, Span<char> destination)
    {
        s.AsSpan().CopyTo(destination);
    }
}

#endif
