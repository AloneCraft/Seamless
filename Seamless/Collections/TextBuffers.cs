using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Seamless.Collections;

[InlineArray(16)]public struct TextBuffer16:IFixedTextBuffer{
    private char element;
    public static int Capacity=>16;
}
[InlineArray(32)]public struct TextBuffer32:IFixedTextBuffer{
    private char element;
    public static int Capacity=>32;
}
[InlineArray(64)]public struct TextBuffer64:IFixedTextBuffer{
    private char element;
    public static int Capacity=>64;
}
[InlineArray(128)]public struct TextBuffer128:IFixedTextBuffer{
    private char element;
    public static int Capacity=>128;
}
[InlineArray(256)]public struct TextBuffer256:IFixedTextBuffer{
    private char element;
    public static int Capacity=>256;
}
