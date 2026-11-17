using EqualityEmpty = Seamless.Collections.Tsavorite.EqualityEmpty;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SpanByteStoreAllocator = Tsavorite.core.SpanByteAllocator<
    Tsavorite.core.StoreFunctions<
        Tsavorite.core.SpanByteComparer,
        Tsavorite.core.DefaultRecordTriggers
    >
>;
using SpanByteStoreFunctions = Tsavorite.core.StoreFunctions<
    Tsavorite.core.SpanByteComparer,
    Tsavorite.core.DefaultRecordTriggers
>;
using Garnet.common;
using Seamless.Collections;
using Tsavorite.core;
namespace Seamless.Collections.Tsavorite;

/// <summary>
/// 追加でメモリ上のログが満杯になった。ほかの原因のメモリ不足と見分けるための派生(呼び出し側が区間を閉じてログを広げ、やり直せる)。
/// 既存の <see cref="OutOfMemoryException"/> の捕捉はそのまま効く。
/// </summary>
public sealed class TsavoriteLogFullException(string message):OutOfMemoryException(message);
