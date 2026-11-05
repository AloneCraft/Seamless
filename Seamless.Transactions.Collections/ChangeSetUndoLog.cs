using System.Buffers.Binary;
using Seamless.Execution;
namespace Seamless.Transactions.Collections;

// Undo entries contain index pointers, never a second copy of the row payload.
internal sealed class ChangeSetUndoLog(ExecutionScope resources,long memoryLimit):IDisposable{
    internal delegate void RestoreEntry(ReadOnlySpan<byte> key,long previousOffset);
    private readonly List<(long Offset,byte[] Bytes)> frames=[];
    private readonly ResourceReservation memory=resources.Budget.ReserveMemory(0);
    private TemporaryFile? file;
    private OwnedBuffer? scratch;
    public long Position{get;private set;}
    public void Append(ReadOnlySpan<byte> key,long previousOffset){
        var size=checked(key.Length+16);
        if(this.scratch is null||this.scratch.Memory.Length<size){var next=resources.Rent(size);this.scratch?.Dispose();this.scratch=next;}
        var bytes=this.scratch.Memory.Span[..size];
        BinaryPrimitives.WriteInt32LittleEndian(bytes,key.Length);key.CopyTo(bytes[4..]);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[(4+key.Length)..],previousOffset);
        BinaryPrimitives.WriteInt32LittleEndian(bytes[(size-4)..],size);
        if(this.file is null&&this.memory.Bytes+size+48<=memoryLimit){
            var previous=this.memory.Bytes;this.memory.Resize(checked(previous+size+48));
            try{this.frames.Add((this.Position,bytes.ToArray()));}catch{this.memory.Resize(previous);throw;}
        }else{
            if(this.file is null){
                var candidate=resources.CreateTemporaryFile();
                try{foreach(var frame in this.frames){candidate.Write(frame.Bytes);}this.file=candidate;}
                catch{candidate.Dispose();throw;}
                this.frames.Clear();this.memory.Resize(0);
            }
            this.file.Position=this.Position;
            try{this.file.Write(bytes);}catch{this.file.SetLength(this.Position);throw;}
        }
        this.Position=checked(this.Position+size);
    }
    public void Rewind(long position,RestoreEntry restore){
        if(position<0||position>this.Position){throw new InvalidOperationException("The savepoint is no longer reachable.");}
        Span<byte> footer=stackalloc byte[4];
        while(this.Position>position){
            if(this.file is null){
                var frame=this.frames[^1];if(frame.Offset<position){throw new InvalidDataException("Invalid undo position.");}
                Restore(frame.Bytes,restore);this.frames.RemoveAt(this.frames.Count-1);
                this.Position=frame.Offset;this.memory.Resize(this.memory.Bytes-frame.Bytes.Length-48);
            }else{
                this.file.Position=this.Position-4;this.file.ReadExactly(footer);
                var size=BinaryPrimitives.ReadInt32LittleEndian(footer);
                if(size<16||size>this.Position-position||this.scratch is null||size>this.scratch.Memory.Length){throw new InvalidDataException("Invalid undo frame.");}
                var start=this.Position-size;this.file.Position=start;var bytes=this.scratch.Memory.Span[..size];this.file.ReadExactly(bytes);
                Restore(bytes,restore);this.file.SetLength(start);this.Position=start;
            }
        }
    }
    private static void Restore(ReadOnlySpan<byte> bytes,RestoreEntry restore){
        var keyLength=BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if(keyLength<0||keyLength!=bytes.Length-16){throw new InvalidDataException("Invalid undo key.");}
        restore(bytes.Slice(4,keyLength),BinaryPrimitives.ReadInt64LittleEndian(bytes[(4+keyLength)..]));
    }
    public void Dispose(){try{this.file?.Dispose();}finally{try{this.scratch?.Dispose();}finally{this.frames.Clear();this.memory.Dispose();}}}
}
