using Collection.Test.CollectionLinqChain1;
using Persisted.Collections.Strategy.CollectionLinq;
namespace Persisted.Collections.Test.CollectionLinqChain1;

public sealed class ListInt32
    :ListLinqChain1<PersistedListCollectionLinqStrategy<BlittableListStrategy<Int32ListValueProvider,int>,int>,PersistedListLinqSource<int>,int,int,int>;

public sealed class ListString
    :ListLinqChain1<PersistedListCollectionLinqStrategy<MemoryPackListStrategy<StringListValueProvider,string>,string>,PersistedListLinqSource<string>,string,string,string>;
