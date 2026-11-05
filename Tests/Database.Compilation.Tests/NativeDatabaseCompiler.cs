using Seamless.Database.Compilation;

namespace Database.Compilation.Tests;

public sealed class NativeDatabaseCompiler{
    internal const string Source="""
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Seamless.Database;
        public readonly record struct Item(int Id,string Name,long Amount);
        public readonly record struct Key(int Id);
        public sealed class Shop(DatabaseContainerOptions options):DatabaseContainer(options){
            public Table<Key,Item>Items=>this.Table<Key,Item>(row=>new Key(row.Id));
            public IEnumerable<Item>Find(long minimum)=>this.Items.Where(row=>row.Amount>=minimum).OrderBy(row=>row.Id).Take(5);
            public long Total()=>this.Items.Sum(row=>row.Amount);
            public IEnumerable<Item>Unsupported()=>this.Items.Where(row=>System.IO.File.Exists(row.Name));
        }
        """;

    [Fact]public void Analyze(){
        var snapshot=NativeCompilationSnapshot.Create("fixture-v1",[new("Shop.cs",Source)]);
        var result=new global::Seamless.Database.Compilation.NativeDatabaseCompiler().Analyze(snapshot,"Shop");
        Assert.Empty(result.Diagnostics.Where(d=>d.Severity==NativeDiagnosticSeverity.CompilationError));
        var table=Assert.Single(result.Database.Tables);
        Assert.Equal("Items",table.Name);
        Assert.Equal("Id",Assert.Single(table.PrimaryKey));
        Assert.Equal(3,table.Columns.Length);
        Assert.Contains(result.Database.Queries,q=>q.Name=="Find");
        Assert.Contains(result.Diagnostics,d=>d.ObjectName=="Unsupported"&&d.Code=="NATIVE_UNSUPPORTED_CALL");
        Assert.DoesNotContain(result.Database.Queries,q=>q.Name=="Unsupported");
        var changed=NativeCompilationSnapshot.Create("fixture-v1",[new("Shop.cs",Source.Replace("Take(5)","Take(3)"))]);
        Assert.NotEqual(snapshot.Fingerprint,changed.Fingerprint);
        {
            var unrelated="""
                public sealed class Unrelated(Seamless.Database.DatabaseContainer database):Seamless.Database.DatabaseSchema(database,"other"){
                    public Seamless.Database.Table<Key,Item>Other=>this.Table<Key,Item>(row=>new Key(row.Id));
                }
                """;
            var multiple=NativeCompilationSnapshot.Create("multiple",[new("Shop.cs",Source),new("Unrelated.cs",unrelated)]);
            var selected=new global::Seamless.Database.Compilation.NativeDatabaseCompiler().Analyze(multiple,"Shop");
            Assert.Empty(selected.Diagnostics.Where(d=>d.Severity==NativeDiagnosticSeverity.CompilationError));
            Assert.Equal("Items",Assert.Single(selected.Database.Tables).Name);
        }
    }

    [Fact]public void GenerateSql(){
        var snapshot=NativeCompilationSnapshot.Create("fixture-v1",[new("Shop.cs",Source)]);
        var model=new global::Seamless.Database.Compilation.NativeDatabaseCompiler().Analyze(snapshot,"Shop").Database;
        var server=new VendorSqlGenerator().Generate(model,SqlVendor.SqlServer);
        var mysql=new VendorSqlGenerator().Generate(model,SqlVendor.MySql);
        Assert.Contains("CREATE TABLE [dbo].[Items]",server.SchemaSql);
        Assert.Contains("PRIMARY KEY",server.SchemaSql);
        Assert.Contains("CREATE TABLE `Items`",mysql.SchemaSql);
        var query=Assert.Single(server.Queries.Where(q=>q.Name=="Find"));
        Assert.Contains("TOP (5)",query.Sql);
        Assert.Contains("@minimum",query.Sql);
        Assert.Contains("ORDER BY",query.Sql);
        Assert.Contains("LIMIT 5",Assert.Single(mysql.Queries.Where(q=>q.Name=="Find")).Sql);
        Assert.Contains("SUM",Assert.Single(mysql.Queries.Where(q=>q.Name=="Total")).Sql);
        Assert.Contains("\"Kind\":\"Filter\"",System.Text.Json.JsonSerializer.Serialize(model));
    }
}
