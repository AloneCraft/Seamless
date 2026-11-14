namespace Seamless.Database.SqlTranslation;
/// <summary>
/// SQL Server の型の規則。式の中での C# の型、型の優先順位、算術の結果の型、CASE・COALESCE の共通の型を決める。
/// 式の中の値はすべて NULL 許容の C# の型で持つ(date・datetime・datetime2・smalldatetime は DateTime?。real は単精度の float? のまま計算する。本物も単精度で計算するので、倍精度で計算すると結果がずれる)。
/// </summary>
internal static class SqlTypeRules{
    /// <summary>型の無い NULL リテラル。</summary>
    public static SqlTypeDescriptor Null{get;}=new("null");
    public static SqlTypeDescriptor Int{get;}=new("int");
    public static SqlTypeDescriptor BigInt{get;}=new("bigint");
    public static SqlTypeDescriptor Bit{get;}=new("bit");
    public static SqlTypeDescriptor Float{get;}=new("float");
    public static SqlTypeDescriptor DateTime{get;}=new("datetime");
    public static SqlTypeDescriptor Date{get;}=new("date");
    public static SqlTypeDescriptor DateTime2{get;}=new("datetime2",null,null,7);
    public static SqlTypeDescriptor UniqueIdentifier{get;}=new("uniqueidentifier");
    public static SqlTypeDescriptor NVarChar(int length,string? collation)=>new("nvarchar",length,null,null,collation);
    public static SqlTypeDescriptor VarChar(int length,string? collation)=>new("varchar",length,null,null,collation);
    public static SqlTypeDescriptor Decimal(int precision,int scale)=>new("decimal",null,(byte)Math.Clamp(precision,1,38),(byte)Math.Clamp(scale,0,38));
    public static bool IsNull(SqlTypeDescriptor type)=>type.Name=="null";
    public static bool IsString(SqlTypeDescriptor type)=>type.Name is "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" or "xml";
    public static bool IsUnicode(SqlTypeDescriptor type)=>type.Name is "nchar" or "nvarchar" or "ntext" or "xml";
    public static bool IsBinary(SqlTypeDescriptor type)=>type.Name is "binary" or "varbinary" or "image" or "timestamp" or "rowversion";
    public static bool IsInteger(SqlTypeDescriptor type)=>type.Name is "tinyint" or "smallint" or "int" or "bigint";
    public static bool IsExactNumeric(SqlTypeDescriptor type)=>IsInteger(type)||type.Name is "decimal" or "money" or "smallmoney";
    public static bool IsNumeric(SqlTypeDescriptor type)=>IsExactNumeric(type)||type.Name is "float" or "real";
    public static bool IsDecimal(SqlTypeDescriptor type)=>type.Name is "decimal" or "money" or "smallmoney";
    public static bool IsTemporal(SqlTypeDescriptor type)=>type.Name is "date" or "datetime" or "datetime2" or "smalldatetime";
    /// <summary>式の中での C# の型(NULL 許容)。扱えない型は例外。</summary>
    public static string ClrType(SqlTypeDescriptor type)=>type.Name switch{
        "bit"=>"bool?",
        "tinyint"=>"byte?",
        "smallint"=>"short?",
        "int"=>"int?",
        "bigint"=>"long?",
        "decimal" or "money" or "smallmoney"=>"decimal?",
        "float"=>"double?",
        "real"=>"float?",
        "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" or "xml"=>"string?",
        "binary" or "varbinary" or "image" or "timestamp" or "rowversion"=>"byte[]?",
        "uniqueidentifier"=>"global::System.Guid?",
        "date" or "datetime" or "datetime2" or "smalldatetime"=>"global::System.DateTime?",
        "datetimeoffset"=>"global::System.DateTimeOffset?",
        "time"=>"global::System.TimeOnly?",
        _=>throw new TranslationUnsupportedException("SQLT010",$"型 {type.Name} は扱えない。",0,0)
    };
    /// <summary>生成された行の型のプロパティの C# の型(取込の生成器と同じ。date は DateOnly、time は TimeOnly)。</summary>
    public static string StorageType(SqlTypeDescriptor type,bool nullable){
        var name=type.Name switch{
            "bit"=>"bool",
            "tinyint"=>"byte",
            "smallint"=>"short",
            "int"=>"int",
            "bigint"=>"long",
            "decimal" or "money" or "smallmoney"=>"decimal",
            "float"=>"double",
            "real"=>"float",
            "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" or "xml"=>"string",
            "binary" or "varbinary" or "image" or "timestamp" or "rowversion"=>"byte[]",
            "uniqueidentifier"=>"global::System.Guid",
            "date"=>"global::System.DateOnly",
            "datetime" or "datetime2" or "smalldatetime"=>"global::System.DateTime",
            "datetimeoffset"=>"global::System.DateTimeOffset",
            "time"=>"global::System.TimeOnly",
            _=>throw new TranslationUnsupportedException("SQLT010",$"型 {type.Name} は扱えない。",0,0)
        };
        if(nullable){
            return name+"?";
        }
        return name;
    }
    /// <summary>
    /// 型の優先順位。2つの型が出会うと、優先順位の高い方へそろえる(SQL Server の「データ型の優先順位」)。数が大きいほど高い。
    /// </summary>
    public static int Precedence(SqlTypeDescriptor type)=>type.Name switch{
        "datetimeoffset"=>30,
        "datetime2"=>29,
        "datetime"=>28,
        "smalldatetime"=>27,
        "date"=>26,
        "time"=>25,
        "float"=>24,
        "real"=>23,
        "decimal"=>22,
        "money"=>21,
        "smallmoney"=>20,
        "bigint"=>19,
        "int"=>18,
        "smallint"=>17,
        "tinyint"=>16,
        "bit"=>15,
        "ntext"=>14,
        "text"=>13,
        "image"=>12,
        "timestamp" or "rowversion"=>11,
        "uniqueidentifier"=>10,
        "nvarchar"=>9,
        "nchar"=>8,
        "varchar"=>7,
        "char"=>6,
        "varbinary"=>5,
        "binary"=>4,
        "xml"=>3,
        _=>0
    };
    /// <summary>比較や CASE などで2つの型が出会ったときにそろえる型。</summary>
    public static SqlTypeDescriptor Common(SqlTypeDescriptor left,SqlTypeDescriptor right){
        if(IsNull(left)){
            return right;
        }
        if(IsNull(right)){
            return left;
        }
        if(IsString(left)&&IsString(right)){
            var name=left.Name;
            if(Precedence(right)>Precedence(left)){
                name=right.Name;
            }
            int length=-1;
            if(left.Length is{} a&&right.Length is{} b&&a>=0&&b>=0){
                length=Math.Max(a,b);
            }
            return new(name,length,null,null,left.Collation??right.Collation);
        }
        if(IsDecimal(left)&&IsDecimal(right)||IsDecimal(left)&&IsInteger(right)||IsInteger(left)&&IsDecimal(right)){
            if(left.Name==right.Name&&left.Name is "money" or "smallmoney"){
                return left;
            }
            var (p1,s1)=Shape(left);
            var (p2,s2)=Shape(right);
            var integral=Math.Max(p1-s1,p2-s2);
            var scale=Math.Max(s1,s2);
            if(integral+scale>38){
                scale=Math.Max(0,38-integral);
            }
            return Decimal(integral+scale,scale);
        }
        if(Precedence(right)>Precedence(left)){
            return right;
        }
        return left;
    }
    /// <summary>算術(+ - * / %)の結果の型。decimal は SQL Server の精度と桁の規則に従う。</summary>
    public static SqlTypeDescriptor Arithmetic(SqlTypeDescriptor left,SqlTypeDescriptor right,char operation){
        if(IsNull(left)){
            left=right;
        }
        if(IsNull(right)){
            right=left;
        }
        if(left.Name=="float"||right.Name=="float"){
            return Float;
        }
        if(left.Name=="real"||right.Name=="real"){
            return new("real");
        }
        var money=left.Name is "money" or "smallmoney"||right.Name is "money" or "smallmoney";
        if(money&&left.Name!="decimal"&&right.Name!="decimal"){
            if(left.Name=="money"||right.Name=="money"){
                return new("money",null,19,4);
            }
            return new("smallmoney",null,10,4);
        }
        if(IsDecimal(left)||IsDecimal(right)){
            var (p1,s1)=Shape(left);
            var (p2,s2)=Shape(right);
            int precision,scale;
            switch(operation){
                case '*':
                    precision=p1+p2+1;
                    scale=s1+s2;
                    break;
                case '/':
                    scale=Math.Max(6,s1+p2+1);
                    precision=p1-s1+s2+scale;
                    break;
                case '%':
                    scale=Math.Max(s1,s2);
                    precision=Math.Min(p1-s1,p2-s2)+scale;
                    break;
                default:
                    scale=Math.Max(s1,s2);
                    precision=Math.Max(p1-s1,p2-s2)+scale+1;
                    break;
            }
            if(precision>38){
                var integral=precision-scale;
                if(operation is '*' or '/'){
                    // 精度が38を超えるとき、整数部を残し、桁は6より小さくしない(SQL Server の規則)。
                    scale=Math.Max(Math.Min(scale,38-integral),Math.Min(scale,6));
                }else{
                    scale=Math.Max(0,Math.Min(scale,38-integral));
                }
                precision=38;
            }
            return Decimal(precision,scale);
        }
        if(Precedence(right)>Precedence(left)){
            return right;
        }
        return left;
    }
    /// <summary>decimal としての精度と桁。整数型は桁0の精度で数える。</summary>
    public static (int Precision,int Scale) Shape(SqlTypeDescriptor type)=>type.Name switch{
        "decimal"=>(type.Precision??18,type.Scale??0),
        "money"=>(19,4),
        "smallmoney"=>(10,4),
        "bigint"=>(19,0),
        "int"=>(10,0),
        "smallint"=>(5,0),
        "tinyint"=>(3,0),
        "bit"=>(1,0),
        _=>(18,0)
    };
}
