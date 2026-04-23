using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Xunit;
namespace Seamless.Collections.Tests;
public class TestRelation{
    private static ulong GetHashCode64<T>(ref T value) where T:unmanaged{
        var span=MemoryMarshal.CreateReadOnlySpan(ref value,1);
        var bytes=MemoryMarshal.AsBytes(span);
        return XxHash64.HashToUInt64(bytes);
    }
    private partial struct UnmanagedKey0{
        public partial int Id0{get;}
        public partial int Id1{get;}
    }
    private partial struct UnmanagedValue0{
        public partial int Id0{get;}
        public partial int Id1{get;}
        public partial int Value0{get;}
        public partial int Value1{get;}
    }
    private partial struct UnmanagedKey0(int Id0,int Id1):IEquatable<UnmanagedKey0>{
        public partial int Id0=>
            this.Id0;
        public partial int Id1=>
            this.Id1;
        public bool Equals(UnmanagedKey0 other){
            if(this.Id0!=other.Id0) return false;
            if(this.Id1!=other.Id1) return false;
            return true;
        }
        public override bool Equals(object? obj){
            return obj is UnmanagedKey0 other&&this.Equals(other);
        }
        public static bool operator==(UnmanagedKey0 left,UnmanagedKey0 right){
            if(left.Id0!=right.Id0) return false;
            if(left.Id1!=right.Id1) return false;
            return true;
        }
        public static bool operator!=(UnmanagedKey0 left,UnmanagedKey0 right){
            if(left.Id0!=right.Id0) return true;
            if(left.Id1!=right.Id1) return true;
            return false;
        }
    }
    private partial struct UnmanagedValue0:IEquatable<UnmanagedValue0>{
        private struct Unmanaged{
            public int Id0;
            public int Id1;
            public int Value0;
            public int Value1;
        }
        private readonly Unmanaged _unmanaged;
        public UnmanagedValue0(int Id0,int Id1,int Value0,int Value1){
            this._unmanaged=new Unmanaged{
                Id0=Id0,
                Id1=Id1,
                Value0=Value0,
                Value1=Value1,
            };
        }
        public UnmanagedKey0 Key=>new(this._unmanaged.Id0,this._unmanaged.Id1);
        public partial int Id0=>this._unmanaged.Id0;
        public partial int Id1=>this._unmanaged.Id1;
        public partial int Value0=>this._unmanaged.Value0;
        public partial int Value1=>this._unmanaged.Value1;

        public bool Equals(UnmanagedValue0 other){
            if(this._unmanaged.Id0!=other._unmanaged.Id0) return false;
            if(this._unmanaged.Id1!=other._unmanaged.Id1) return false;
            if(this._unmanaged.Value0!=other._unmanaged.Value0) return false;
            if(this._unmanaged.Value1!=other._unmanaged.Value1) return false;
            return true;
        }
        public override bool Equals(object? obj)=>
            obj is UnmanagedValue0 other&&this.Equals(other);
        public override int GetHashCode()=>
            HashCode.Combine(this._unmanaged.Id0,this._unmanaged.Id1,this._unmanaged.Value0,this._unmanaged.Value1);
        public static bool operator==(UnmanagedValue0 left,UnmanagedValue0 right)=>left.Equals(right);
        public static bool operator!=(UnmanagedValue0 left,UnmanagedValue0 right)=>!left.Equals(right);
    }
    private partial struct UnmanagedKey1{
        public partial int Id0{get;}
        public partial int Id1{get;}
    }
    private partial struct ManagedValue1{
        public partial int Id0{get;}
        public partial int Id1{get;}
        public partial string Value0{get;}
        public partial int Value1{get;}
    }
    private partial struct UnmanagedKey1(int Id0,int Id1):IEquatable<UnmanagedKey1>{
        public partial int Id0=>
            this.Id0;
        public partial int Id1=>
            this.Id1;
        public bool Equals(UnmanagedKey1 other){
            if(this.Id0!=other.Id0) return false;
            if(this.Id1!=other.Id1) return false;
            return true;
        }
        public override bool Equals(object? obj){
            return obj is UnmanagedKey1 other&&this.Equals(other);
        }
        public override int GetHashCode(){
            return HashCode.Combine(this.Id0,this.Id1);
        }
        public static bool operator==(UnmanagedKey1 left,UnmanagedKey1 right){
            if(left.Id0!=right.Id0) return false;
            if(left.Id1!=right.Id1) return false;
            return true;
        }
        public static bool operator!=(UnmanagedKey1 left,UnmanagedKey1 right){
            if(left.Id0!=right.Id0) return true;
            if(left.Id1!=right.Id1) return true;
            return false;
        }
    }
    private partial struct ManagedValue1:IEquatable<ManagedValue1>{
        private struct Unmanaged{
            public int Id0;
            public int Id1;
            public int Value1;
        }
        private Unmanaged _unmanaged;
        private struct Managed{
            public string Value0;
        }
        private Managed _managed;
        public ManagedValue1(int Id0,int Id1,string Value0,int Value1){
            this._unmanaged=new Unmanaged{
                Id0=Id0,
                Id1=Id1,
                Value1=Value1,
            };
            this._managed=new Managed{
                Value0=Value0,
            };
        }
        public UnmanagedKey1 Key=>new(this._unmanaged.Id0,this._unmanaged.Id1);
        public partial int Id0=>this._unmanaged.Id0;
        public partial int Id1=>this._unmanaged.Id1;
        public partial string  Value0=>this._managed.Value0;
        public partial int Value1=>this._unmanaged.Value1;

        public bool Equals(ManagedValue1 other){
            var L=MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<Unmanaged,byte>(ref Unsafe.AsRef(in this._unmanaged)),12);
            var R=MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<Unmanaged,byte>(ref Unsafe.AsRef(in other._unmanaged)),12);
            if(!L.SequenceEqual(R)) return false;
            if(this._managed.Value0!=other._managed.Value0) return false;
            return true;
        }
        public override bool Equals(object? obj)=>
            obj is ManagedValue1 other&&this.Equals(other);
        public override int GetHashCode(){
            var h64=GetHashCode64(ref this._unmanaged);
            return(int)(h64^(h64>>32));
        }
        public static bool operator==(ManagedValue1 left,ManagedValue1 right)=>left.Equals(right);
        public static bool operator!=(ManagedValue1 left,ManagedValue1 right)=>!left.Equals(right);
    }
    [Fact,Trait("Category","Bug")]public void Relation0(){
        var store=new RelationStore<UnmanagedKey0,UnmanagedValue0>();
        var R=store.Create();
        R.Add(new UnmanagedValue0(1,2,3,4));
    }
}
