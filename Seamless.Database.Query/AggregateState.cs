using Seamless.Execution;
using Seamless.Collections.Persisted;
using Seamless.Linq;
namespace Seamless.Database.Query;
internal sealed class AggregateState:IDisposable{
    private readonly AggregateBinding binding;
    private readonly ExecutionScope scope;
    private readonly ExecutionOptions?options;
    private readonly ScopedStoredSequence<object?[]>?distinct;
    private long count;
    private object?value;
    internal AggregateState(AggregateBinding binding,ExecutionScope scope,ExecutionOptions?options=null){this.binding=binding;this.scope=scope;this.options=options;if(binding.Distinct){this.distinct=new(scope);}scope.Own(this);}
    internal void Add(EvalFrame frame){object?item=null;if(this.binding.Value is not null){item=this.binding.Value.Value(frame);if(item is null){return;}}if(this.distinct is not null){this.distinct.Append([item]);return;}this.Accumulate(item);}
    private void Accumulate(object?item){this.count=checked(this.count+1);switch(this.binding.Name){case "COUNT":case "COUNT_BIG":return;case "SUM":case "AVG":if(this.value is null){this.value=SqlValues.ConvertTo(item,this.binding.Type);}else{this.value=SqlValues.Binary(this.value,item,SqlBinaryOperator.Add,this.binding.Type);}return;case "MIN":if(this.value is null||SqlValues.CompareValue(item,this.value,this.binding.Type)<0){this.value=item;}return;case "MAX":if(this.value is null||SqlValues.CompareValue(item,this.value,this.binding.Type)>0){this.value=item;}return;}}
    internal object?Finish(){
        if(this.distinct is not null){this.distinct.Seal();object?[]?previous=null;var has=false;foreach(var item in SqlBoundedOperators.Sort(new(this.scope,new System.Collections.Generic.Dictionary<string,object?>(),this.options),this.distinct,x=>x,new SqlRowComparer(types:[this.binding.Value?.Type??this.binding.Type]))){if(!has||new SqlRowComparer(types:[this.binding.Value?.Type??this.binding.Type]).Compare(previous,item)!=0){this.Accumulate(item[0]);previous=item;has=true;}}}
        if(this.binding.Name=="COUNT"){return checked((int)this.count);}if(this.binding.Name=="COUNT_BIG"){return this.count;}if(this.value is null){return null;}if(this.binding.Name=="AVG"){return SqlValues.Binary(this.value,this.count,SqlBinaryOperator.Divide,this.binding.Type);}return this.value;
    }
    public void Dispose(){this.distinct?.Dispose();this.scope.Release(this);}
}

