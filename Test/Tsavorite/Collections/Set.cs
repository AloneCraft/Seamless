using MemoryPack;

using Test.Tsavorite.Collections;

using Tsavorite.Collections;
using Tsavorite.Stores;

namespace Test.Tsavorite.Collections;

[MemoryPackable]
public partial record struct ManagedSetRecord(int Key,string Name):IRecord<int>;

public readonly record struct UnmanagedSetRecord(int Key,long Value):IRecord<int>;

public class Set {
    [Fact, Trait("Category","Unit")]
    public void ManagedSet_AddTryGetRemove_UsesRecordKey() {
        using var store = Store.Create().CreateSet<int,ManagedSetRecord>();
        using var set = store.Create();

        var record = new ManagedSetRecord(1,"alpha");

        Assert.True(set.Add(record));
        Assert.False(set.Add(new ManagedSetRecord(1,"beta")));
        Assert.True(set.ContainsKey(1));
        var containsDuplicateKey = set.Contains(new ManagedSetRecord(1,"ignored"));
        Assert.True(containsDuplicateKey);
        Assert.True(set.TryGetValue(1,out var found));
        Assert.Equal(record,found);
        Assert.True(set.Remove(1,out var removed));
        Assert.Equal(record,removed);
        Assert.False(set.ContainsKey(1));
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedSet_UnionWith_KeepsExistingRecordForDuplicateKey() {
        using var store = Store.Create().CreateSet<int,ManagedSetRecord>();
        using var otherStore = Store.Create().CreateSet<int,ManagedSetRecord>();
        using var set = store.Create();
        using var other = otherStore.Create();

        var original = new ManagedSetRecord(1,"alpha");
        var duplicate = new ManagedSetRecord(1,"beta");
        var added = new ManagedSetRecord(2,"gamma");

        set.Add(original);
        other.Add(duplicate);
        other.Add(added);

        set.UnionWith(other);

        Assert.Equal(2,set.Count);
        Assert.True(set.TryGetValue(1,out var foundOriginal));
        Assert.Equal(original,foundOriginal);
        Assert.True(set.TryGetValue(2,out var foundAdded));
        Assert.Equal(added,foundAdded);
    }

    [Fact, Trait("Category","Unit")]
    public void UnmanagedSet_AddTryGetRemove_UsesRecordKey() {
        using var store = Store.Create().CreateSet<int,UnmanagedSetRecord>();
        using var set = store.Create();

        var record = new UnmanagedSetRecord(7,99);

        Assert.True(set.Add(record));
        Assert.False(set.Add(new UnmanagedSetRecord(7,100)));
        Assert.True(set.ContainsKey(7));
        Assert.True(set.TryGetValue(7,out var found));
        Assert.Equal(record,found);
        Assert.True(set.Remove(7,out var removed));
        Assert.Equal(record,removed);
        Assert.False(set.ContainsKey(7));
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedSet_FunctionalOperations_WithManagedSet_KeepReturnTypesAndValues() {
        using var store = Store.Create().CreateSet<int,ManagedSetRecord>();
        using var otherStore = Store.Create().CreateSet<int,ManagedSetRecord>();
        using var set = store.Create();
        using var other = otherStore.Create();

        var alpha = new ManagedSetRecord(1,"alpha");
        var bravo = new ManagedSetRecord(2,"bravo");
        var duplicateBravo = new ManagedSetRecord(2,"shadow");
        var charlie = new ManagedSetRecord(3,"charlie");

        set.Add(alpha);
        set.Add(bravo);
        other.Add(duplicateBravo);
        other.Add(charlie);

        using var union = set.Union(other);
        Assert.IsType<Set<int,ManagedSetRecord>>(union);
        Assert.Equal(3,union.Count);
        Assert.True(union.TryGetValue(2,out var foundBravo));
        Assert.Equal(bravo,foundBravo);
        Assert.True(union.TryGetValue(3,out var foundCharlie));
        Assert.Equal(charlie,foundCharlie);

        using ISet<int,ManagedSetRecord> intersect = set.Intersect((ISet<int,ManagedSetRecord>)other);
        Assert.Single(intersect);
        Assert.True(intersect.TryGetValue(2,out var intersected));
        Assert.Equal(bravo,intersected);

        using var except = set.Except(other);
        Assert.Equal(1,except.Count);
        Assert.True(except.TryGetValue(1,out var excepted));
        Assert.Equal(alpha,excepted);

        using var symmetric = set.SymmetricExcept(other);
        Assert.Equal(2,symmetric.Count);
        Assert.True(symmetric.TryGetValue(1,out var symmetricAlpha));
        Assert.Equal(alpha,symmetricAlpha);
        Assert.True(symmetric.TryGetValue(3,out var symmetricCharlie));
        Assert.Equal(charlie,symmetricCharlie);

        Assert.Equal(2,set.Count);
        Assert.Equal(2,other.Count);
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedSet_FunctionalOperations_WithManagedHashSet_KeepReturnTypesAndValues() {
        using var store = Store.Create().CreateSet<int,ManagedSetRecord>();
        using var set = store.Create();
        using var keyStore = Store.Create().CreateHashSet<int>();
        using var keys = keyStore.Create();

        set.Add(new ManagedSetRecord(1,"alpha"));
        set.Add(new ManagedSetRecord(2,"bravo"));
        keys.Add(2);
        keys.Add(3);

        using var union = set.Union(keys);
        Assert.IsType<global::Tsavorite.Collections.HashSet<int>>(union);
        Assert.Equal(3,union.Count);
        Assert.True(union.Contains(1));
        Assert.True(union.Contains(2));
        Assert.True(union.Contains(3));

        using IHashSet<int> intersect = set.Intersect((IHashSet<int>)keys);
        Assert.Single(intersect);
        Assert.True(intersect.Contains(2));

        using var except = set.Except(keys);
        Assert.Single(except);
        Assert.True(except.Contains(1));

        using var symmetric = set.SymmetricExcept(keys);
        Assert.Equal(2,symmetric.Count);
        Assert.True(symmetric.Contains(1));
        Assert.True(symmetric.Contains(3));
    }

    [Fact, Trait("Category","Unit")]
    public void UnmanagedSet_FunctionalOperations_WithUnmanagedSet_KeepReturnTypesAndValues() {
        using var store = Store.Create().CreateSet<int,UnmanagedSetRecord>();
        using var otherStore = Store.Create().CreateSet<int,UnmanagedSetRecord>();
        using var set = store.Create();
        using var other = otherStore.Create();

        var alpha = new UnmanagedSetRecord(1,10);
        var bravo = new UnmanagedSetRecord(2,20);
        var duplicateBravo = new UnmanagedSetRecord(2,99);
        var charlie = new UnmanagedSetRecord(3,30);

        set.Add(alpha);
        set.Add(bravo);
        other.Add(duplicateBravo);
        other.Add(charlie);

        using var union = set.Union(other);
        Assert.IsType<Set<int,UnmanagedSetRecord>>(union);
        Assert.Equal(3,union.Count);
        Assert.True(union.TryGetValue(2,out var foundBravo));
        Assert.Equal(bravo,foundBravo);
        Assert.True(union.TryGetValue(3,out var foundCharlie));
        Assert.Equal(charlie,foundCharlie);

        using ISet<int,UnmanagedSetRecord> intersect = set.Intersect((ISet<int,UnmanagedSetRecord>)other);
        Assert.Single(intersect);
        Assert.True(intersect.TryGetValue(2,out var intersected));
        Assert.Equal(bravo,intersected);

        using var except = set.Except(other);
        Assert.Single(except);
        Assert.True(except.TryGetValue(1,out var excepted));
        Assert.Equal(alpha,excepted);

        using var symmetric = set.SymmetricExcept(other);
        Assert.Equal(2,symmetric.Count);
        Assert.True(symmetric.TryGetValue(1,out var symmetricAlpha));
        Assert.Equal(alpha,symmetricAlpha);
        Assert.True(symmetric.TryGetValue(3,out var symmetricCharlie));
        Assert.Equal(charlie,symmetricCharlie);
    }

    [Fact, Trait("Category","Unit")]
    public void UnmanagedSet_FunctionalOperations_WithUnmanagedHashSet_KeepReturnTypesAndValues() {
        using var store = Store.Create().CreateSet<int,UnmanagedSetRecord>();
        using var set = store.Create();
        using var keyStore = Store.Create().Create<int>(new BlittableStrategy());
        using var keys = keyStore.Create();

        set.Add(new UnmanagedSetRecord(1,10));
        set.Add(new UnmanagedSetRecord(2,20));
        keys.Add(2);
        keys.Add(3);

        using var union = set.Union(keys);
        Assert.IsType<global::Tsavorite.Collections.HashSet<int>>(union);
        Assert.Equal(3,union.Count);
        Assert.True(union.Contains(1));
        Assert.True(union.Contains(2));
        Assert.True(union.Contains(3));

        using IHashSet<int> intersect = set.Intersect((IHashSet<int>)keys);
        Assert.Single(intersect);
        Assert.True(intersect.Contains(2));

        using var except = set.Except(keys);
        Assert.Single(except);
        Assert.True(except.Contains(1));

        using var symmetric = set.SymmetricExcept(keys);
        Assert.Equal(2,symmetric.Count);
        Assert.True(symmetric.Contains(1));
        Assert.True(symmetric.Contains(3));
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedSetOfT_Distinct_SortsAndDeduplicates() {
        var path = CreatePath("managed-view-set");
        try {
            using var set = new global::Persistent.Collections.Set<string>(path,initialCapacity: 1,initialDataCapacity: 128);
            set.Add("bravo");
            set.Add("alpha");
            set.Add("alpha");

            var same = set.Distinct();

            Assert.Same(set,same);
            Assert.Equal(2L,set.Count);
            Assert.Equal("alpha",set[0]);
            Assert.Equal("bravo",set[1]);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void UnmanagedSetOfT_Distinct_SortsAndDeduplicates() {
        var path = CreatePath("unmanaged-view-set");
        try {
            using var set = new global::Persistent.Collections.Set<int>(path,initialCapacity: 1,initialDataCapacity: 128);
            set.Add(5);
            set.Add(3);
            set.Add(5);
            set.Add(1);
            set.Add(3);

            var same = set.Distinct();

            Assert.Same(set,same);
            Assert.Equal(3L,set.Count);
            Assert.Equal(1,set[0]);
            Assert.Equal(3,set[1]);
            Assert.Equal(5,set[2]);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    private static string CreatePath(string tag) {
        var directoryPath = TestPath.Directory<Set>(tag);
        return Path.Combine(directoryPath,"items.dat");
    }

    private static void TryDeleteDirectory(string path) {
        var directoryPath = Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath))
            return;
        try {
            Directory.Delete(directoryPath,recursive: true);
        }
        catch(IOException) {
        }
        catch(UnauthorizedAccessException) {
        }
    }
}
