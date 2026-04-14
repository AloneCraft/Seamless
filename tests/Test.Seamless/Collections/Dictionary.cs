namespace Test.Seamless.Collections;

public class Dictionary:DictionaryTestBase<ManagedDictStrategy,string,string>{}
public class Dictionary_Unmanaged:DictionaryTestBase<UnmanagedDictStrategy,int,int>{}
public class Dictionary_UnmanagedKey:DictionaryTestBase<UnmanagedKeyDictStrategy,int,string>{}
public class Dictionary_UnmanagedValue:DictionaryTestBase<UnmanagedValueDictStrategy,string,int>{}
