using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Source2Roblox.FileSystem;
using Source2Roblox.Geometry.MeshTypes;
using Source2Roblox.Models;
using Source2Roblox.Octree;
using Source2Roblox.Textures;
using Source2Roblox.Upload;
using Source2Roblox.World;
using Source2Roblox.World.Types;

using RobloxFiles;
using RobloxFiles.Enums;
using RobloxFiles.DataTypes;

namespace Source2Roblox.Geometry
{
    public static class MeshBuilder
    {
        private struct ModelRequest
        {
            public string ModelName;
            public int Skin;
        }

        private const float DEG_TO_RAD = (float)(Math.PI / 180f);
        private const string ENV_DARK = "materials/dev/reflectivity_10b.vtf";
        private const string ENV_LIGHT = "materials/dev/reflectivity_90b.vtf";
        private static readonly CFrame BONE_OFFSET = CFrame.Angles(-90f * DEG_TO_RAD, 0, 0);

        public static Region3 ComputeAABB(IEnumerable<Vector3> vertices)
        {
            float min_X = float.MaxValue,
                  min_Y = float.MaxValue,
                  min_Z = float.MaxValue,

                  max_X = float.MinValue,
                  max_Y = float.MinValue,
                  max_Z = float.MinValue;

            foreach (var pos in vertices)
            {
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

        public static void BakeMDL_OBJ(ModelFile model, string exportDir, int skin = 0, int subModel = 0, int lod = 0)
        {
            var game = model.Game;
            var info = new FileInfo(model.Name);

            string name = info.Name.Replace(".mdl", "");
            exportDir = Path.Combine(exportDir, "SourceModels", name);

            if (!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);

            var objMesh = new ObjMesh();
            var meshBuffers = new List<MeshBuffer>();
            
            for (int bodyPart = 0; bodyPart < model.BodyPartCount; bodyPart++)
            {
                var meshes = model.GetMeshes(bodyPart, skin, subModel, lod);
                meshBuffers.AddRange(meshes);
            }

            var allVerts = meshBuffers
                .SelectMany(mesh => mesh.Vertices)
                .ToArray();

            foreach (var vert in allVerts)
            {
                var pos = vert.Position / Program.STUDS_TO_VMF;
                objMesh.AddVertex(pos.X, pos.Z, -pos.Y);

                var norm = vert.Normal;
                objMesh.AddNormal(norm.X, norm.Z, -norm.Y);

                var uv = vert.UV;
                objMesh.AddUV(uv.X, 1f - uv.Y);
            }

            // Write Faces.
            int faceIndexBase = 1;
            
            foreach (MeshBuffer mesh in meshBuffers)
            {
                string matPath = mesh.MaterialPath;
                objMesh.SetObject(mesh.BodyPart);

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
                objMesh.SetGroup(matName);
                
                if (!string.IsNullOrEmpty(diffusePath))
                {
                    string diffuse = diffusePath.Replace(".vtf", ".png");
                    diffuse = Program.CleanPath(diffuse);

                    objMesh.SetMaterial(matName);
                    objMesh.WriteLine_MTL($"newmtl", matName);

                    string bumpPath = vmt.BumpPath;
                    bool noAlpha = vmt.NoAlpha;

                    if (!string.IsNullOrEmpty(bumpPath))
                    {
                        string bump = bumpPath.Replace(".vtf", ".png");
                        objMesh.WriteLine_MTL("bump", Program.CleanPath(bump));
                    }

                    objMesh.WriteLine_MTL("map_Kd", diffuse);
                    objMesh.WriteLine_MTL(noAlpha ? "" : $"map_d {diffuse}\n");
                }
                
                for (int i = 0; i < mesh.NumIndices; i += 3)
                {
                    for (int j = 2; j >= 0; j--)
                    {
                        int f = faceIndexBase + mesh.Indices[i + j];
                        objMesh.AddIndex(f, f, f);
                    }

                    objMesh.AddFace();
                }

                faceIndexBase += mesh.NumVerts;
            }

            string obj = objMesh.WriteOBJ();
            string objPath = Path.Combine(exportDir, $"{name}.obj");

            string mtl = objMesh.WriteMTL();
            string mtlPath = Path.Combine(exportDir, $"{name}.mtl");

            File.WriteAllText(objPath, obj);
            Console.WriteLine($"\tWrote: {objPath}");

            File.WriteAllText(mtlPath, mtl);
            Console.WriteLine($"\tWrote: {mtlPath}");
        }

        public static Model BakeMDL_RBXM(ModelFile model, int skin = 0)
        {
            var modelPath = model.Location;
            var modelInfo = new FileInfo(modelPath);
            var uploadPool = new List<Task>();

            var game = model.Game ?? Program.GameMount;
            var gameName = game.GameName;

            string modelName = modelInfo.Name
                .Replace(".mdl", "")
                .ToLowerInvariant();

            string localAppData = Environment.GetEnvironmentVariable("localappdata");
            string rootWorkDir = Path.Combine(localAppData, "Roblox Studio", "content", "source", gameName);

            string rbxAssetDir = $"rbxasset://source/{gameName}";
            var assetManager = new AssetManager(rootWorkDir, rbxAssetDir);

            string modelDir = modelPath.Replace(modelInfo.Name, "");
            string meshDir = Path.Combine(modelDir, modelName);

            var meshBuffers = new List<MeshBuffer>();
            var exportBlob = new BinaryRobloxFile();

            var exportModel = new Model()
            {
                Name = modelName,
                Parent = exportBlob
            };

            for (int bodyPart = 0; bodyPart < model.BodyPartCount; bodyPart++)
            {
                var meshes = model.GetMeshes(bodyPart, skin);
                meshBuffers.AddRange(meshes);
            }

            var bones = new List<Bone>();
            var objectSpace = new Dictionary<Bone, CFrame>();
            
            foreach (var studioBone in model.Bones)
            {
                int parentId = studioBone.Parent;
                var quat = studioBone.Quaternion;

                var pos = studioBone.Position;
                pos = new Vector3(pos.X, pos.Z, -pos.Y) / Program.STUDS_TO_VMF;

                var cf = new CFrame(pos)
                    * BONE_OFFSET
                    * quat.ToCFrame()
                    * BONE_OFFSET.Inverse();

                var bone = new Bone()
                {
                    Name = studioBone.Name,
                    CFrame = cf,
                };

                if (parentId >= 0)
                {
                    var parent = bones[parentId];
                    bone.CFrame = parent.CFrame * cf;
                    bone.Parent = parent;
                }

                bones.Add(bone);
                objectSpace[bone] = cf;
            }

            var lastBodyPart = "";
            var meshBinds = new Dictionary<MeshPart, RobloxMeshFile>();

            foreach (var meshBuffer in meshBuffers)
            {
                var pointCloud = meshBuffer.Vertices
                    .Select(vert => (RobloxVertex)vert)
                    .Select(vert => vert.Position);

                Region3 aabb = ComputeAABB(pointCloud);
                Vector3 center = aabb.CFrame.Position;

                int numVerts = meshBuffer.NumVerts;
                int numIndices = meshBuffer.NumIndices;

                var verts = meshBuffer.Vertices;
                var indices = meshBuffer.Indices;

                string matPath = meshBuffer.MaterialPath;

                if (matPath == "")
                    matPath = "error.vmt";

                var meshFile = new RobloxMeshFile();
                meshFile.Bones.AddRange(bones);

                var mesh = new RobloxMesh();
                meshFile.Meshes.Add(mesh);

                foreach (var studioVert in verts)
                {
                    var vert = (RobloxVertex)studioVert;
                    vert.Position -= center;

                    var boneIds = studioVert.Bones;
                    var weights = studioVert.Weights;

                    byte weightLeft = 255;

                    for (int i = 0; i < vert.NumBones; i++)
                    {
                        var boneId = boneIds[i];
                        var bone = bones[boneId];

                        var weight = weights[i];
                        var rounded = (byte)(weight * 255f);

                        if (i + 1 == vert.NumBones)
                            rounded = weightLeft;

                        vert.Bones[bone] = rounded;
                        weightLeft -= rounded;
                    }

                    meshFile.Verts.Add(vert);
                }

                for (int i = 0; i < numIndices; i += 3)
                {
                    var face = new int[3]
                    {
                        indices[i + 2],
                        indices[i + 1],
                        indices[i + 0],
                    };

                    mesh.Faces.Add(face);
                }

                var info = new FileInfo(matPath);
                string name = meshBuffer.BodyPart;
                string matName = info.Name.Replace(".vmt", "");

                if (name == lastBodyPart)
                    name = matName;
                else
                    lastBodyPart = name;

                if (!name.StartsWith(modelName))
                    name = $"{modelName}_{name}";

                if (!GameMount.HasFile(matPath, game))
                    matPath = "error.vmt";

                var vmt = new ValveMaterial(matPath, game);
                bool noAlpha = vmt.NoAlpha;
                
                string diffusePath = vmt.DiffusePath;
                vmt.SaveVTF(diffusePath, rootWorkDir);

                string bumpPath = vmt.BumpPath;
                vmt.SaveVTF(bumpPath, rootWorkDir, true);

                var size = aabb.Size;

                if (size.X <= 0.1f)
                    size = new Vector3(0.1f, size.Y, size.Z);

                if (size.Y <= 0.1f)
                    size = new Vector3(size.X, 0.1f, size.Z);

                if (size.Z <= 0.1f)
                    size = new Vector3(size.X, size.Y, 0.1f);
                
                var meshPart = new MeshPart()
                {
                    Material = vmt.Material,
                    CFrame = aabb.CFrame,
                    DoubleSided = true,
                    InitialSize = size,
                    Anchored = true,
                    Size = size,
                    Name = name,
                };

                if (vmt.Material == Material.Glass && !vmt.NoAlpha)
                    meshPart.CastShadow = false;

                var surface = new SurfaceAppearance() { AlphaMode = noAlpha ? AlphaMode.Overlay : AlphaMode.Transparency };

                if (!string.IsNullOrEmpty(diffusePath))
                    assetManager.BindAssetId(diffusePath, uploadPool, surface, "ColorMap");

                if (!string.IsNullOrEmpty(bumpPath))
                    assetManager.BindAssetId(bumpPath, uploadPool, surface, "NormalMap");

                vmt.SaveVTF(ENV_DARK, rootWorkDir);
                vmt.SaveVTF(ENV_LIGHT, rootWorkDir);

                if (vmt.EnvMap)
                {
                    assetManager.BindAssetId(ENV_DARK, uploadPool, surface, "RoughnessMap");
                    assetManager.BindAssetId(ENV_LIGHT, uploadPool, surface, "MetalnessMap");
                }
                else
                {
                    assetManager.BindAssetId(ENV_DARK, uploadPool, surface, "MetalnessMap");
                    assetManager.BindAssetId(ENV_LIGHT, uploadPool, surface, "RoughnessMap");
                }

                surface.Parent = meshPart;
                meshPart.Parent = exportModel;
                meshBinds[meshPart] = meshFile;
            }

            string meshWorkDir = Path.Combine(rootWorkDir, meshDir);
            var parts = exportModel.GetChildrenOfType<BasePart>();

            BasePart largestPart = null;
            float largestMass = 0;

            foreach (var part in parts)
            {
                var size = part.Size;
                var mass = size.X * size.Y * size.Z;

                if (mass > largestMass)
                {
                    largestMass = mass;
                    largestPart = part;
                }
            }

            if (largestPart == null)
            {
                // No geometry...?
                var dummy = new Part()
                {
                    CFrame = new CFrame(),
                    Size = new Vector3(),
                    CanCollide = false,
                    CanTouch = false,
                    CanQuery = false,
                    Name = modelName,
                    Transparency = 1,
                    Anchored = true,
                    Locked = true,
                };

                largestPart = dummy;
                dummy.Parent = exportModel;
            }

            foreach (var bone in bones)
            {
                if (bone.Parent == null)
                    bone.Parent = largestPart;

                bone.CFrame -= largestPart.Position;
            }
            
            foreach (var part in parts)
            {
                MeshPart meshPart = null;

                if (part is MeshPart)
                {
                    meshPart = part as MeshPart;
                    meshPart.HasSkinnedMesh = bones.Any();
                }

                if (part == largestPart)
                    continue;

                var weld = new WeldConstraint()
                {
                    Part0 = largestPart,
                    Part1 = part,
                    State = 1,
                };

                part.Anchored = false;
                weld.Parent = part;
            }

            foreach (var meshPart in meshBinds.Keys)
            {
                var meshFile = meshBinds[meshPart];
                string name = meshPart.Name;

                string meshPath = Path.Combine(meshWorkDir, $"{name}.mesh");
                Directory.CreateDirectory(meshWorkDir);

                using (var stream = File.OpenWrite(meshPath))
                {
                    meshFile.Save(stream);
                    Console.WriteLine($"\tWrote {meshPath}");
                }

                assetManager.BindAssetId($"{meshDir}/{name}.mesh", uploadPool, meshPart, "MeshId");
            }

            var uploadTask = Task.WhenAll(uploadPool);
            uploadTask.Wait();

            foreach (var bone in bones)
            {
                bone.CFrame = objectSpace[bone];

                if (bone.Parent is Bone parent)
                    continue;

                bone.CFrame -= largestPart.Position;
            }

            string exportPath = Path.Combine(rootWorkDir, modelDir, $"{modelName}.rbxm");
            largestPart.PivotOffset = largestPart.CFrame.Inverse();
            exportModel.PrimaryPart = largestPart;

            exportBlob.Save(exportPath);
            Console.WriteLine($"\tWrote {exportPath}");

            return exportModel;
        }

        public static void BakeBSP_OBJ(BSPFile bsp, string exportDir, GameMount game = null)
        {
            string mapName = bsp.Name;
            var geometry = new WorldGeometry(bsp, exportDir, game);

            // Write OBJ files.
            Console.WriteLine("Writing OBJ files...");

            string objPath = Path.Combine(exportDir, $"{mapName}.obj");
            string mtlPath = Path.Combine(exportDir, $"{mapName}.mtl");

            var objMesh = geometry.Mesh;
            var clusters = geometry.FaceClusters;

            for (int i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];

                foreach (var face in cluster)
                {
                    int dispInfo = face.DispInfo,
                        numEdges = face.NumEdges,
                        firstVert = face.FirstVert,
                        firstNorm = face.FirstNorm,
                        firstUV = face.FirstUV;

                    var center = face.Center;
                    objMesh.SetMaterial(face.Material);

                    if (dispInfo >= 0)
                    {
                        int dispSize = (int)Math.Sqrt(numEdges);
                        objMesh.SetObject($"disp_{dispInfo}");

                        for (int y = 0; y < dispSize - 1; y++)
                        {
                            for (int x = 0; x < dispSize - 1; x++)
                            {
                                int aa = firstVert + (y * dispSize) + x;
                                int ab = aa + 1;

                                int bb = ab + dispSize;
                                int ba = bb - 1;

                                objMesh.AddIndex(ba, ba, ba);
                                objMesh.AddIndex(bb, bb, bb);
                                objMesh.AddIndex(ab, ab, ab);
                                objMesh.AddIndex(aa, aa, aa);

                                objMesh.AddFace();
                            }
                        }
                    }
                    else
                    {
                        objMesh.SetObject($"cluster_{i}");

                        for (int j = 0; j < numEdges; j++)
                        {
                            var norm = firstNorm + j;
                            var vert = firstVert + j;
                            var uv = firstUV + j;

                            objMesh.AddIndex(vert, norm, uv);
                        }
                    }

                    objMesh.AddFace();
                }
            }

            string obj = objMesh.WriteOBJ();
            File.WriteAllText(objPath, obj);

            string mtl = objMesh.WriteMTL();
            File.WriteAllText(mtlPath, mtl);
        }

        public static WorldGeometry BakeBSP_RBXL(BSPFile bsp, GameMount game = null)
        {
            string mapName = bsp.Name;
            string gameName = GameMount.GetGameName(game);
            string localAppData = Environment.GetEnvironmentVariable("localappdata");

            string contentDir = Path.Combine(localAppData, "Roblox Studio", "content");
            string sourceDir = Path.Combine(contentDir, "source", gameName);
            string rbxAsset = $"rbxasset://source/{gameName}";

            var assetManager = new AssetManager(sourceDir, rbxAsset);
            var geometry = new WorldGeometry(bsp, sourceDir, game);

            string mapsDir = Path.Combine(sourceDir, "maps");
            string mapDir = Path.Combine(mapsDir, bsp.Name);

            Console.WriteLine("Writing Roblox files...");
            Directory.CreateDirectory(mapDir);

            var uploadPool = new List<Task>();
            var map = new BinaryRobloxFile();

            var lighting = new Lighting()
            {
                Technology = Technology.Future,
                EnvironmentSpecularScale = 0.2f,
                EnvironmentDiffuseScale = 0.2f,
                OutdoorAmbient = new Color3(),
                Ambient = new Color3(),
                GlobalShadows = true,
                Brightness = 2,
                Parent = map
            };

            var workspace = new Workspace()
            {
                StreamingEnabled = true,
                Parent = map
            };

            var camera = new Camera() { Parent = workspace };
            workspace.CurrentCamera = camera;

            var skyCamera = bsp.FindEntityByClass("sky_camera");
            var worldSpawn = bsp.FindEntityByClass("worldspawn");
            var lightEnv = bsp.FindEntityByClass("light_environment");

            if (skyCamera != null)
            {
                var skyCam = new Part()
                {
                    Name = "SKY_CAMERA",
                    Shape = PartType.Ball,
                    Color = new Color3(1, 0, 0),
                    CFrame = skyCamera.CFrame,
                    Anchored = true,
                    CanCollide = false,
                    Size = new Vector3(1, 1, 1),
                    TopSurface = SurfaceType.Smooth,
                    BottomSurface = SurfaceType.Smooth,
                    Parent = workspace
                };

                skyCam.Tags.Add("SkyCamera");
            }
            
            if (worldSpawn != null)
            {
                var skyName = worldSpawn.GetString("skyname");

                if (!string.IsNullOrEmpty(skyName))
                {
                    var sky = new Sky();

                    foreach (var pair in sky.Properties)
                    {
                        if (!pair.Key.StartsWith("Skybox"))
                            continue;

                        string suffix = pair.Key
                            .Replace("Skybox", "")
                            .ToLowerInvariant();

                        string matName = $"materials/skybox/{skyName}{suffix}.vmt";
                        ValveMaterial material = geometry.RegisterMaterial(matName, game);

                        if (material == null)
                            continue;

                        string diffuse = material.DiffusePath;
                        string prop = pair.Value.Name;

                        if (string.IsNullOrEmpty(diffuse))
                            continue;

                        material.SaveVTF(diffuse, sourceDir, true);
                        assetManager.BindAssetId(diffuse, uploadPool, sky, prop);
                    }

                    sky.Parent = lighting;
                }
            }

            if (lightEnv != null)
            {
                var ambient = lightEnv.TryGet<Ambient>("_ambient");
                var light = lightEnv.TryGet<Ambient>("_light");

                var angles = lightEnv.Get<Vector3>("angles");
                var pitch = lightEnv.GetInt("pitch");

                if (ambient != null)
                {
                    var black = new Color3();
                    var brightness = ambient.Value.Brightness / 400f;
                    lighting.Ambient = black.Lerp(ambient.Value.Color, brightness);
                }

                if (light != null)
                {
                    lighting.ColorShift_Top = light.Value.Color;
                    lighting.ExposureCompensation = light.Value.Brightness / 1000f;
                }

                if (angles != null)
                {
                    if (pitch != null)
                        angles = new Vector3(pitch.Value, angles.Y, angles.Z);

                    lighting.GeographicLatitude = -angles.X;
                    angles = new Vector3(90 - angles.X, angles.Y, angles.Z);

                    var cf = Entity.GetCFrame(new Vector3(), angles);
                    var sunDir = -cf.UpVector;

                    var longitude = Math.Atan2(-sunDir.Y, -sunDir.X);
                    var clockTime = 24 + ((longitude / (Math.PI * 2)) * 24 - 6);

                    var hour = (int)clockTime;
                    var min = (int)((clockTime - hour) * 60);

                    lighting.TimeOfDay = $"{hour}:{min}";
                }
            }

            var entityModels = new Dictionary<Entity, Model>();
            var materialSets = geometry.Materials;

            var clusters = geometry.FaceClusters;
            var objMesh = geometry.Mesh;
            
            int clusterId = 0;
            int dispId = 0;

            foreach (var cluster in clusters)
            {
                string meshName = "";

                string material = cluster
                    .First()
                    .Material;

                bool hasDisp = cluster
                    .Where(face => face.DispInfo >= 0)
                    .Any();

                if (hasDisp)
                    meshName = $"disp_{++dispId}.mesh";
                else
                    meshName = $"cluster_{++clusterId}.mesh";

                var meshPath = Path.Combine(mapDir, meshName);
                var meshFile = new RobloxMeshFile();

                var mesh = new RobloxMesh();
                meshFile.Meshes.Add(mesh);

                using (var meshStream = File.OpenWrite(meshPath))
                {
                    var entity = cluster
                        .Select(face => face.Entity)
                        .Where(ent => ent != null)
                        .FirstOrDefault();

                    foreach (var face in cluster)
                    {
                        short numEdges = face.NumEdges,
                              dispInfo = face.DispInfo;

                        int firstUV = face.FirstUV,
                            firstNorm = face.FirstNorm,
                            firstVert = face.FirstVert,
                            vertIndex = meshFile.Verts.Count;

                        for (int i = 0; i < numEdges; i++)
                        {
                            var pos = objMesh.GetVertex(firstVert + i);
                            var norm = objMesh.Normals[firstNorm + i];
                            var uv = objMesh.UVs[firstUV + i];

                            var vert = new RobloxVertex()
                            {
                                Position = pos,
                                Normal = norm,
                                UV = uv
                            };

                            meshFile.Verts.Add(vert);
                        }

                        if (dispInfo >= 0)
                        {
                            int dispSize = (int)Math.Sqrt(numEdges);

                            for (int y = 0; y < dispSize - 1; y++)
                            {
                                for (int x = 0; x < dispSize - 1; x++)
                                {
                                    int aa = vertIndex + (y * dispSize) + x;
                                    int ab = aa + 1;

                                    int bb = ab + dispSize;
                                    int ba = bb - 1;

                                    var faceA = new int[3] { bb, ba, aa };
                                    mesh.Faces.Add(faceA);

                                    var faceB = new int[3] { ab, bb, aa };
                                    mesh.Faces.Add(faceB);
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < numEdges - 2; i++)
                            {
                                mesh.Faces.Add(new int[3]
                                {
                                    vertIndex,
                                    vertIndex + i + 1,
                                    vertIndex + i + 2,
                                });
                            }
                        }
                    }

                    var verts = meshFile.Verts
                        .Select(vert => vert.Position)
                        .ToList();

                    var aabb = ComputeAABB(verts);
                    var cf = aabb.CFrame;
                    var size = aabb.Size;

                    var origin = cf.Position;
                    var padding = new Vector3();

                    foreach (var vert in meshFile.Verts)
                        vert.Position -= origin;

                    if (size.X < 0.1f)
                        size = new Vector3(0.1f, size.Y, size.Z);

                    if (size.Y < 0.1f)
                        size = new Vector3(size.X, 0.1f, size.Z);

                    if (size.Z < 0.1f)
                        size = new Vector3(size.X, size.Y, 0.1f);

                    meshFile.Save(meshStream);

                    var physicsMesh = new PhysicsMesh(meshFile);
                    var physics = physicsMesh.Serialize();

                    var meshPart = new MeshPart()
                    {
                        Name = material.ToLowerInvariant(),
                        PhysicalConfigData = physics,
                        InitialSize = size,
                        DoubleSided = true,
                        Anchored = true,
                        Locked = true,
                        Size = size,
                        CFrame = cf,
                    };

                    if (entity != null)
                    {
                        if (!entityModels.TryGetValue(entity, out var entModel))
                        {
                            entModel = new Model()
                            {
                                Name = entity.Name,
                                Parent = workspace
                            };

                            entityModels[entity] = entModel;
                        }

                        if (entity.ClassName == "func_illusionary" || entity.ClassName == "func_brush")
                        {
                            var noShadows = entity.GetInt("disableshadows");
                            var renderMode = entity.GetInt("rendermode");
                            var solidity = entity.GetInt("solidity");

                            bool visible = (renderMode ?? 0) != 10;
                            meshPart.Transparency = visible ? 0 : 1;
                            meshPart.CastShadow = (noShadows ?? 0) == 0;

                            if (solidity.HasValue)
                            {
                                switch (solidity.Value)
                                {
                                    case 0:
                                    {
                                        meshPart.CanCollide = visible;
                                        break;
                                    }
                                    case 1:
                                    {
                                        meshPart.CanCollide = false;
                                        break;
                                    }
                                    case 2:
                                    {
                                        meshPart.CanCollide = true;
                                        break;
                                    }
                                }
                            }
                        }

                        meshPart.Parent = entModel;
                    }

                    if (meshPart.Parent == null)
                        meshPart.Parent = workspace;

                    assetManager.BindAssetId($"maps/{mapName}/{meshName}", uploadPool, meshPart, "MeshId");

                    if (materialSets.TryGetValue(material, out var vmt))
                    {
                        string diffuse = vmt.DiffusePath;
                        string bump = vmt.BumpPath;

                        var surface = new SurfaceAppearance()
                        {
                            Name = "Material",
                            Parent = meshPart,
                            AlphaMode = vmt.NoAlpha ? AlphaMode.Overlay : AlphaMode.Transparency
                        };

                        if (vmt.Material == Material.Glass && !vmt.NoAlpha)
                            meshPart.CastShadow = false;

                        if (!string.IsNullOrEmpty(diffuse))
                        {
                            var baseLight = new SurfaceLight()
                            {
                                Angle = 180,
                                Shadows = true,
                                Brightness = .2f,
                            };

                            var addFace = new Action<NormalId>(face =>
                            {
                                var light = baseLight.Clone() as SurfaceLight;
                                light.Parent = meshPart;
                                light.Face = face;
                            });

                            if (meshPart.Name.StartsWith("lights/white"))
                            {
                                if (size.X > 0.1f && size.Z > 0.1f)
                                {
                                    addFace(NormalId.Top);
                                    addFace(NormalId.Bottom);
                                }

                                if (size.X > 0.1f && size.Y > 0.1f)
                                {
                                    addFace(NormalId.Front);
                                    addFace(NormalId.Back);
                                }

                                if (size.Y > 0.01f && size.Z > 0.1f)
                                {
                                    addFace(NormalId.Left);
                                    addFace(NormalId.Right);
                                }

                                vmt.Material = Material.Neon;
                                surface.Parent = null;
                            }

                            vmt.SaveVTF(diffuse, sourceDir);
                            assetManager.BindAssetId(diffuse, uploadPool, surface, "ColorMap");
                        }
                        
                        if (!string.IsNullOrEmpty(bump))
                        {
                            vmt.SaveVTF(bump, sourceDir, true);
                            assetManager.BindAssetId(bump, uploadPool, surface, "NormalMap");
                        }

                        vmt.SaveVTF(ENV_DARK, sourceDir);
                        vmt.SaveVTF(ENV_LIGHT, sourceDir);

                        if (vmt.EnvMap)
                        {
                            assetManager.BindAssetId(ENV_DARK, uploadPool, surface, "RoughnessMap");
                            assetManager.BindAssetId(ENV_LIGHT, uploadPool, surface, "MetalnessMap");
                        }
                        else
                        {
                            assetManager.BindAssetId(ENV_DARK, uploadPool, surface, "MetalnessMap");
                            assetManager.BindAssetId(ENV_LIGHT, uploadPool, surface, "RoughnessMap");
                        }

                        meshPart.Material = vmt.Material;
                    }
                }
            }
            
            var devShots = bsp.FindEntitiesOfClass("point_devshot_camera");

            var cameras = new Folder()
            {
                Name = "Cameras",
                Parent = workspace
            };

            foreach (var devShot in devShots)
            {
                var renderTest = new RenderingTest()
                {
                    CFrame = devShot.CFrame,
                    Name = devShot.Name,
                    Parent = cameras
                };

                var fov = devShot.TryGet<int>("FOV");

                if (fov == null)
                    continue;

                renderTest.FieldOfView = fov.Value;
            }

            var entityProps = bsp.FindEntitiesOfClass
            (
                "prop_dynamic",
                "prop_physics",
                "prop_physics_multiplayer"
            );

            var staticProps = geometry.StaticProps;
            var detailProps = geometry.DetailProps;

            var partTree = new Octree<BasePart>();
            var modelSets = new HashSet<ModelRequest>();
            
            var models = new Dictionary<string, Dictionary<int, Model>>();
            Console.WriteLine("Collecting models...");

            if (staticProps != null)
            {
                foreach (var prop in staticProps)
                {
                    var modelIndex = prop.PropType;
                    string modelName = staticProps.Strings[modelIndex];

                    var request = new ModelRequest()
                    {
                        ModelName = modelName,
                        Skin = prop.Skin
                    };

                    modelSets.Add(request);
                }
            }

            foreach (var prop in entityProps)
            {
                var modelName = prop.Get<string>("model");
                int skin = prop.TryGet<int>("skin") ?? 0;

                if (modelName == null)
                    continue;

                if (!GameMount.HasFile(modelName, game))
                    continue;

                var detail = new ModelRequest()
                {
                    ModelName = modelName,
                    Skin = skin
                };

                modelSets.Add(detail);
            }

            foreach (var request in modelSets)
            {
                string modelName = request.ModelName;
                string newExt = ".rbxm";
                int skin = request.Skin;

                if (skin > 0)
                    newExt = $"_skin{skin}.rbxm";

                string rbxModel = request.ModelName.Replace(".mdl", newExt);
                string modelPath = Path.Combine(sourceDir, rbxModel);

                if (!models.TryGetValue(modelName, out var skins))
                {
                    skins = new Dictionary<int, Model>();
                    models[modelName] = skins;
                }

                if (!GameMount.HasFile(modelName, game))
                {
                    Program.LogError($"Could not find model: {modelName}");
                    continue;
                }

                var modelFile = new ModelFile(modelName, game);
                skins[skin] = BakeMDL_RBXM(modelFile, skin);
            }

            foreach (var prop in staticProps)
            {
                int skin = prop.Skin;

                if (!models.TryGetValue(prop.Name, out var skins))
                    continue;

                if (!skins.TryGetValue(skin, out var modelSource))
                    continue;

                var origin = prop.Position;
                var angles = prop.Rotation;

                var model = modelSource.Clone() as Model;
                var cf = Entity.GetCFrame(origin, angles);

                model.PivotTo(cf);
                model.Parent = workspace;

                foreach (var part in model.GetDescendantsOfType<BasePart>())
                {
                    var position = part.Position;
                    partTree.CreateNode(position, part);
                }
            }

            foreach (var prop in entityProps)
            {
                var cf = prop.CFrame;
                int skin = prop.TryGet<int>("skin") ?? 0;
                var modelName = prop.Get<string>("model");

                if (!models.TryGetValue(modelName, out var skins))
                    continue;

                if (!skins.TryGetValue(skin, out var modelSource))
                    continue;

                var model = modelSource.Clone() as Model;
                var primary = model.PrimaryPart;

                model.PivotTo(cf);
                model.Parent = workspace;

                if (prop.ClassName == "prop_dynamic")
                    continue;

                foreach (var part in model.GetDescendantsOfType<BasePart>())
                {
                    part.Anchored = false;

                    if (primary != null)
                    {
                        if (primary == part)
                            continue;

                        var weld = new WeldConstraint()
                        {
                            State = 1,
                            Part0 = primary,
                            Part1 = part,
                            Parent = part
                        };
                    }
                }
            }

            var pointLights = bsp.FindEntitiesOfClass("light");
            var spotLights = bsp.FindEntitiesOfClass("light_spot");

            foreach (var lightEnt in pointLights.Concat(spotLights))
            {
                var origin = lightEnt.Get<Vector3>("origin");
                var angles = lightEnt.Get<Vector3>("angles");

                if (origin != null && angles != null)
                {
                    Ambient? effects = lightEnt.TryGet<Ambient>("_light");
                    float? pitch = lightEnt.TryGet<float>("pitch");

                    string className = lightEnt.ClassName;
                    Light light = null;

                    if (className == "light")
                    {
                        light = new PointLight() { Range = 60 };
                    }
                    else if (className == "light_spot")
                    {
                        float? cone = lightEnt.TryGet<float>("_cone");

                        light = new SpotLight() 
                        {
                            Angle = (cone ?? 45) * 2,
                            Face = NormalId.Right,
                            Range = 60
                        };
                    }

                    origin /= Program.STUDS_TO_VMF;

                    if (pitch != null)
                        angles = new Vector3(pitch.Value, angles.Y, angles.Z);

                    var cf = new CFrame(origin.X, origin.Z, -origin.Y)
                           * CFrame.Angles(0, angles.Y * DEG_TO_RAD, 0)
                           * CFrame.Angles(0, 0, angles.X * DEG_TO_RAD);

                    var emitter = new Part()
                    {
                        Name = lightEnt.Name,
                        Size = new Vector3(),
                        CanCollide = false,
                        CanTouch = false,
                        Anchored = true,
                        CFrame = cf,
                        Transparency = 1,
                        Parent = workspace
                    };

                    light.Brightness = (effects?.Brightness ?? 1000f) / 800f;
                    light.Color = effects?.Color ?? new Color3(1, 1, 1);
                    light.Parent = emitter;
                    light.Shadows = true;
                }
            }

            var ropes = bsp.FindEntitiesOfClass("keyframe_rope");
            var ropeDict = new Dictionary<string, Attachment>();

            var ropeBin = new Part()
            {
                Name = "Ropes",
                Size = new Vector3(),
                Transparency = 1,
                Locked = true,
                Anchored = true,
                CanQuery = false,
                CanTouch = false,
                CanCollide = false,
                Parent = workspace
            };

            foreach (var rope in ropes)
            {
                var angles = new Vector3();
                var origin = rope.Get<Vector3>("origin");

                var ropeAtt = new Attachment()
                {
                    Name = rope.Name,
                    CFrame = Entity.GetCFrame(origin, angles),
                    Parent = ropeBin
                };

                ropeDict[rope.Name] = ropeAtt;
            }

            foreach (var rope in ropes)
            {
                string material = rope
                    .Get<string>("ropematerial")
                    .Replace("cable/", "");

                if (material == "chain")
                    continue;

                string key0 = rope.Name,
                       key1 = rope.GetString("nextkey");

                if (key1 == null)
                    continue;

                if (!ropeDict.TryGetValue(key0, out var att0))
                    continue;

                if (!ropeDict.TryGetValue(key1, out var att1))
                    continue;

                var length = (att1.CFrame.Position - att0.CFrame.Position).Magnitude;
                var slack = rope.TryGet<float>("slack");

                if (slack.HasValue)
                    length += slack.Value / Program.STUDS_TO_VMF / 8;

                var ropeConstraint = new RopeConstraint()
                {
                    Name = key0,
                    Visible = true,
                    Attachment0 = att0,
                    Attachment1 = att1,
                    Length = length,
                    Parent = ropeBin
                };

                var width = rope.TryGet<float>("width");

                if (width.HasValue)
                    ropeConstraint.Thickness = width.Value / 10f;

                switch (material)
                {
                    case "cable":
                    {
                        ropeConstraint.Color = BrickColor.FromName("Really black");
                        break;
                    }
                    case "red":
                    {
                        ropeConstraint.Color = BrickColor.Red();
                        break;
                    }
                    case "green":
                    {
                        ropeConstraint.Color = BrickColor.Green();
                        break;
                    }
                    case "blue":
                    {
                        ropeConstraint.Color = BrickColor.Blue();
                        break;
                    }
                    default:
                    {
                        ropeConstraint.Color = BrickColor.FromName("Earth orange");
                        break;
                    }
                }
            }

            var uploadTask = Task.WhenAll(uploadPool);
            uploadTask.Wait();

            string savePath = Path.Combine(mapsDir, bsp.Name + ".rbxl");
            map.Save(savePath);

            Process.Start(savePath);
            return geometry;
        }
    }
}
