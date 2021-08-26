using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Source2Roblox.FileSystem;
using Source2Roblox.Models;
using Source2Roblox.World;
using Source2Roblox.World.Types;

using RobloxFiles.DataTypes;
using Source2Roblox.Util;
using System.Diagnostics;

namespace Source2Roblox.Geometry
{
    public static class ObjMesher
    {
        private const TextureFlags IGNORE = TextureFlags.Hint | TextureFlags.Skip | TextureFlags.Sky | TextureFlags.Trigger;
        
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
            string mapName = bsp.Name;

            var objWriter = new StringBuilder();
            var mtlWriter = new StringBuilder();

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

            var uvs = new List<Vector2>();
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();

            int numUVs = 0;
            int numVerts = 0;
            int numNorms = 0;

            var materialSets = new Dictionary<string, ValveMaterial>();
            var vertOffsets = new Dictionary<int, Vector3>();

            var brushModels = bsp.BrushModels;
            var leafFaces = bsp.LeafFaces;
            var leaves = bsp.Leaves;

            foreach (var entity in bsp.Entities)
            {
                string className = entity.ClassName;
                string modelId = entity.Get<string>("model");

                if (modelId == null)
                    continue;

                if (!modelId.StartsWith("*"))
                    continue;

                if (!int.TryParse(modelId.Substring(1), out int modelIndex))
                    continue;

                if (modelIndex < 0 || modelIndex >= brushModels.Count)
                    continue;

                bool shouldSkip = false;

                if (className.StartsWith("trigger"))
                    shouldSkip = true;

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

                    if (shouldSkip)
                    {
                        face.Skip = true;
                        continue;
                    }

                    var firstEdge = face.FirstEdge;
                    var numEdges = face.NumEdges;

                    for (int j = 0; j < numEdges; j++)
                    {
                        var vertId = (int)vertIndices[firstEdge + j];
                        vertOffsets[vertId] = offset;
                    }

                    face.EntityId = modelIndex;
                }

                model.Origin = origin;
                model.BoundsMin += offset;
                model.BoundsMax += offset;
            }

            var marked = new HashSet<int>();

            for (int i = 0; i < leaves.Count; i++)
            {
                var leaf = leaves[i];
                var firstFace = leaf.FirstLeafFace;

                for (int j = 0; j < leaf.NumLeafFaces; j++)
                {
                    var index = leafFaces[firstFace + j];
                    var face = bsp.Faces[index];

                    face.LeafIndex = i;
                    marked.Add(index);
                }
            }

            foreach (var face in bsp.Faces)
            {
                var numEdges = face.NumEdges;
                face.FirstNorm = numNorms;
                
                var texInfo = bsp.TexInfo[face.TexInfo];
                var texData = bsp.TexData[texInfo.TextureData];

                int stringIndex = texData.StringTableIndex;
                var matIndex = bsp.TexDataStringTable[stringIndex];

                face.Material = bsp.TexDataStringData[matIndex];
                numNorms += numEdges;
            }

            var faces = bsp.Faces.OrderBy(face => face.Material);
            objWriter.AppendLine();
            numNorms = 0;

            foreach (Face face in faces)
            {
                int planeId = face.PlaneNum;
                int dispInfo = face.DispInfo;
                int numEdges = face.NumEdges;

                int firstEdge = face.FirstEdge;
                int firstNorm = face.FirstNorm;

                var texInfo = bsp.TexInfo[face.TexInfo];
                var texData = bsp.TexData[texInfo.TextureData];

                var texel = texInfo.TextureVecs;
                var size = texData.Size;
                
                var material = face.Material;
                var center = new Vector3();
                var flags = texInfo.Flags;

                if ((flags & IGNORE) != TextureFlags.None)
                {
                    face.Skip = true;
                    continue;
                }

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

                    face.Material = material;
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
                }

                if (face.DispInfo >= 0)
                {
                    var disp = bsp.Displacements[dispInfo];
                    Debug.Assert(face.NumEdges == 4);

                    var corners = new Vector3[4];
                    var startPos = disp.StartPosition;

                    var bestDist = float.MaxValue;
                    var bestIndex = -1;

                    for (int i = 0; i < 4; i++)
                    {
                        var vertIndex = (int)vertIndices[firstEdge + i];
                        var vertex = bsp.Vertices[vertIndex];

                        var dist = (vertex - startPos).Magnitude;
                        corners[i] = vertex;

                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestIndex = i;
                        }
                    }

                    // Rotate the corners so the startPos
                    // is first in line in the loop.

                    if (bestIndex != 0)
                    {
                        var leftHalf = corners.Skip(bestIndex);
                        var rightHalf = corners.Take(bestIndex);

                        var slice = leftHalf.Concat(rightHalf);
                        corners = slice.ToArray();
                    }

                    var dispSize = (1 << disp.Power) + 1;
                    numEdges = (dispSize * dispSize);
                    
                    for (int y = 0; y < dispSize; y++)
                    {
                        var rowSample = (float)y / (dispSize - 1);
                        var rowStart = corners[0].Lerp(corners[1], rowSample);
                        var rowEnd = corners[3].Lerp(corners[2], rowSample);

                        for (int x = 0; x < dispSize; x++)
                        {
                            var colSample = (float)x / (dispSize - 1);
                            var i = disp.DispVertStart + (y * dispSize) + x;
                            
                            var pos = rowStart.Lerp(rowEnd, colSample);
                            var uv = texel.CalcTexCoord(pos, size);

                            var dispVert = bsp.DispVerts[i];
                            pos += dispVert.Vector * dispVert.Dist;
                            center += (pos / numEdges);

                            norms.Add(pos.Unit); // TODO
                            verts.Add(pos);
                            uvs.Add(uv);
                        }
                    }

