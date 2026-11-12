namespace Seamless.Database;

internal static class ViewDefinitionCalls{
    [ThreadStatic]private static List<(DatabaseContainer Owner,string Name,global::System.Reflection.MethodInfo Method)>? active;

    internal static Lease Enter(DatabaseContainer owner,string name,global::System.Reflection.MethodInfo method){
        var calls=active??=new();
        if(calls.Count>=64){throw new InvalidOperationException("宣言ビューの呼出し深さが上限64を超えました。");}
        foreach(var call in calls){
            if(ReferenceEquals(call.Owner,owner)&&StringComparer.Ordinal.Equals(call.Name,name)&&call.Method.Equals(method)){
                throw new InvalidOperationException("宣言ビューの定義が循環しています: "+name);
            }
        }
        calls.Add((owner,name,method));
        return new Lease();
    }

    internal readonly struct Lease:IDisposable{
        public void Dispose(){active!.RemoveAt(active.Count-1);}
    }
}
