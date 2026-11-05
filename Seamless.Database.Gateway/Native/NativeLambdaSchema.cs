using System.Reflection;
using System.Runtime.CompilerServices;
using Seamless.Database.Client;

namespace Seamless.Database.Gateway.Native;

public sealed record NativeLambdaAccess(string Table,string Operation);

/// <summary>管理者が登録した公開DB表現と通信型。受信した名前から任意の型を探索しない。</summary>
public sealed class NativeLambdaSchema<TDatabase>{
    private readonly object gate=new();
    private bool sealedCatalog;
    private readonly Dictionary<string,Type> types=new(StringComparer.Ordinal);
    private readonly HashSet<Type> contracts=[];
    private readonly Func<DatabaseContainer,TDatabase> bind;
    internal Func<NativeLambdaAccess,bool> Authorize{get;}
    public Type DatabaseType=>typeof(TDatabase);
    internal IReadOnlyDictionary<string,Type> Types=>this.types;
    internal IReadOnlyDictionary<string,PropertyInfo> Tables{get;}
    public string ContractId{get;}
    public NativeWireLimits WireLimits{get;init;}=NativeWireLimits.Default;

    public NativeLambdaSchema(Func<DatabaseContainer,TDatabase> bind,Func<NativeLambdaAccess,bool> authorize){
        this.bind=bind??throw new ArgumentNullException(nameof(bind));this.Authorize=authorize??throw new ArgumentNullException(nameof(authorize));
        if(!typeof(TDatabase).IsInterface||!typeof(TDatabase).IsVisible){throw new ArgumentException("The public database surface must be a public interface.");}
        var tables=typeof(TDatabase).GetProperties().ToDictionary(static property=>property.Name,StringComparer.Ordinal);
        if(tables.Count==0||tables.Values.Any(static property=>property.SetMethod is not null||!property.PropertyType.IsGenericType||property.PropertyType.GetGenericTypeDefinition()!=typeof(NativeTable<,>))){throw new ArgumentException("The public database surface must expose read-only NativeTable properties.");}
        this.Tables=tables;
        this.AddType(typeof(TDatabase));
        foreach(var type in new[]{typeof(bool),typeof(byte),typeof(sbyte),typeof(short),typeof(ushort),typeof(char),typeof(int),typeof(uint),typeof(long),typeof(ulong),typeof(float),typeof(double),typeof(decimal),typeof(string),typeof(Guid),typeof(DateTime),typeof(DateTimeOffset),typeof(TimeSpan),typeof(DateOnly),typeof(TimeOnly),typeof(StringComparison)}){this.Expose(type);}
        foreach(var table in tables.Values){this.AddType(table.PropertyType);foreach(var type in table.PropertyType.GenericTypeArguments){this.Expose(type);}}
        this.ContractId=NativeLambdaContracts.Surface<TDatabase>();
    }

    public NativeLambdaSchema<TDatabase> Expose<T>()=>this.Expose(typeof(T));
    public NativeLambdaSchema<TDatabase> Expose(Type type){
        lock(this.gate){
            if(this.contracts.Contains(type)){return this;}
            if(this.sealedCatalog){throw new InvalidOperationException("Expose data contracts before registering the lambda runtime.");}
            var pending=new HashSet<Type>();
            void Collect(Type current){
                if(!current.IsVisible||current.IsPointer||current.IsByRefLike||current==typeof(object)||current.IsInterface||typeof(Delegate).IsAssignableFrom(current)||current.ContainsGenericParameters){throw new ArgumentException("Only concrete public data contracts can be exposed.");}
                if(this.contracts.Contains(current)||!pending.Add(current)){return;}
                if(current.IsArray){Collect(current.GetElementType()!);return;}
                if(current.IsGenericType){if(current.GetGenericTypeDefinition()!=typeof(Nullable<>)){throw new ArgumentException("Only nullable and array data contracts are supported.");}Collect(current.GenericTypeArguments[0]);}
                if(!current.IsPrimitive&&!current.IsEnum&&current.Namespace!="System"){
                    foreach(var property in current.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(static property=>property.GetIndexParameters().Length==0)){Collect(property.PropertyType);}
                    foreach(var field in current.GetFields(BindingFlags.Public|BindingFlags.Instance)){Collect(field.FieldType);}
                }
                NativeWireContract.For(current);
            }
            Collect(type);
            foreach(var current in pending){this.contracts.Add(current);this.AddType(current);}
            return this;
        }
    }

    internal void Seal(){lock(this.gate){this.sealedCatalog=true;}}
    internal object Bind(DatabaseContainer database)=>this.bind(database)!;
    internal Type Contract(string name)=>this.Contract(name,0);
    private Type Contract(string name,int depth){
        if(depth>=32){throw new NativeLambdaException("Contract","The native array type exceeds the supported depth.",true);}
        if(this.types.TryGetValue(name,out var type)&&this.contracts.Contains(type)){return type;}
        if(name.EndsWith("[]",StringComparison.Ordinal)){return this.Contract(name[..^2],depth+1).MakeArrayType();}
        throw new NativeLambdaException("Contract","The lambda refers to a data contract that the server has not exposed.",true);
    }
    private void AddType(Type type){
        this.types[type.FullName!]=type;this.types[type.FullName!.Replace('+','.')]=type;
        var alias=type.FullName switch{"System.Boolean"=>"bool","System.Byte"=>"byte","System.SByte"=>"sbyte","System.Int16"=>"short","System.UInt16"=>"ushort","System.Char"=>"char","System.Int32"=>"int","System.UInt32"=>"uint","System.Int64"=>"long","System.UInt64"=>"ulong","System.Single"=>"float","System.Double"=>"double","System.Decimal"=>"decimal","System.String"=>"string",_=>null};
        if(alias is not null){this.types[alias]=type;}
        if(Nullable.GetUnderlyingType(type) is{} underlying){var name=this.types.FirstOrDefault(item=>item.Value==underlying&&!item.Key.Contains('.')).Key;if(name is not null){this.types[name+"?"]=type;}}
    }
}

/// <summary>実Tableのsession・取引・制約を保ち、公開表現へ接続する。</summary>
public sealed class NativeTableAdapter<TKey,TRecord>(Table<TKey,TRecord> table):NativeTable<TKey,TRecord> where TKey:struct where TRecord:struct{
    public override bool Add(TRecord record)=>table.Add(record);
    public override bool Remove(TKey key)=>table.Remove(key);
    public override bool Replace(TKey key,TRecord record)=>table.Replace(key,record);
    public override TRecord this[TKey key]{get=>table[key];set=>table[key]=value;}
    public override IEnumerator<TRecord> GetEnumerator()=>table.GetEnumerator();
}
