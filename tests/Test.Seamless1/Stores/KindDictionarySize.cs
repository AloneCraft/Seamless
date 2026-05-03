namespace Test.Seamless.Unit.Stores;

/// <summary>
/// 4種類のDictionaryがログサイズに差が出ることを確認するテスト。
/// int キー + int 値 で N 件挿入後の TailAddress を比較する。
/// </summary>
public partial class KindDictionarySize {
    private static partial void AssertUnmanagedUnmanagedVsManagedManaged(long uuBytes,long mmBytes) {
        Assert.True(uuBytes<mmBytes,
            $"UnmanagedUnmanaged({uuBytes}) > ManagedManaged({mmBytes})");
    }

    private static partial void AssertManagedUnmanagedVsManagedManaged(long muBytes,long mmBytes) {
        Assert.True(muBytes==mmBytes,
            $"ManagedUnmanaged({muBytes}) > ManagedManaged({mmBytes})");
    }

    private static partial void AssertUnmanagedManagedVsManagedManaged(long umBytes,long mmBytes) {
        Assert.True(umBytes==mmBytes,
            $"UnmanagedManaged({umBytes}) > ManagedManaged({mmBytes})");
    }

    private static partial System.Collections.Generic.Dictionary<string,long> GetExpectedSizes() {
        return new System.Collections.Generic.Dictionary<string,long> {
            ["UnmanagedUnmanaged"]=16000,   // unmanaged key+value で直接格納
            ["UnmanagedManaged"]=24064,     // unmanaged key + managed value (serialized as SpanByte)
            ["ManagedUnmanaged"]=24064,     // managed key (serialized as SpanByte) + managed/unmanaged (serialized as SpanByte)
            ["ManagedManaged"]=24064,       // managed key (serialized as SpanByte) + managed value (serialized as SpanByte)
        };
        throw new InvalidOperationException("Expected constants for TSAVORITE version are not defined.");
    }
}
