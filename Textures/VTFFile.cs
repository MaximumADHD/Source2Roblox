using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;

using RobloxFiles.DataTypes;

namespace Source2Roblox.Textures
{
    public class VTFImages : List<Image> { }
    public class VTFSlices : List<VTFImages> { }

    public enum VTFImageFormat
    {
        NONE = -1,
        RGBA8888,
        ABGR8888,
        RGB888,
        BGR888,
        RGB565,
        I8,
        IA88,
        P8,
        A8,
        RGB888_BLUESCREEN,
        BGR888_BLUESCREEN,
        ARGB8888,
        BGRA8888,
        DXT1,
        DXT3,
        DXT5,
        BGRX8888,
        BGR565,
        BGRX5551,
        BGRA4444,
        DXT1_ONEBITALPHA,
        BGRA5551,
        UV88,
        UVWQ8888,
        RGBA16161616F,
        RGBA16161616,
        UVLX8888
    }

    public enum VTFFlags
    {
        None = 0x0,
        PointSample = 0x1,
        Trilinear = 0x2,
        ClampS = 0x4,
        ClampT = 0x8,
        SRGB = 0x40,
        NoMip = 0x100,
        OneBitAlpha = 0x1000,
        EightBitAlpha = 0x2000,
        EnvMap = 0x4000,
    }

    public class VTFFile
    {
        public readonly IReadOnlyList<VTFSlices> Frames;
        public readonly VTFImageFormat Format;
        public readonly VTFFlags Flags;

        public readonly ushort Width;
        public readonly ushort Height;
        public readonly ushort Depth;

        public readonly ushort NumFrames;
        public readonly ushort StartFrame;

        public readonly Vector3 Reflectivity;
        public readonly float BumpScale;
        public readonly byte NumMipmaps;

        public readonly Image LowResImage;
        public readonly byte LowResImageWidth;
        public readonly byte LowResImageHeight;
        public readonly VTFImageFormat LowResImageFormat;
        
        public readonly uint VersionMajor;
        public readonly uint VersionMinor;
        public readonly uint HeaderSize;

        public readonly bool NoAlpha;

        public Image HighResImage => Frames
             ?.FirstOrDefault()   // Slices
             ?.FirstOrDefault()   // Mipmaps
             ?.FirstOrDefault();  // Image
        
        private Color[] ReadPixels(BinaryReader reader, int width, int height, int depth)
        {
            Color[] result = null;

            switch (Format)
            {
                case VTFImageFormat.DXT1:
                {
                    result = DXTHelper.ReadPixels_DXT1(reader, width, height, depth);
                    break;
                }
                case VTFImageFormat.DXT1_ONEBITALPHA:
                {
                    result = DXTHelper.ReadPixels_DXT1(reader, width, height, depth, alpha: true);
                    break;
                }
                case VTFImageFormat.DXT3:
                {
                    result = DXTHelper.ReadPixels_DXT3(reader, width, height, depth);
                    break;
                }
                case VTFImageFormat.DXT5:
                {
                    result = DXTHelper.ReadPixels_DXT5(reader, width, height, depth);
                    break;
                }
            }

            if (result != null)
                return result;

            int count = width * height * depth;
            byte r, g, b, a = 255;

            result = new Color[count];
            
            for (int i = 0; i < count; i++)
            {
                switch (Format)
                {
                    case VTFImageFormat.ARGB8888:
                    {
                        a = reader.ReadByte();
                        r = reader.ReadByte();
                        g = reader.ReadByte();
                        b = reader.ReadByte();

                        break;
                    }
                    case VTFImageFormat.RGBA8888:
                    {
                        r = reader.ReadByte();
                        g = reader.ReadByte();
                        b = reader.ReadByte();
                        a = reader.ReadByte();

                        break;
                    }
                    case VTFImageFormat.ABGR8888:
                    {
                        a = reader.ReadByte();
                        b = reader.ReadByte();
                        g = reader.ReadByte();
                        r = reader.ReadByte();

                        break;
                    }
                    case VTFImageFormat.BGRA8888:
                    {
                        b = reader.ReadByte();
                        g = reader.ReadByte();
                        r = reader.ReadByte();
                        a = reader.ReadByte();

                        break;
                    }
                    case VTFImageFormat.BGRX8888:
                    {
                        b = reader.ReadByte();
                        g = reader.ReadByte();
                        r = reader.ReadByte();

                        reader.Skip(1);
                        break;
                    }
                    case VTFImageFormat.RGB888:
                    {
                        r = reader.ReadByte();
                        g = reader.ReadByte();
                        b = reader.ReadByte();
                        
                        break;
                    }
                    case VTFImageFormat.BGR888:
                    {
                        b = reader.ReadByte();
                        g = reader.ReadByte();
                        r = reader.ReadByte();
                        
                        break;
                    }
                    case VTFImageFormat.BGRA5551:
                    {
                        var bgra = reader.ReadUInt16();

                        b = (byte)(((bgra & 0xF800) >> 0xB) * 0x08);
                        g = (byte)(((bgra & 0x07C0) >> 0x6) * 0x08);
                        r = (byte)(((bgra & 0x003E) >> 0x1) * 0x08);
                        a = (byte)(((bgra & 0x0001) >> 0x0) * 0xFF);

                        break;
                    }
                    case VTFImageFormat.BGR565:
                    {
                        var bgr = reader.ReadUInt16();
                        var color = Color565.FromBGR(bgr);

                        r = color.R;
                        g = color.G;
                        b = color.B;
                        
                        break;
                    }
                    case VTFImageFormat.I8:
                    {
                        r = g = b = reader.ReadByte();
                        break;
                    }
                    default:
                    {
                        r = g = b = 0;
                        break;
                    }
                }

                result[i] = Color.FromArgb(a, r, g, b);
            }

            return result;
        }

