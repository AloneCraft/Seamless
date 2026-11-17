using System.Reflection;
using System.Reflection.Emit;
using Seamless.Database.Client;
using Contract=Seamless.Database.Client.NativeRecordQueryContract;

namespace Seamless.Database.Gateway.Tests.RecordContracts;

public sealed class NativeRecordQueryContract{
    public interface IFirst{int Run(int input);}
    public interface ISecond{int Run(int input);}

    [Fact]
    public void For_BindsThePublicInterfaceAndCachesTheContract(){
        var first=Contract.For<int,int,IFirst>();
        var second=Contract.For<int,int,ISecond>();
        var unbound=Contract.For<int,int>();
        Assert.Same(first,Contract.For<int,int,IFirst>());
        Assert.Equal(typeof(int),first.ArgumentsType);Assert.Equal(typeof(int),first.ResultType);
        Assert.NotEqual(first.ArgumentsContract,second.ArgumentsContract);
        Assert.NotEqual(first.ResultContract,second.ResultContract);
        Assert.NotEqual(first.ArgumentsContract,unbound.ArgumentsContract);
        Assert.Throws<InvalidDataException>(()=>first.ValidateDefinition(second.Define("Run","db","schema",true)));
        first.ValidateDefinition(first.Define("Run","db","schema",true));
        first.ValidateArguments(NativeRecordCodec<int>.Serialize(1));first.ValidateResult(NativeRecordCodec<int>.Serialize(2));
    }

    [Fact]
    public void For_RejectsDifferentBuildsWithTheSameAssemblyAndTypeNames(){
        var name="NativeContractRevision"+Guid.NewGuid().ToString("N");
        var first=Build(name);var second=Build(name);
        Assert.Equal(first.AssemblyQualifiedName,second.AssemblyQualifiedName);
        Assert.NotEqual(first.Module.ModuleVersionId,second.Module.ModuleVersionId);
        var factory=typeof(Contract).GetMethods().Single(static method=>method.Name=="For"&&method.GetGenericArguments().Length==3);
        var original=(Contract)factory.MakeGenericMethod(typeof(int),typeof(int),first).Invoke(null,null)!;
        var replacement=(Contract)factory.MakeGenericMethod(typeof(int),typeof(int),second).Invoke(null,null)!;
        Assert.NotEqual(original.ArgumentsContract,replacement.ArgumentsContract);
        Assert.NotEqual(original.ResultContract,replacement.ResultContract);
        Assert.Throws<InvalidDataException>(()=>original.ValidateDefinition(replacement.Define("Run","db","schema",true)));
    }

    private static Type Build(string name){
        var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name){Version=new(1,0,0,0)},AssemblyBuilderAccess.RunAndCollect);
        var module=assembly.DefineDynamicModule(name);
        var type=module.DefineType("Fixture.IQueries",TypeAttributes.Public|TypeAttributes.Interface|TypeAttributes.Abstract);
        type.DefineMethod("Run",MethodAttributes.Public|MethodAttributes.Abstract|MethodAttributes.Virtual,typeof(int),[typeof(int)]);
        return type.CreateType()!;
    }
}
