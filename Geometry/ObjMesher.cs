using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Source2Roblox.FileSystem;
using Source2Roblox.Models;
using Source2Roblox.Textures;
using Source2Roblox.World;
using Source2Roblox.World.Types;

using RobloxFiles.DataTypes;
using ValveKeyValue;
using Source2Roblox.Util;
using System.Diagnostics;

namespace Source2Roblox.Geometry
{
    public static class ObjMesher
    {
        private const TextureFlags IGNORE = TextureFlags.Hint | TextureFlags.Skip | TextureFlags.Sky;
        
        public static void BakeMDL(ModelFile model, string exportDir, int skin = 0, int lod = 0, int subModel = 0)
        {
            var game = model.Game;
            var info = new FileInfo(model.Name);

            string name = info.Name.Replace(".mdl", "");
            exportDir = Path.Combine(exportDir, "SourceModels", name);

            if (!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);

            var objWriter = new StringBuilder();
            var mtlWriter = new StringBuilder();
            var meshBuffers = new List<MeshBuffer>();
            var handledFiles = new HashSet<string>();
            
            for (int bodyPart = 0; bodyPart < model.BodyPartCount; bodyPart++)
            {
                var meshes = model.GetMeshes(bodyPart, subModel, lod, skin);
                meshBuffers.AddRange(meshes);
            }

            // Write Vertices.
            objWriter.AppendLine("# Vertices\n");

            var allVerts = meshBuffers
                .SelectMany(mesh => mesh.Vertices)
                .ToArray();

            foreach (var vert in allVerts)
            {
                var pos = vert.Position / 10f;
                objWriter.AppendLine($"v  {pos.X} {pos.Z} {-pos.Y}");

                var norm = vert.Normal;
                objWriter.AppendLine($"vn {norm.X} {norm.Z} {-norm.Y}");

                var uv = vert.UV;
                objWriter.AppendLine($"vt {uv.X} {1f - uv.Y}\n");
            }

            // Write Faces.
            int faceIndexBase = 1;
            string lastBodyPart = "";
            
            foreach (MeshBuffer mesh in meshBuffers)
            {
                string matPath = mesh.MaterialPath;
                string bodyPart = mesh.BodyPart;

                if (bodyPart != lastBodyPart)
                {
                    objWriter.AppendLine($"o {bodyPart}\n");
                    lastBodyPart = bodyPart;
                }

                var matInfo = new FileInfo(matPath);
                string matName = matInfo.Name.Replace(".vmt", "");
                ValveMaterial vmt = null;
                
                if (GameMount.HasFile(matPath, game))
                {
                    vmt = new ValveMaterial(matPath, game);
                    vmt.SaveVTF(vmt.DiffusePath, exportDir);
                    vmt.SaveVTF(vmt.BumpPath, exportDir, true);
                }

                string diffusePath = vmt?.DiffusePath;
                objWriter.AppendLine($"\tg {matName}");

                if (!string.IsNullOrEmpty(diffusePath))
                {
                    string diffuse = diffusePath.Replace(".vtf", ".png");
                    diffuse = Program.CleanPath(diffuse);

                    objWriter.AppendLine($"\tusemtl {matName}\n");
                    mtlWriter.AppendLine($"newmtl {matName}");

                    string bumpPath = vmt.BumpPath;
                    bool noAlpha = vmt.NoAlpha;

                    if (!string.IsNullOrEmpty(bumpPath))
                    {
                        string bump = bumpPath.Replace(".vtf", ".png");
                        bump = Program.CleanPath(bump);
                        mtlWriter.AppendLine($"bump {bump}");
                    }
                    
                    mtlWriter.AppendLine($"map_Kd {diffuse}");
                    mtlWriter.AppendLine(noAlpha ? "" : $"map_d {diffuse}\n");
                }
                
                for (int i = 0; i < mesh.NumIndices; i += 3)
                {
                    objWriter.Append("\tf");

                    for (int j = 2; j >= 0; j--)
                    {
                        int f = faceIndexBase + mesh.Indices[i + j];
                        objWriter.Append($" {f}/{f}/{f}");
                    }

                    objWriter.AppendLine();
                }

                objWriter.AppendLine();
                faceIndexBase += mesh.NumVerts;
            }

            string obj = objWriter.ToString();
            string objPath = Path.Combine(exportDir, $"{name}.obj");

            string mtl = mtlWriter.ToString();
            string mtlPath = Path.Combine(exportDir, $"{name}.mtl");

            File.WriteAllText(objPath, obj);
            Console.WriteLine($"\tWrote: {objPath}");

            File.WriteAllText(mtlPath, mtl);
            Console.WriteLine($"\tWrote: {mtlPath}");
        }

