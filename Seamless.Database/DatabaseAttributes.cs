namespace Seamless.Database;

/// <summary>SQL Server 由来のデータベース名を宣言コンテナへ対応付ける補助属性。</summary>
[AttributeUsage(AttributeTargets.Class,AllowMultiple = false)]
public sealed class DatabaseAttribute(string name):Attribute {
    public string Name { get; } = name;
}

/// <summary>テーブル宣言プロパティへ SQL Server 互換名(例: "Sales.Customer")を対応付ける補助属性。</summary>
[AttributeUsage(AttributeTargets.Property,AllowMultiple = false)]
public sealed class TableAttribute(string name):Attribute {
    public string Name { get; } = name;
}

/// <summary>ビュー宣言へ SQL Server 互換名(例: "Sales.vActiveCustomer")を対応付ける補助属性。</summary>
[AttributeUsage(AttributeTargets.Property|AttributeTargets.Method,AllowMultiple = false)]
public sealed class ViewAttribute(string name):Attribute {
    public string Name { get; } = name;
}

/// <summary>スカラー関数・テーブル関数宣言へ SQL Server 互換名を対応付ける補助属性。</summary>
[AttributeUsage(AttributeTargets.Method,AllowMultiple = false)]
public sealed class FunctionAttribute(string name):Attribute {
    public string Name { get; } = name;
}
