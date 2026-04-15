namespace Test.Seamless.Collections;

public abstract class ManagedDictionary<TStrategy,TKey,TValue>:DictionaryTestBase<TStrategy,TKey,TValue>
    where TStrategy:IDictTestStrategy<TKey,TValue>
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
}

public sealed class ManagedDictionary_UnmanagedStruct_ManagedStruct
    : ManagedDictionary<UnmanagedKeyDictionaryStrategy<UnmanagedStructDictTypeProvider,ManagedStructDictTypeProvider,UnmanagedStruct,ManagedStruct>,UnmanagedStruct,ManagedStruct>{
}

public sealed class ManagedDictionary_UnmanagedStruct_ManagedClass
    : ManagedDictionary<UnmanagedKeyDictionaryStrategy<UnmanagedStructDictTypeProvider,ManagedClassDictTypeProvider,UnmanagedStruct,ManagedClass>,UnmanagedStruct,ManagedClass>{
}

public sealed class ManagedDictionary_ManagedStruct_UnmanagedStruct
    : ManagedDictionary<UnmanagedValueDictionaryStrategy<ManagedStructDictTypeProvider,UnmanagedStructDictTypeProvider,ManagedStruct,UnmanagedStruct>,ManagedStruct,UnmanagedStruct>{
}

public sealed class ManagedDictionary_ManagedStruct_ManagedStruct
    : ManagedDictionary<ManagedStoreDictionaryStrategy<ManagedStructDictTypeProvider,ManagedStructDictTypeProvider,ManagedStruct,ManagedStruct>,ManagedStruct,ManagedStruct>{
}

public sealed class ManagedDictionary_ManagedStruct_ManagedClass
    : ManagedDictionary<ManagedStoreDictionaryStrategy<ManagedStructDictTypeProvider,ManagedClassDictTypeProvider,ManagedStruct,ManagedClass>,ManagedStruct,ManagedClass>{
}

public sealed class ManagedDictionary_ManagedClass_UnmanagedStruct
    : ManagedDictionary<UnmanagedValueDictionaryStrategy<ManagedClassDictTypeProvider,UnmanagedStructDictTypeProvider,ManagedClass,UnmanagedStruct>,ManagedClass,UnmanagedStruct>{
}

public sealed class ManagedDictionary_ManagedClass_ManagedStruct
    : ManagedDictionary<ManagedStoreDictionaryStrategy<ManagedClassDictTypeProvider,ManagedStructDictTypeProvider,ManagedClass,ManagedStruct>,ManagedClass,ManagedStruct>{
}

public sealed class ManagedDictionary_ManagedClass_ManagedClass
    : ManagedDictionary<ManagedStoreDictionaryStrategy<ManagedClassDictTypeProvider,ManagedClassDictTypeProvider,ManagedClass,ManagedClass>,ManagedClass,ManagedClass>{
}