        public static void BakeBSP(BSPFile bsp, string exportDir, GameMount game = null)
        {
            var objWriter = new StringBuilder();
            var mtlWriter = new StringBuilder();

            string mapName = bsp.Name;
          
            var edges = bsp.Edges;
            var surfEdges = bsp.SurfEdges;

            var vertIndices = new List<uint>();
            var normIndices = bsp.VertNormalIndices;

            foreach (int surf in surfEdges)
            {
                uint edge;

                if (surf >= 0)
                    edge = edges[surf * 2];
                else
                    edge = edges[-surf * 2 + 1];

                vertIndices.Add(edge);
            }

            int numUVs = 0;
            int numVerts = 0;
            int numNorms = 0;

            var materialSets = new Dictionary<string, ValveMaterial>();
            var facesByMaterial = new Dictionary<string, List<Face>>();

            var vertOffsets = new Dictionary<int, Vector3>();
            var brushModels = bsp.BrushModels;

            foreach (var entity in bsp.Entities)
            {
                string modelId = entity.Get<string>("model");

                if (modelId == null)
                    continue;

                if (!modelId.StartsWith("*"))
                    continue;

                if (!int.TryParse(modelId.Substring(1), out int modelIndex))
                    continue;

                if (modelIndex < 0 || modelIndex >= brushModels.Count)
                    continue;

                var model = bsp.BrushModels[modelIndex];
                var origin = entity.Get<Vector3>("origin");

                if (origin == null)
                    continue;

                var offset = origin - model.Origin;
                int faceIndex = model.FirstFace;
                int numFaces = model.NumFaces;

                for (int i = 0; i < numFaces; i++)
                {
                    int faceId = faceIndex + i;
                    var face = bsp.Faces[faceId];

                    var firstEdge = face.FirstEdge;
                    var numEdges = face.NumEdges;

                    for (int j = 0; j < numEdges; j++)
                    {
                        var vertId = (int)vertIndices[firstEdge + j];
                        vertOffsets[vertId] = offset;
                    }
                }

                model.Origin = origin;
                model.BoundsMin += offset;
                model.BoundsMax += offset;
            }

            foreach (Face face in bsp.Faces)
            {
                int planeId = face.PlaneNum;
                int dispInfo = face.DispInfo;
                int numEdges = face.NumEdges;
                int firstEdge = face.FirstEdge;

                var texInfo = bsp.TexInfo[face.TexInfo];
                var flags = texInfo.Flags;

                if ((flags & IGNORE) != TextureFlags.None)
                    continue;

                var texData = bsp.TexData[texInfo.TextureData];
                int stringIndex = texData.StringTableIndex;

                var matIndex = bsp.TexDataStringTable[stringIndex];
                var material = bsp.TexDataStringData[matIndex];

                if (material.StartsWith($"maps/{mapName}"))
                {
                    while (true)
                    {
                        var lastUnderscore = material.LastIndexOf('_');

                        if (lastUnderscore < 0)
                            break;

                        string number = material.Substring(lastUnderscore + 1);

                        if (int.TryParse(number, out int _))
                        {
                            material = material.Substring(0, lastUnderscore);
                            continue;
                        }

                        material = material.Replace($"maps/{mapName}/", "");
                        break;
                    }
                }

                if (!materialSets.ContainsKey(material))
                {
                    var saveInfo = new FileInfo(material);
                    string saveDir = saveInfo.DirectoryName;

                    Directory.CreateDirectory(saveDir);
                    Console.WriteLine($"Fetching material {material}");

                    var vmt = new ValveMaterial(material, game);
                    mtlWriter.AppendLine($"newmtl {material}");
                    materialSets[material] = vmt;

                    string diffuse = vmt.DiffusePath;
                    string bump = vmt.BumpPath;

                    if (!string.IsNullOrEmpty(diffuse))
                    {
                        string png = diffuse.Replace(".vtf", ".png");
                        mtlWriter.AppendLine($"\tmap_Kd {png}");
                        vmt.SaveVTF(diffuse, exportDir);
                    }

                    if (!string.IsNullOrEmpty(bump))
                    {
                        string png = bump.Replace(".vtf", ".png");
                        mtlWriter.AppendLine($"\tbump {png}");
                        vmt.SaveVTF(bump, exportDir);
                    }

                    facesByMaterial[material] = new List<Face>();
                }

                var texel = texInfo.TextureVecs;
                var size = texData.Size;

                for (int i = numEdges - 1; i >= 0; i--)
                {
                    var vertIndex = (int)vertIndices[firstEdge + i];
                    var normIndex = (int)normIndices[numNorms + i];

                    var norm = bsp.VertNormals[normIndex];
                    var vert = bsp.Vertices[vertIndex];

                    if (vertOffsets.ContainsKey(vertIndex))
                        vert += vertOffsets[vertIndex];

                    var uv = texel.CalcTexCoord(vert, size);
                    vert /= 10f;

                    objWriter.AppendLine($"v {vert.X} {vert.Z} {-vert.Y}");
                    objWriter.AppendLine($"vn {norm.X} {norm.Z} {-norm.Y}");
                    objWriter.AppendLine($"vt {uv.X} {1f - uv.Y}\n");
                }

                face.FirstUV = numUVs;
                face.FirstVert = numVerts;
                face.FirstNorm = numNorms;

                numUVs += numEdges;
                numVerts += numEdges;
                numNorms += numEdges;

                facesByMaterial[material].Add(face);
            }

            string objPath = Path.Combine(exportDir, $"{mapName}.obj");
            string mtlPath = Path.Combine(exportDir, $"{mapName}.mtl");

            foreach (string material in facesByMaterial.Keys)
            {
                var faces = facesByMaterial[material];
                objWriter.AppendLine($"\nusemtl {material}");
                
                foreach (var face in faces)
                {
                    int numEdges  = face.NumEdges,
                        firstVert = face.FirstVert,
                        firstNorm = face.FirstNorm,
                        firstUV   = face.FirstUV;

                    objWriter.Append("  f");

                    for (int i = 0; i < numEdges; i++)
                    {
                        var normIndex = 1 + normIndices[firstNorm + i];
                        var vertIndex = 1 + firstVert + i;
                        var uvIndex   = 1 + firstUV + i;

                        objWriter.Append($" {vertIndex}/{uvIndex}/{normIndex}");
                    }

                    objWriter.AppendLine();
                }
            }

            string obj = objWriter.ToString();
            File.WriteAllText(objPath, obj);

            string mtl = mtlWriter.ToString();
            File.WriteAllText(mtlPath, mtl);
        }
    }
}
