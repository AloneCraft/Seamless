using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Gateway.Sql;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Tests{
    public sealed class SystemCatalogNativeRoutineTests{
        [Fact,Trait("Category","Unit")]
        public void NativeScalarExecutesCompiledMethodAndPreservesNull(){
            var input=new DatabaseType(DatabaseTypeKind.Byte,true,SqlTypeName:"tinyint");
            var output=new DatabaseType(DatabaseTypeKind.String,true,10,SqlTypeName:"nvarchar");
            var routine=new RoutineCatalog("dbo","Lookup",RoutineKind.ScalarFunction,output,[new(1,"@code",input,false)],[],"not valid SQL",false,0,1){Implementation=new("body","hash","SqlServerScalarTinyintCaseNvarcharV1")};
            var binding=new GeneratedRoutineBinding("fixture","dbo","Lookup",RoutineKind.ScalarFunction,"BridgeFixture.Schemas.Dbo","Lookup","fixture.cs","compiled source",false);
            var item=new ComparisonDatabase("fixture","BridgeFixture","fixture",true,new("fixture",[new("dbo",[],[],[routine])]),[]){Routines=[binding]};
            using var database=new BridgeFixture.Database();
            var routines=ComparisonHost.ResolveRoutines(item,typeof(BridgeFixture.Database).Assembly,database);
            var registry=new DatabaseRegistry();registry.Register("fixture",new EmptyAdapter(),routines:routines);
            var engine=new SqlExecutionEngine(registry);using var session=new SessionContext(1,"fixture");
            var values=Assert.Single(engine.Execute("SELECT dbo.Lookup(1),dbo.Lookup(NULL)",session).Rows);
            Assert.Equal("one",values[0]);Assert.Equal("other",values[1]);Assert.Equal(2,database.Dbo.Calls);
            var failure=Assert.Throws<SqlQueryException>(()=>engine.Execute("SELECT dbo.Lookup(256)",session).Rows.ToArray());Assert.Equal(8115,failure.Number);Assert.Equal(2,database.Dbo.Calls);
            using var cancel=new CancellationTokenSource();cancel.Cancel();session.CancellationToken=cancel.Token;
            Assert.ThrowsAny<OperationCanceledException>(()=>engine.Execute("SELECT dbo.Lookup(1)",session).Rows.ToArray());Assert.Equal(2,database.Dbo.Calls);
            var unbound=ComparisonHost.ResolveRoutines(item,typeof(BridgeFixture.Database).Assembly);
            var another=new DatabaseRegistry();another.Register("fixture",new EmptyAdapter(),routines:unbound);
            Assert.Throws<SqlQueryException>(()=>another.FindRoutine(new("fixture","dbo","Lookup")));
        }
        private sealed class EmptyAdapter:IDatabaseAdapter{
            public string DatabaseName=>"fixture";public IReadOnlyList<TableMetadata>GetTables()=>[];public TableMetadata? FindTable(string name)=>null;
            public QueryResult ExecuteSelect(string tableName,IReadOnlyList<string>? columns,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
            public int ExecuteInsert(string tableName,IReadOnlyDictionary<string,object?>values,SessionContext session)=>throw new NotSupportedException();
            public int ExecuteUpdate(string tableName,IReadOnlyDictionary<string,object?>values,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
            public int ExecuteDelete(string tableName,Func<object,bool>? predicate,SessionContext session)=>throw new NotSupportedException();
            public void BeginTransaction(SessionContext session)=>throw new NotSupportedException();public void CommitTransaction(SessionContext session)=>throw new NotSupportedException();public void RollbackTransaction(SessionContext session)=>throw new NotSupportedException();
        }
    }
}
namespace BridgeFixture{
    public sealed class Database:DatabaseContainer{
        public Database(){this.Dbo=new(this);}
        public BridgeFixture.Schemas.Dbo Dbo{get;}
    }
}
namespace BridgeFixture.Schemas{
    public sealed class Dbo(DatabaseContainer database):DatabaseSchema(database,"dbo"){
        public int Calls{get;private set;}
        public ScalarFunction<string> Lookup(byte? code)=>this.ScalarFunction(()=>{this.Calls++;return code switch{1=>"one",_=>"other"};});
    }
}
