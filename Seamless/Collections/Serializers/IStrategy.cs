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

public interface IStrategy<T> {
    static abstract ReadOnlySpan<byte> Serialize(ref T value);
    static abstract T Deserialize(ReadOnlySpan<byte> bytes);
}

public interface IStrategy{
    static abstract bool UsesMemoryPackSerializer{get;}
}
