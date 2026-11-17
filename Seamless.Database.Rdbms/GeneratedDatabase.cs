namespace Seamless.Database.Rdbms;

public sealed record GeneratedDatabase(
    string SourceDatabaseName,
    string DatabaseName,
    string SchemaFingerprint,
    IReadOnlyList<GeneratedSourceFile> Files,
    IReadOnlyList<GeneratedTableBinding> Tables){
    public IReadOnlyList<GeneratedRoutineBinding> Routines{get;init;}=[];
    public string? RootNamespace{get;init;}
    public bool IsCollectible{get;init;}
    /// <summary>元の DB と同じ動きにならない所(訳せないビュー・中身の無い関数や手続き・写しにしたビューなど)と回避策。</summary>
    public IReadOnlyList<CompatibilityIssue> Compatibility{get;init;}=[];
    /// <summary>C# のビューへ訳したビュー(ビューを訳す指定で生成したときだけ)。</summary>
    public IReadOnlyList<GeneratedViewBinding> Views{get;init;}=[];
    public string TypeNamespace=>this.RootNamespace??this.DatabaseName;
}
