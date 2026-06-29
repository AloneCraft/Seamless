namespace Seamless.System.Carrier.Internal.Polyfill; // first priority

internal static class GC
{
    internal static T[] AllocateUninitializedArray<T>(int length)
    {
#if NET8_0_OR_GREATER
        return global::System.GC.AllocateUninitializedArray<T>(length);
#else
        return new T[length];
#endif
    }
}
