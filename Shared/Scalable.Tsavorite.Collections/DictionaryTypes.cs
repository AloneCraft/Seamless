namespace Scalable.Tsavorite.Collections.Dictionary;

public sealed class DictionaryStringString
    :TsavoriteDictionaryScalable<ManagedStoreDictionaryStrategy<StringDictTypeProvider,StringDictTypeProvider,string,string>,string,string>;

public sealed class DictionaryStringInt32
    :TsavoriteDictionaryScalable<UnmanagedValueDictionaryStrategy<StringDictTypeProvider,IntDictTypeProvider,string,int>,string,int>;

public sealed class DictionaryInt32String
    :TsavoriteDictionaryScalable<UnmanagedKeyDictionaryStrategy<IntDictTypeProvider,StringDictTypeProvider,int,string>,int,string>;

public sealed class DictionaryInt32Int32
    :TsavoriteDictionaryScalable<UnmanagedStoreDictionaryStrategy<IntDictTypeProvider,IntDictTypeProvider,int,int>,int,int>;

public sealed class DictionaryUnmanagedStructUnmanagedStruct
    :TsavoriteDictionaryScalable<UnmanagedStoreDictionaryStrategy<UnmanagedStructDictTypeProvider,UnmanagedStructDictTypeProvider,UnmanagedStruct,UnmanagedStruct>,UnmanagedStruct,UnmanagedStruct>;

public sealed class DictionaryManagedStructManagedStruct
    :TsavoriteDictionaryScalable<ManagedStoreDictionaryStrategy<ManagedStructDictTypeProvider,ManagedStructDictTypeProvider,ManagedStruct,ManagedStruct>,ManagedStruct,ManagedStruct>;

public sealed class DictionaryManagedClassManagedClass
    :TsavoriteDictionaryScalable<ManagedStoreDictionaryStrategy<ManagedClassDictTypeProvider,ManagedClassDictTypeProvider,ManagedClass,ManagedClass>,ManagedClass,ManagedClass>;
