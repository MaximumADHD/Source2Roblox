using Source2Roblox.World.Types;

using RobloxFiles.DataTypes;
using RobloxFiles.Enums;

using System.Collections.Generic;
using System.IO;

namespace Source2Roblox.World.Lumps
{
    public class Planes : List<Plane>, ILump
    {
        public void Read(Stream stream, BinaryReader reader)
        {
            while (stream.Position < stream.Length)
            {
                Vector3 normal = reader.ReadVector3();

                float dist = reader.ReadSingle();
                Axis axis = (Axis)reader.ReadInt32();

                Plane result = new Plane(normal, dist, axis);
                Add(result);
            }
        }
    }
}
