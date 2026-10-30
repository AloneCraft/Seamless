using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;

namespace Seamless.Database.Gateway.Tds;

public readonly struct TdsMessage{
    public TdsMessage(byte packetType,byte[]payload){
        this.PacketType=packetType;
        this.Payload=payload;
    }

    public byte PacketType{get;}
    public byte[]Payload{get;}
}

public static class TdsPacketReader{
    public static async ValueTask<TdsMessage?>ReadMessageAsync(PipeReader reader,CancellationToken cancellationToken=default){
        using var memoryStream=new MemoryStream();
        byte messageType=0;

        while(true){
            var result=await reader.ReadAsync(cancellationToken);
            var buffer=result.Buffer;

            while(buffer.Length>=TdsConstants.HeaderSize){
                var headerSpan=buffer.Slice(0,TdsConstants.HeaderSize).ToSpan();
                var packetType=headerSpan[0];
                var status=headerSpan[1];
                var length=BinaryPrimitives.ReadUInt16BigEndian(headerSpan.Slice(2,2));

                if(buffer.Length<length){
                    // パケット全体がまだ届いていないため、さらに読み進める
                    break;
                }else{
                }

                messageType=packetType;
                var payloadLength=length-TdsConstants.HeaderSize;
                if(payloadLength>0){
                    var payloadBuffer=buffer.Slice(TdsConstants.HeaderSize,payloadLength);
                    foreach(var segment in payloadBuffer){
                        memoryStream.Write(segment.Span);
                    }
                }else{
                }

                buffer=buffer.Slice(length);

                if((status&TdsConstants.StatusEndOfMessage)!=0){
                    reader.AdvanceTo(buffer.Start,buffer.End);
                    return new TdsMessage(messageType,memoryStream.ToArray());
                }else{
                }
            }

            reader.AdvanceTo(buffer.Start,buffer.End);

            if(result.IsCompleted||result.IsCanceled){
                if(memoryStream.Length>0){
                    return new TdsMessage(messageType,memoryStream.ToArray());
                }else{
                    return null;
                }
            }else{
            }
        }
    }

    private static Span<byte>ToSpan(this ReadOnlySequence<byte>sequence){
        if(sequence.IsSingleSegment){
            return sequence.FirstSpan.ToArray();
        }else{
            return sequence.ToArray();
        }
    }
}
