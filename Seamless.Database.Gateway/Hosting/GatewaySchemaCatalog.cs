using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Hosting;

public sealed record GatewayColumnIdentity(long Id,int SqlColumnId,string Name);
public sealed record GatewayTableIdentity(int Id,string Schema,string Name,IReadOnlyList<GatewayColumnIdentity> Columns,int NextSqlColumnId);

/// <summary>SQL 名とは独立した永続 ID。CLR 型や delegate を保存しない。</summary>
public sealed record GatewaySchemaCatalog(DatabaseCatalog Definition,IReadOnlyList<GatewayTableIdentity> Tables,int NextTableId,long NextColumnId){
    public static GatewaySchemaCatalog Create(DatabaseCatalog definition){
        ArgumentNullException.ThrowIfNull(definition);var tables=new List<GatewayTableIdentity>();var nextTable=1000;long nextColumn=1;
        foreach(var schema in definition.Schemas){foreach(var table in schema.Tables){var columns=table.Columns.Select(column=>new GatewayColumnIdentity(nextColumn++,checked(column.Ordinal+1),column.Name)).ToArray();tables.Add(new(nextTable++,schema.Name,table.Name,Array.AsReadOnly(columns),columns.Length==0?1:checked(columns.Max(static column=>column.SqlColumnId)+1)));}}
        var result=new GatewaySchemaCatalog(definition,tables.AsReadOnly(),nextTable,nextColumn);result.Validate();return result;
    }
    public void Validate(){
        ArgumentException.ThrowIfNullOrWhiteSpace(this.Definition.Name);if(this.NextTableId<1000||this.NextColumnId<=0||this.Tables.Count>1024||this.Tables.Count!=this.Definition.Schemas.Sum(static schema=>schema.Tables.Count)){throw new InvalidDataException("The persistent table identity map is inconsistent.");}
        var tableIds=new HashSet<int>();var columnIds=new HashSet<long>();var names=new HashSet<(string,string)>();
        foreach(var table in this.Tables){
            var definition=this.Definition.Schemas.SingleOrDefault(schema=>schema.Name==table.Schema)?.Tables.SingleOrDefault(item=>item.Name==table.Name);
            if(table.Id<=0||table.Id>=this.NextTableId||!tableIds.Add(table.Id)||!names.Add((table.Schema.ToUpperInvariant(),table.Name.ToUpperInvariant()))||definition is null||definition.Columns.Count!=table.Columns.Count||table.Columns.Count>1024){throw new InvalidDataException("Invalid persistent table identity.");}
            var ordinals=new HashSet<int>();foreach(var column in table.Columns){if(column.Id<=0||column.Id>=this.NextColumnId||!columnIds.Add(column.Id)||column.SqlColumnId<=0||column.SqlColumnId>=table.NextSqlColumnId||!ordinals.Add(column.SqlColumnId)||!definition.Columns.Any(item=>item.Name==column.Name)){throw new InvalidDataException("Invalid persistent column identity.");}}
        }
        // System.Type の JSON 化や assembly-qualified name に永続互換性を依存させない。
        foreach(var column in this.Definition.Schemas.SelectMany(static schema=>schema.Tables.SelectMany(static table=>table.Columns).Concat(schema.Views.SelectMany(static view=>view.Columns)))){if(column.Type.RuntimeType is not null){throw new InvalidDataException("A persistent SQL catalog cannot contain a CLR RuntimeType.");}}
        foreach(var routine in this.Definition.Schemas.SelectMany(static schema=>schema.Routines)){if(routine.ReturnType?.RuntimeType is not null||routine.Parameters.Any(static parameter=>parameter.Type.RuntimeType is not null)||routine.ResultColumns.Any(static column=>column.Type.RuntimeType is not null)){throw new InvalidDataException("A persistent SQL routine cannot contain a CLR RuntimeType.");}}
    }
}
