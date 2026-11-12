using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using Seamless.Transport.Kestrel;

namespace Seamless.Database.Gateway.Tds;

public readonly struct TdsMessage{
    public TdsMessage(byte packetType,byte[]payload,byte status=0){
        this.PacketType=packetType;
        this.Payload=payload;this.Status=status;
    }

    public byte PacketType{get;}
    public byte Status{get;}
    public byte[]Payload{get;}
}

public static class TdsPacketReader{
    private const int MaximumMessageBytes=16*1024*1024;
    public static ValueTask<TdsMessage?>ReadMessageAsync(PipeReader reader,CancellationToken cancellationToken=default)=>ReadMessageAsync(reader,cancellationToken,null);
    internal static async ValueTask<TdsMessage?>ReadMessageAsync(PipeReader reader,CancellationToken cancellationToken,TimeSpan? incompleteMessageTimeout,FrameBufferQuota? receiveBuffers=null,bool protectedLoginLimits=false){
        MemoryStream? memoryStream=null;
        IDisposable? streamReservation=null;
        byte messageType=0;byte messageStatus=0;
        var hasPacket=false;
        CancellationTokenSource? deadline=null;

        try{
        while(true){
            var result=await reader.ReadAsync(deadline?.Token??cancellationToken);
            var buffer=result.Buffer;
            var examined=buffer.End;
            bool incomplete;
            try{
                while(buffer.Length>=TdsConstants.HeaderSize){
                    var length=ReadHeader(buffer,out var packetType,out var status);
                    if(length<TdsConstants.HeaderSize){throw new InvalidDataException("TDS packet is shorter than its header.");}
                    if(buffer.Length<length){
                        break;
                    }else{
                    }

                    var messageLimit=protectedLoginLimits?TdsFedAuthLogin.MaximumLoginBytes:MaximumMessageBytes;
                    var payloadBuffer=buffer.Slice(TdsConstants.HeaderSize,length-TdsConstants.HeaderSize);
                    var endOfMessage=(status&TdsConstants.StatusEndOfMessage)!=0;
                    if(!hasPacket&&endOfMessage){
                        using var copyReservation=Reserve(payloadBuffer.Length);
                        var payload=payloadBuffer.ToArray();
                        buffer=buffer.Slice(length);
                        examined=buffer.Start;
                        return new TdsMessage(packetType,payload,status);
                    }else{
                    }
                    if(hasPacket&&messageType!=packetType){throw new InvalidDataException("TDS message changes packet type between fragments.");}
                    messageType=packetType;messageStatus|=status;hasPacket=true;
                    memoryStream??=new MemoryStream();
                    var required=memoryStream.Length+payloadBuffer.Length;
                    if(required>messageLimit){throw new InvalidDataException("TDS request exceeds the maximum message length.");}
                    if(receiveBuffers is not null&&required>memoryStream.Capacity){
                        var capacity=(int)Math.Max(required,Math.Min(messageLimit,Math.Max(256,2*memoryStream.Capacity)));
                        var nextReservation=Reserve(capacity);
                        try{memoryStream.Capacity=capacity;}
                        catch{nextReservation?.Dispose();throw;}
                        streamReservation?.Dispose();
                        streamReservation=nextReservation;
                    }
                    foreach(var segment in payloadBuffer){
                        memoryStream.Write(segment.Span);
                    }

                    buffer=buffer.Slice(length);
                    if(endOfMessage){
                        using var copyReservation=Reserve(memoryStream.Length);
                        var payload=memoryStream.ToArray();
                        examined=buffer.Start;
                        return new TdsMessage(messageType,payload,messageStatus);
                    }else{
                    }
                }
                incomplete=hasPacket||!buffer.IsEmpty;
            }finally{
                reader.AdvanceTo(buffer.Start,examined);
            }

            if(result.IsCanceled&&deadline?.IsCancellationRequested==true){throw new OperationCanceledException(deadline.Token);}
            if(result.IsCompleted||result.IsCanceled){
                if(incomplete){
                    throw new EndOfStreamException("The client disconnected during a TDS message.");
                }else{
                    return null;
                }
            }else if(deadline is null&&incompleteMessageTimeout is{} timeout&&incomplete){
                deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(timeout);
            }
        }
        }catch(OperationCanceledException error)when(deadline?.IsCancellationRequested==true&&!cancellationToken.IsCancellationRequested){
            throw new ReceiveDeadlineExceeded(error);
        }finally{deadline?.Dispose();memoryStream?.Dispose();streamReservation?.Dispose();}

        IDisposable? Reserve(long bytes){
            if(receiveBuffers is null||bytes==0){return null;}
            return receiveBuffers.TryReserve(bytes)??throw new ReceiveBufferLimitExceeded();
        }
    }

    internal sealed class ReceiveDeadlineExceeded(OperationCanceledException cause):IOException("The incomplete TDS message has timed out.",cause);
    internal sealed class ReceiveBufferLimitExceeded():IOException("The TDS receive buffer limit has been reached.");

    private static ushort ReadHeader(ReadOnlySequence<byte>buffer,out byte packetType,out byte status){
        Span<byte>header=stackalloc byte[4];
        buffer.Slice(0,header.Length).CopyTo(header);
        packetType=header[0];
        status=header[1];
        return BinaryPrimitives.ReadUInt16BigEndian(header.Slice(2));
    }
}
