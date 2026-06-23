#if NETSTANDARD2_0

using System;
using global::System.Collections.Generic;
using global::System.Text;

namespace global::System.Runtime.CompilerServices
{
    internal static class RuntimeHelpers
    {
        public static bool IsReferenceOrContainsReferences<T>()
        {
            // only primitive
            if (typeof(T).IsPrimitive)
            {
                return false;
            }

            return true;
        }
    }
}

#endif
