using Seamless.Collections;
using Seamless.Collections.Persisted;
namespace Seamless.TestKit;

public sealed class ListHandle<T>(string path,PersistedList<T> list):IDisposable {
    public PersistedList<T> List => list;
    public string Path => path;
    public void Dispose() => list.Dispose();
}

public interface IListValueProvider<T> {
    static abstract T Create(int i);
}

public readonly struct Int32ListValueProvider:IListValueProvider<int> {
    public static int Create(int i) => i;
}

public readonly struct StringListValueProvider:IListValueProvider<string> {
    public static string Create(int i) => i.ToString();
}

public interface IListTestStrategy<T> {
    static abstract T Elem(int i);
    static abstract ListHandle<T> CreateList(string path,long initialCapacity = 16,long initialDataCapacity = 4096);
}

public readonly struct MemoryPackListStrategy<TProvider, T>:IListTestStrategy<T>
    where TProvider : IListValueProvider<T> {
    public static T Elem(int i) => TProvider.Create(i);
    public static ListHandle<T> CreateList(string path,long initialCapacity = 16,long initialDataCapacity = 4096) {
        var list = global::Seamless.Collections.Persisted.Store.CreateList<T>(path,InitialCapacity: initialCapacity,InitialDataCapacity: initialDataCapacity);
        return new ListHandle<T>(path,list);
    }
}

public readonly struct BlittableListStrategy<TProvider, T>:IListTestStrategy<T>
    where TProvider : IListValueProvider<T>
    where T : unmanaged {
    public static T Elem(int i) => TProvider.Create(i);
    public static ListHandle<T> CreateList(string path,long initialCapacity = 16,long initialDataCapacity = 4096) {
        var list = global::Seamless.Collections.Persisted.Store.CreateList<T>(path,InitialCapacity: initialCapacity,InitialDataCapacity: initialDataCapacity);
        return new ListHandle<T>(path,list);
    }
}
