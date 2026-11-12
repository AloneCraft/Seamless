using Seamless.Management;
using System.Xml.Linq;
using Designer=Seamless.Management.DatabaseDesigner;

namespace Management.Tests;

public sealed class QueryPlanAvailability{
    [Theory][InlineData(DatabaseQueryKind.View)][InlineData(DatabaseQueryKind.ScalarFunction)]
    public void ConstantDefinitionsExplainThePlanGenerationBoundary(DatabaseQueryKind kind){
        var definition=Designer.Empty("constant-plan","ConstantPlans");
        ColumnDesign[] columns=[];var returnType="int";var body="return 42;";
        if(kind==DatabaseQueryKind.View){columns=[new("Value","int",false)];returnType="";body="return [];";}
        definition=Designer.ApplyQuery(definition,new("Constant","",kind,columns,[],returnType,body));
        var selection=Designer.SelectQueryPlans(definition,"Constant",[]);
        Assert.NotEmpty(selection.Methods);Assert.Empty(selection.Plans);
        Assert.Contains("定数を返す処理などは生成対象外",selection.UnavailableReason);
        Assert.Contains("ソース版とSDKでのビルド・取り込み・明示適用",selection.UnavailableReason);
        var root=Environment.GetEnvironmentVariable("SEAMLESS_TEST_REPOSITORY")??throw new InvalidOperationException("試験のソースルートがありません。");
        foreach(var language in new[]{"",".en"}){
            var resource=XDocument.Load(Path.Combine(root,"Seamless.Management.Components","表示文言"+language+".resx"));
            var value=Assert.Single(resource.Root!.Elements("data"),item=>(string?)item.Attribute("name")==selection.UnavailableReason).Element("value")!.Value;
            Assert.Contains("FLinq",value);Assert.Contains("SDK",value);
            if(language==".en"){Assert.Contains("Constant-returning",value);Assert.Contains("explicit publication",value);}
            else{Assert.Equal(selection.UnavailableReason,value);}
        }
    }
}
