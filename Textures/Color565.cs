using System;
using System.Drawing;

namespace Source2Roblox.Textures
{
    public class Color565
    {
        public readonly ushort Value;

        public byte R => (byte)((( Value & 0xF800) >> 11) << 3);
        public byte G => (byte)(((Value & 0x07E0) >> 5) << 2);
        public byte B => (byte)((Value & 0x001F) << 3);

        public override string ToString() => $"[{Value}] {R}, {G}, {B}";

        private Color565(ushort value)
        {
            Value = value;
        }

        public static Color565 FromBGR(ushort bgr)
        {
            byte r = (byte)(( bgr & 0x001F) << 3),
                 g = (byte)(( bgr & 0x07E0) >> 3),
                 b = (byte)(((bgr & 0xF800) >> 11) << 3);

            return FromRGB(r, g, b);
        }

        public static Color565 FromRGB(byte r, byte g, byte b)
        {
            int r5 = r >> 3,
                g6 = g >> 2,
                b5 = b >> 3;

            var value = (ushort)(r5 << 11 | g6 << 5 | b5);
            return new Color565(value);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Color565 color))
                return false;

            return Value == color.Value;
        }

        public Color Modify(Color565 other, Func<int, int, float> modifier)
        {
            byte r = (byte)modifier(R, other.R),
                 g = (byte)modifier(G, other.G),
                 b = (byte)modifier(B, other.B);

            return Color.FromArgb(r, g, b);
        }

        public static bool operator <(Color565 colorA, Color565 colorB)
        {
            return colorA.Value < colorB.Value;
        }

        public static bool operator >(Color565 colorA, Color565 colorB)
        {
            return colorA.Value > colorB.Value;
        }

        public static implicit operator Color565(ushort value)
        {
            return new Color565(value);
        }

        public static implicit operator ushort(Color565 color)
        {
            return color.Value;
        }

        public static implicit operator Color(Color565 color)
        {
            return Color.FromArgb(color.R, color.G, color.B);
        }

        public static implicit operator Color565(Color color)
        {
            return FromRGB(color.R, color.G, color.B);
        }
    }
}
