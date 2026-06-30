namespace Scalable.Persisted.Collections.List;

public class ListInt32
    :PersistedListScalable<BlittableListStrategy<Int32ListValueProvider,int>,int>;

public class ListString
    :PersistedListScalable<MemoryPackListStrategy<StringListValueProvider,string>,string>;
