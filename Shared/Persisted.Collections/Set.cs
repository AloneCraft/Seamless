namespace Shared.Persisted.Collections;

public class Set {
    [BenchFact, Benchmark]
    public void ManagedSerializedSet_Distinct_SortsAndDeduplicates() {
        var path = CreatePath("managed-view-set");
        try {
            using var set = new global::Seamless.Collections.Persisted.SerializedSet<ManagedStruct>(path,initialCapacity: 1,initialDataCapacity: 128);
            set.Add(new ManagedStruct { Name="bravo" });
            set.Add(new ManagedStruct { Name="alpha" });
            set.Add(new ManagedStruct { Name="alpha" });

            var same = set.Compact();

            Assert.Same(set,same);
            Assert.Equal(2L,set.Count);
            Assert.Equal(new ManagedStruct { Name="alpha" },set[0]);
            Assert.Equal(new ManagedStruct { Name="bravo" },set[1]);
        } finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact, Benchmark]
    public void UnmanagedBlittableSet_Distinct_SortsAndDeduplicates() {
        var path = CreatePath("unmanaged-view-set");
        try {
            using var set = new global::Seamless.Collections.Persisted.BlittableSet<int>(path,initialCapacity: 1,initialDataCapacity: 128);
            set.Add(5);
            set.Add(3);
            set.Add(5);
            set.Add(1);
            set.Add(3);

            var same = set.Compact();

            Assert.Same(set,same);
            Assert.Equal(3L,set.Count);
            Assert.Equal(1,set[0]);
            Assert.Equal(3,set[1]);
            Assert.Equal(5,set[2]);
        } finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact, Benchmark]
    public void ManagedFacadeSet_UsesSerializedStorageAndReadOnlySetContract() {
        var path = CreatePath("managed-facade-set");
        try {
            using var set = new global::Seamless.Collections.Persisted.Set<ManagedStruct>(path,initialCapacity: 1,initialDataCapacity: 128);
            set.Add(new ManagedStruct { Name="bravo" });
            set.Add(new ManagedStruct { Name="alpha" });
            set.Add(new ManagedStruct { Name="alpha" });

            set.Compact();

            global::Seamless.Collections.ISet<ManagedStruct> contract = set;
            Assert.Equal(2,contract.Count);
            Assert.True(contract.Contains(new ManagedStruct { Name="alpha" }));
            Assert.True(contract.SetEquals([
                new ManagedStruct { Name = "alpha" },
                new ManagedStruct { Name = "bravo" },
            ]));
        } finally {
            TryDeleteDirectory(path);
        }
    }

    [BenchFact, Benchmark]
    public void UnmanagedFacadeSet_UsesBlittableStorageAndReadOnlySetContract() {
        var path = CreatePath("unmanaged-facade-set");
        try {
            using var set = new global::Seamless.Collections.Persisted.Set<int>(path,initialCapacity: 1,initialDataCapacity: 128);
            set.Add(5);
            set.Add(3);
            set.Add(5);
            set.Add(1);
            set.Add(3);

            set.Compact();

            global::Seamless.Collections.ISet<int> contract = set;
            Assert.Equal(3,contract.Count);
            Assert.True(contract.Contains(1));
            Assert.True(contract.SetEquals([1,3,5]));
        } finally {
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
        } catch(IOException) {
        } catch(UnauthorizedAccessException) {
        }
    }
}
