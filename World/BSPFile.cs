using System;
using System.IO;
using System.Text;
using Source2Roblox.FileSystem;
using Source2Roblox.World.Lumps;

namespace Source2Roblox.World
{
    public class BSPFile
    {
        public readonly string Header;
        public readonly int Version;

        public readonly int MapRevision;
        public readonly Lump[] Lumps = new Lump[64];

        public readonly Faces Faces;
        public readonly Edges Edges;
        public readonly Planes Planes;
        public readonly Brushes Brushes;
        public readonly TexData TexData;
        public readonly TexInfo TexInfo;
        public readonly Entities Entities;
        public readonly Vertices Vertices;
        public readonly DispTris DispTris;
        public readonly DispVerts DispVerts;
        public readonly SurfEdges SurfEdges;
        public readonly BrushSides BrushSides;
        public readonly VertNormals VertNormals;
        public readonly Displacements Displacements;
        public readonly VertNormalIndices VertNormalIndices;
        public readonly TexDataStringData TexDataStringData;
        public readonly TexDataStringTable TexDataStringTable;

        private void ReadLump(BinaryReader reader, LumpType type)
        {
            var stream = reader.BaseStream;
            var i = (int)type;

            var lump = new Lump()
            {
                Type = type,
                
                Offset = reader.ReadInt32(),
                Length = reader.ReadInt32(),

                Version = reader.ReadInt32(),
                Uncompressed = reader.ReadInt32()
            };

            Lumps[i] = lump;

            if (lump.Length == 0 || lump.Offset == 0)
                return;

            var root = nameof(Source2Roblox);
            var world = nameof(Source2Roblox.World);
            var lumps = nameof(Source2Roblox.World.Lumps);
            var lumpType = Type.GetType($"{root}.{world}.{lumps}.{lump.Type}");

            if (!typeof(ILump).IsAssignableFrom(lumpType))
                return;

            long restore = stream.Position;
            stream.Position = lump.Offset;

            using (var buffer = new MemoryStream())
            {
                bool didDecomp = false;
                int length = lump.Length;

                if (lump.Uncompressed > 0)
                {
                    byte[] rawHead = reader.ReadBytes(4);
                    string head = Encoding.ASCII.GetString(rawHead);

                    if (head == "LZMA")
                    {
                        uint actualSize = reader.ReadUInt32();
                        uint lzmaSize = reader.ReadUInt32();

                        byte[] compressed = reader.ReadBytes(lump.Length - 12);
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
                
                using (var readBuffer = new BinaryReader(buffer))
                {
                    var data = Activator.CreateInstance(lumpType) as ILump;
                    data?.Read(buffer, readBuffer);

                    var fieldInfo = typeof(BSPFile)
                        .GetField(lumpType.Name);

                    fieldInfo?.SetValue(this, data);
                    lump.Value = data;
                }
            }
        }

        public BSPFile(string path, GameMount game = null)
        {
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
