using System.Collections.Generic;
using System.Text;

using Source2Roblox.World;
using Source2Roblox.World.Types;

using RobloxFiles.DataTypes;
using Source2Roblox.Models;

namespace Source2Roblox
{
    public static class ObjMesher
    {
        private const TextureFlags IGNORE = TextureFlags.Sky | TextureFlags.Trans | TextureFlags.Hint | TextureFlags.Skip | TextureFlags.Trigger;

        public static string BakeMDL(ModelFile model, int lod = 0)
        {
            var verts = model.VertexData.GetVertices(lod);
            var writer = new StringBuilder();

            foreach (var vert in verts)
            {
                var pos = vert.Position / 10f;
                writer.AppendLine($"v {pos.X} {pos.Z} {-pos.Y}");

                var norm = vert.Normal;
                writer.AppendLine($"vn {norm.X} {norm.Z} {-norm.Y}");

                var uv = vert.UV;
                writer.AppendLine($"vt {uv.X} {uv.Y}");
            }

            return writer.ToString();
        }

        public static string BakeBSP(BSPFile bsp)
        {
            var writer = new StringBuilder();

            foreach (Vector3 vert in bsp.Vertices)
            {
                var v = vert / 10f;
                writer.AppendLine($"v {v.X} {v.Z} {-v.Y}");
            }

            foreach (Vector3 norm in bsp.VertNormals)
                writer.AppendLine($"vn {norm.X} {norm.Z} {-norm.Y}");

            var edges = bsp.Edges;
            var surfEdges = bsp.SurfEdges;

            var vertIndices = new List<uint>();
            var normIndices = bsp.VertNormalIndices;

            foreach (int surf in surfEdges)
            {
                uint edge;

                if (surf >= 0)
                    edge = edges[surf * 2 + 0];
                else
                    edge = edges[-surf * 2 + 1];

                vertIndices.Add(edge);
            }

            int normBaseIndex = 0;

            foreach (Face face in bsp.Faces)
            {
                int planeId = face.PlaneNum;
                int dispInfo = face.DispInfo;
                int numEdges = face.NumEdges;
                int firstEdge = face.FirstEdge;

                int firstNorm = normBaseIndex;
                normBaseIndex += numEdges;

                var texInfo = bsp.TexInfo[face.TexInfo];
                var flags = texInfo.Flags;

                if ((flags & IGNORE) != TextureFlags.None)
                    continue;

                writer.Append("f");

                for (int i = 0; i < numEdges; i++)
                {
                    var vertIndex = 1 + vertIndices[firstEdge + i];
                    var normIndex = 1 + normIndices[firstNorm + i];
                    writer.Append($" {vertIndex}//{normIndex}");
                }

                writer.AppendLine();
            }

            return writer.ToString();
        }
    }
}
