using System.IO;
using System.Collections.Generic;
using Source2Roblox.World.Types;

namespace Source2Roblox.World.Lumps
{
    public class Faces : List<Face>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                Face face = new Face(reader);
                Add(face);
            }
        }
    }
}
