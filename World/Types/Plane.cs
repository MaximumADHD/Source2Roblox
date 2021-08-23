using RobloxFiles.DataTypes;
using RobloxFiles.Enums;
using System.IO;

namespace Source2Roblox.World.Types
{
    public class Plane
    {
        public readonly Vector3 Normal;
        public readonly float Dist;
        public readonly Axis? Axis;

        public Plane(Vector3 normal, float dist, Axis? axis = null)
        {
            Normal = normal.Unit;
            Dist = dist;
            Axis = axis;
        }

        public Plane(BinaryReader reader)
        {
            Normal = reader.ReadVector3();
            Dist = reader.ReadSingle();
            Axis = (Axis)reader.ReadInt32();
        }

        public override string ToString()
        {
            return $"{{{Normal}}} [{Dist}]";
        }
    }
}
