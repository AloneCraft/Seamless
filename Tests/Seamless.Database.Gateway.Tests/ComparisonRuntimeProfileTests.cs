using Seamless.Database;
using Seamless.Database.Gateway.Hosting;
using Seamless.Database.Query;
using Seamless.Database.Rdbms;

namespace Seamless.Database.Gateway.Tests{

internal sealed class Legacy:DatabaseContainer{
    public const string GeneratedSchemaFingerprint="fingerprint";
}
public sealed class ComparisonRuntimeProfileTests{
    [Fact]
    public void LegacyManifestRejectsRuntimeTypeAndKeepsFallback(){
        var type=typeof(Legacy);var item=new ComparisonDatabase("legacy",type.Name,"fingerprint",true,new DatabaseCatalog("legacy",[]),[]){GeneratedNamespace=type.Namespace};
        ComparisonHost.ValidateRuntimeProfile(item,type,DatabaseContainerOptions.InMemory);
        Assert.Throws<InvalidDataException>(()=>ComparisonHost.ValidateRuntimeProfile(item with{RuntimeSqlEnabled=true},type,DatabaseContainerOptions.InMemory));
        Assert.Throws<InvalidDataException>(()=>ComparisonHost.ValidateRuntimeProfile(item with{Fingerprint="bad"},type,DatabaseContainerOptions.InMemory));
        Assert.Equal("legacy.legacy",new ComparisonDatabase("legacy","legacy","f",true,new DatabaseCatalog("legacy",[]),[]).TypeFullName);
    }
    [Fact]
    public void RuntimeManifestRequiresSharedV2AndMatchingType(){
        var type=typeof(Seamless.Generated.Profile_RuntimeSqlV1.Profile_RuntimeSqlV1);
        var item=new ComparisonDatabase("fixture",type.Name,"fingerprint",false,new DatabaseCatalog("fixture",[]),[]){GeneratedNamespace=type.Namespace,RuntimeSqlEnabled=true};
        ComparisonHost.ValidateRuntimeProfile(item,type,DatabaseContainerOptions.InMemory with{Transactions=DatabaseTransactionMode.SharedV2});
        Assert.Throws<InvalidDataException>(()=>ComparisonHost.ValidateRuntimeProfile(item,type,DatabaseContainerOptions.InMemory));
        Assert.Throws<InvalidDataException>(()=>ComparisonHost.ValidateRuntimeProfile(item with{RuntimeSqlEnabled=false},type,DatabaseContainerOptions.InMemory));
        Assert.Throws<InvalidDataException>(()=>ComparisonHost.ValidateRuntimeProfile(item with{GeneratedNamespace="wrong"},type,DatabaseContainerOptions.InMemory with{Transactions=DatabaseTransactionMode.SharedV2}));
    }
}
}
namespace Seamless.Generated.Profile_RuntimeSqlV1{
    internal sealed class Profile_RuntimeSqlV1:RuntimeSqlDatabaseContainer{
        public const string GeneratedSchemaFingerprint="fingerprint";
        public Profile_RuntimeSqlV1(DatabaseContainerOptions options):base(options,"fingerprint"){}
        protected override void EnsureTransactionSchema(){}
    }
}
