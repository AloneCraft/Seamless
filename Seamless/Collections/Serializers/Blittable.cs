using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Seamless.Collections.Serializers;

public readonly struct Blittable<T>:IStrategy<T>
    where T : unmanaged {
    public static ReadOnlySpan<byte> Serialize(ref T value) =>
        MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T,byte>(ref value),Unsafe.SizeOf<T>());

    public static T Deserialize(ReadOnlySpan<byte> bytes) {
        if(bytes.IsEmpty)
            return default;
        return MemoryMarshal.Read<T>(bytes);
    }
}

public readonly struct Blittable:IStrategy{
    public static bool UsesMemoryPackSerializer=>false;
}
