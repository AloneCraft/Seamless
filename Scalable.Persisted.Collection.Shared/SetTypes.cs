using Seamless.Collections;

namespace Scalable.Persisted.Collection.Set;

public sealed class SetInt32
    :PersistedSetScalable<Int32HashSetValueProvider,int>;

public sealed class SetString
    :PersistedSetScalable<StringHashSetValueProvider,string>;

public sealed class SetUnmanagedStruct
    :PersistedSetScalable<UnmanagedStructHashSetValueProvider,UnmanagedStruct>;

public sealed class SetManagedStruct
    :PersistedSetScalable<ManagedStructHashSetValueProvider,ManagedStruct>;

public sealed class SetManagedClass
    :PersistedSetScalable<ManagedClassHashSetValueProvider,ManagedClass>;
