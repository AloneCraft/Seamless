using Seamless.Database.SqlServer.Functions;
using F=Seamless.Database.SqlServer.Functions.SecurityFunctions;
namespace Database.SqlServer.Functions.Tests;
public sealed class SecurityFunctions{
    private static readonly SqlSession session=new(){
        UserName="Sales",
        OriginalLogin="DOMAIN\\sales",
        Roles=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"public","SalesManagers"},
        SessionContext=new Dictionary<string,object?>{["SalesTerritory"]="East"}
    };
    [Fact,Trait("Category","Unit")]public void UserName(){
        Assert.Equal("Sales",F.UserName(session));
    }
    [Fact,Trait("Category","Unit")]public void OriginalLogin(){
        Assert.Equal("DOMAIN\\sales",F.OriginalLogin(session));
    }
    [Fact,Trait("Category","Unit")]public void IsMember(){
        {
            //属しているロール
            Assert.Equal(1,F.IsMember(session,"SalesManagers"));
        }
        {
            //属していないロール
            Assert.Equal(0,F.IsMember(session,"db_owner"));
        }
        {
            //NULL
            Assert.Null(F.IsMember(session,null));
        }
    }
    [Fact,Trait("Category","Unit")]public void IsRoleMember(){
        {
            //今の利用者
            Assert.Equal(1,F.IsRoleMember(session,"SalesManagers"));
        }
        {
            //他の利用者は分からないので NULL
            Assert.Null(F.IsRoleMember(session,"SalesManagers","Other"));
        }
    }
    [Fact,Trait("Category","Unit")]public void SessionContext(){
        {
            //置かれた値
            Assert.Equal("East",F.SessionContext(session,"SalesTerritory"));
        }
        {
            //置かれていない値
            Assert.Null(F.SessionContext(session,"Missing"));
        }
    }
}
