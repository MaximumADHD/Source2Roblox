using RobloxFiles.DataTypes;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Source2Roblox.Geometry.MeshTypes
{
    public class PhysicsSubMesh
    {
        public byte[] TriIndices = new byte[16]
        {
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0,
            0, 0, 0, 0
        };

        public byte[] TransformOffsets = new byte[16]
        {
            0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0,
            0, 0, 0x80, 0x3f,
        };

        public List<Vector3> Vertices = new List<Vector3>();
        public List<int> Indices = new List<int>();
    }

    public class PhysicsMesh
    {
        public List<PhysicsSubMesh> SubMeshes = new List<PhysicsSubMesh>();
        public string Magic = "CSGPHS";
        public uint Version = 3;

        public PhysicsMesh(BinaryReader reader)
        {
            Magic = reader.ReadString(6);
            Version = reader.ReadUInt32();

            if (Version == 0)
            {
                string block = reader.ReadString(5);
                Debug.Assert(block == "BLOCK");
            }

            var stream = reader.BaseStream;

            while (stream.Position < stream.Length)
            {
                var subMesh = new PhysicsSubMesh();

                int sizeof_TriIndices = reader.ReadInt32();
                subMesh.TriIndices = reader.ReadBytes(sizeof_TriIndices);

                int sizeof_TransformOffsets = reader.ReadInt32();
                subMesh.TransformOffsets = reader.ReadBytes(sizeof_TransformOffsets);

                int sizeof_Verts = reader.ReadInt32();
                subMesh.Vertices = new List<Vector3>(sizeof_Verts / 3);

                int sizeof_Float = reader.ReadInt32();
                Debug.Assert(sizeof_Float == sizeof(float));

                for (int i = 0; i < sizeof_Verts; i += 3)
                {
                    var vert = reader.ReadVector3();
                    subMesh.Vertices.Add(vert);
                }

                int numIndices = reader.ReadInt32();
                subMesh.Indices = new List<int>();

                for (int i = 0; i < numIndices; i++)
                {
                    int index = reader.ReadInt32();
                    subMesh.Indices.Add(index);
                }

                SubMeshes.Add(subMesh);
            }
        }

        public PhysicsMesh(RobloxMesh mesh)
        {
            // This is an awful hack but it'll work for now.
            var indices = new int[6] { 0, 2, 4, 5, 3, 1 };

            foreach (var face in mesh.Faces)
            {
                var subMesh = new PhysicsSubMesh();
                subMesh.Indices.AddRange(indices);

                for (int i = 0; i < 3; i++)
                {
                    int index = face[i];
                    var vert = mesh.Verts[index];

                    var pos = vert.Position;
                    subMesh.Vertices.Add(pos);

                    var norm = vert.Normal;
                    subMesh.Vertices.Add(pos + (norm / 8f));
                }

                SubMeshes.Add(subMesh);
            }
        }

        public SharedString Serialize()
        {
            using (var stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                byte[] magic = Encoding.UTF8.GetBytes(Magic);
                writer.Write(magic, 0, 6);
                writer.Write(Version);

                foreach (var subMesh in SubMeshes)
                {
                    writer.Write(subMesh.TriIndices.Length);
                    writer.Write(subMesh.TriIndices);

                    writer.Write(subMesh.TransformOffsets.Length);
                    writer.Write(subMesh.TransformOffsets);

                    writer.Write(subMesh.Vertices.Count * 3);
                    writer.Write(sizeof(float));

                    foreach (var vert in subMesh.Vertices)
                    {
                        writer.Write(vert.X);
                        writer.Write(vert.Y);
                        writer.Write(vert.Z);
                    }

                    writer.Write(subMesh.Indices.Count);
                    subMesh.Indices.ForEach(writer.Write);
                }

                byte[] buffer = stream.ToArray();
                return SharedString.FromBuffer(buffer);
            }
        }
    }
}
