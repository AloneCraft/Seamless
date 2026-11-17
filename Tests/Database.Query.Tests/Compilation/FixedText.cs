using System.Runtime.CompilerServices;
using Seamless.Collections;

namespace Database.Compilation.Tests;

public sealed class FixedText{
    [Fact]public void Create(){
        {
            FixedText<TextBuffer32>value="a\0日本😀";
            Assert.Equal(6,value.Length);
            Assert.Equal("a\0日本😀",value.ToString());
            Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<FixedText<TextBuffer32>>());
            Assert.Equal("",default(FixedText<TextBuffer32>).ToString());
        }
        {
            Assert.Throws<ArgumentException>(()=>new FixedText<TextBuffer32>(new string('a',33)));
            Assert.Throws<ArgumentNullException>(()=>new FixedText<TextBuffer32>((string)null!));
            FixedText<TextBuffer32>value=new string('a',32);
            Assert.Equal(32,value.Length);
        }
    }

    [Fact]public void EqualsValue(){
        FixedText<TextBuffer32>first="ABC";
        FixedText<TextBuffer32>same="ABC";
        FixedText<TextBuffer32>different="abc";
        Assert.True(first==same);
        Assert.False(first==different);
        Assert.Equal(first.GetHashCode(),same.GetHashCode());
        Assert.Equal(2,new System.Collections.Generic.HashSet<FixedText<TextBuffer32>>{first,same,different}.Count);
        var copy=first;
        first="replacement";
        Assert.Equal("ABC",copy.ToString());
    }

    [Fact]public void AsSpan(){
        FixedText<TextBuffer32>value="abc日本";
        Assert.True(value.AsSpan().SequenceEqual("abc日本"));
        Assert.True(value.Contains("日本"));
        Assert.True(value.StartsWith("ABC",StringComparison.OrdinalIgnoreCase));
        Assert.True(value.EndsWith("日本",StringComparison.Ordinal));
        Assert.Equal(3,value.IndexOf("日本",StringComparison.Ordinal));
        Assert.Equal('b',value[1]);
        Span<char>buffer=stackalloc char[5];
        value.CopyTo(buffer);
        Assert.True(buffer.SequenceEqual("abc日本"));
    }
}
