using MemoryPack;
using Xunit;
namespace Seamless.Collections.Tests;

/// <summary>
/// MemoryPackObjectSerializer の BeginSerialize/Serialize/EndSerialize/BeginDeserialize/Deserialize/EndDeserialize の
/// ラウンドトリップを検証する。
/// </summary>
public partial class MemoryPackObjectSerializerTest {
    // --- シングル値のラウンドトリップ ---
    [Fact]
    public void RoundTrip_Int() {
        var serializer = new MemoryPackObjectSerializer<int>();
        using var ms = new MemoryStream();
        var expected = 42;
        serializer.BeginSerialize(ms);
        serializer.Serialize(ref expected);
        serializer.EndSerialize();
        ms.Position = 0;
        serializer.BeginDeserialize(ms);
        serializer.Deserialize(out var actual);
        serializer.EndDeserialize();
        Assert.Equal(expected, actual);
    }
    [Fact]
    public void RoundTrip_String() {
        var serializer = new MemoryPackObjectSerializer<string>();
        using var ms = new MemoryStream();
        var expected = "hello, MemoryPack";
        serializer.BeginSerialize(ms);
        serializer.Serialize(ref expected);
        serializer.EndSerialize();
        ms.Position = 0;
        serializer.BeginDeserialize(ms);
        serializer.Deserialize(out var actual);
        serializer.EndDeserialize();
        Assert.Equal(expected, actual);
    }
    [Fact]
    public void RoundTrip_EmptyString() {
        var serializer = new MemoryPackObjectSerializer<string>();
        using var ms = new MemoryStream();
        var expected = "";
        serializer.BeginSerialize(ms);
        serializer.Serialize(ref expected);
        serializer.EndSerialize();
        ms.Position = 0;
        serializer.BeginDeserialize(ms);
        serializer.Deserialize(out var actual);
        serializer.EndDeserialize();
        Assert.Equal(expected, actual);
    }
    // --- 複数値の連続シリアライズ/デシリアライズ ---
    [Fact]
    public void RoundTrip_MultipleValues() {
        var serializer = new MemoryPackObjectSerializer<int>();
        using var ms = new MemoryStream();
        var values = new[] { 1, 2, 3, 100, -1 };
        serializer.BeginSerialize(ms);
        foreach(var v in values) {
            var copy = v;
            serializer.Serialize(ref copy);
        }
        serializer.EndSerialize();
        ms.Position = 0;
        serializer.BeginDeserialize(ms);
        var results = new int[values.Length];
        for(var i = 0; i < values.Length; i++) {
            serializer.Deserialize(out results[i]);
        }
        serializer.EndDeserialize();
        Assert.Equal(values, results);
    }
    // --- 複合型 ---
    [MemoryPackable]
    public partial record SampleRecord(int Id, string Name);
    [Fact]
    public void RoundTrip_ComplexObject() {
        var serializer = new MemoryPackObjectSerializer<SampleRecord>();
        using var ms = new MemoryStream();
        var expected = new SampleRecord(99, "テスト");
        serializer.BeginSerialize(ms);
        serializer.Serialize(ref expected);
        serializer.EndSerialize();
        ms.Position = 0;
        serializer.BeginDeserialize(ms);
        serializer.Deserialize(out var actual);
        serializer.EndDeserialize();
        Assert.Equal(expected, actual);
    }
    // --- LengthPrefixの確認: 先頭4バイトがペイロードサイズと一致 ---
    [Fact]
    public void LengthPrefix_MatchesPayload() {
        var serializer = new MemoryPackObjectSerializer<string>();
        using var ms = new MemoryStream();
        var value = "abc";
        serializer.BeginSerialize(ms);
        serializer.Serialize(ref value);
        serializer.EndSerialize();
        var bytes = ms.ToArray();
        var prefixedLength = BitConverter.ToInt32(bytes, 0);
        Assert.Equal(bytes.Length - 4, prefixedLength);
    }
}
