using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Seamless.Collections;
using Seamless.Stores;

Console.WriteLine($""sizeof(EqualityEmpty)          = {Unsafe.SizeOf<EqualityEmpty>()}"");
Console.WriteLine($""sizeof(int)                    = {Unsafe.SizeOf<int>()}"");

var store = Factory.Create().Create<int>();
var set = store.Create();
Console.WriteLine($""Initial TailAddress            = {store.Store.Log.TailAddress}"");
Console.WriteLine($""Initial BeginAddress           = {store.Store.Log.BeginAddress}"");
set.Add(1);
Console.WriteLine($""After Add(1) TailAddress       = {store.Store.Log.TailAddress}"");
set.Add(2);
Console.WriteLine($""After Add(2) TailAddress       = {store.Store.Log.TailAddress}"");
set.Add(3);
Console.WriteLine($""After Add(3) TailAddress       = {store.Store.Log.TailAddress}"");
Console.WriteLine($""RecordSize (3 records)         = {(store.Store.Log.TailAddress-64) / 3}"");
set.Dispose(); store.Dispose();