        public VTFFile(BinaryReader reader, bool noAlpha = false)
        {
            string header = reader.ReadString(4);
            var stream = reader.BaseStream;

            if (header != "VTF\0")
                throw new InvalidDataException("Not a VTF file!");

            VersionMajor = reader.ReadUInt32();
            VersionMinor = reader.ReadUInt32();
            HeaderSize = reader.ReadUInt32();

            if (VersionMajor != 7)
                throw new InvalidDataException($"Unsupported major version {VersionMajor}!");

            if (VersionMinor > 5)
                throw new InvalidDataException($"Unsupported minor version {VersionMinor}!");

            Width = reader.ReadUInt16();
            Height = reader.ReadUInt16();
            Flags = (VTFFlags)reader.ReadUInt32();

            NumFrames = reader.ReadUInt16();
            StartFrame = reader.ReadUInt16();

            reader.Skip(4);
            Reflectivity = reader.ReadVector3();

            reader.Skip(4);
            BumpScale = reader.ReadSingle();

            Format = (VTFImageFormat)reader.ReadUInt32();
            NumMipmaps = reader.ReadByte();

            LowResImageFormat = (VTFImageFormat)reader.ReadUInt32();
            LowResImageWidth = reader.ReadByte();
            LowResImageHeight = reader.ReadByte();

            reader.Skip(1);
            Depth = 1;
            
            if (VersionMinor >= 2)
            {
                Depth = Math.Max(reader.ReadUInt16(), (ushort)1);
                stream.Position = 0x44;
            }

            if (VersionMinor >= 3)
            {
                uint numResources = reader.ReadUInt32();
                stream.Position = 0x50 + (numResources * 8);
            }

            if (LowResImageFormat != VTFImageFormat.NONE)
            {
                var lowResImage = new Bitmap(LowResImageWidth, LowResImageHeight);
                Debug.Assert(LowResImageFormat == VTFImageFormat.DXT1, "LowResImage must use DXT1!");

                var colorData = DXTHelper.ReadPixels_DXT1(reader, LowResImageWidth, LowResImageHeight, 1);
                int i = 0;

                for (int h = 0; h < LowResImageHeight; h++)
                {
                    for (int w = 0; w < LowResImageWidth; w++)
                    {
                        var color = colorData[i++];
                        lowResImage.SetPixel(w, h, color);
                    }
                }

                LowResImage = lowResImage;
            }

            bool isCube = ((Flags & VTFFlags.EnvMap) != VTFFlags.None);
            int faceCount = (isCube ? 6 : 1);

            bool hasSpheremap = (VersionMinor < 5);
            faceCount += (isCube && hasSpheremap) ? 1 : 0;

            var frames = new List<VTFSlices>();
            Frames = frames;

            for (int i = 0; i < NumFrames; i++)
            {
                var slices = new VTFSlices();
                frames.Add(slices);

                for (int j = 0; j < Depth; j++)
                {
                    var images = new VTFImages();
                    slices.Add(images);
                }
            }

            for (int i = NumMipmaps - 1; i >= 0; i--)
            {
                int mipWidth = Math.Max(Width >> i, 1);
                int mipHeight = Math.Max(Height >> i, 1);

                Console.WriteLine($"Reading Mipmap {mipWidth}x{mipHeight}");

                for (int j = 0; j < NumFrames; j++)
                {
                    var mipData = ReadPixels(reader, mipWidth, mipHeight, Depth * faceCount);
                    var frame = frames[j];
                    int index = 0;

                    for (int d = 0; d < Depth; d++)
                    {
                        var layer = frame[d];
                        var mipmap = new Bitmap(mipWidth, mipHeight);

                        for (int h = 0; h < mipHeight; h++)
                        {
                            for (int w = 0; w < mipWidth; w++)
                            {
                                var color = mipData[index++];

                                if (noAlpha)
                                    color = Color.FromArgb(255, color);

                                mipmap.SetPixel(w, h, color);
                            }
                        }

                        layer.Insert(0, mipmap);
                    }
                }
            }

            NoAlpha = noAlpha;
        }
    }
}
