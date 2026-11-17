using Seamless.Execution;
using Store=Seamless.Execution.Store;
using Xunit;

namespace Seamless.Core.Tests;

public sealed class LegacyTypeIdentityTests{
    private const string StoreIdentity="Seamless.Execution.Store, Seamless.Execution, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";

    [Fact]
    public void ProductTypeRetainsItsPersistedAssemblyIdentity(){
        Assert.Equal(StoreIdentity,PersistedTypeIdentity.Name(typeof(Store)));
        Assert.Same(typeof(Store),PersistedTypeIdentity.Resolve(StoreIdentity));
    }

    [Fact]
    public void GenericArgumentsAndArraysRetainTheirPersistedIdentities(){
        var expected="System.Tuple`1[[Seamless.Execution.Store[], Seamless.Execution, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null]], "+typeof(Tuple<>).Assembly.FullName;
        Assert.Equal(expected,PersistedTypeIdentity.Name(typeof(Tuple<Store[]>)));
        Assert.Same(typeof(Tuple<Store[]>),PersistedTypeIdentity.Resolve(expected));
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(Dictionary<string,int[]>))]
    [InlineData(typeof(LegacyTypeIdentityTests))]
    public void ExternalTypeIdentityRetainsTheReflectionContract(Type type){
        Assert.Equal(type.AssemblyQualifiedName,PersistedTypeIdentity.Name(type));
    }
}
