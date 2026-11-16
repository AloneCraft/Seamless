using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Seamless.Database;

/// <summary>
/// DB の生成器が作った行の型(Tables と [Key] で宣言した行)の、列と作り方。生成コンストラクターに付いた <see cref="GeneratedRecordConstructorAttribute"/> の列名の一覧から読む。
/// 行のコンストラクターは先頭がキーの型(<c>Key</c>)で、残りがキー以外の列。キーの型は自分の生成コンストラクターでキーの列を受け取る。
/// 取込・Gateway・世代の移行は、列の名前と値から行を作るときにこれを使い、キーの型を先に作ることを意識しない。
/// 先頭がキーでない生成コンストラクター(キーの型そのものなど)は、すべての列を受け取る形として扱う。
/// </summary>
internal sealed class GeneratedRecordShape{
    private const string KeyMember="Key";
    private readonly ConstructorInfo constructor;
    private readonly string[] members;
    private readonly ConstructorInfo? keyConstructor;
    private readonly string[] keyMembers;

    private GeneratedRecordShape(Type recordType,ConstructorInfo constructor,string[] members,ConstructorInfo? keyConstructor,string[] keyMembers){
        this.RecordType=recordType;this.constructor=constructor;this.members=members;this.keyConstructor=keyConstructor;this.keyMembers=keyMembers;
    }

    public Type RecordType{get;}
    /// <summary>行を作るのに要るすべての列の名前(キーの列を先に、残りをコンストラクターの順に)。</summary>
    public IEnumerable<string> Columns=>this.keyMembers.Concat(this.members);
    /// <summary>キーの型が持つ列の名前(キーを受け取らない形では空)。</summary>
    public IEnumerable<string> KeyColumns=>this.keyMembers;
    /// <summary>キーの型を先頭に受け取る行か。</summary>
    public bool IsKeyed=>this.keyConstructor is not null;

    /// <summary>生成コンストラクターを持たない型(手書きの行など)では null。</summary>
    public static GeneratedRecordShape? TryCreate(Type recordType){
        ArgumentNullException.ThrowIfNull(recordType);
        foreach(var candidate in recordType.GetConstructors(BindingFlags.Public|BindingFlags.Instance)){
            if(Members(candidate) is not{} members){continue;}
            var parameters=candidate.GetParameters();
            if(parameters.Length!=members.Count){throw new InvalidDataException("The generated record constructor metadata does not match its parameters: "+recordType.FullName);}
            if(members.Count>0&&members[0]==KeyMember&&IsRecordKey(recordType,parameters[0].ParameterType)){
                var keyType=parameters[0].ParameterType;
                foreach(var keyCandidate in keyType.GetConstructors(BindingFlags.Public|BindingFlags.Instance)){
                    if(Members(keyCandidate) is not{} keyMembers){continue;}
                    if(keyCandidate.GetParameters().Length!=keyMembers.Count){throw new InvalidDataException("The generated key constructor metadata does not match its parameters: "+keyType.FullName);}
                    return new(recordType,candidate,members.Skip(1).ToArray(),keyCandidate,keyMembers.ToArray());
                }
                throw new InvalidDataException("The generated key type has no generated constructor: "+keyType.FullName);
            }
            return new(recordType,candidate,members.ToArray(),null,[]);
        }
        return null;
    }

    // 生成した DB は別の読み込み領域(AssemblyLoadContext)で読むことがあり、属性や IRecord<> の型が呼び出し側の型と同じとは限らない。完全な名前で見分ける。
    private static IReadOnlyList<string>? Members(ConstructorInfo constructor){
        foreach(var attribute in constructor.GetCustomAttributesData()){
            if(attribute.AttributeType.FullName!=typeof(GeneratedRecordConstructorAttribute).FullName||attribute.ConstructorArguments.Count!=1){continue;}
            if(attribute.ConstructorArguments[0].Value is not IReadOnlyCollection<CustomAttributeTypedArgument> values){continue;}
            return values.Select(static value=>(string)value.Value!).ToArray();
        }
        return null;
    }

    private static bool IsRecordKey(Type recordType,Type keyType)=>recordType.GetInterfaces().Any(contract=>
        contract.IsGenericType&&contract.GetGenericTypeDefinition().FullName=="Seamless.Collections.IRecord`1"&&contract.GenericTypeArguments[0]==keyType);

    /// <summary>
    /// キーの型を先頭に受け取る行の型か(生成コンストラクターを読めない場合も含めて、型の形から判定する)。
    /// 生成器が作る行は Key をキーの列から作り、非公開のフィールド _Key に持つ。手で書いた IRecord&lt;TKey&gt; の行(取引を通さない表の行など)の Key は本物の列なので外さない。
    /// </summary>
    public static bool IsDeclaredRecord(Type recordType)=>recordType.GetInterfaces().Any(contract=>
        contract.IsGenericType&&contract.GetGenericTypeDefinition().FullName=="Seamless.Collections.IRecord`1")
        &&recordType.GetField("_Key",BindingFlags.NonPublic|BindingFlags.Instance) is not null;

    /// <summary>列の名前から値を読み、行を作る。</summary>
    public object Create(Func<string,object?> column){
        ArgumentNullException.ThrowIfNull(column);
        var values=this.members.Select(column).ToList();
        if(this.keyConstructor is not null){values.Insert(0,this.keyConstructor.Invoke(this.keyMembers.Select(column).ToArray()));}
        return this.constructor.Invoke(values.ToArray());
    }

    /// <summary>列の名前から値の式を読み、行を作る式を返す。</summary>
    public Expression New(Func<string,Expression> column){
        ArgumentNullException.ThrowIfNull(column);
        var values=this.members.Select(column).ToList();
        if(this.keyConstructor is not null){values.Insert(0,Expression.New(this.keyConstructor,this.keyMembers.Select(column)));}
        return Expression.New(this.constructor,values);
    }

    /// <summary>
    /// 列の保存した値を読む式(計算列は、プロパティを読むと Gateway が登録した式で計算し直すため、保存した値はこちらで読む)。
    /// キーの列は _Key、キー以外の列は参照を含む列(_C)・含まない列(_S)の入れ物に持つ。
    /// </summary>
    public Expression Stored(Expression record,string column){
        ArgumentNullException.ThrowIfNull(record);
        if(this.keyConstructor is null){throw new InvalidDataException("The generated type is not a keyed record: "+this.RecordType.FullName);}
        // キーの列(計算列を主キーにした表など)はキーの型 _Key が持つ。キーの型のプロパティは計算し直さない。
        if(Array.IndexOf(this.keyMembers,column)>=0){
            var key=this.RecordType.GetField("_Key",BindingFlags.NonPublic|BindingFlags.Instance)??throw new InvalidDataException("The generated record has no key field: "+this.RecordType.FullName);
            return Expression.Property(Expression.Field(record,key),column);
        }
        foreach(var group in new[]{"_C","_S"}){
            if(this.RecordType.GetField(group,BindingFlags.NonPublic|BindingFlags.Instance) is not{} storage){continue;}
            if(storage.FieldType.GetField(column,BindingFlags.Public|BindingFlags.Instance) is{} field){return Expression.Field(Expression.Field(record,storage),field);}
        }
        throw new InvalidDataException("The generated record has no stored field for the column: "+column);
    }
}
