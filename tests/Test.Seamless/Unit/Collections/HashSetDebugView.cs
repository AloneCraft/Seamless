//using Seamless.Collections;
//using Seamless.Stores;
//namespace Test.Seamless.Collections;

//public class HashSetDebugView{
//    [Fact,Trait("Category","Unit")]
//    public void Items_�S�G���g����Ԃ�(){
//        using var store=HashStore.Create().CreateUnmanagedHashSet<int>();
//        using UnmanagedHashSet<int> set=store.Create();
//        set.Add(10);
//        set.Add(20);
//        var view=new DebugView<int>(set);
//        var items=view.Items;
//        Assert.Equal(2,items.Length);
//        Assert.Contains(10,items);
//        Assert.Contains(20,items);
//    }
//    [Fact,Trait("Category","Unit")]
//    public void ctor_null_��O(){
//        Assert.Throws<ArgumentNullException>(()=>new DebugView<int>(null!));
//    }
//}
