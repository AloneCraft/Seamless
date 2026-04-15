namespace Test.Seamless.Collections;

public class ManagedManagedIDictionary : DictionaryTestBase<ManagedDictStrategy, string, string>;
public class ManagedUnmanagedIDictionary : DictionaryTestBase<UnmanagedValueDictStrategy, string, int>;
public class UnmanagedManagedIDictionary : DictionaryTestBase<UnmanagedKeyDictStrategy, int, string>;
public class UnmanagedUnmanagedIDictionary : DictionaryTestBase<UnmanagedDictStrategy, int, int>;
