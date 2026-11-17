using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Security.Cryptography;
using System.Text;

namespace Seamless.Transactions;

internal static class WalMetadataAcknowledgement{
    internal const int Length=68;
    internal static string GetPath(string directory)=>Path.Combine(directory,"metadata-ack.v1");
    internal static byte[] Encode(Guid wal,string consumer,long lsn){var bytes=new byte[Length];var span=bytes.AsSpan();"SLACK001"u8.CopyTo(span);wal.TryWriteBytes(span[8..24]);BinaryPrimitives.WriteInt64LittleEndian(span[24..32],lsn);SHA256.HashData(Encoding.UTF8.GetBytes(consumer)).CopyTo(span[32..64]);BinaryPrimitives.WriteUInt32LittleEndian(span[64..],Crc32.HashToUInt32(span[..64]));return bytes;}
    internal static long Read(string directory,Guid wal,string consumer,long maximumLsn,long minimumLsn){
        Span<byte> span=stackalloc byte[Length];using(var file=new FileStream(GetPath(directory),FileMode.Open,FileAccess.Read,FileShare.Read)){if(file.Length!=Length){throw new InvalidDataException("Invalid metadata acknowledgement size.");}file.ReadExactly(span);}
        if(!span[..8].SequenceEqual("SLACK001"u8)||new Guid(span[8..24])!=wal||!span[32..64].SequenceEqual(SHA256.HashData(Encoding.UTF8.GetBytes(consumer)))||BinaryPrimitives.ReadUInt32LittleEndian(span[64..])!=Crc32.HashToUInt32(span[..64])){throw new InvalidDataException("Metadata acknowledgement identity/CRC mismatch.");}
        var lsn=BinaryPrimitives.ReadInt64LittleEndian(span[24..32]);if(lsn<0||lsn>maximumLsn||lsn<minimumLsn){throw new InvalidDataException("Metadata acknowledgement is outside retained WAL coverage.");}return lsn;
    }
}
