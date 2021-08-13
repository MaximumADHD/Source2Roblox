using Source2Roblox.World.Types;
using System.Collections.Generic;
using System.IO;

namespace Source2Roblox.World.Lumps
{
    public class Displacements : List<DispInfo>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                DispInfo info = new DispInfo(reader);
                Add(info);
            }
        }
    }

    public class DispVerts : List<DispVert>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                DispVert vert = new DispVert(reader);
                Add(vert);
            }
        }
    }

    public class DispTris : List<DispTriangleTags>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                ushort tags = reader.ReadUInt16();
                Add((DispTriangleTags)tags);
            }
        }
    }
}
