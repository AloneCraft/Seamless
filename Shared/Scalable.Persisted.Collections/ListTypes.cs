namespace Scalable.Persisted.Collections.List;

public sealed class ListInt32
    :PersistedListScalable<BlittableListStrategy<Int32ListValueProvider,int>,int>;

public sealed class ListString
    :PersistedListScalable<MemoryPackListStrategy<StringListValueProvider,string>,string>;
