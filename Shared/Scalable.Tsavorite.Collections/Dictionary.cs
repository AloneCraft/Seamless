namespace Scalable.Tsavorite.Collections.Dictionary;

public class DictionaryStringString
    :TsavoriteDictionaryScalable<ManagedStoreDictionaryStrategy<StringDictTypeProvider,StringDictTypeProvider,string,string>,string,string>;

public class DictionaryStringInt32
    :TsavoriteDictionaryScalable<UnmanagedValueDictionaryStrategy<StringDictTypeProvider,IntDictTypeProvider,string,int>,string,int>;

public class DictionaryInt32String
    :TsavoriteDictionaryScalable<UnmanagedKeyDictionaryStrategy<IntDictTypeProvider,StringDictTypeProvider,int,string>,int,string>;

public class DictionaryInt32Int32
    :TsavoriteDictionaryScalable<UnmanagedStoreDictionaryStrategy<IntDictTypeProvider,IntDictTypeProvider,int,int>,int,int>;

public class DictionaryUnmanagedStructUnmanagedStruct
    :TsavoriteDictionaryScalable<UnmanagedStoreDictionaryStrategy<UnmanagedStructDictTypeProvider,UnmanagedStructDictTypeProvider,UnmanagedStruct,UnmanagedStruct>,UnmanagedStruct,UnmanagedStruct>;

public class DictionaryManagedStructManagedStruct
    :TsavoriteDictionaryScalable<ManagedStoreDictionaryStrategy<ManagedStructDictTypeProvider,ManagedStructDictTypeProvider,ManagedStruct,ManagedStruct>,ManagedStruct,ManagedStruct>;

public class DictionaryManagedClassManagedClass
    :TsavoriteDictionaryScalable<ManagedStoreDictionaryStrategy<ManagedClassDictTypeProvider,ManagedClassDictTypeProvider,ManagedClass,ManagedClass>,ManagedClass,ManagedClass>;
