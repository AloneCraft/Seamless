using Seamless.Collections;
using Collection.Shared.Test.CollectionLinqChain1;
using Tsavorite.Collection.Shared.Strategy.CollectionLinq;

namespace Tsavorite.Collection.Shared.Test.CollectionLinqChain1;

public sealed class HashSetMemoryPackInt32
    :HashSetLinqChain1<TsavoriteHashSetCollectionLinqStrategy<MemoryPackHashSetStrategy<Int32HashSetValueProvider,int>,int>,HashSetHandle<int>,int,int,int>;

public sealed class HashSetMemoryPackString
    :HashSetLinqChain1<TsavoriteHashSetCollectionLinqStrategy<MemoryPackHashSetStrategy<StringHashSetValueProvider,string>,string>,HashSetHandle<string>,string,string,string>;

public sealed class HashSetMemoryPackUnmanagedStruct
    :HashSetLinqChain1<TsavoriteHashSetCollectionLinqStrategy<MemoryPackHashSetStrategy<UnmanagedStructHashSetValueProvider,UnmanagedStruct>,UnmanagedStruct>,HashSetHandle<UnmanagedStruct>,UnmanagedStruct,UnmanagedStruct,UnmanagedStruct>;

public sealed class HashSetMemoryPackManagedStruct
    :HashSetLinqChain1<TsavoriteHashSetCollectionLinqStrategy<MemoryPackHashSetStrategy<ManagedStructHashSetValueProvider,ManagedStruct>,ManagedStruct>,HashSetHandle<ManagedStruct>,ManagedStruct,ManagedStruct,ManagedStruct>;

public sealed class HashSetMemoryPackManagedClass
    :HashSetLinqChain1<TsavoriteHashSetCollectionLinqStrategy<MemoryPackHashSetStrategy<ManagedClassHashSetValueProvider,ManagedClass>,ManagedClass>,HashSetHandle<ManagedClass>,ManagedClass,ManagedClass,ManagedClass>;

public sealed class HashSetBlittableInt32
    :HashSetLinqChain1<TsavoriteHashSetCollectionLinqStrategy<BlittableHashSetStrategy<Int32HashSetValueProvider,int>,int>,HashSetHandle<int>,int,int,int>;

public sealed class HashSetBlittableUnmanagedStruct
    :HashSetLinqChain1<TsavoriteHashSetCollectionLinqStrategy<BlittableHashSetStrategy<UnmanagedStructHashSetValueProvider,UnmanagedStruct>,UnmanagedStruct>,HashSetHandle<UnmanagedStruct>,UnmanagedStruct,UnmanagedStruct,UnmanagedStruct>;
