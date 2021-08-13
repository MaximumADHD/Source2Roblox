using Source2Roblox.World.Types;
using System.Collections.Generic;
using System.IO;

namespace Source2Roblox.World.Lumps
{
    public class Brushes : List<Brush>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                Brush brush = new Brush(reader);
                Add(brush);
            }
        }
    }

    public class BrushSides : List<BrushSide>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                BrushSide side = new BrushSide(reader);
                Add(side);
            }
        }
    }
}
