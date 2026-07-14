#if NET8_0_OR_GREATER
namespace Seamless.System.Carrier;

internal static class SimdIndexVectors {
    internal static readonly Vector<byte> Byte = CreateByte();
    internal static readonly Vector<sbyte> SByte = CreateSByte();
    internal static readonly Vector<ushort> UInt16 = CreateUInt16();
    internal static readonly Vector<short> Int16 = CreateInt16();
    internal static readonly Vector<uint> UInt32 = CreateUInt32();
    internal static readonly Vector<int> Int32 = CreateInt32();
    internal static readonly Vector<ulong> UInt64 = CreateUInt64();
    internal static readonly Vector<long> Int64 = CreateInt64();
    internal static readonly Vector<char> Char = CreateChar();
    private static Vector<byte> CreateByte() {
        var values = new byte[Vector<byte>.Count];
        for(var i = 0;i<values.Length;i++) {
            values[i]=(byte)i;
        }
        return new Vector<byte>(values);
    }
    private static Vector<sbyte> CreateSByte() {
        var values = new sbyte[Vector<sbyte>.Count];
        for(var i = 0;i<values.Length;i++) {
            values[i]=(sbyte)i;
        }
        return new Vector<sbyte>(values);
    }
    private static Vector<ushort> CreateUInt16() {
        var values = new ushort[Vector<ushort>.Count];
        for(var i = 0;i<values.Length;i++) {
            values[i]=(ushort)i;
        }
        return new Vector<ushort>(values);
    }
    private static Vector<short> CreateInt16() {
        var values = new short[Vector<short>.Count];
        for(var i = 0;i<values.Length;i++) {
            values[i]=(short)i;
        }
        return new Vector<short>(values);
    }
    private static Vector<uint> CreateUInt32() {
        var values = new uint[Vector<uint>.Count];
        for(var i = 0;i<values.Length;i++) {
            values[i]=(uint)i;
        }
        return new Vector<uint>(values);
    }
    private static Vector<int> CreateInt32() {
        var values = new int[Vector<int>.Count];
        for(var i = 0;i<values.Length;i++) {
            values[i]=i;
        }
        return new Vector<int>(values);
    }
    private static Vector<ulong> CreateUInt64() {
        var values = new ulong[Vector<ulong>.Count];
        for(var i = 0;i<values.Length;i++) {
            values[i]=(ulong)i;
        }
        return new Vector<ulong>(values);
    }
    private static Vector<long> CreateInt64() {
        var values = new long[Vector<long>.Count];
        for(var i = 0;i<values.Length;i++) {
            values[i]=i;
        }
        return new Vector<long>(values);
    }
    private static Vector<char> CreateChar() {
        var values = new char[Vector<char>.Count];
        for(var i = 0;i<values.Length;i++) {
            values[i]=(char)i;
        }
        return new Vector<char>(values);
    }
}
#endif
