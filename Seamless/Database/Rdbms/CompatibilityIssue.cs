namespace Seamless.Database.Rdbms;

/// <summary>元の DB と同じ動きにならない所が、どの種類のオブジェクトにあるか。</summary>
public enum CompatibilityObjectKind{
    Table,
    Column,
    View,
    ScalarFunction,
    TableFunction,
    StoredProcedure,
    Trigger,
    Constraint,
}

/// <summary>
/// 回避策の候補。<paramref name="Draft"/> は利用者が手で直して使う下書き(C# や SQL)。
/// 回避策は黙って当てない。利用者が選んだものだけを使う。
/// </summary>
/// <param name="Code">回避策の種類(画面の文言と選択の識別に使う)。</param>
/// <param name="Description">何をすれば、何が同じになり、何が残るか。</param>
/// <param name="Draft">下書き。無ければ null。</param>
public sealed record CompatibilityWorkaround(string Code,string Description,string? Draft=null);

/// <summary>
/// 取り込みや書き出しで、元の DB と同じ動きにならない所1つ。下見で示し、処理の最後の報告にも残す。
/// </summary>
/// <param name="Kind">オブジェクトの種類。</param>
/// <param name="Schema">元の DB のスキーマ名。</param>
/// <param name="Name">元の DB のオブジェクト名。</param>
/// <param name="Column">列に限られるときの列名。無ければ null。</param>
/// <param name="Code">差の種類(翻訳器の診断のコードなど)。</param>
/// <param name="Difference">何が同じにならないか。</param>
/// <param name="Workarounds">回避策の候補。</param>
public sealed record CompatibilityIssue(
    CompatibilityObjectKind Kind,
    string Schema,
    string Name,
    string? Column,
    string Code,
    string Difference,
    IReadOnlyList<CompatibilityWorkaround> Workarounds);
