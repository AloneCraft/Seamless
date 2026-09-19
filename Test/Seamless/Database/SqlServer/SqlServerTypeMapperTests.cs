namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerTypeMapperTests {
    [Fact,Trait("Category","Unit")]
    public void Map_NormalizesSqlServerTypesToCommonTypes() {
        {
            var type=global::Seamless.Database.SqlServer.SqlServerTypeMapper.Map(nameof(global::Seamless.Database.SqlServer.Types.Bit),false,1,1,0);
            Assert.Equal(global::Seamless.Database.Rdbms.DatabaseTypeKind.Byte,type.Kind);
            Assert.Equal(typeof(global::Seamless.Database.SqlServer.Types.Bit),type.RuntimeType);
        }
        {
            var type=global::Seamless.Database.SqlServer.SqlServerTypeMapper.Map("int",false,4,10,0);
            Assert.Equal(global::Seamless.Database.Rdbms.DatabaseTypeKind.Int32,type.Kind);
            Assert.False(type.IsNullable);
        }
        {
            var type=global::Seamless.Database.SqlServer.SqlServerTypeMapper.Map("nvarchar",true,200,0,0);
            Assert.Equal(global::Seamless.Database.Rdbms.DatabaseTypeKind.String,type.Kind);
            Assert.True(type.IsNullable);
            Assert.Equal(100,type.MaximumLength);
        }
        {
            var type=global::Seamless.Database.SqlServer.SqlServerTypeMapper.Map("geography",false,-1,0,0);
            Assert.Equal(global::Seamless.Database.Rdbms.DatabaseTypeKind.Binary,type.Kind);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void Map_RejectsUnsupportedSqlServerType() {
        Assert.Throws<NotSupportedException>(()=>
            global::Seamless.Database.SqlServer.SqlServerTypeMapper.Map("cursor",false,0,0,0));
    }

    [Fact,Trait("Category","Unit")]
    public void Map_UsesCiphertextBytesForAlwaysEncryptedColumn() {
        var type=global::Seamless.Database.SqlServer.SqlServerTypeMapper.Map(
            "nvarchar",false,22,0,0,isEncrypted:true);

        Assert.Equal(global::Seamless.Database.Rdbms.DatabaseTypeKind.Binary,type.Kind);
        Assert.False(type.IsNullable);
    }
}
