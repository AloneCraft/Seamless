namespace Persisted.Collections;

public abstract class ListBase<TStrategy, T>:ListTestBase<TStrategy,T>
    where TStrategy : IListTestStrategy<T>;

public sealed class List_String:ListBase<MemoryPackListStrategy<StringListValueProvider,string>,string>;
public sealed class List_Int32:ListBase<BlittableListStrategy<Int32ListValueProvider,int>,int>;
