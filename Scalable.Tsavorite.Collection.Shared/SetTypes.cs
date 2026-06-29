using Seamless.Collections;

namespace Scalable.Tsavorite.Collection.Set;

public sealed class SetInt32
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<Int32HashSetValueProvider,int>,int>;

public sealed class SetString
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<StringHashSetValueProvider,string>,string>;

public sealed class SetUnmanagedStruct
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<UnmanagedStructHashSetValueProvider,UnmanagedStruct>,UnmanagedStruct>;

public sealed class SetManagedStruct
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<ManagedStructHashSetValueProvider,ManagedStruct>,ManagedStruct>;

public sealed class SetManagedClass
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<ManagedClassHashSetValueProvider,ManagedClass>,ManagedClass>;
