namespace Seamless.Persisted.Collections;

public static class Store {
    public static PersistedList<T> CreateList<T>(
        string Path,
        long InitialCapacity = 16,
        long InitialDataCapacity = 4096,
        StorageTuning? StorageTuning = null) =>
        RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            ? new SerializedList<T,MemoryPackSerializer<T>>(Path,InitialCapacity,InitialDataCapacity,StorageTuning)
            : CreateBlittableList<T>(Path,InitialCapacity,StorageTuning);

    public static Set<T> CreateSet<T>(
        string Path,
        long InitialCapacity = 16,
        long InitialDataCapacity = 4096,
        StorageTuning? StorageTuning = null) =>
        new(Path,InitialCapacity,InitialDataCapacity,StorageTuning);

    public static Dictionary<TKey,TValue> CreateDictionary<TKey,TValue>(
        string Path,
        long InitialCapacity = 16,
        long InitialDataCapacity = 4096,
        StorageTuning? StorageTuning = null)
        where TKey : notnull =>
        new(Path,InitialCapacity,InitialDataCapacity,StorageTuning);

    public static AppendOnlyList<T> CreateAppendOnlyList<T>(
        string Path,
        long InitialDataCapacity = 4096,
        StorageTuning? StorageTuning = null) =>
        new(Path,InitialDataCapacity,StorageTuning);

    public static PersistedList<T> CreateTempList<T>(
        long InitialCapacity = 16,
        long InitialDataCapacity = 4096,
        StorageTuning? StorageTuning = null,
        string? TemporaryRoot = null) {
        var path=CreateTemporaryPath(TemporaryRoot);
        var list=CreateList<T>(path,InitialCapacity,InitialDataCapacity,StorageTuning);
        list.SetDisposeAction(()=>DeleteTemporaryDirectory(path));
        return list;
    }

    public static Set<T> CreateTempSet<T>(
        long InitialCapacity = 16,
        long InitialDataCapacity = 4096,
        StorageTuning? StorageTuning = null,
        string? TemporaryRoot = null) {
        var path=CreateTemporaryPath(TemporaryRoot);
        var set=CreateSet<T>(path,InitialCapacity,InitialDataCapacity,StorageTuning);
        set.SetDisposeAction(()=>DeleteTemporaryDirectory(path));
        return set;
    }

    public static Dictionary<TKey,TValue> CreateTempDictionary<TKey,TValue>(
        long InitialCapacity = 16,
        long InitialDataCapacity = 4096,
        StorageTuning? StorageTuning = null,
        string? TemporaryRoot = null)
        where TKey : notnull {
        var path=CreateTemporaryPath(TemporaryRoot);
        var dictionary=CreateDictionary<TKey,TValue>(path,InitialCapacity,InitialDataCapacity,StorageTuning);
        dictionary.SetDisposeAction(()=>DeleteTemporaryDirectory(path));
        return dictionary;
    }

    public static AppendOnlyList<T> CreateTempAppendOnlyList<T>(
        long InitialDataCapacity = 4096,
        StorageTuning? StorageTuning = null,
        string? TemporaryRoot = null) {
        var path=CreateTemporaryPath(TemporaryRoot);
        var list=CreateAppendOnlyList<T>(path,InitialDataCapacity,StorageTuning);
        list.SetDisposeAction(()=>DeleteTemporaryDirectory(path));
        return list;
    }

    private static PersistedList<T> CreateBlittableList<T>(string path,long initialCapacity,StorageTuning? tuning) =>
        (PersistedList<T>)Activator.CreateInstance(
            typeof(BlittableList<>).MakeGenericType(typeof(T)),
            path,
            initialCapacity,
            tuning)!;

    private static string CreateTemporaryPath(string? temporaryRoot) {
        var root=string.IsNullOrWhiteSpace(temporaryRoot)
            ? Path.Combine(Path.GetTempPath(),"Seamless","Persisted")
            : temporaryRoot!;
        var directory=Path.Combine(root,Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,"items.dat");
    }

    private static void DeleteTemporaryDirectory(string path) {
        var directory=Path.GetDirectoryName(path);
        if(string.IsNullOrWhiteSpace(directory)) {
            return;
        }
        try {
            Directory.Delete(directory,recursive: true);
        }
        catch(IOException) {
        }
        catch(UnauthorizedAccessException) {
        }
    }
}
