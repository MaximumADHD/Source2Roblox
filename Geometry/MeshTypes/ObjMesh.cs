using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using RobloxFiles.DataTypes;
using System.Linq;

namespace Source2Roblox.Geometry
{
    public class FaceIndex
    {
        public readonly int Vertex;
        public readonly int? Normal;
        public readonly int? UV;

        public FaceIndex(int vertex, int? normal = null, int? uv = null)
        {
            Vertex = vertex;
            Normal = normal;
            UV = uv;
        }

        public override string ToString()
        {
            string result = (Vertex + 1).ToString();

            if (UV.HasValue)
                result += "/" + (UV.Value + 1);
            else if (Normal.HasValue)
                result += "/";

            if (Normal.HasValue)
                result += "/" + (Normal.Value + 1);

            return result;
        }
    }

    public class FaceIndices : IEnumerable<FaceIndex>, IComparable<FaceIndices>
    {
        private readonly List<FaceIndex> Indices = new List<FaceIndex>();

        public string Material = "";
        public string Object = "";
        public string Group = "";

        public FaceIndex this[int key]
        {
            get => Indices[key];
        }

        public void AddIndex(int vertex, int? normal = null, int? uv = null)
        {
            var index = new FaceIndex(vertex, normal, uv);
            Indices.Add(index);
        }

        public IEnumerator<FaceIndex> GetEnumerator()
        {
            return Indices.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Indices.GetEnumerator();
        }

        public override string ToString()
        {
            return string.Join(" ", Indices);
        }

        public int CompareTo(FaceIndices other)
        {
            if (Object != other.Object)
                return string.Compare(Object, other.Object);

            if (Group != other.Group)
                return string.Compare(Group, other.Group);

            if (Material != other.Material)
                return string.Compare(Material, other.Material);

            var faceA = ToString()
                .Split('/', ' ')
                .Select(int.Parse)
                .ToArray();

            var faceB = other.ToString()
                .Split('/', ' ')
                .Select(int.Parse)
                .ToArray();

            int lenA = faceA.Length,
                lenB = faceB.Length;

            if (lenA != lenB)
                return lenA - lenB;

            for (int i = 0; i < lenA; i++)
            {
                int valueA = faceA[i],
                    valueB = faceB[i];

                if (valueA == valueB)
                    continue;

                return (valueA - valueB);
            }

            return 0;
        }
    }

    public class ObjMesh
    {
        public readonly List<FaceIndices> Faces = new List<FaceIndices>();
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector2> UVs = new List<Vector2>();
        public readonly float Scale = 1f;

        private FaceIndices currFace = null;
        private string Object, Group, Material;
        private readonly StringBuilder MaterialWriter = new StringBuilder();

        public ObjMesh(float scale = 1f)
        {
            Scale = scale;
        }

        private FaceIndices CurrentFace
        {
            get
            {
                if (currFace == null)
                {
                    currFace = new FaceIndices()
                    {
                        Group = Group,
                        Object = Object,
                        Material = Material
                    };
                }

                return currFace;
            }
        }

        public void AddIndex(int vert, int? norm = null, int? uv = null)
        {
            CurrentFace.AddIndex(vert, norm, uv);
        }

        public void SetObject(string obj)
        {
            Object = obj;
        }

        public void SetGroup(string group)
        {
            Group = group;
        }

        public void SetMaterial(string material)
        {
            Material = material;
        }

        public void AddFace()
        {
            if (currFace == null)
                return;

            currFace.Group = Group;
            currFace.Object = Object;
            currFace.Material = Material;

            Faces.Add(currFace);
            currFace = null;
        }

        public void AddVertex(Vector3 vertex) => Vertices.Add(vertex);
        public void AddNormal(Vector3 normal) => Normals.Add(normal);
        public void AddUV(Vector2 uv) => UVs.Add(uv);

        public void AddVertex(float x, float y, float z)
        {
            var vert = new Vector3(x, y, z);
            AddVertex(vert);
        }

        public void AddNormal(float x, float y, float z)
        {
            var norm = new Vector3(x, y, z);
            AddNormal(norm);
        }

        public void AddUV(float x, float y)
        {
            var uv = new Vector2(x, y);
            AddUV(uv);
        }

        public Vector3 GetVertex(int index)
        {
            return Vertices[index] * Scale;
        }

        public void WriteLine_MTL(params object[] data)
        {
            string line = string.Join(" ", data);
            MaterialWriter.AppendLine(line);
        }

        public string WriteOBJ()
        {
            var writer = new StringBuilder();
            var byObject = Faces.GroupBy(face => face.Object ?? "");

            foreach (var vert in Vertices)
            {
                var v = vert * Scale;
                writer.AppendLine($"v {v.X} {v.Y} {v.Z}");
            }

            writer.AppendLine();

            foreach (var norm in Normals)
                writer.AppendLine($"vn {norm.X} {norm.Y} {norm.Z}");

            writer.AppendLine();

            foreach (var uv in UVs)
                writer.AppendLine($"vt {uv.X} {uv.Y}");

            writer.AppendLine();

            foreach (var objFaces in byObject)
            {
                string obj = objFaces.Key;
                var byGroup = objFaces.GroupBy(face => face.Group ?? "");

                if (!string.IsNullOrEmpty(obj))
                    writer.AppendLine($"o {obj}");

                foreach (var groupFaces in byGroup)
                {
                    string group = groupFaces.Key;
                    var byMaterial = groupFaces.GroupBy(face => face.Material);

                    if (!string.IsNullOrEmpty(group))
                        writer.AppendLine($"g {group}");

                    foreach (var matFaces in byMaterial)
                    {
                        string material = matFaces.Key;

                        if (!string.IsNullOrEmpty(material))
                            writer.AppendLine($"usemtl {material}");

                        var faces = matFaces.ToList();
                        faces.ForEach(face => writer.AppendLine($"  f {face}"));
                    }
                }
            }

            return writer.ToString();
        }

        public string WriteMTL()
        {
            return MaterialWriter.ToString();
        }
    }
}
