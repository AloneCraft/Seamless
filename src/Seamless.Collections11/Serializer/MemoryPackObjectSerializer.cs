namespace Seamless.Collections;
using MemoryPack;
using Tsavorite.core;
/// <summary>
/// MemoryPackを使用してTsavoriteのオブジェクトシリアライズを行う。
/// Tsavoriteがストレージにオーバーフローした際にマネージドオブジェクトを正しくシリアライズ/デシリアライズする。
/// 各レコードはlength-prefixed形式で書き込む。
/// </summary>
internal sealed class MemoryPackObjectSerializer<T>:IObjectSerializer<T>{
	private Stream stream=null!;
	public void BeginSerialize(Stream stream)=>this.stream=stream;
	public void Serialize(ref T obj){
		var stream=this.stream;
		var bytes=MemoryPackSerializer.Serialize(obj);
		var length=bytes.Length;
		Span<byte> lengthBytes=stackalloc byte[4];
		BitConverter.TryWriteBytes(lengthBytes,length);
		stream.Write(lengthBytes);
		stream.Write(bytes);
	}
	public void EndSerialize()=>this.stream=null!;
	public void BeginDeserialize(Stream stream)=>this.stream=stream;
	public void Deserialize(out T obj){
		var stream=this.stream;
		Span<byte> lengthBytes=stackalloc byte[4];
		stream.ReadExactly(lengthBytes);
		var length=BitConverter.ToInt32(lengthBytes);
		var buffer=new byte[length];
		stream.ReadExactly(buffer);
		obj=MemoryPackSerializer.Deserialize<T>(buffer)!;
	}
	public void EndDeserialize()=>this.stream=null!;
}
