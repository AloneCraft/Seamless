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

public readonly struct RawByteArray:IStrategy<byte[]> {
    public static ReadOnlySpan<byte> Serialize(ref byte[] value) => value;
    public static byte[] Deserialize(ReadOnlySpan<byte> bytes) => bytes.ToArray();
}
