using Collection.Test.CollectionLinqChain1;
using Persisted.Collection.Strategy.CollectionLinq;
namespace Persisted.Collection.Test.CollectionLinqChain1;

public sealed class SetInt32
    :HashSetLinqChain1<PersistedSetCollectionLinqStrategy<Int32HashSetValueProvider,int>,PersistedSetLinqSource<int>,int,int,int>;

public sealed class SetString
    :HashSetLinqChain1<PersistedSetCollectionLinqStrategy<StringHashSetValueProvider,string>,PersistedSetLinqSource<string>,string,string,string>;

public sealed class SetUnmanagedStruct
    :HashSetLinqChain1<PersistedSetCollectionLinqStrategy<UnmanagedStructHashSetValueProvider,UnmanagedStruct>,PersistedSetLinqSource<UnmanagedStruct>,UnmanagedStruct,UnmanagedStruct,UnmanagedStruct>;

public sealed class SetManagedStruct
    :HashSetLinqChain1<PersistedSetCollectionLinqStrategy<ManagedStructHashSetValueProvider,ManagedStruct>,PersistedSetLinqSource<ManagedStruct>,ManagedStruct,ManagedStruct,ManagedStruct>;

public sealed class SetManagedClass
    :HashSetLinqChain1<PersistedSetCollectionLinqStrategy<ManagedClassHashSetValueProvider,ManagedClass>,PersistedSetLinqSource<ManagedClass>,ManagedClass,ManagedClass,ManagedClass>;
