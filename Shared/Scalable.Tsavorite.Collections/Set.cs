namespace Shared.Scalable.Tsavorite.Collections.Set;

public class SetInt32
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<Int32HashSetValueProvider,int>,int>;

public class SetString
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<StringHashSetValueProvider,string>,string>;

public class SetUnmanagedStruct
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<UnmanagedStructHashSetValueProvider,UnmanagedStruct>,UnmanagedStruct>;

public class SetManagedStruct
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<ManagedStructHashSetValueProvider,ManagedStruct>,ManagedStruct>;

public class SetManagedClass
    :TsavoriteSetScalable<MemoryPackHashSetStrategy<ManagedClassHashSetValueProvider,ManagedClass>,ManagedClass>;
