namespace Shared.Scalable.Persisted.Collections.Set;

public class SetInt32
    :PersistedSetScalable<Int32HashSetValueProvider,int>;

public class SetString
    :PersistedSetScalable<StringHashSetValueProvider,string>;

public class SetUnmanagedStruct
    :PersistedSetScalable<UnmanagedStructHashSetValueProvider,UnmanagedStruct>;

public class SetManagedStruct
    :PersistedSetScalable<ManagedStructHashSetValueProvider,ManagedStruct>;

public class SetManagedClass
    :PersistedSetScalable<ManagedClassHashSetValueProvider,ManagedClass>;
