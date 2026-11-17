using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Seamless.Collections;

/// <summary>文字バッファの容量を型として宣言する。要素はUTF-16のchar。</summary>
public interface IFixedTextBuffer{
    static abstract int Capacity{get;}
}

/// <summary>参照を含まない固定容量の文字列。容量は文字数ではなくUTF-16コード単位数。</summary>
public struct FixedText<TBuffer>:IEquatable<FixedText<TBuffer>>,IFixedText
    where TBuffer:unmanaged,IFixedTextBuffer{
    private TBuffer buffer;
    private int length;

    static FixedText(){
        if(TBuffer.Capacity<=0||Unsafe.SizeOf<TBuffer>()!=checked(TBuffer.Capacity*sizeof(char))){
            throw new InvalidOperationException("A fixed text buffer must contain exactly Capacity UTF-16 characters.");
        }
    }

    public FixedText(string value):this(Required(value)){}

    public FixedText(ReadOnlySpan<char>value){
        if(value.Length>TBuffer.Capacity){throw new ArgumentException("Text exceeds the fixed buffer capacity.",nameof(value));}
        this.buffer=default;
        this.length=value.Length;
        value.CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.As<TBuffer,char>(ref this.buffer),TBuffer.Capacity));
    }

    public static int Capacity=>TBuffer.Capacity;
    public readonly int Length=>this.length;
    public readonly char this[int index]=>this.AsSpan()[index];

    [UnscopedRef]public readonly ReadOnlySpan<char>AsSpan(){
        if((uint)this.length>(uint)TBuffer.Capacity){throw new InvalidDataException("Invalid fixed text length.");}
        return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<TBuffer,char>(ref Unsafe.AsRef(in this.buffer)),this.length);
    }

    public readonly void CopyTo(Span<char>destination)=>this.AsSpan().CopyTo(destination);
    public readonly bool Contains(string value)=>this.AsSpan().Contains(Required(value),StringComparison.Ordinal);
    public readonly bool Contains(string value,StringComparison comparison)=>this.AsSpan().Contains(Required(value),comparison);
    public readonly bool StartsWith(string value,StringComparison comparison=StringComparison.CurrentCulture)=>this.AsSpan().StartsWith(Required(value),comparison);
    public readonly bool EndsWith(string value,StringComparison comparison=StringComparison.CurrentCulture)=>this.AsSpan().EndsWith(Required(value),comparison);
    public readonly int IndexOf(string value,StringComparison comparison=StringComparison.CurrentCulture)=>this.AsSpan().IndexOf(Required(value),comparison);
    public readonly bool Equals(FixedText<TBuffer>other)=>this.AsSpan().SequenceEqual(other.AsSpan());
    public override readonly bool Equals(object?obj)=>obj is FixedText<TBuffer>other&&this.Equals(other);
    public override readonly int GetHashCode()=>string.GetHashCode(this.AsSpan(),StringComparison.Ordinal);
    public override readonly string ToString()=>new(this.AsSpan());
    public static implicit operator FixedText<TBuffer>(string value)=>new(value);
    public static explicit operator string(FixedText<TBuffer>value)=>value.ToString();
    public static bool operator ==(FixedText<TBuffer>left,FixedText<TBuffer>right)=>left.Equals(right);
    public static bool operator !=(FixedText<TBuffer>left,FixedText<TBuffer>right)=>!left.Equals(right);

    private static ReadOnlySpan<char>Required(string value){ArgumentNullException.ThrowIfNull(value);return value;}
}

/// <summary>汎用codecが文字列の論理内容を保存するための入口。可変バッファは公開しない。</summary>
public interface IFixedText{
    int Length{get;}
    char this[int index]{get;}
    void CopyTo(Span<char>destination);
}
