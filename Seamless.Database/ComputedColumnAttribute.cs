namespace Seamless.Database;

/// <summary>生成レコードの SQL 計算列を識別する。永続値は通常のフィールド codec で保存する。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ComputedColumnAttribute(int ordinal):Attribute{
    public int Ordinal{get;}=ordinal;
}

/// <summary>閉じたレコード型ごとの評価器。collectible 型から共通アセンブリへの静的な逆参照を作らない。</summary>
public static class GeneratedRowExpression<TRow> where TRow:struct{
    public static Func<TRow,int,object?>? Evaluate{get;set;}
    public static TValue Read<TValue>(in TRow row,int column,TValue stored){var evaluate=Evaluate;if(evaluate is null){return stored;}return(TValue)evaluate(row,column)!;}
}
