using System;
using System.Drawing;
using System.IO;

namespace Source2Roblox.Textures
{
    enum DXTColorRule
    {
        None,
        Alpha,
        UseColor0
    }
    
    struct DXTColorParams
    {
        public DXTColorRule ColorRule;
        public Func<byte> GetAlpha;
    }

    public static class DXTHelper
    {
        private static Color GetColor(Color565 c0, Color565 c1, uint code, DXTColorParams colorParams)
        {
            var colorRule = colorParams.ColorRule;
            var getAlpha = colorParams.GetAlpha;
            var color = Color.Black;
            
            if (colorRule == DXTColorRule.Alpha)
                color = Color.FromArgb(0);

            switch (code)
            {
                case 0:
                {
                    color = c0;
                    break;
                }
                case 1:
                {
                    color = c1;
                    break;
                }
                case 2:
                {
                    if (c0 > c1 || colorRule == DXTColorRule.UseColor0)
                        color = c0.Modify(c1, (rgb0, rgb1) => ((2 * rgb0) + rgb1) / 3f);
                    else
                        color = c0.Modify(c1, (rgb0, rgb1) => (rgb0 + rgb1) / 2f);

                    break;
                }
                case 3:
                {
                    if (c0 > c1 || colorRule == DXTColorRule.UseColor0)
                        color = c0.Modify(c1, (rgb0, rgb1) => (rgb0 + (2 * rgb1)) / 3f);
                    
                    break;
                }
            }

            if (getAlpha != null)
            {
                byte alpha = getAlpha();
                color = Color.FromArgb(alpha, color);
            }

            return color;
        }

        private static void ReadColorBlock(ref Color[] colors, ref int index, BinaryReader reader, int row, DXTColorParams colorParams)
        {
            Color565 c0 = reader.ReadUInt16(),
                     c1 = reader.ReadUInt16();

            uint codes = reader.ReadUInt32();
            
            for (int h1 = 0; h1 < 4; h1++)
            {
                for (int w1 = 0; w1 < 4; w1++)
                {
                    var code = codes & 3;
                    codes >>= 2;

                    var color = GetColor(c0, c1, code, colorParams);
                    colors[index + w1] = color;
                }

                // Move down to the next row.
                index += row;
            }

            // Move to top-left of next block.
            index -= (row * 4) - 4;

            // Align index to 4x4 grid.
            if (index % row == 0)
            {
                int col = index / row % 4;
                index += row * (4 - col);
            }
        }

        private static void LazySetupEnv(ref int width, ref int height, int depth, out int blocks, out Color[] colors)
        {
            if (width < 4)
                width = 4;

            if (height < 4)
                height = 4;

            int pixels = width * height;
            blocks = pixels / 16;

            colors = new Color[pixels * depth];
        }

        public static Color[] ReadPixels_DXT1(BinaryReader reader, int width, int height, int depth, bool alpha = false)
        {
            LazySetupEnv(ref width, ref height, depth, out int blocks, out var colors);

            var clrParams = new DXTColorParams
            {
                ColorRule = alpha ? DXTColorRule.Alpha : DXTColorRule.None,
                GetAlpha = null
            };

            for (int d = 0; d < depth; d++)
                for (int block = 0, offset = d * depth; block < blocks; block++)
                    ReadColorBlock(ref colors, ref offset, reader, width, clrParams);

            return colors;
        }

        public static Color[] ReadPixels_DXT3(BinaryReader reader, int width, int height, int depth)
        {
            ulong alpha = 0;
            LazySetupEnv(ref width, ref height, depth, out int blocks, out var colors);
            
            var clrParams = new DXTColorParams
            {
                ColorRule = DXTColorRule.UseColor0,

                GetAlpha = () =>
                {
                    byte value = (byte)((alpha & 0xF) << 4);
                    alpha >>= 4;

                    return value;
                }
            };

            for (int d = 0; d < depth; d++)
            {
                int offset = d * depth;

                for (int block = 0; block < blocks; block++)
                {
                    alpha = reader.ReadUInt64();
                    ReadColorBlock(ref colors, ref offset, reader, width, clrParams);
                }
            }

            return colors;
        }

        public static Color[] ReadPixels_DXT5(BinaryReader reader, int width, int height, int depth)
        {
            byte a0 = 0,
                 a1 = 0;

            ulong alphaBuffer = 0;
            LazySetupEnv(ref width, ref height, depth, out int blocks, out var colors);

            var clrParams = new DXTColorParams
            {
                ColorRule = DXTColorRule.UseColor0,
                
                GetAlpha = () =>
                {
                    var code = (byte)(alphaBuffer & 0x7);
                    alphaBuffer >>= 3;

                    if (code == 0)
                        return a0;
                    else if (code == 1)
                        return a1;

                    int alpha = 255;

                    if (a0 > a1)
                        alpha = ((8 - code) * a0 + (code - 1) * a1) / 7;
                    else if (code < 6)
                        alpha = ((6 - code) * a0 + (code - 1) * a1) / 5;
                    else if (code == 6)
                        alpha = 255;

                    return (byte)alpha;
                }
            };

            for (int d = 0; d < depth; d++)
            {
                int offset = d * depth;

                for (int block = 0; block < blocks; block++)
                {
                    a0 = reader.ReadByte();
                    a1 = reader.ReadByte();

                    ulong part1 = reader.ReadUInt16();
                    ulong part2 = reader.ReadUInt32();

                    alphaBuffer = (part1 << 32) | part2;
                    ReadColorBlock(ref colors, ref offset, reader, width, clrParams);
                }
            }

            return colors;
        }
    }
}
