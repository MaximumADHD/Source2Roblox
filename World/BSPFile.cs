using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

using Source2Roblox.FileSystem;
using Source2Roblox.World.Types;

using RobloxFiles.DataTypes;

namespace Source2Roblox.World
{
    public class BSPFile
    {
        public readonly string Name;
        public readonly string Header;

        public readonly int Version;
        public readonly int MapRevision;

        public readonly Lump[] Lumps = new Lump[64];

        public readonly List<Face> Faces = new List<Face>();
        public readonly List<ushort> Edges = new List<ushort>();

        public readonly List<Plane> Planes = new List<Plane>();
        public readonly List<Brush> Brushes = new List<Brush>();

        public readonly List<TextureData> TexData = new List<TextureData>();
        public readonly List<TextureInfo> TexInfo = new List<TextureInfo>();

        public readonly List<Entity> Entities = new List<Entity>();
        public readonly List<Vector3> Vertices = new List<Vector3>();

        public readonly List<DispTriTags> DispTris = new List<DispTriTags>();
        public readonly List<DispVert> DispVerts = new List<DispVert>();

        public readonly List<int> SurfEdges = new List<int>();
        public readonly List<BrushSide> BrushSides = new List<BrushSide>();
        public readonly List<BrushModel> BrushModels = new List<BrushModel>();

        public readonly List<Vector3> VertNormals = new List<Vector3>();
        public readonly List<DispInfo> Displacements = new List<DispInfo>();
        public readonly List<ushort> VertNormalIndices = new List<ushort>();

        public readonly List<int> TexDataStringTable = new List<int>();
        public readonly Dictionary<int, string> TexDataStringData = new Dictionary<int, string>();

        private void ReadLump(BinaryReader bspReader, LumpType type)
        {
            var stream = bspReader.BaseStream;
            var i = (int)type;

            var lump = new Lump()
            {
                Type = type,
                
                Offset = bspReader.ReadInt32(),
                Length = bspReader.ReadInt32(),

                Version = bspReader.ReadInt32(),
                Uncompressed = bspReader.ReadInt32()
            };

            Lumps[i] = lump;

            if (lump.Length == 0 || lump.Offset == 0)
                return;

            long restore = stream.Position;
            stream.Position = lump.Offset;

            using (var buffer = new MemoryStream())
            {
                bool didDecomp = false;
                int length = lump.Length;

                if (lump.Uncompressed > 0)
                {
                    byte[] rawHead = bspReader.ReadBytes(4);
                    string head = Encoding.ASCII.GetString(rawHead);

                    if (head == "LZMA")
                    {
                        uint actualSize = bspReader.ReadUInt32();
                        uint lzmaSize = bspReader.ReadUInt32();

                        byte[] compressed = bspReader.ReadBytes(lump.Length - 12);
                        byte[] decompressed = LZMA.Decompress(compressed, null, actualSize);

                        length = decompressed.Length;
                        didDecomp = true;

                        buffer.SetLength(length);
                        buffer.Write(decompressed, 0, length);
                    }
                }
                
                if (!didDecomp)
                {
                    stream.Position = lump.Offset;
                    stream.CopyTo(buffer, length);
                    buffer.SetLength(length);
                }
                
                buffer.Position = 0;
                stream.Position = restore;
                
                using (var reader = new BinaryReader(buffer))
                {
                    switch (lump.Type)
                    {
                        case LumpType.Entities:
                        {
                            Entity.ReadEntities(reader, Entities);
                            break;
                        }
                        case LumpType.Planes:
                        {
                            reader.ReadToEnd(Planes);
                            break;
                        }
                        case LumpType.TexData:
                        {
                            reader.ReadToEnd(TexData);
                            break;
                        }
                        case LumpType.Vertices:
                        {
                            reader.ReadToEnd(Vertices, reader.ReadVector3);
                            break;
                        }
                        case LumpType.TexInfo:
                        {
                            reader.ReadToEnd(TexInfo);
                            break;
                        }
                        case LumpType.Faces:
                        {
                            reader.ReadToEnd(Faces);
                            break;
                        }
                        case LumpType.Edges:
                        {
                            reader.ReadToEnd(Edges, reader.ReadUInt16);
                            break;
                        }
                        case LumpType.SurfEdges:
                        {
                            reader.ReadToEnd(SurfEdges, reader.ReadInt32);
                            break;
                        }
                        case LumpType.Models:
                        {
                            reader.ReadToEnd(BrushModels);
                            break;
                        }
                        case LumpType.Brushes:
                        {
                            reader.ReadToEnd(Brushes);
                            break;
                        }
                        case LumpType.Displacements:
                        {
                            reader.ReadToEnd(Displacements);
                            break;
                        }
                        case LumpType.VertNormals:
                        {
                            reader.ReadToEnd(VertNormals, reader.ReadVector3);
                            break;
                        }
                        case LumpType.VertNormalIndices:
                        {
                            reader.ReadToEnd(VertNormalIndices, reader.ReadUInt16);
                            break;
                        }
                        case LumpType.DispVerts:
                        {
                            reader.ReadToEnd(DispVerts);
                            break;
                        }
                        case LumpType.TexDataStringData:
                        {
                            int start = 0;
                            
                            while (buffer.Position < buffer.Length)
                            {
                                string value = reader.ReadString(null);
                                TexDataStringData.Add(start, value);

                                start = (int)buffer.Position;
                            }

                            break;
                        }
                        case LumpType.TexDataStringTable:
                        {
                            reader.ReadToEnd(TexDataStringTable, reader.ReadInt32);
                            break;
                        }
                        case LumpType.DispTris:
                        {
                            reader.ReadToEnd(DispTris, () =>
                            {
                                ushort tags = reader.ReadUInt16();
                                return (DispTriTags)tags;
                            });

                            break;
                        }
                    }
                }
            }
        }

        public BSPFile(string path, GameMount game = null)
        {
            var info = new FileInfo(path);
            Name = info.Name.Replace(info.Extension, "");

            using (var stream = GameMount.OpenRead(path, game))
            using (var reader = new BinaryReader(stream))
            {
                Header = reader.ReadString(4);
                Version = reader.ReadInt32();

                for (int i = 0; i < 64; i++)
                {
                    var lumpType = (LumpType)i;
                    ReadLump(reader, lumpType);
                }

                MapRevision = reader.ReadInt32();
            }
        }
    }
}
