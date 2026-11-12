using Seamless.Database.Rdbms;

namespace TestSeamless.Tests;

public sealed class RuntimeQueryModuleWriter{
    [Fact]
    public void WritesTypedFixtureUsingRuntimeNamespace(){
        var root=TestTempDirectory.NewPath("runtime-query-module-");Directory.CreateDirectory(root);
        try{
            var name="Fixture_RuntimeSqlV1";var space="Seamless.Generated."+name;
            var fixture=new ComparisonDatabase("Seamless_Comparison_Fixture",name,"f",false,new DatabaseCatalog("Seamless_Comparison_Fixture",[]),[]){GeneratedNamespace=space,RuntimeSqlEnabled=true};
            var generation=new ComparisonGeneration("test","version",DateTimeOffset.UtcNow,[fixture]);var input=Path.Combine(root,"input.json");File.WriteAllText(input,"[]");
            global::TestSeamless.QueryModuleWriter.Write(root,generation,input);var source=File.ReadAllText(Path.Combine(root,"queries","QueryModule.cs"));
            Assert.Contains("global::"+space+"."+name,source);Assert.Contains("global::"+space+".Keys.FixtureParent",source);Assert.Contains("global::"+space+".Records.FixtureParent",source);
        }finally{if(Directory.Exists(root)){Directory.Delete(root,true);}}
    }
}
