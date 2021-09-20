using System.Drawing;
using RobloxFiles.DataTypes;

namespace Source2Roblox.Textures
{
    public static class SSBump
    {
        private static readonly Vector3 RNBasis0 = new Vector3( 0.8660254037844386f,  0.0000000000000000f, 0.5773502691896258f); //  sqrt3/2,  0,       sqrt1/3
        private static readonly Vector3 RNBasis1 = new Vector3(-0.4082482904638631f,  0.7071067811865475f, 0.5773502691896258f); // -sqrt1/6,  sqrt1/2, sqrt1/3
        private static readonly Vector3 RNBasis2 = new Vector3(-0.4082482904638631f, -0.7071067811865475f, 0.5773502691896258f); // -sqrt1/6, -sqrt1/2, sqrt1/3

        private static byte PackNormal(float f)
        {
            return (byte)((0.5f + (f / 2f)) * 255);
        }

        public static Bitmap ToNormalMap(Image image)
        {
            var ssbump = image as Bitmap;
            var normalMap = new Bitmap(ssbump);

            for (int h = 0; h < ssbump.Height; h++)
            {
                for (int w = 0; w < ssbump.Width; w++)
                {
                    Color color = ssbump.GetPixel(w, h);

                    float x = color.R / 255f,
                          y = color.G / 255f,
                          z = color.B / 255f;

                    Vector3 normal = (RNBasis0 * x + RNBasis1 * y + RNBasis2 * z).Unit;

                    byte r = PackNormal(normal.X),
                         g = PackNormal(normal.Y),
                         b = PackNormal(normal.Z);

                    color = Color.FromArgb(r, g, b);
                    normalMap.SetPixel(w, h, color);
                }
            }

            return normalMap;
        }
    }
}
