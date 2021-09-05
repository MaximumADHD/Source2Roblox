using System.IO;

namespace Source2Roblox.FileSystem
{
    public class VPKEntry
    {
        public readonly uint CRC = 0;
        public readonly ushort Index = 0;
        public readonly uint Offset = 0;
        public readonly uint Size = 0;

        public readonly ushort PreloadBytes;
        public readonly byte[] PreloadContent;

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
    }
}
