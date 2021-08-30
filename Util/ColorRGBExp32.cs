using System.Drawing;
using System.IO;

namespace Source2Roblox.Util
{
    public struct ColorRGBExp32
    {
        public byte R, G, B;
        public sbyte Exponent;

        public ColorRGBExp32(byte r, byte g, byte b, sbyte exponent)
        {
            R = r;
            G = g;
            B = b;

            Exponent = exponent;
        }

        public ColorRGBExp32(BinaryReader reader)
        {
            R = reader.ReadByte();
            G = reader.ReadByte();
            B = reader.ReadByte();

            Exponent = reader.ReadSByte();
        }

        public static implicit operator Color(ColorRGBExp32 color)
        {
            byte a = (byte)color.Exponent,
                 r = color.R,
                 g = color.G,
                 b = color.B;

            return Color.FromArgb(a, r, g, b);
        }

        public static implicit operator ColorRGBExp32(Color color)
        {
            sbyte a = (sbyte)color.A;

            byte r = color.R,
                 g = color.G,
                 b = color.B;

            return new ColorRGBExp32(r, g, b, a);
        }
    }
}
