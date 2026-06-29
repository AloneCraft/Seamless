using Collection.Shared.Test.CollectionLinqChain1;
using Persisted.Collection.Shared.Strategy.CollectionLinq;
using Seamless.Collections;

namespace Persisted.Collection.Shared.Test.CollectionLinqChain1;

public sealed class ListInt32
    :ListLinqChain1<PersistedListCollectionLinqStrategy<BlittableListStrategy<Int32ListValueProvider,int>,int>,PersistedListLinqSource<int>,int,int,int>;

public sealed class ListString
    :ListLinqChain1<PersistedListCollectionLinqStrategy<MemoryPackListStrategy<StringListValueProvider,string>,string>,PersistedListLinqSource<string>,string,string,string>;
