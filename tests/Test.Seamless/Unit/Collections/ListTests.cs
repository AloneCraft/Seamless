using IntList = global::Seamless.Collections.UnmanagedList<int>;
using StringList = global::Seamless.Collections.ManagedList<string>;

namespace Test.Seamless.Unit.Collections;

public sealed class ListTests {
    private static string CreatePath() {
        var directoryPath = Path.Combine(Environment.CurrentDirectory,nameof(ListTests));
        Directory.CreateDirectory(directoryPath);
        return Path.Combine(directoryPath,"list.dat");
    }

    [Fact, Trait("Category","Unit")]
    public void Add_Enumerate_AndIndexer() {
        var path = CreatePath();
        try {
            using var list = new IntList(path,initialCapacity: 2);
            list.Add(10);
            list.Add(20);
            list.Add(30);

            Assert.Equal(3L,list.Count);
            Assert.Equal(10,list[0]);
            Assert.Equal(20,list[1]);
            Assert.Equal(30,list[2]);

            var items = list.ToArray();
            Assert.True(items.SequenceEqual(new[] { 10,20,30 }));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Remove_DeletesTailOnly() {
        var path = CreatePath();
        try {
            using var list = new IntList(path);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var removed = list.Remove();

            Assert.True(removed);
            Assert.Equal(2L,list.Count);
            Assert.True(list.ToArray().SequenceEqual(new[] { 1,2 }));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void DuplicateRemoval_SortsAndDeduplicates() {
        var path = CreatePath();
        try {
            using var list = new IntList(path,initialCapacity: 1);
            list.Add(5);
            list.Add(3);
            list.Add(5);
            list.Add(1);
            list.Add(3);

            list.DuplicateRemoval();

            Assert.True(list.ToArray().SequenceEqual(new[] { 1,3,5 }));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void Reopen_PreservesCountAndValues() {
        var path = CreatePath();
        try {
            using(var list = new IntList(path,initialCapacity: 1)) {
                for(var i = 0; i<8; i++) {
                    list.Add(i*2);
                }
            }

            using var reopened = new IntList(path);
            Assert.Equal(8L,reopened.Count);
            Assert.True(reopened.ToArray().SequenceEqual(new[] { 0,2,4,6,8,10,12,14 }));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedList_AddT_AddSpanByte_AndDuplicateRemoval() {
        var path = CreatePath();
        try {
            using var list = new StringList(path,initialCapacity: 1,initialDataCapacity: 32);
            list.Add("bravo");
            var alpha = "alpha";
            var alphaBytes = global::Seamless.Collections.ManagedSerializer<string>.Serialize(ref alpha).ToArray();
#if TSAVORITE1_1_1
            list.Add(global::Tsavorite.core.SpanByte.FromPinnedSpan(alphaBytes));
#else
            list.Add(alphaBytes);
#endif
            list.Add(alpha);

            list.DuplicateRemoval();

            Assert.Equal(2L,list.Count);
            Assert.Equal("alpha",list[0]);
            Assert.Equal("bravo",list[1]);
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    [Fact, Trait("Category","Unit")]
    public void ManagedList_Reopen_PreservesCountAndValues() {
        var path = CreatePath();
        try {
            using(var list = new StringList(path,initialCapacity: 1,initialDataCapacity: 32)) {
                list.Add("x");
                list.Add("yy");
                list.Add("zzz");
            }

            using var reopened = new StringList(path);
            Assert.Equal(3L,reopened.Count);
            Assert.True(reopened.ToArray().SequenceEqual(new[] { "x","yy","zzz" }));
        }
        finally {
            TryDeleteDirectory(path);
        }
    }

    private static void TryDeleteDirectory(string path) {
        var directoryPath = Path.GetDirectoryName(path);
        if(string.IsNullOrEmpty(directoryPath))
            return;
        try {
            Directory.Delete(directoryPath,recursive: true);
        }
        catch {
        }
    }
}
