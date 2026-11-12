using Seamless.Database.Query;
namespace Database.Query.Tests;
public sealed partial class SqlQueryEngine{
    [Fact]
    public void XmlConversionsValidateFragmentsNormalizeWhitespaceAndRejectDtd(){
        var engine=new Seamless.Database.Query.SqlQueryEngine(new SemanticCatalog());
        var result=engine.Prepare("SELECT CONVERT(xml,N'<a/>'),CAST(N'text' AS xml),CONVERT(xml,N'<a/><b/>'),CONVERT(xml,N'<a>  </a>')","Original DB").Execute();
        Assert.Equal(new object?[]{"<a />","text","<a /><b />","<a />"},Assert.Single(result.Rows));
        Assert.Equal("xml",result.Columns[0].Type.Name);
        var preserved=engine.Prepare("SELECT CONVERT(xml,N'<a><![CDATA[<]]></a>'),CONVERT(xml,N'<?xml version=\"1.0\"?><a></a>'),CONVERT(xml,N'<a xml:space=\"preserve\">  </a>')","Original DB").Execute();Assert.Equal(new object?[]{"<a>&lt;</a>","<a />","<a xml:space=\"preserve\">  </a>"},Assert.Single(preserved.Rows));
        foreach(var (value,number) in new[]{("<a>",9400),("<!DOCTYPE a [<!ELEMENT a ANY>]><a/>",6359)}){
            var error=Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT CONVERT(xml,N'"+value+"')","Original DB").Execute().Rows.ToArray());Assert.Equal(number,error.Number);
        }
        Assert.Equal(529,Assert.Throws<SqlQueryException>(()=>engine.Prepare("SELECT CONVERT(xml,1)","Original DB").Execute().Rows.ToArray()).Number);
    }
}
