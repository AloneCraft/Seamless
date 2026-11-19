using Seamless.Collections.Tsavorite;
using Tsavorite.core;
namespace Seamless.Database;

/// <summary>
/// ビュー・関数を名前で呼ぶための入口。DB生成器が、生成される <c>IReader</c> への拡張メンバー(ビューはプロパティ、関数はメソッド)ごとに作る。
/// </summary>
/// <remarks>
/// 利用者のコードはビュー・関数を拡張メンバーとして直接呼び、FLinq が呼び出し側へ展開する。
/// この入口は、名前と引数を実行時に受け取る Manager・Gateway のためだけにある。
/// 呼ぶたびに読み込み専用の入口(<c>BeginRead</c>)を開き、結果を読み終えて <see cref="DatabaseQueryResult.Dispose"/> するまで開いたままにする。
/// </remarks>
public sealed class DatabaseQuery{
    private readonly Func<DatabaseContainer,object?[],DatabaseQueryResult> execute;

    public DatabaseQuery(string name,global::Seamless.Database.System.DatabaseObjectKind kind,Type resultType,DatabaseQueryParameter[] parameters,
        Func<DatabaseContainer,object?[],DatabaseQueryResult> execute){
        ArgumentException.ThrowIfNullOrWhiteSpace(name);ArgumentNullException.ThrowIfNull(resultType);
        ArgumentNullException.ThrowIfNull(parameters);ArgumentNullException.ThrowIfNull(execute);
        if(kind is not(global::Seamless.Database.System.DatabaseObjectKind.View or global::Seamless.Database.System.DatabaseObjectKind.TableFunction or global::Seamless.Database.System.DatabaseObjectKind.ScalarFunction or global::Seamless.Database.System.DatabaseObjectKind.Property)){
            throw new ArgumentOutOfRangeException(nameof(kind),kind,"A query is a view, a function or a property.");
        }
        if((kind is global::Seamless.Database.System.DatabaseObjectKind.View or global::Seamless.Database.System.DatabaseObjectKind.Property)&&parameters.Length!=0){throw new ArgumentException("A view or property has no parameters.",nameof(parameters));}
        this.Name=name;this.Kind=kind;this.ResultType=resultType;this.Parameters=parameters;this.execute=execute;
    }

    /// <summary>目録(<c>DatabaseObjects</c>)と同じ名前。スキーマに属するものは「スキーマ.名前」。</summary>
    public string Name{get;}
    public global::Seamless.Database.System.DatabaseObjectKind Kind{get;}
    /// <summary>ビュー・テーブル関数は行型、スカラー関数は戻り値の型。</summary>
    public Type ResultType{get;}
    public IReadOnlyList<DatabaseQueryParameter> Parameters{get;}

    /// <summary>読み込み専用の入口を開いて呼ぶ。引数は <see cref="Parameters"/> の型の値で、数と順を合わせる。</summary>
    public DatabaseQueryResult Execute(DatabaseContainer database,params object?[] arguments){
        ArgumentNullException.ThrowIfNull(database);ArgumentNullException.ThrowIfNull(arguments);
        if(arguments.Length!=this.Parameters.Count){throw new ArgumentException($"'{this.Name}' takes {this.Parameters.Count} argument(s).",nameof(arguments));}
        return this.execute(database,arguments);
    }
}

public sealed record DatabaseQueryParameter(string Name,Type Type);

/// <summary>ビュー・関数の結果と、それを読むために開いた読み込み専用の入口。</summary>
/// <remarks>ビュー・テーブル関数の <see cref="Value"/> は列挙できる集合で、列挙は入口を閉じる前に終える。</remarks>
public sealed class DatabaseQueryResult(IDisposable reader,object? value):IDisposable{
    private IDisposable? reader=reader;
    public object? Value{get;}=value;
    public void Dispose(){global::System.Threading.Interlocked.Exchange(ref this.reader,null)?.Dispose();}
}
