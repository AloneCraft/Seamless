using System.Runtime.CompilerServices;

namespace Seamless.Execution;

internal ref partial struct RecordGraphReader{
    private void ValidationNode(){
        this.state.Check();
        if(++this.validationNodes>this.validationLimits!.MaximumNodes){throw new InvalidDataException("The graph exceeds the validation node limit.");}
    }

    public void Skip<T>(){
        this.ValidationNode();
        if(++this.validationDepth>this.validationLimits!.MaximumDepth){throw new InvalidDataException("The graph exceeds the validation depth limit.");}
        try{this.SkipValue<T>();}finally{this.validationDepth--;}
    }

    private void SkipValue<T>(){
        RuntimeHelpers.EnsureSufficientExecutionStack();
        var policy=RecordGraphPolicy.Get(typeof(T));
        if(RecordScalarValue<T>.IsScalar){policy.SkipFields(ref this,0);return;}
        if(typeof(T).IsValueType){this.state.Reserve(policy.Values.ObjectBytes);policy.SkipFields(ref this,0);return;}
        var marker=this.Byte();if(marker==0){return;}
        if(marker==3){
            if(!typeof(T).IsAssignableFrom(typeof(string))||this.Text() is null){throw new InvalidDataException("Invalid string graph node.");}
            return;
        }
        if(marker==1){
            var id=this.Unsigned();
            if(id==0||this.validatedReferences is null||id>this.validatedReferences.Count||!typeof(T).IsAssignableFrom(this.validatedReferences[id-1])){
                throw new InvalidDataException("Invalid graph reference.");
            }
            return;
        }
        if(marker!=2){throw new InvalidDataException("Invalid graph node marker.");}
        var dynamicType=this.Byte();Type type;
        if(dynamicType==0){type=typeof(T);}else if(dynamicType==1){type=this.Type();}else{throw new InvalidDataException("Invalid graph type marker.");}
        if(!typeof(T).IsAssignableFrom(type)||type==typeof(string)||type.IsInterface||type.IsAbstract||Nullable.GetUnderlyingType(type) is not null){throw new InvalidDataException("Incompatible graph node type.");}
        var nodePolicy=RecordGraphPolicy.Get(type);this.state.Reserve(256);
        (this.validatedReferences??=[]).Add(type);
        long count=0;
        if(type.IsArray){
            var rank=type.GetArrayRank();Span<int> lengths=stackalloc int[rank];Span<int> bounds=stackalloc int[rank];
            count=this.ArrayBounds(type,nodePolicy,lengths,bounds);
        }else{this.state.ReserveOutput(Math.Max(32,nodePolicy.Values.ObjectBytes));}
        nodePolicy.SkipFields(ref this,count);
    }
}
