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

        public readonly List<Node> Nodes = new List<Node>();
        public readonly List<Leaf> Leaves = new List<Leaf>();

        public readonly List<ushort> LeafFaces = new List<ushort>();
        public readonly List<ushort> LeafBrushes = new List<ushort>();

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

        private const float ROUND_VERTEX_EPSILON = 0.01f;
        private const float MIN_EDGE_LENGTH_EPSILON = 0.1f;

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
                        case LumpType.Nodes:
                        {
                            reader.ReadToEnd(Nodes);
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
                        case LumpType.Leaves:
                        {
                            reader.ReadToEnd(Leaves, () => new Leaf(reader, this));
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
                        case LumpType.LeafFaces:
                        {
                            reader.ReadToEnd(LeafFaces, reader.ReadUInt16);
                            break;
                        }
                        case LumpType.LeafBrushes:
                        {
                            reader.ReadToEnd(LeafBrushes, reader.ReadUInt16);
                            break;
                        }
                        case LumpType.Brushes:
                        {
                            reader.ReadToEnd(Brushes);
                            break;
                        }
                        case LumpType.BrushSides:
                        {
                            reader.ReadToEnd(BrushSides);
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
                                var tags = reader.ReadUInt16();
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

        private static float RoundCoord(float value)
        {
            float round = (float)Math.Round(value);

            if (value != round && Math.Abs(value - round) < ROUND_VERTEX_EPSILON)
                return round;

            return value;
        }

        public List<Winding> SolveFaces(Brush brush)
        {
            int numSides = brush.NumSides,
                firstSide = brush.FirstSide;

            var usePlane = new bool[numSides];

            // For every face that is not set to be ignored, check the plane and make sure
            // it is unique.We mark each plane that we intend to keep with `true` in the
            // 'usePlane' array.

            for (int i = 0; i < brush.NumSides; i++)
            {
                var side = BrushSides[firstSide + i];
                var plane = Planes[side.PlaneNum];

                // Don't use this plane if it has a zero-length normal.
                if (plane.Normal.Magnitude == 0f)
                {
                    usePlane[i] = false;
                    continue;
                }

                // If the plane duplicates another plane, don't use it
                usePlane[i] = true;

                for (int j = 0; j < i; j++)
                {
                    var sideCheck = BrushSides[firstSide + j];
                    var planeCheck = Planes[sideCheck.PlaneNum];

                    var f1 = plane.Normal;
                    var f2 = planeCheck.Normal;

                    // Check for duplicate plane within some tolerance.
                    if (f1.Dot(f2) > 0.99)
                    {
                        var d1 = plane.Dist;
                        var d2 = planeCheck.Dist;

                        if (Math.Abs(d1 - d2) < 0.01f)
                        {
                            usePlane[j] = false;
                            break;
                        }
                    }
                }
            }

            // Now we have a set of planes, indicated by `true` values in the 'usePlanes' array,
            // from which we will build a solid.

            var faces = new List<Winding>();

            for (int i = 0; i < numSides; i++)
            {
                var side = BrushSides[firstSide + i];
                var plane = Planes[side.PlaneNum];

                if (!usePlane[i])
                    continue;

                // Create a huge winding from this plane,
                // then clip it by all other planes.

                var clipId = 0;
                var winding = new Winding(plane);

                while (winding != null && clipId < numSides)
                {
                    if (i != clipId)
                    {
                        var clipSide = BrushSides[firstSide + clipId];
                        var clip = Planes[clipSide.PlaneNum];

                        // Flip the plane, because we want to keep the back side.
                        winding = winding.Clip(-clip.Normal, -clip.Dist);
                    }

                    clipId++;
                }

                // If we still have a winding after all that clipping,
                // build a face from the winding.

                if (winding != null)
                {
                    // Round all points in the winding that are within
                    // ROUND_VERTEX_EPSILON OF integer values.

                    for (int j = 0; j < winding.Count; j++)
                    {
                        var point = winding[j];

                        float x = RoundCoord(point.X),
                              y = RoundCoord(point.Y),
                              z = RoundCoord(point.Z);

                        winding[j] = new Vector3(x, y, z);
                    }

                    winding.RemoveDuplicates(MIN_EDGE_LENGTH_EPSILON);
                    faces.Add(winding);
                }
            }

            return faces;
        }
    }
}
