using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Seamless.Database.Gateway.Sql;

internal static class SqlServerObjectCatalogProfile{
    // 元定義が CREATE/ALTER VIEW のときだけ、WITH SCHEMABINDING の有無を判定する。
    internal static bool? IsSchemaBound(string? definition){
        if(string.IsNullOrWhiteSpace(definition)){return null;}
        var parser=new TSql160Parser(true);
        var script=parser.Parse(new StringReader(definition),out var errors) as TSqlScript;
        if(errors.Count!=0||script is null){return null;}
        var statements=script.Batches.SelectMany(batch=>batch.Statements).ToArray();
        if(statements.Length!=1||statements[0] is not ViewStatementBody view){return null;}
        return view.ViewOptions.Any(option=>option.OptionKind==ViewOptionKind.SchemaBinding);
    }
}
