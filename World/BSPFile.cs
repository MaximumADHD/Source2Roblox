using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

using Source2Roblox.FileSystem;
using Source2Roblox.World.Types;

using RobloxFiles.DataTypes;
using System.Linq;
using System.Diagnostics;

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
        public readonly List<GameLump> GameLumps = new List<GameLump>();

        public readonly List<BrushSide> BrushSides = new List<BrushSide>();
        public readonly List<BrushModel> BrushModels = new List<BrushModel>();

        public readonly List<Vector3> VertNormals = new List<Vector3>();
        public readonly List<DispInfo> Displacements = new List<DispInfo>();
        public readonly List<ushort> VertNormalIndices = new List<ushort>();

        public readonly List<int> TexDataStringTable = new List<int>();
        public readonly Dictionary<int, string> TexDataStringData = new Dictionary<int, string>();

        private const float ROUND_VERTEX_EPSILON = 0.01f;
        private const float MIN_EDGE_LENGTH_EPSILON = 0.1f;

        public static MemoryStream ReadBuffer(BinaryReader reader, int offset, int length)
        {
            var buffer = new MemoryStream();
            var stream = reader.BaseStream;

            long restore = stream.Position;
            stream.Position = offset;

            bool didDecomp = false;
            byte[] rawHead = reader.ReadBytes(4);
            string head = Encoding.ASCII.GetString(rawHead);

            if (head == "LZMA")
            {
                uint actualSize = reader.ReadUInt32();
                int lzmaSize = reader.ReadInt32();

                byte[] props = reader.ReadBytes(5);
                byte[] compressed = reader.ReadBytes(lzmaSize);
                byte[] decompressed = LZMA.Decompress(compressed, props, actualSize);

                length = decompressed.Length;
                Debug.Assert(length == actualSize);

                buffer.SetLength(length);
                buffer.Write(decompressed, 0, length);

                didDecomp = true;
            }

            if (!didDecomp && length > 0)
            {
                stream.Position = offset;
                stream.CopyTo(buffer, length);
            }

            buffer.Position = 0;
            buffer.SetLength(length);
            stream.Position = restore;

            return buffer;
        }

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

            int length = lump.Length;
            int offset = lump.Offset;

            using (var buffer = ReadBuffer(bspReader, offset, length))
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
                    case LumpType.GameLump:
                    {
                        int numLumps = reader.ReadInt32();

                        for (int j = 0; j < numLumps; j++)
                        {
                            var gameLump = new GameLump(reader);
                            GameLumps.Add(gameLump);
                        }

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
                GameLumps.ForEach(gameLump => gameLump.Read(reader));
            }
        }

        public Entity FindEntityByClass(string className)
        {
            return Entities.Find(ent => ent.ClassName == className);
        }

        public IEnumerable<Entity> FindEntitiesOfClass(params string[] classNames)
        {
            var classes = classNames.ToHashSet();
            return Entities.Where(ent => classes.Contains(ent.ClassName));
        }

        public Entity FindEntityByName(string name)
        {
            return Entities.Find(ent => ent.Name == name);
        }

        public GameLump FindGameLump(string id)
        {
            return GameLumps.Find(lump => lump.Id == id);
        }

        private static float RoundCoord(float value)
        {
            float round = (float)Math.Round(value);

            if (value != round && Math.Abs(value - round) < ROUND_VERTEX_EPSILON)
                return round;

            return value;
        }

        public Dictionary<int, Winding> SolveFaces(Brush brush)
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
                    if (f1.Dot(f2) > 0.999f)
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

            var faces = new Dictionary<int, Winding>();

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
                    faces[i] = winding;
                }
            }

            return faces;
        }
    }
}
