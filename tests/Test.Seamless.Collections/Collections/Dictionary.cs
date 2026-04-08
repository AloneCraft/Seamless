using Seamless;
using Seamless.Collections;
using Seamless.Store;

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Tsavorite.core;
namespace Test.Seamless.Collections;

public class Dictionary               : DictionaryTestBase<ManagedDictStrategy,          int,    string> { }
public class Dictionary_Unmanaged     : DictionaryTestBase<UnmanagedDictStrategy,         int,    int>    { }
public class Dictionary_UnmanagedKey  : DictionaryTestBase<UnmanagedKeyDictStrategy,      int,    string> { }
public class Dictionary_UnmanagedValue: DictionaryTestBase<UnmanagedValueDictStrategy,    string, int>    { }