                    face.NumEdges = (short)numEdges;
                }
                else
                {
                    for (int i = numEdges - 1; i >= 0; i--)
                    {
                        var vertIndex = (int)vertIndices[firstEdge + i];
                        var normIndex = (int)normIndices[firstNorm + i];

                        var vert = bsp.Vertices[vertIndex];
                        var norm = bsp.VertNormals[normIndex];
                        var uv = texel.CalcTexCoord(vert, size);

                        if (vertOffsets.ContainsKey(vertIndex))
                            vert += vertOffsets[vertIndex];

                        center += (vert / numEdges);
                        verts.Add(vert);
                        norms.Add(norm);
                        uvs.Add(uv);
                    }
                }

                face.Center = center;
                face.FirstUV = numUVs;
                face.FirstNorm = numNorms;
                face.FirstVert = numVerts;
                
                numUVs += numEdges;
                numNorms += numEdges;
                numVerts += numEdges;
            }

            string objPath = Path.Combine(exportDir, $"{mapName}.obj");
            string mtlPath = Path.Combine(exportDir, $"{mapName}.mtl");

            foreach (var vertex in verts)
            {
                var v = vertex / 10f;
                objWriter.AppendLine($"v {v.X} {v.Z} {-v.Y}");
            }

            foreach (var norm in norms)
                objWriter.AppendLine($"vn {norm.X} {norm.Z} {-norm.Y}");

            foreach (var uv in uvs)
                objWriter.AppendLine($"vt {uv.X} {1f - uv.Y}");

            var flatten = new Vector3(1, 1, 0);

            var faceQueue = faces.Where(face => !face.Skip);
            var faceGroups = new List<List<Face>>();
            
            // This is very inefficient but it'll do for now.
            // I want to get a nicer grouping algorithm ASAP.

            while (faceQueue.Any())
            {
                var face = faceQueue.First();

                var group = new List<Face>();
                group.Add(face);

                if (face.DispInfo >= 0)
                {
                    faceQueue = faceQueue
                        .Except(group)
                        .ToList();

                    faceGroups.Add(group);

                    continue;
                }

                Console.WriteLine($"Grouping faces... ({faceQueue.Count()}/{faces.Count()} remaining)");

                foreach (var otherFace in faceQueue)
                {
                    if (face.EntityId >= 0)
                    {
                        if (otherFace.EntityId != face.EntityId)
                            continue;

                        group.Add(otherFace);
                    }
                    else
                    {
                        if (otherFace.DispInfo >= 0)
                            continue;

                        if (otherFace.EntityId >= 0)
                            continue;

                        var dist = ((otherFace.Center - face.Center) * flatten).Magnitude;

                        if (dist > 400)
                            continue;

                        group.Add(otherFace);
                    }
                }

                faceQueue = faceQueue
                    .Except(group)
                    .ToList();

                faceGroups.Add(group);
            }

            int groupId = 0;

            foreach (var group in faceGroups)
            {
                string lastMaterial = "";
                objWriter.AppendLine($"o group_{groupId++}");

                foreach (var face in group)
                {
                    int dispInfo = face.DispInfo,
                        entityId = face.EntityId,
                        numEdges = face.NumEdges,
                        firstVert = face.FirstVert,
                        firstNorm = face.FirstNorm,
                        firstUV = face.FirstUV;

                    if (dispInfo >= 0)
                    {
                        int dispSize = (int)Math.Sqrt(numEdges);
                        Debug.Assert(dispSize * dispSize == numEdges);

                        objWriter.AppendLine($"o disp_{dispInfo}");
                        objWriter.AppendLine($" usemtl {face.Material}");

                        lastMaterial = "";

                        for (int y = 0; y < dispSize - 1; y++)
                        {
                            for (int x = 0; x < dispSize - 1; x++)
                            {
                                int aa = firstVert + (y * dispSize) + x + 1;
                                int ab = aa + 1;

                                int bb = ab + dispSize;
                                int ba = bb - 1;

                                string quad = $"  f"
                                    + $" {aa}/{aa}/{aa}"
                                    + $" {ab}/{ab}/{ab}"
                                    + $" {bb}/{bb}/{bb}"
                                    + $" {ba}/{ba}/{ba}";

                                objWriter.AppendLine(quad);
                            }
                        }
                    }
                    else
                    {
                        var material = face.Material;
                        var center = face.Center;

                        if (material.ToLowerInvariant() == "tools/toolstrigger")
                            continue;

                        if (face.Skip)
                            continue;

                        if (material != lastMaterial)
                        {
                            objWriter.AppendLine($" usemtl {material}");
                            lastMaterial = material;
                        }

                        objWriter.Append("  f");

                        for (int i = 0; i < numEdges; i++)
                        {
                            var normIndex = 1 + firstNorm + i;
                            var vertIndex = 1 + firstVert + i;
                            var uvIndex = 1 + firstUV + i;

                            objWriter.Append($" {vertIndex}/{uvIndex}/{normIndex}");
                        }
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
