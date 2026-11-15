namespace Seamless.Collections.Tsavorite;

/// <summary>
/// 追加でメモリ上のログが満杯になった。ほかの原因のメモリ不足と見分けるための派生(呼び出し側が区間を閉じてログを広げ、やり直せる)。
/// 既存の <see cref="OutOfMemoryException"/> の捕捉はそのまま効く。
/// </summary>
public sealed class TsavoriteLogFullException(string message):OutOfMemoryException(message);
