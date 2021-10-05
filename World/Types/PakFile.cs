using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace Source2Roblox.World.Types
{
    public enum PakCompressionMethod
    {
        None = 0,
        DEFLATE = 8,
        LZMA = 14
    }

    public class PakFileEntry
    {
        public string FileName;
        public byte[] Data;

        public uint UncompressedSize;
        public PakCompressionMethod CompressionMethod;
    }

    public class PakFile
    {
        public PakFile(BinaryReader reader)
        {
            var stream = reader.BaseStream;
            int length = (int)stream.Length;

            for (int i = length - 0x16; i > length - 0x40; i--)
            {
                stream.Position = i;
                var str = reader.ReadString(4);

                if (str == "PK\x5\x6")
                {
                    reader.Skip(4);
                    break;
                }

                continue;
            }

            /*
            ushort numEntries = reader.ReadUInt16();
            reader.Skip(6);

            uint dirStart = reader.ReadUInt32();
            stream.Position = dirStart;

            for (int i = 0; i < numEntries; i++)
            {
                var header = reader.ReadString(4);
                reader.Skip(6);

                var compressionMethod = reader.ReadUInt16();
                reader.Skip(8);

                var compressedSize = reader.ReadUInt32();
                var uncompressedSize = reader.ReadUInt32();

                var fileNameLength = reader.ReadUInt16();
                var extraFieldLength = reader.ReadUInt16();
                var commentSize = reader.ReadUInt16();

                var localHeaderOffset = reader.ReadUInt32();
                var fileName = reader.ReadString(fileNameLength);

                var restore = stream.Position + 0x2E + extraSize + commentSize;
                stream.Position = localHeaderOffset + 0x1A;

                var fileNameLength2 = reader.ReadUInt16();
                Debug.Assert(fileNameLength == fileNameLength2);

                var extraSize2 = reader.ReadUInt16();
                stream.Position = localHeaderOffset + 0x1E + fileNameLength + extraSize2;

                var data = reader.ReadBytes(dataSize);
                stream.Position = restore;
            }

            Debugger.Break();*/
        }
    }
}
