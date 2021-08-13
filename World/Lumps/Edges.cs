using Source2Roblox.World.Types;
using System.Collections.Generic;
using System.IO;

namespace Source2Roblox.World.Lumps
{
    public class Edges : List<ushort>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                var index0 = reader.ReadUInt16();
                Add(index0);

                var index1 = reader.ReadUInt16();
                Add(index1);
            }
        }
    }

    public class SurfEdges : List<int>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            Capacity = 512000;

            while (stream.Position < stream.Length)
            {
                int edge = reader.ReadInt32();
                Add(edge);
            }
        }
    }
}
