namespace Seamless.Database.Rdbms;

/// <summary>訳に渡す列。名前は生成器が付けた C# の名前。</summary>
public sealed record ViewTranslationColumn(string Name,string CSharpName,DatabaseType Type);

/// <summary>
/// 訳の中で FROM に書ける表やビュー。<paramref name="Access"/> は読み取りの入口 q からたどる式(q を除く)、
/// <paramref name="RowType"/> は行の型の完全な名前(global:: から)。どちらも生成器が出すコードの名前に合わせる。
/// </summary>
public sealed record ViewTranslationRelation(string Schema,string Name,string Access,string RowType,IReadOnlyList<ViewTranslationColumn> Columns,bool IsView);

/// <summary>訳すビュー1つ。</summary>
public sealed record ViewTranslationTarget(string Schema,ViewCatalog View);

/// <summary>
/// ビューの訳の頼み。生成器が自分の名前の付け方で作って、<see cref="IViewTranslator"/> に渡す。
/// </summary>
/// <param name="RootNamespace">生成されるコードのルートの名前空間。</param>
/// <param name="ContainerType">コンテナの型の完全な名前(global:: から)。</param>
/// <param name="DatabaseCollation">DB の既定の照合順序。</param>
/// <param name="SchemaIdentifiers">SQL 上のスキーマ名から、生成器が付けた C# の名前への対応。</param>
/// <param name="Relations">FROM に書ける表とビュー(写しにするビューは表として入る)。</param>
/// <param name="Views">訳すビュー。</param>
public sealed record ViewTranslationRequest(
    string RootNamespace,
    string ContainerType,
    string DatabaseCollation,
    IReadOnlyDictionary<string,string> SchemaIdentifiers,
    IReadOnlyList<ViewTranslationRelation> Relations,
    IReadOnlyList<ViewTranslationTarget> Views);

/// <summary>
/// ビューの訳の結果。<paramref name="Translated"/> に入ったビューだけが <paramref name="Source"/> に含まれる。
/// 訳せなかったビューは <paramref name="Issues"/> に理由と回避策を持つ。
/// </summary>
public sealed record ViewTranslationResult(
    IReadOnlyList<ViewTranslationTarget> Translated,
    GeneratedSourceFile? Source,
    IReadOnlyList<CompatibilityIssue> Issues);

/// <summary>
/// SQL のビューの定義を、生成されるコンテナの読み取りの入口への拡張プロパティ(C#)へ訳す部品。
/// 製品ごとに作る(SQL Server は Seamless.Database.SqlServer の SqlServerViewTranslator)。
/// </summary>
public interface IViewTranslator{
    /// <summary>訳の規則の版。生成物の指紋に混ぜる(版が変われば生成物を作り直す)。</summary>
    string Version{get;}
    /// <summary>ビューを訳す。訳せないビューは例外にせず、結果の Issues に入れる。</summary>
    ViewTranslationResult Translate(ViewTranslationRequest request);
}

/// <summary>
/// 取り込みでのビューの扱い。ビューは訳して C# のビューにする。訳せないビューは取り込まず、
/// 回避策つきで報告する。<see cref="SnapshotViews"/> に挙げたビューだけは、利用者が明示して選んだものとして、
/// 取り込んだ時点の結果を表(写し)として残す。
/// </summary>
public sealed record ViewImportOptions(IViewTranslator Translator){
    /// <summary>写しにするビュー(「スキーマ.名前」、SQL 上の名前。大文字小文字は区別しない)。</summary>
    public IReadOnlyCollection<string> SnapshotViews{get;init;}=[];
}
