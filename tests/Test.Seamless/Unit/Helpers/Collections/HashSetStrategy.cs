using Seamless;
using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Unit.Helpers.Collections;

public sealed class HashSetHandle<T>(IDisposable store,IHashSet<T> set):IDisposable
    where T : IEquatable<T> {
    public IHashSet<T> Set => set;
    public void Dispose(){
        set.Dispose();
        store.Dispose();
    }
}
public interface IHashSetValueProvider<T>
    where T : IEquatable<T> {
    static abstract T Create(int i);
}
public readonly struct Int32HashSetValueProvider:IHashSetValueProvider<int> {
    public static int Create(int i) => i;
}
public readonly struct StringHashSetValueProvider:IHashSetValueProvider<string> {
    public static string Create(int i) => i.ToString();
}
/// <summary>
/// HashSet �e�X�g�̐����헪�B
/// Dictionary �� IDictTestStrategy �Ɠ��l�ɁA�l�����ƃX�g�A�����݂̂�S���B
/// ����� ISeamlessHashSet&lt;T&gt; �𒼐ڌĂяo���B
/// </summary>
public interface IHashSetTestStrategy<T>
    where T : IEquatable<T> {
    static abstract T Elem(int i);
    static abstract HashSetHandle<T> CreateMemorySet(
        long memorySize = Const.LogMemorySize,
        double mutableFraction = 1.0);
}
public readonly struct ManagedHashSetStrategy<TProvider, T>:IHashSetTestStrategy<T>
    where TProvider : IHashSetValueProvider<T>
    where T : IEquatable<T> {
    public static T Elem(int i) => TProvider.Create(i);
    public static HashSetHandle<T> CreateMemorySet(
        long memorySize = Const.LogMemorySize,
        double mutableFraction = 1.0){
        var store = HashStore.Create(MemorySize: memorySize,MutableFraction: mutableFraction).Create<T>(new ManagedTag());
        return new HashSetHandle<T>(store,store.Create());
    }
}
public readonly struct UnmanagedHashSetStrategy<TProvider, T>:IHashSetTestStrategy<T>
    where TProvider : IHashSetValueProvider<T>
    where T : unmanaged, IEquatable<T> {
    public static T Elem(int i) => TProvider.Create(i);
    public static HashSetHandle<T> CreateMemorySet(
        long memorySize = Const.LogMemorySize,
        double mutableFraction = 1.0){
        var store = HashStore.Create(MemorySize: memorySize,MutableFraction: mutableFraction).Create<T>();
        return new HashSetHandle<T>(store,store.Create());
    }
}
