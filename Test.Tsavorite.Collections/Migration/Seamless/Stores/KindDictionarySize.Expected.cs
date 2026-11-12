namespace Test.Seamless.Stores;

/// <summary>
/// 4種類のDictionaryがログサイズに差が出ることを確認するテスト。
/// int キー + int 値 で N 件挿入後の TailAddress を比較する。
/// </summary>
public partial class KindDictionarySize {
    private static partial void AssertUnmanagedUnmanagedVsManagedManaged(long uuBytes,long mmBytes) {
        Assert.True(uuBytes==mmBytes,
            $"UnmanagedUnmanaged({uuBytes}) > ManagedManaged({mmBytes})");
    }

    private static partial void AssertManagedUnmanagedVsManagedManaged(long muBytes,long mmBytes) {
        Assert.True(muBytes<=mmBytes,
            $"ManagedUnmanaged({muBytes}) > ManagedManaged({mmBytes})");
    }

    private static partial void AssertUnmanagedManagedVsManagedManaged(long umBytes,long mmBytes) {
        Assert.True(umBytes==mmBytes,
            $"UnmanagedManaged({umBytes}) > ManagedManaged({mmBytes})");
    }

    private static partial Dictionary<string,long> GetExpectedSizes() {
        return new Dictionary<string,long> {
            ["UnmanagedUnmanaged"]=24320,
            ["UnmanagedManaged"]=24320,
            ["ManagedUnmanaged"]=24320,
            ["ManagedManaged"]=24320,
        };
        throw new InvalidOperationException("Expected constants for TSAVORITE version are not defined.");
    }
}
