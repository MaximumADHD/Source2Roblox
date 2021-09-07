using System.IO;
using System.IO.Compression;

namespace Source2Roblox.FileSystem
{
    public class VPKEntry
    {
        public readonly uint CRC = 0;
        public readonly ushort Index = 0;
        public readonly uint Offset = 0;
        public readonly uint Size = 0;

        public readonly int PreloadBytes;
        public readonly ZipArchiveEntry ZipEntry;

        private byte[] PreloadContent;

        public VPKEntry(BinaryReader reader)
        {
            CRC = reader.ReadUInt32();

            PreloadBytes = reader.ReadUInt16();
            Index = reader.ReadUInt16();

            if (Index != 0xFFFF)
            {
                Offset = reader.ReadUInt32();
                Size = reader.ReadUInt32();
            }

            PreloadContent = reader.ReadBytes(PreloadBytes);
            reader.Skip(2);
        }

        public byte[] EmbeddedContent
        {
            get
            {
                if (Index != 0x7FFF)
                    return null;

                if (PreloadContent != null)
                    return PreloadContent;

                if (ZipEntry == null)
                    return null;

                using (var stream = ZipEntry.Open())
                using (var reader = new BinaryReader(stream))
                {
                    PreloadContent = reader.ReadBytes(PreloadBytes);
                    return PreloadContent;
                }
            }
        }

        public VPKEntry(ZipArchiveEntry entry)
        {
            Index = 0x7FFF;
            ZipEntry = entry;
            PreloadBytes = (int)entry.Length;
        }
    }
}
