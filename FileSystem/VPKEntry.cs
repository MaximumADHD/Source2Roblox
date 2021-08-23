using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Source2Roblox.FileSystem
{
    public class VPKEntryChunk
    {
        public readonly ushort PackFileIndex;
        public readonly uint ChunkOffset;
        public readonly uint ChunkSize;

        public VPKEntryChunk(ushort index, uint offset, uint size)
        {
            PackFileIndex = index;
            ChunkOffset = offset;
            ChunkSize = size;
        }
    }

    public struct VPKEntry
    {
        public readonly List<VPKEntryChunk> Chunks;

        private VPKEntryChunk Chunk0
        {
            get
            {
                if (Chunks.Any())
                    return Chunks.First();

                return null;
            }
        }

        public bool HasData => Chunks.Any();
        public readonly uint CRC;

        public ushort Index => Chunk0?.PackFileIndex ?? 0;
        public uint Offset => Chunk0?.ChunkOffset ?? 0;
        public uint Size => Chunk0?.ChunkSize ?? 0;

        public VPKEntry(BinaryReader reader)
        {
            CRC = reader.ReadUInt32();
            
            var skip = reader.ReadUInt16();
            Chunks = new List<VPKEntryChunk>();
            
            while (true)
            {
                ushort index = reader.ReadUInt16();

                if (index == 0xFFFF)
                    break;

                uint offset = reader.ReadUInt32();
                uint size = reader.ReadUInt32();

                var chunk = new VPKEntryChunk(index, offset, size);
                Chunks.Add(chunk);
            }

            reader.Skip(skip);
        }
    }
}
