using Common;
using MemoryPack;
using Tsavorite.core;
namespace Seamless.Collections.Tests;

[Collection("Sequential")]
public class DefaultSessionFunctions{
    private readonly struct DummyEpochAccessor:IEpochAccessor{
        public void Resume(){}
        public bool TrySuspend()=>true;
    }
    private static string CreateFolder()=>Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
    private const long TestMemorySize=Const.MinObjectLogCapacity;
    private static byte[] SerInt(int v)=>MemoryPackSerializer.Serialize(v);
    private static byte[] SerStr(string v)=>MemoryPackSerializer.Serialize(v);

    [Fact,Trait("Category","Coverage")]
    public void DefaultSessionFunctions_通常パス(){
        using var store=new DictionaryStore<int,string>();
        using var session=store.Store.NewSession<
            SpanByte,
            SpanByteAndMemory,
            Seamless.Collections.Empty,
            Seamless.Collections.DefaultSessionFunctions<int>
        >(new Seamless.Collections.DefaultSessionFunctions<int>());
        var bc=session.BasicContext;

        {
            var kb=SerInt(1);
            var vb=SerStr("hello");
            unsafe{
                fixed(byte* pk=kb,pv=vb){
                    var k=SpanByte.FromPinnedPointer(pk,kb.Length);
                    var v=SpanByte.FromPinnedPointer(pv,vb.Length);
                    var s=bc.Upsert(k,v);
                    if(s.IsPending){
                        bc.CompletePendingWithOutputs(out var c,wait:true);
                        c.Dispose();
                    }
                }
            }
        }
        {
            var kb=SerInt(1);
            unsafe{
                fixed(byte* pk=kb){
                    var k=SpanByte.FromPinnedPointer(pk,kb.Length);
                    SpanByteAndMemory output=default;
                    var s=bc.Read(ref k,ref output);
                    if(s.IsPending){
                        bc.CompletePendingWithOutputs(out var c,wait:true);
                        c.Next();
                        s=c.Current.Status;
                        c.Dispose();
                    }
                    Assert.False(s.NotFound);
                }
            }
        }
        {
            var kb=SerInt(1);
            var vb=SerStr("world");
            unsafe{
                fixed(byte* pk=kb,pv=vb){
                    var k=SpanByte.FromPinnedPointer(pk,kb.Length);
                    var v=SpanByte.FromPinnedPointer(pv,vb.Length);
                    var s=bc.Upsert(k,v);
                    if(s.IsPending){
                        bc.CompletePendingWithOutputs(out var c,wait:true);
                        c.Dispose();
                    }
                }
            }

        }
        {
            var kb=SerInt(2);
            var ib=SerStr("rmw_new");
            unsafe{
                fixed(byte* pk=kb,pi=ib){
                    var k=SpanByte.FromPinnedPointer(pk,kb.Length);
                    var i=SpanByte.FromPinnedPointer(pi,ib.Length);
                    SpanByteAndMemory output=default;
                    var s=bc.RMW(ref k,ref i,ref output);
                    if(s.IsPending){
                        bc.CompletePendingWithOutputs(out var c,wait:true);
                        c.Dispose();
                    }
                }
            }
        }
        {
            var kb=SerInt(2);
            var ib=SerStr("rmw_update");
            unsafe{
                fixed(byte* pk=kb,pi=ib){
                    var k=SpanByte.FromPinnedPointer(pk,kb.Length);
                    var i=SpanByte.FromPinnedPointer(pi,ib.Length);
                    SpanByteAndMemory output=default;
                    var s=bc.RMW(ref k,ref i,ref output);
                    if(s.IsPending){
                        bc.CompletePendingWithOutputs(out var c,wait:true);
                        c.Dispose();
                    }
                }
            }
        }
    }
}
