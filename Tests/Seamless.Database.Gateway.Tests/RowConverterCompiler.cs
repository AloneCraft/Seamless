using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tests;

public sealed class RowConverterCompiler{
    public readonly record struct Before(int Id,string Name,int Removed);
    public readonly record struct After(string Caption,long Key,int? Extra);
    public readonly record struct NullableBefore(int? Id);
    public readonly record struct RequiredAfter(int Id);
    public readonly record struct PairAfter(int Second,int First);
    public readonly record struct CaseAfter(int Id,int ID);
    public readonly record struct TextBefore(string Value);
    public readonly record struct GuidAfter(Guid Value);
    public readonly record struct IntBefore(int Value);
    public readonly record struct BinaryAfter(byte[] Value);

    [Fact]
    public void TypedConvertersApplySqlGuidBinaryAndXmlRulesAcrossDifferentAndIdenticalClrTypes(){
        var guidPlan=new MigrationPlan(2,[new(1,"Value",new("nvarchar",typeof(string),100),false)],[new(1,"Value",new("uniqueidentifier",typeof(Guid)),false)]);var guid=Seamless.Database.Query.RowConverterCompiler.Compile<TextBefore,GuidAfter>(guidPlan);var text=new TextBefore("12345678-1234-4321-abcd-123456789012extra");Assert.Equal(Guid.Parse("12345678-1234-4321-abcd-123456789012"),guid(in text,new()).Value);text=new("1234567812344321abcd123456789012");Assert.Equal(8169,Assert.Throws<SqlQueryException>(()=>guid(in text,new())).Number);
        var binaryPlan=new MigrationPlan(3,[new(1,"Value",SqlType.Int,false)],[new(1,"Value",new("binary",typeof(byte[]),6),false)]);var binary=Seamless.Database.Query.RowConverterCompiler.Compile<IntBefore,BinaryAfter>(binaryPlan);var integer=new IntBefore(264);Assert.Equal(new byte[]{0,0,0,0,1,8},binary(in integer,new()).Value);
        var xmlPlan=new MigrationPlan(4,[new(1,"Value",new("nvarchar",typeof(string),100),false)],[new(1,"Value",new("xml",typeof(string)),false)]);var xml=Seamless.Database.Query.RowConverterCompiler.Compile<TextBefore,TextBefore>(xmlPlan);text=new("<a><![CDATA[<]]></a>");Assert.Equal("<a>&lt;</a>",xml(in text,new()).Value);text=new("<a>");Assert.Equal(9400,Assert.Throws<SqlQueryException>(()=>xml(in text,new())).Number);
    }

    [Fact]
    public void CompileMapsStableIdsInsteadOfMemberNamesOrOrdinals(){
        var plan=new MigrationPlan(1,
            [new(1,"Id",SqlType.Int,false),new(2,"Name",new("nvarchar",typeof(string),40),false),new(3,"Removed",SqlType.Int,false)],
            [new(2,"Caption",new("nvarchar",typeof(string),40),false),new(1,"Key",new("bigint",typeof(long)),false),new(4,"Extra",SqlType.Int,true)]);
        var convert=Seamless.Database.Query.RowConverterCompiler.Compile<Before,After>(plan);var source=new Before(12,"saved",9);
        Assert.Equal(new After("saved",12L,null),convert(in source,new MigrationContext()));
        var calls=0;var context=new MigrationContext(column=>{Assert.Equal(4,column);calls++;return 7;});
        Assert.Equal(new After("saved",12L,7),convert(in source,context));Assert.Equal(1,calls);
    }

    [Fact]
    public void CompileRejectsNullAndChecksCancellation(){
        var plan=new MigrationPlan(1,[new(1,"Id",SqlType.Int,true)],[new(1,"Id",SqlType.Int,false)]);
        var convert=Seamless.Database.Query.RowConverterCompiler.Compile<NullableBefore,RequiredAfter>(plan);var source=new NullableBefore(null);
        var error=Assert.Throws<SqlQueryException>(()=>convert(in source,new MigrationContext()));Assert.Equal(515,error.Number);
        source=new(5);Assert.Equal(new RequiredAfter(5),convert(in source,new MigrationContext()));
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();Assert.Throws<OperationCanceledException>(()=>convert(in source,new MigrationContext(cancellationToken:cancellation.Token)));
    }

    [Fact]
    public void CompileRejectsDuplicateIdsAndIncompatibleBindingsBeforeCopy(){
        Assert.Throws<ArgumentException>(()=>new MigrationPlan(1,[new(1,"Id",SqlType.Int,false),new(1,"Removed",SqlType.Int,false)],[]));
        var plan=new MigrationPlan(1,[new(1,"Missing",SqlType.Int,false)],[new(1,"Id",SqlType.Int,false)]);
        Assert.Throws<InvalidDataException>(()=>Seamless.Database.Query.RowConverterCompiler.Compile<Before,RequiredAfter>(plan));
        var swapped=new MigrationPlan(1,[new(1,"Id",SqlType.Int,false),new(2,"Removed",SqlType.Int,false)],[new(1,"First",SqlType.Int,false),new(2,"Second",SqlType.Int,false)]);
        Assert.Throws<InvalidDataException>(()=>Seamless.Database.Query.RowConverterCompiler.Compile<Before,PairAfter>(swapped));
        var differentCase=new MigrationPlan(1,[new(1,"Id",SqlType.Int,false),new(2,"Removed",SqlType.Int,false)],[new(1,"ID",SqlType.Int,false),new(2,"Id",SqlType.Int,false)]);
        Assert.Throws<InvalidDataException>(()=>Seamless.Database.Query.RowConverterCompiler.Compile<Before,CaseAfter>(differentCase));
    }
}
