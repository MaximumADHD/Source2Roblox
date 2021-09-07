using System.IO;
using RobloxFiles.DataTypes;

namespace Source2Roblox.Geometry
{
    public class Tangent
    {
        public readonly float X, Y, Z, Sign;

        public override string ToString()
        {
            return $"{X}, {Y}, {Z}, {Sign}";
        }

        public Tangent(float x, float y, float z, float sign)
        {
            X = x;
            Y = y;
            Z = z;

            Sign = sign;
        }

        public Tangent(Vector3 pos, float sign)
        {
            X = pos.X;
            Y = pos.Y;
            Z = pos.Z;

            Sign = sign;
        }

        public Tangent(uint xyzs)
        {
            if (xyzs == 0)
            {
                X = 0;
                Y = 0;
                Z = -1;
                Sign = 1;
            }
            else
            {
                uint x = xyzs >> 24;
                X = (x - 127) / 127f;

                uint y = (xyzs << 8) >> 24;
                Y = (y - 127) / 127f;

                uint z = (xyzs << 16) >> 24;
                Z = (z - 127) / 127f;

                uint s = (xyzs << 24) >> 24;
                Sign = (s - 127) / 127f;
            }
        }

        public static implicit operator Tangent(uint xyzs)
        {
            return new Tangent(xyzs);
        }

        public Tangent(BinaryReader reader)
        {
            X = reader.ReadSingle();
            Y = reader.ReadSingle();
            Z = reader.ReadSingle();

            Sign = reader.ReadSingle();
        }

        public uint ToUInt32()
        {
            uint x = (uint)(X * 127f),
                 y = (uint)(Y * 127f),
                 z = (uint)(Z * 127f),
                 s = (uint)(Sign * 127f);

            return x << 24 | y << 16 | z << 8 | s;
        }
    }

}
