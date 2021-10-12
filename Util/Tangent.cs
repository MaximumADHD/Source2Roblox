using System;
using System.Diagnostics;
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

        public Tangent(int xyzs)
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
                int x = xyzs & 0xFF;
                X = (x - 127) / 127f;

                int y = (xyzs >> 8) & 0xFF;
                Y = (y - 127) / 127f;

                int z = (xyzs >> 16) & 0xFF;
                Z = (z - 127) / 127f;

                int s = (xyzs >> 24) & 0xFF;
                Sign = (s - 127) / 127f;
            }
        }

        public static implicit operator Tangent(int xyzs)
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

        public int ToInt32()
        {
            int x = (int)(X * 127f),
                y = (int)(Y * 127f),
                z = (int)(Z * 127f),
                s = (int)(Sign * 127f);

            return x << 24 | y << 16 | z << 8 | s;
        }
    }

}
