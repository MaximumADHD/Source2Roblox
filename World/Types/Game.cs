using RobloxFiles.DataTypes;
using System.IO;
using System.Linq;

namespace Source2Roblox.World.Types
{
    public class GameLump
    {
        public readonly string Id;
        public readonly ushort Flags;
        public readonly ushort Version;

        public readonly int Offset;
        public readonly int Length;
        public byte[] Content { get; private set; }
        
        public GameLump(BinaryReader reader)
        {
            Id = reader.ReadString(4);
            Id = string.Concat(Id.Reverse());

            Flags = reader.ReadUInt16();
            Version = reader.ReadUInt16();

            Offset = reader.ReadInt32();
            Length = reader.ReadInt32();
        }

        public void Read(BinaryReader reader)
        {
            if (Content != null)
                return;

            var buffer = BSPFile.ReadBuffer(reader, Offset, Length);
            Content = buffer.ToArray();

            buffer.Dispose();
        }
    }
}
