using RobloxFiles.DataTypes;
using System.Collections.Generic;
using System.IO;

namespace Source2Roblox.Models
{
    public class CompactSurface
    {
        public readonly string Magic;

        public readonly short Version;
        public readonly short ModelType;

        public readonly int SurfaceSize;
        public readonly Vector3 DragAxisAreas;
        public readonly int AxisMapSize;

        public CompactSurface(BinaryReader reader)
        {
            Magic = reader.ReadString(4);

            Version = reader.ReadInt16();
            ModelType = reader.ReadInt16();

            SurfaceSize = reader.ReadInt32();
            DragAxisAreas = reader.ReadVector3();
            AxisMapSize = reader.ReadInt32();

            reader.Skip(44);
        }
    }

    public class LegacySurface
    {
        public readonly int Size;

        public readonly Vector3 MassCenter;
        public readonly Vector3 RotationInertia;

        public readonly float UpperLimitRadius;
        public readonly int MaxDeviation;
        public readonly int ByteSize;

        public readonly int OffsetLedgeTreeRoot;
        public readonly string Id;

        public LegacySurface(BinaryReader reader)
        {
            MassCenter = reader.ReadVector3();
            RotationInertia = reader.ReadVector3();

            UpperLimitRadius = reader.ReadSingle();
            MaxDeviation = reader.ReadByte();

            byte[] byteSize = reader.ReadBytes(3);
            ByteSize = 0;

            for (int i = 0; i < 3; i++)
            {
                int chunk = byteSize[i] << (i * 8);
                ByteSize |= chunk;
            }

            OffsetLedgeTreeRoot = reader.ReadInt32();
            reader.Skip(4);

            Id = reader.ReadString(4);
            reader.Skip(4);
        }
    }

    public class PhysicsData
    {
        public readonly int Size;
        public readonly int Id;

        public readonly int SolidCount;
        public readonly long Checksum;

        public readonly List<LegacySurface> LegacySurfaces = new List<LegacySurface>();
        public readonly List<CompactSurface> CompactSurfaces = new List<CompactSurface>();

        public PhysicsData(BinaryReader reader)
        {
            // var stream = reader.BaseStream;
            // var fileStart = stream.Position;

            Size = reader.ReadInt32();
            Id = reader.ReadInt32();

            SolidCount = reader.ReadInt32();
            Checksum = reader.ReadInt32();

            /*for (int i = 0; i < SolidCount; i++)
            {
                int collisionSize = reader.ReadInt32();
                var startPos = stream.Position;

                var compact = new CompactSurface(reader);
                CompactSurfaces.Add(compact);

                if (compact.Magic != "VPHY")
                {
                    LegacySurface legacy;
                    stream.Position = startPos;
                    legacy = new LegacySurface(reader);

                    LegacySurfaces.Add(legacy);
                    CompactSurfaces.Remove(compact);
                }

                string ivps = reader.ReadString(4);
                Debug.Assert(ivps == "IVPS");

                var meshIndices = new List<List<ushort>>();

                while (stream.Position < collisionSize)
                {
                    int vertDataOffset = reader.ReadInt32();
                    int boneIndex = reader.ReadInt32();

                    reader.Skip(4);

                    int numTriangles = reader.ReadInt32();
                    var indices = new List<ushort>();

                    for (int j = 0; j < numTriangles; j++)
                    {
                        reader.Skip(4);

                        for (int k = 0; k < 3; k++)
                        {
                            var vertIndex = reader.ReadUInt16();
                            indices.Add(vertIndex);
                            reader.Skip(2);
                        }
                    }

                    meshIndices.Add(indices);
                }

                Debugger.Break();
            }

            var keySize = (int)(stream.Length - stream.Position);
            string keyValues = reader.ReadString(keySize);

            Debugger.Break();*/
        }
    }
}
