using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using Seamless.Execution;

namespace Seamless.Transactions;

public sealed partial class WalFileDevice{
    internal WalMetadataPage ReadMetadata(long after,long ceiling,WalMetadataPosition? start,int maximumRecords,int maximumPayload,int maximumScan,int maximumFrames,ResourceBudget budget){
        using(this.EnterDevice()){
            ObjectDisposedException.ThrowIf(this.disposed,this);if(maximumScan<this.options.SectorBytes+WalFormat.HeaderBytes){throw new ArgumentOutOfRangeException(nameof(maximumScan));}var retained=budget.ReserveMemory(512);var page=new WalMetadataPage(after,ceiling,retained);
            try{
                if(after>=ceiling){return page;}
                var position=this.MetadataStart(after,start);var expected=position.ExpectedLsn;var rows=new List<WalMetadataRecord>();
                using var scratch=budget.ReserveMemory(this.options.SectorBytes+WalFormat.HeaderBytes);var buffer=Array.Empty<byte>();var header=new byte[WalFormat.HeaderBytes];var stop=false;
                for(var segment=position.Segment;segment<=this.ordinal&&!stop;segment++){
                    if(page.ScannedBytes+this.options.SectorBytes>maximumScan){stop=true;break;}
                    using var stream=new FileStream(this.SegmentPath(segment),FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
                    var identity=this.ReadSegmentHeader(stream);page.ScannedBytes+=this.options.SectorBytes;
                    var offset=(long)this.options.SectorBytes;if(segment==position.Segment){offset=position.Offset;}
                    if(identity.Id!=this.walId||identity.Ordinal!=segment||identity.Base>expected||offset<this.options.SectorBytes||offset>stream.Length||offset==this.options.SectorBytes&&identity.Base!=expected){throw new InvalidDataException("WAL metadata segment/cursor mismatch.");}
                    stream.Position=offset;page.NextPosition=new(segment,offset,expected);
                    while(stream.Position<stream.Length){
                        if(expected>ceiling||rows.Count>=maximumRecords||page.ScannedFrames>=maximumFrames||page.ScannedBytes+WalFormat.HeaderBytes>maximumScan){stop=true;break;}
                        var frameStart=stream.Position;stream.ReadExactly(header);var info=WalFormat.ReadHeader(header,Math.Max(this.options.MaxFrameBytes,this.options.SectorBytes+WalFormat.HeaderBytes));
                        if(info.Length>stream.Length-frameStart||info.Length>this.options.SegmentBytes-frameStart){throw new InvalidDataException("Incomplete durable WAL frame.");}
                        if(page.ScannedBytes+info.Length>maximumScan){if(page.ScannedFrames==0){throw new InvalidDataException("A WAL frame exceeds the metadata scan quota.");}stop=true;break;}
                        if(buffer.Length<info.Length){
                            var size=Math.Min(Math.Max(this.options.MaxFrameBytes,this.options.SectorBytes+WalFormat.HeaderBytes),Math.Max(info.Length,Math.Max(256,buffer.Length*2)));scratch.Resize(checked(this.options.SectorBytes+WalFormat.HeaderBytes+(long)buffer.Length+size));buffer=new byte[size];scratch.Resize(this.options.SectorBytes+WalFormat.HeaderBytes+(long)size);
                        }
                        header.CopyTo(buffer,0);stream.ReadExactly(buffer.AsSpan(WalFormat.HeaderBytes,info.Length-WalFormat.HeaderBytes));var frame=buffer.AsSpan(0,info.Length);WalFormat.CheckBody(frame);page.ScannedBytes+=info.Length;page.ScannedFrames++;
                        if(info.Type==2){if(info.Length>this.options.SectorBytes+WalFormat.HeaderBytes-1||stream.Position%this.options.SectorBytes!=0){throw new InvalidDataException("Invalid PAD sector boundary.");}}
                        else{
                            if(info.Length>this.options.MaxFrameBytes||info.Lsn!=expected){throw new InvalidDataException("WAL metadata LSN gap/duplicate.");}
                            if(info.Lsn>after&&info.Type==1){
                                var payload=MetadataPayload(frame,out var id);
                                if(payload.Length>maximumPayload){throw new InvalidDataException("A metadata payload exceeds the page quota.");}
                                if(payload.Length>maximumPayload-page.PayloadBytes){stop=true;break;}
                                if(payload.Length>0){retained.Resize(checked(retained.Bytes+payload.Length+128L));rows.Add(new(id,info.Lsn,payload.ToArray()));page.PayloadBytes+=payload.Length;}
                            }
                            expected=checked(expected+1);if(info.Lsn>after){page.ThroughLsn=info.Lsn;}
                        }
                        page.NextPosition=new(segment,stream.Position,expected);
                    }
                    if(expected>ceiling){stop=true;}
                }
                if(!stop&&expected<=ceiling){throw new InvalidDataException("Durable WAL suffix is missing.");}page.Records=rows.AsReadOnly();return page;
            }catch{page.Dispose();throw;}
        }
    }
    private WalMetadataPosition MetadataStart(long after,WalMetadataPosition? start){
        if(start is{} saved&&saved.Segment>=this.firstSegment&&saved.Segment<=this.ordinal&&saved.ExpectedLsn<=after+1){return saved;}
        var low=this.firstSegment;var high=this.ordinal;
        while(low<high){var middle=low+(high-low)/2;if(this.segmentEnds[middle]<=after){low=middle+1;}else{high=middle;}}
        long expected=this.baseLsn;if(low>this.firstSegment){expected=checked(this.segmentEnds[low-1]+1);}return new(low,this.options.SectorBytes,expected);
    }
    // 行の復元はwriter/recoveryが検査済み。転記側ではCRCと付随情報の境界だけを読み、業務行を生成しない。
    private static ReadOnlySpan<byte> MetadataPayload(ReadOnlySpan<byte> frame,out Guid id){
        var body=frame[WalFormat.HeaderBytes..];if(body.Length<24){throw new InvalidDataException("Invalid WAL metadata prefix.");}id=new(body[..16]);var count=BinaryPrimitives.ReadInt32LittleEndian(body[16..]);
        if(id==Guid.Empty||count<=0||count>(body.Length-24)/4){throw new InvalidDataException("Invalid WAL metadata identity/partitions.");}
        var offset=20;var prior=-1;for(var i=0;i<count;i++){var part=BinaryPrimitives.ReadInt32LittleEndian(body[offset..]);if(part<=prior){throw new InvalidDataException("Invalid WAL metadata partitions.");}prior=part;offset+=4;}
        var length=BinaryPrimitives.ReadInt32LittleEndian(body[offset..]);offset+=4;if(length<0||length>body.Length-offset-4){throw new InvalidDataException("Invalid WAL metadata length.");}return body.Slice(offset,length);
    }
}
