using System.Buffers;
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using MemoryPack;

namespace Seamless.Database.Client;

/// <summary>既知の通信型を、値の生成・formatter呼出しなしで事前検査する。</summary>
public static class NativeWireValidation{
    private static readonly ConditionalWeakTable<Type,NativeWireValidator> Validators=new();
    public static NativeWireValidator For<T>()=>For(typeof(T));
    public static NativeWireValidator For(Type type)=>Validators.GetValue(type,static item=>new(new Builder().Build(item)));
    public static void Validate<T>(ReadOnlySpan<byte> bytes,NativeWireLimits? limits=null)=>For<T>().Validate(bytes,limits);
    public static void ValidateSequence<T>(ReadOnlySequence<byte> bytes,NativeWireLimits? limits=null)=>For<T>().ValidateSequence(bytes,limits);

    private sealed class Builder{
        private readonly HashSet<Type> active=[];
        private int count;
        public Plan Build(Type type){
            if(this.active.Count==0){var version=typeof(MemoryPackSerializer).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];if(version!="1.21.4"){throw new NotSupportedException("The native wire validation profile requires the verified MemoryPack 1.21.4 runtime.");}}
            if(++this.count>1024||this.active.Count>=32||!this.active.Add(type)){throw Unsupported(type);}
            try{
                if(type.IsPointer||type.IsFunctionPointer||type.IsByRefLike||type==typeof(IntPtr)||type==typeof(UIntPtr)||type==typeof(object)||type.IsInterface||type.IsAbstract||type.ContainsGenericParameters||typeof(Delegate).IsAssignableFrom(type)){throw Unsupported(type);}
                if(type==typeof(string)){return new(Kind.String);}
                if(type.IsArray){if(type.GetArrayRank()!=1){throw Unsupported(type);}return new(Kind.Array,children:[this.Build(type.GetElementType()!)]);}
                if(type.IsValueType&&!References(type)){
                    FixedGraph(type,[]);var size=Size(type);if(size<=0||size>NativeFrame.MaximumBytes-1){throw Unsupported(type);}return new(Kind.Fixed,size);
                }
                if(Nullable.GetUnderlyingType(type) is{} underlying){return new(Kind.Nullable,children:[this.Build(underlying)]);}
                if(type.IsGenericType){throw Unsupported(type);}
                CheckHooks(type);
                var layout=type.GetCustomAttribute<NativeWireLayoutAttribute>();
                var members=new List<MemberInfo>();var kind=Kind.Object;
                if(layout is not null){
                    if(layout.Version!=1||!type.IsValueType||layout.Members.Count>249||layout.Members.Distinct(StringComparer.Ordinal).Count()!=layout.Members.Count){throw Unsupported(type);}
                    kind=Kind.Sequential;
                    foreach(var name in layout.Members){members.Add(type.GetProperty(name,BindingFlags.Public|BindingFlags.Instance)??throw Unsupported(type));}
                }else{
                    var attribute=type.GetCustomAttribute<MemoryPackableAttribute>();
                    if(attribute is null||attribute.GenerateType!=GenerateType.Object){throw Unsupported(type);}
                    var hierarchy=new Stack<Type>();for(var current=type;current is not null&&current!=typeof(object)&&current!=typeof(ValueType);current=current.BaseType){hierarchy.Push(current);}
                    while(hierarchy.TryPop(out var declaring)){
                        var declared=declaring.GetMembers(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(Serialized).ToArray();
                        var explicitOrder=declared.Any(static member=>member.GetCustomAttribute<MemoryPackOrderAttribute>() is not null);
                        if(explicitOrder&&declared.Any(static member=>member.GetCustomAttribute<MemoryPackOrderAttribute>() is null)||!explicitOrder&&declared.Any(static member=>member is FieldInfo)&&declared.Any(static member=>member is PropertyInfo)){throw Unsupported(type);}
                        if(explicitOrder){declared=declared.OrderBy(static member=>member.GetCustomAttribute<MemoryPackOrderAttribute>()!.Order).ToArray();}
                        else{declared=declared.OrderBy(static member=>member.MetadataToken).ToArray();}
                        members.AddRange(declared);
                    }
                    if(members.Count>249){throw Unsupported(type);}
                }
                var children=new List<Plan>();
                foreach(var member in members){
                    CheckAttributes(member);var memberType=MemberType(member);
                    if(layout is not null&&Nullable.GetUnderlyingType(memberType) is{} value){children.Add(new(Kind.GeneratedNullable,children:[this.Build(value)]));}
                    else{children.Add(this.Build(memberType));}
                }
                return new(kind,children:children.ToArray(),nullable:!type.IsValueType);
            }finally{this.active.Remove(type);}
        }
        private static bool Serialized(MemberInfo member){
            if(member.IsDefined(typeof(MemoryPackIgnoreAttribute))){return false;}var included=member.IsDefined(typeof(MemoryPackIncludeAttribute));
            return member is FieldInfo field&&(field.IsPublic||included)||member is PropertyInfo property&&property.GetIndexParameters().Length==0&&(property.GetMethod?.IsPublic==true||included);
        }
        private static Type MemberType(MemberInfo member){if(member is FieldInfo field){return field.FieldType;}return((PropertyInfo)member).PropertyType;}
        private static void CheckHooks(Type type){
            CheckAttributes(type);
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance)){foreach(var attribute in method.CustomAttributes){if(attribute.AttributeType.Namespace=="MemoryPack"&&attribute.AttributeType.Name.StartsWith("MemoryPackOn",StringComparison.Ordinal)){throw Unsupported(type);}}}
        }
        private static void CheckAttributes(MemberInfo member){
            foreach(var attribute in member.CustomAttributes){
                for(var type=attribute.AttributeType;type is not null;type=type.BaseType){if(type.IsGenericType&&type.Namespace=="MemoryPack"&&type.Name.StartsWith("MemoryPackCustomFormatterAttribute",StringComparison.Ordinal)){throw Unsupported(MemberTypeOwner(member));}}
                if(attribute.AttributeType.Namespace=="MemoryPack"&&attribute.AttributeType.Name is "MemoryPackUnionAttribute" or "MemoryPackUnionFormatterAttribute" or "MemoryPackAllowSerializeAttribute"){throw Unsupported(MemberTypeOwner(member));}
            }
        }
        private static Type MemberTypeOwner(MemberInfo member)=>member as Type??member.DeclaringType!;
        private static void FixedGraph(Type type,HashSet<Type> active){
            if(type.IsPointer||type.IsFunctionPointer||type==typeof(IntPtr)||type==typeof(UIntPtr)||active.Count>=32||!active.Add(type)){throw Unsupported(type);}
            try{
                if(type.IsPrimitive||type.IsEnum){return;}
                foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)){FixedGraph(field.FieldType,active);}
            }finally{active.Remove(type);}
        }
        private static bool References(Type type)=>(bool)typeof(Builder).GetMethod(nameof(ReferencesCore),BindingFlags.NonPublic|BindingFlags.Static)!.MakeGenericMethod(type).Invoke(null,null)!;
        private static bool ReferencesCore<T>()=>RuntimeHelpers.IsReferenceOrContainsReferences<T>();
        private static int Size(Type type)=>(int)typeof(Builder).GetMethod(nameof(SizeCore),BindingFlags.NonPublic|BindingFlags.Static)!.MakeGenericMethod(type).Invoke(null,null)!;
        private static int SizeCore<T>()=>Unsafe.SizeOf<T>();
        private static NotSupportedException Unsupported(Type type)=>new("The type is outside the verified native wire profile: "+type.FullName);
    }

    internal enum Kind{Fixed,String,Array,Object,Sequential,Nullable,GeneratedNullable}
    internal sealed class Plan(Kind kind,int size=0,Plan[]? children=null,bool nullable=false){
        private readonly Plan[] children=children??[];
        public int FixedSize{get{if(kind==Kind.Fixed){return size;}return 0;}}
        public void Read(ref Reader reader){
            reader.Step();
            switch(kind){
                case Kind.Fixed:reader.Skip(size);return;
                case Kind.String:reader.String();return;
                case Kind.Array:
                    var count=reader.Integer();if(count==-1){return;}reader.ArrayCount(count);
                    var element=this.children[0];if(element.FixedSize!=0){reader.TakeProduct(count,element.FixedSize);return;}
                    for(var index=0;index<count;index++){element.Read(ref reader);}return;
                case Kind.Nullable:
                    var optional=reader.Byte();if(optional==255){return;}if(optional!=1){throw Invalid();}this.children[0].Read(ref reader);return;
                case Kind.GeneratedNullable:
                    var present=reader.Byte();if(present==0){return;}if(present!=1){throw Invalid();}this.children[0].Read(ref reader);return;
                case Kind.Object:
                    var members=reader.Byte();if(nullable&&members==255){return;}if(members!=this.children.Length){throw Invalid();}break;
                case Kind.Sequential:break;
                default:throw Invalid();
            }
            foreach(var child in this.children){child.Read(ref reader);}
        }
    }
    internal ref struct Reader{
        private ReadOnlySpan<byte> remaining;
        private ReadOnlySequence<byte> sequence;
        private readonly bool segmented;
        private readonly NativeWireLimits limits;
        private int nodes;
        private int elements;
        private static readonly UTF8Encoding StrictUtf8=new(false,true);
        public Reader(ReadOnlySpan<byte> bytes,NativeWireLimits limits){this.remaining=bytes;this.sequence=default;this.segmented=false;this.limits=limits;this.nodes=limits.MaximumValueNodes;this.elements=limits.MaximumArrayElements;}
        public Reader(ReadOnlySequence<byte> bytes,NativeWireLimits limits){this.remaining=bytes.FirstSpan;this.sequence=bytes;this.segmented=!bytes.IsSingleSegment;this.limits=limits;this.nodes=limits.MaximumValueNodes;this.elements=limits.MaximumArrayElements;}
        private long Remaining{get{if(this.segmented){return this.sequence.Length;}return this.remaining.Length;}}
        public void Step(){if(--this.nodes<0){throw Invalid();}}
        private ReadOnlySpan<byte> TakeSpan(int length){if(length<0||length>this.remaining.Length){throw Invalid();}var value=this.remaining[..length];this.remaining=this.remaining[length..];return value;}
        public void Skip(int length){if(length<0||length>this.Remaining){throw Invalid();}if(this.segmented){this.sequence=this.sequence.Slice(length);}else{this.remaining=this.remaining[length..];}}
        public byte Byte(){if(!this.segmented){return this.TakeSpan(1)[0];}var reader=new SequenceReader<byte>(this.sequence);if(!reader.TryRead(out var value)){throw Invalid();}this.sequence=this.sequence.Slice(reader.Position);return value;}
        public int Integer(){if(!this.segmented){return BinaryPrimitives.ReadInt32LittleEndian(this.TakeSpan(4));}var reader=new SequenceReader<byte>(this.sequence);if(!reader.TryReadLittleEndian(out int value)){throw Invalid();}this.sequence=this.sequence.Slice(reader.Position);return value;}
        public void TakeProduct(int count,int width){var length=(long)count*width;if(length<0||length>this.Remaining){throw Invalid();}this.Skip((int)length);}
        public void ArrayCount(int count){if(count<0||count>this.elements){throw Invalid();}this.elements-=count;}
        public bool String(){
            var header=this.Integer();if(header==-1){return false;}if(header==0){return true;}
            if(header>0){if(header>this.limits.MaximumStringCharacters){throw Invalid();}this.TakeProduct(header,2);return true;}
            var length=~header;var characters=this.Integer();if(length<0||length>this.Remaining||characters<0||characters>this.limits.MaximumStringCharacters){throw Invalid();}
            int actual;
            try{if(this.segmented){actual=this.CountUtf8(this.sequence.Slice(0,length));this.Skip(length);}else{actual=StrictUtf8.GetCharCount(this.TakeSpan(length));}}catch(DecoderFallbackException){throw Invalid();}
            if(actual>this.limits.MaximumStringCharacters||characters!=0&&characters!=actual){throw Invalid();}return true;
        }
        private int CountUtf8(ReadOnlySequence<byte> data){
            if(data.IsSingleSegment){return StrictUtf8.GetCharCount(data.FirstSpan);}
            var decoder=StrictUtf8.GetDecoder();Span<char> characters=stackalloc char[256];var total=0;long processed=0;
            foreach(var memory in data){
                var bytes=memory.Span;
                while(true){
                    decoder.Convert(bytes,characters,processed+bytes.Length==data.Length,out var used,out var written,out var completed);processed+=used;total+=written;bytes=bytes[used..];
                    if(total>this.limits.MaximumStringCharacters){throw Invalid();}if(completed){break;}if(used==0&&written==0){throw Invalid();}
                }
            }
            return total;
        }
        public void Finish(){if(this.Remaining!=0){throw Invalid();}}
    }
    private static InvalidDataException Invalid()=>new("The native payload violates its declared type or receive limits.");
}

public sealed class NativeWireValidator{
    private readonly NativeWireValidation.Plan plan;
    internal NativeWireValidator(NativeWireValidation.Plan plan)=>this.plan=plan;
    public void Validate(ReadOnlySpan<byte> bytes,NativeWireLimits? limits=null){
        limits??=NativeWireLimits.Default;limits.CheckProfile();if(bytes.Length>limits.MaximumBytes){throw new InvalidDataException("The native payload exceeds the receive limit.");}
        var reader=new NativeWireValidation.Reader(bytes,limits);this.plan.Read(ref reader);reader.Finish();
    }
    public void ValidateSequence(ReadOnlySequence<byte> bytes,NativeWireLimits? limits=null){
        limits??=NativeWireLimits.Default;limits.CheckProfile();if(bytes.Length>limits.MaximumBytes){throw new InvalidDataException("The native payload exceeds the receive limit.");}
        var reader=new NativeWireValidation.Reader(bytes,limits);this.plan.Read(ref reader);reader.Finish();
    }
}
