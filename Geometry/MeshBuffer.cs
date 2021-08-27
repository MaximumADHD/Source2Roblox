using System;
using System.Collections.Generic;
using RobloxFiles.DataTypes;

namespace Source2Roblox.Geometry
{
    public class MeshBuffer
    {
        public string BodyPart;
        public string MaterialPath;

        public int NumVerts => Vertices.Count;
        public List<StudioVertex> Vertices;

        public int NumIndices => Indices.Count;
        public List<ushort> Indices;

        public static Region3 ComputeAABB(IEnumerable<StudioVertex> vertices)
        {
            float min_X = float.MaxValue,
                  min_Y = float.MaxValue,
                  min_Z = float.MaxValue,

                  max_X = float.MinValue,
                  max_Y = float.MinValue,
                  max_Z = float.MinValue;

            foreach (StudioVertex studioVertex in vertices)
            {
                RobloxVertex rbxVertex = studioVertex;
                Vector3 pos = rbxVertex.Position;

                min_X = Math.Min(min_X, pos.X);
                min_Y = Math.Min(min_Y, pos.Y);
                min_Z = Math.Min(min_Z, pos.Z);

                max_X = Math.Max(max_X, pos.X);
                max_Y = Math.Max(max_Y, pos.Y);
                max_Z = Math.Max(max_Z, pos.Z);
            }

            var min = new Vector3(min_X, min_Y, min_Z);
            var max = new Vector3(max_X, max_Y, max_Z);

            return new Region3(min, max);
        }
    }

}
