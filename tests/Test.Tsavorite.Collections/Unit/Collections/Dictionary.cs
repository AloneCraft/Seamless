using Test.Tsavorite.Collections.Unit.Helpers.Collections;

namespace Test.Tsavorite.Collections.Unit.Collections;

public class DictionaryStringString : DictionaryTestBase<ManagedStoreDictionaryStrategy<StringDictTypeProvider, StringDictTypeProvider, string, string>, string, string>;
public class DictionaryStringInt32 : DictionaryTestBase<UnmanagedValueDictionaryStrategy<StringDictTypeProvider, IntDictTypeProvider, string, int>, string, int>;
public class DictionaryInt32String : DictionaryTestBase<UnmanagedKeyDictionaryStrategy<IntDictTypeProvider, StringDictTypeProvider, int, string>, int, string>;
public class DictionaryInt32Int32 : DictionaryTestBase<UnmanagedStoreDictionaryStrategy<IntDictTypeProvider, IntDictTypeProvider, int, int>, int, int>;

