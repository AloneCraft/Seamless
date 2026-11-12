using System.Buffers;
using System.Buffers.Binary;
using Seamless.Database.Client;
using Wire=Seamless.Database.Client.NativeAuthenticationWire;

namespace Management.Tests;

public sealed class NativeAuthenticationWire{
    [Fact]public void TokensAndAcknowledgementsSurviveEverySegmentBoundary(){
        const string token="eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJkZXYtdXNlciJ9.signature";
        var request=Wire.Request(token);for(var split=0;split<=request.Length;split++){Assert.Equal(token,Wire.ReadRequest(Split(request,split)));}
        var authority=Guid.NewGuid();const string subject="利用者-α";var response=Wire.Acknowledgement(authority,subject);
        for(var split=0;split<=response.Length;split++){Assert.Equal((authority,subject),Wire.ReadAcknowledgement(Split(response,split)));}
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public void IncompleteRequestHeaderIsRejected(int length)=>Assert.Throws<InvalidDataException>(()=>Wire.ReadRequest(new(new byte[length])));
    [Theory][InlineData(0,2)][InlineData(1,2)][InlineData(2,0)][InlineData(2,-1)][InlineData(2,int.MaxValue)][InlineData(2,65537)]
    public void InvalidRequestHeaderIsRejectedBeforeTextDecoding(int field,int value){
        var bytes=Wire.Request("a.b.c");if(field<2){bytes[field]=(byte)value;}else{BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2),value);}
        Assert.Throws<InvalidDataException>(()=>Wire.ReadRequest(new(bytes)));
    }
    [Fact]public void RequestRequiresExactLengthAndValidUtf8(){
        var request=Wire.Request("a.b.c");Assert.Throws<InvalidDataException>(()=>Wire.ReadRequest(new(request[..^1])));Assert.Throws<InvalidDataException>(()=>Wire.ReadRequest(new(request.Concat(new byte[]{0}).ToArray())));
        request[^1]=0xff;Assert.Throws<InvalidDataException>(()=>Wire.ReadRequest(new(request)));
    }
    [Theory][InlineData("")][InlineData(" ")][InlineData("a b.c")][InlineData("a\nb.c")][InlineData("a\0b.c")]
    public void EmptyOrWhitespaceTokensAreRejected(string token)=>Assert.ThrowsAny<ArgumentException>(()=>Wire.Request(token));
    [Fact]public void TokenLimitIsAppliedOnBothSides(){
        var token=new string('x',Wire.MaximumTokenBytes);Assert.Equal(token,Wire.ReadRequest(new(Wire.Request(token))));Assert.Throws<ArgumentException>(()=>Wire.Request(token+"x"));
        Assert.Throws<ArgumentException>(()=>Wire.Request(new string('界',Wire.MaximumTokenBytes/3+1)));
        var bytes=new byte[Wire.MaximumTokenBytes+7];bytes[0]=1;bytes[1]=1;BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2),Wire.MaximumTokenBytes+1);Assert.Throws<InvalidDataException>(()=>Wire.ReadRequest(new(bytes)));
    }
    [Fact]public void AcknowledgementRequiresAnAuthorityAndExactSubject(){
        Assert.Throws<ArgumentException>(()=>Wire.Acknowledgement(Guid.Empty,"user"));Assert.Throws<ArgumentException>(()=>Wire.Acknowledgement(Guid.NewGuid()," "));Assert.Throws<ArgumentException>(()=>Wire.Acknowledgement(Guid.NewGuid(),"user\0"));Assert.Throws<ArgumentException>(()=>Wire.Acknowledgement(Guid.NewGuid(),new string('u',1025)));
        var bytes=Wire.Acknowledgement(Guid.NewGuid(),"user");Assert.Throws<InvalidDataException>(()=>Wire.ReadAcknowledgement(new(bytes[..^1])));Assert.Throws<InvalidDataException>(()=>Wire.ReadAcknowledgement(new(bytes.Concat(new byte[]{0}).ToArray())));
        bytes[0]=2;Assert.Throws<InvalidDataException>(()=>Wire.ReadAcknowledgement(new(bytes)));
    }
    [Theory][InlineData(NativeAuthenticationError.InvalidRequest)][InlineData(NativeAuthenticationError.Rejected)][InlineData(NativeAuthenticationError.Unavailable)][InlineData(NativeAuthenticationError.Busy)]
    public void FailureCodesUseABoundedVersionedResponse(NativeAuthenticationError code){var bytes=Wire.Failure(code);Assert.Equal(2,bytes.Length);Assert.Equal(code,Wire.ReadFailure(bytes));}
    [Fact]public void FailureResponsesRejectUnknownCodesAndTrailingData(){
        Assert.Throws<InvalidDataException>(()=>Wire.ReadFailure(new byte[]{1,0}));Assert.Throws<InvalidDataException>(()=>Wire.ReadFailure(new byte[]{2,1}));Assert.Throws<InvalidDataException>(()=>Wire.ReadFailure(new byte[]{1,1,0}));Assert.Throws<ArgumentOutOfRangeException>(()=>Wire.Failure((NativeAuthenticationError)255));
    }
    [Theory][InlineData((byte)0)][InlineData((byte)32)][InlineData((byte)127)]
    public void ReceivedControlCharactersAreRejected(byte character){var bytes=Wire.Request("a.b.c");bytes[^1]=character;Assert.Throws<InvalidDataException>(()=>Wire.ReadRequest(new(bytes)));}
    [Fact]public void ReceivedSubjectValidationDoesNotTrustTheAcknowledgement(){
        var bytes=Wire.Acknowledgement(Guid.NewGuid(),"user");bytes[^1]=0xff;Assert.Throws<InvalidDataException>(()=>Wire.ReadAcknowledgement(new(bytes)));
        bytes=Wire.Acknowledgement(Guid.NewGuid(),"user");bytes[^1]=0;Assert.Throws<InvalidDataException>(()=>Wire.ReadAcknowledgement(new(bytes)));
        bytes=Wire.Acknowledgement(Guid.NewGuid(),"user");bytes.AsSpan(1,16).Clear();Assert.Throws<InvalidDataException>(()=>Wire.ReadAcknowledgement(new(bytes)));
    }
    private static ReadOnlySequence<byte> Split(byte[] bytes,int split){var first=new Segment(bytes.AsMemory(0,split));var last=first.Append(bytes.AsMemory(split));return new(first,0,last,last.Memory.Length);}
    private sealed class Segment:ReadOnlySequenceSegment<byte>{
        internal Segment(ReadOnlyMemory<byte> memory){this.Memory=memory;}
        internal Segment Append(ReadOnlyMemory<byte> memory){var next=new Segment(memory){RunningIndex=this.RunningIndex+this.Memory.Length};this.Next=next;return next;}
    }
}
