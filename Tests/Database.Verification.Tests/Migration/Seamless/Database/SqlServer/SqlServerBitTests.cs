namespace Test.Seamless.Database.SqlServer;

public sealed class SqlServerBitTests {
    [Fact,Trait("Category","Unit")]
    public void Constructor_NormalizesEveryNonzeroIntegerToOne() {
        Assert.Equal(global::Seamless.Database.SqlServer.Types.Bit.Zero,new global::Seamless.Database.SqlServer.Types.Bit(0));
        Assert.Equal(global::Seamless.Database.SqlServer.Types.Bit.One,new global::Seamless.Database.SqlServer.Types.Bit(1));
        Assert.Equal(global::Seamless.Database.SqlServer.Types.Bit.One,new global::Seamless.Database.SqlServer.Types.Bit(-1));
        Assert.Equal(global::Seamless.Database.SqlServer.Types.Bit.One,new global::Seamless.Database.SqlServer.Types.Bit(42));
        Assert.Equal(global::Seamless.Database.SqlServer.Types.Bit.One,new global::Seamless.Database.SqlServer.Types.Bit((byte)2));
        Assert.Equal(global::Seamless.Database.SqlServer.Types.Bit.One,new global::Seamless.Database.SqlServer.Types.Bit(uint.MaxValue));
        Assert.Equal(global::Seamless.Database.SqlServer.Types.Bit.One,new global::Seamless.Database.SqlServer.Types.Bit(long.MinValue));
        Assert.Equal(global::Seamless.Database.SqlServer.Types.Bit.One,new global::Seamless.Database.SqlServer.Types.Bit(ulong.MaxValue));
    }

    [Fact,Trait("Category","Unit")]
    public void Operators_FollowSqlServerBitSemantics() {
        var zero=global::Seamless.Database.SqlServer.Types.Bit.Zero;
        var one=global::Seamless.Database.SqlServer.Types.Bit.One;

        Assert.Equal(zero,zero&one);
        Assert.Equal(one,zero|one);
        Assert.Equal(one,zero^one);
        Assert.Equal(one,~zero);
        Assert.Equal(zero,~one);
        Assert.Equal(one,+one);
        Assert.True(zero<one);
        Assert.True(one>=zero);
        Assert.Equal(0,(int)zero);
        Assert.Equal(1,(int)one);
    }

    [Fact,Trait("Category","Unit")]
    public void Operators_DoNotExposeUnsupportedArithmeticOrImplicitConversions() {
        var methodNames=typeof(global::Seamless.Database.SqlServer.Types.Bit)
            .GetMethods(global::System.Reflection.BindingFlags.Public|global::System.Reflection.BindingFlags.Static)
            .Select(static method=>method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("op_Addition",methodNames);
        Assert.DoesNotContain("op_Subtraction",methodNames);
        Assert.DoesNotContain("op_Multiply",methodNames);
        Assert.DoesNotContain("op_Division",methodNames);
        Assert.DoesNotContain("op_Modulus",methodNames);
        Assert.DoesNotContain("op_LeftShift",methodNames);
        Assert.DoesNotContain("op_RightShift",methodNames);
        Assert.DoesNotContain("op_UnaryNegation",methodNames);
        Assert.DoesNotContain("op_Implicit",methodNames);
    }
}
