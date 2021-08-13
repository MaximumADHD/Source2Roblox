using RobloxFiles.DataTypes;

using System.Collections.Generic;
using System.IO;

namespace Source2Roblox.World.Lumps
{
    public class Vertices : List<Vector3>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                Vector3 vec = reader.ReadVector3();
                Add(vec);
            }
        }
    }

    public class VertNormals : List<Vector3>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                Vector3 vec = reader.ReadVector3();
                Add(vec);
            }
        }
    }

    public class VertNormalIndices : List<ushort>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                ushort value = reader.ReadUInt16();
                Add(value);
            }
        }
    }
}
