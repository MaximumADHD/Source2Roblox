using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Source2Roblox.FileSystem;
using Source2Roblox.Models;
using Source2Roblox.World;
using Source2Roblox.Textures;

using RobloxFiles;
using RobloxFiles.Enums;
using RobloxFiles.DataTypes;
using Source2Roblox.World.Types;
using Source2Roblox.Geometry.MeshTypes;

namespace Source2Roblox.Geometry
{
    public static class MeshBuilder
    {
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

        public static void BakeMDL_OBJ(ModelFile model, string exportDir, int skin = 0, int lod = 0, int subModel = 0)
        {
            var game = model.Game;
            var info = new FileInfo(model.Name);

            string name = info.Name.Replace(".mdl", "");
            exportDir = Path.Combine(exportDir, "SourceModels", name);

            if (!Directory.Exists(exportDir))
                Directory.CreateDirectory(exportDir);

            var objMesh = new ObjMesh();
            var meshBuffers = new List<MeshBuffer>();
            var handledFiles = new HashSet<string>();
            
            for (int bodyPart = 0; bodyPart < model.BodyPartCount; bodyPart++)
            {
                var meshes = model.GetMeshes(bodyPart, subModel, lod, skin);
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

        public static Model BakeMDL_RBXM(ModelFile model)
        {
            var modelPath = model.Location;
            var modelInfo = new FileInfo(modelPath);

            var game = model.Game ?? Program.GameMount;
            var gameName = game.GameName;

            string modelName = modelInfo.Name
                .Replace(".mdl", "")
                .ToLowerInvariant();

            string localAppData = Environment.GetEnvironmentVariable("localappdata");
            string rootWorkDir = Path.Combine(localAppData, "Roblox Studio", "content", "source", gameName);

            string rbxAssetDir = $"rbxasset://source/{gameName}";
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
                var meshes = model.GetMeshes(bodyPart);
                meshBuffers.AddRange(meshes);
            }

            var handledFiles = new HashSet<string>();
            string lastBodyPart = "";

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

                var mesh = new RobloxMesh()
                {
                    NumVerts = numVerts,
                    NumFaces = numIndices / 3,

                    Verts = new List<RobloxVertex>(),
                    Faces = new List<int[]>()
                };

                mesh.NumVerts = meshBuffer.NumVerts;
                mesh.NumFaces = meshBuffer.NumIndices / 3;

                foreach (RobloxVertex vert in verts)
                {
                    vert.Position -= center;
                    mesh.Verts.Add(vert);
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

                string matPath = meshBuffer.MaterialPath;
                var info = new FileInfo(matPath);

                string matName = info.Name.Replace(".vmt", "");
                string name = meshBuffer.BodyPart;

                if (name == lastBodyPart)
                    name = matName;
                else
                    lastBodyPart = name;

                if (!name.StartsWith(modelName))
                    name = $"{modelName}_{name}";

                ValveMaterial vmt = null;

                if (GameMount.HasFile(matPath, game))
                {
                    vmt = new ValveMaterial(matPath, game);
                    vmt.SaveVTF(vmt.DiffusePath, rootWorkDir);
                    vmt.SaveVTF(vmt.BumpPath, rootWorkDir, true);
                    vmt.SaveVTF(vmt.IrisPath, rootWorkDir, false);
                }

                string meshWorkDir = Path.Combine(rootWorkDir, meshDir);
                Directory.CreateDirectory(meshWorkDir);

                var meshPart = new MeshPart()
                {
                    MeshId = $"{rbxAssetDir}/{meshDir}/{name}.mesh",
                    InitialSize = aabb.Size,
                    CFrame = aabb.CFrame,
                    DoubleSided = true,
                    Size = aabb.Size,
                    Anchored = true,
                    Name = name,
                };

                string diffusePath = vmt?.DiffusePath;

                if (string.IsNullOrEmpty(diffusePath))
                {
                    meshPart.Color3uint8 = new Color3(1, 1, 1);
                    meshPart.Material = Material.SmoothPlastic;
                }
                else
                {
                    string diffuseRoblox = diffusePath.Replace(".vtf", ".png");
                    meshPart.TextureID = $"{rbxAssetDir}/{diffuseRoblox}";

                    bool noAlpha = vmt.NoAlpha;
                    string bumpPath = vmt.BumpPath;
                    string irisPath = vmt.IrisPath;

                    if (!string.IsNullOrEmpty(bumpPath) || !noAlpha)
                    {
                        var surface = new SurfaceAppearance()
                        {
                            AlphaMode = noAlpha ? AlphaMode.Overlay : AlphaMode.Transparency,
                            ColorMap = meshPart.TextureID,
                        };

                        if (!string.IsNullOrEmpty(bumpPath))
                        {
                            string bumpRoblox = bumpPath.Replace(".vtf", ".png");
                            surface.NormalMap = $"{rbxAssetDir}/{bumpRoblox}";
                        }

                        if (!string.IsNullOrEmpty(irisPath))
                        {
                            string irisRoblox = irisPath.Replace(".vtf", ".png");

                            var eye = new BillboardGui
                            {
                                ExtentsOffsetWorldSpace = new Vector3(0, 0, -1.5f),
                                Size = new UDim2(.06f, 0, .06f, 0),
                                LightInfluence = 1.1f,
                                Adornee = meshPart,
                                Parent = meshPart,
                                Name = "Eye"
                            };

                            var eyeball = new ImageLabel
                            {
                                AnchorPoint = new Vector2(.5f, .5f),
                                Position = new UDim2(.5f, 0, .5f, 0),
                                Size = new UDim2(2f, 0, 2f, 0),
                                Image = meshPart.TextureID,
                                Name = "Eyeball",
                                Parent = eye,
                            };

                            var corner = new UICorner
                            {
                                CornerRadius = new UDim(1),
                                Parent = eyeball
                            };

                            var iris = new ImageLabel
                            {
                                Image = $"{rbxAssetDir}/{irisRoblox}",
                                Position = new UDim2(.5f, 0, .5f, 0),
                                AnchorPoint = new Vector2(.5f, .5f),
                                Size = new UDim2(1.1f, 0, 1.1f, 0),
                                BackgroundTransparency = 1,
                                Name = "Iris",
                                Parent = eye,
                                ZIndex = 2,
                            };

                            meshPart.Transparency = 1;
                        }

                        surface.Parent = meshPart;
                    }
                }

                string meshPath = Path.Combine(meshWorkDir, $"{name}.mesh");

                using (var stream = File.OpenWrite(meshPath))
                {
                    mesh.Save(stream);
                    Console.WriteLine($"\tWrote {meshPath}");
                }

                meshPart.Parent = exportModel;
            }

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

            string exportPath = Path.Combine(rootWorkDir, modelDir, $"{modelName}.rbxm");
            exportModel.WorldPivotData = new CFrame();
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

                                objMesh.AddIndex(aa, aa, aa);
                                objMesh.AddIndex(ab, ab, ab);
                                objMesh.AddIndex(bb, bb, bb);
                                objMesh.AddIndex(ba, ba, ba);

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

        public static void BakeBSP_RBXL(BSPFile bsp, GameMount game = null)
        {
            string mapName = bsp.Name;
            string gameName = GameMount.GetGameName(game);
            string localAppData = Environment.GetEnvironmentVariable("localappdata");

            string contentDir = Path.Combine(localAppData, "Roblox Studio", "content");
            string sourceDir = Path.Combine(contentDir, "source", gameName);

            string rbxasset = $"rbxasset://source/{gameName}";
            var geometry = new WorldGeometry(bsp, sourceDir, game);
            
            string mapsDir = Path.Combine(sourceDir, "maps");
            string mapDir = Path.Combine(mapsDir, bsp.Name);

            Console.WriteLine("Writing Roblox files...");
            Directory.CreateDirectory(mapDir);

            var map = new XmlRobloxFile();

            var lighting = new Lighting()
            {
                Technology = Technology.Future,
                EnvironmentSpecularScale = 0.2f,
                EnvironmentDiffuseScale = 0.5f,
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

            var worldSpawn = bsp.FindEntityByClass("worldspawn");
            var lightEnv = bsp.FindEntityByClass("light_environment");

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

                        Property prop = pair.Value;
                        string diffuse = material.DiffusePath;

                        if (string.IsNullOrEmpty(diffuse))
                            continue;

                        string png = diffuse.Replace(".vtf", ".png");
                        material.SaveVTF(diffuse, sourceDir, true);
                        prop.Value = $"{rbxasset}/{png}";
                    }

                    sky.Parent = lighting;
                }
            }

            if (lightEnv != null)
            {
                var ambient = lightEnv.TryGet<Ambient>("_ambient");
                var light = lightEnv.TryGet<Ambient>("_light");

                if (ambient != null)
                {
                    var black = new Color3();
                    var brightness = ambient.Value.Brightness / 1000f;
                    lighting.Ambient = black.Lerp(ambient.Value.Color, brightness);
                }

                if (light != null)
                {
                    lighting.ColorShift_Top = light.Value.Color;
                    lighting.ExposureCompensation = light.Value.Brightness / 1000f;
                }
            }

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
                var mesh = new RobloxMesh();

                using (var meshStream = File.OpenWrite(meshPath))
                {
                    foreach (var face in cluster)
                    {
                        short numEdges = face.NumEdges,
                              dispInfo = face.DispInfo;

                        int firstUV = face.FirstUV,
                            firstNorm = face.FirstNorm,
                            firstVert = face.FirstVert,
                            vertIndex = mesh.Verts.Count;

                        for (int i = 0; i < numEdges; i++)
                        {
                            var pos = objMesh.GetVertex(firstVert + i);
                            var norm = objMesh.Normals[firstNorm + i];
                            var uv = objMesh.UVs[firstUV + i];

                            var vert = new RobloxVertex()
                            {
                                Position = pos,
                                Normal = norm
                            };

                            vert.SetUV(uv);
                            mesh.Verts.Add(vert);
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

                                    var faceA = new int[3] { aa, ba, bb };
                                    mesh.Faces.Add(faceA);

                                    var faceB = new int[3] { aa, bb, ab };
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

                    var verts = mesh.Verts
                        .Select(vert => vert.Position)
                        .ToList();

                    var aabb = ComputeAABB(verts);
                    var cf = aabb.CFrame;
                    var size = aabb.Size;

                    var origin = cf.Position;
                    var padding = new Vector3();

                    foreach (var vert in mesh.Verts)
                        vert.Position -= origin;

                    if (size.X <= 0)
                        padding += new Vector3(0.1f, 0, 0);

                    if (size.Y <= 0)
                        padding += new Vector3(0, 0.1f, 0);

                    if (size.Z <= 0)
                        padding += new Vector3(0, 0, 0.1f);

                    if (padding.Magnitude > 0)
                    {
                        var extraVerts = new Vector3[2]
                        {
                            origin - (padding / 2f),
                            origin + (padding / 2f),
                        };

                        foreach (var extraVert in extraVerts)
                        {
                            var vert = new RobloxVertex()
                            {
                                Position = extraVert,
                                Normal = new Vector3(),
                                UV = new Vector3()
                            };

                            mesh.Verts.Add(vert);
                        }
                        
                        size += padding;
                    }

                    var physicsMesh = new PhysicsMesh(mesh);
                    var physics = physicsMesh.Serialize();

                    var meshPart = new MeshPart()
                    {
                        MeshId = $"{rbxasset}/maps/{mapName}/{meshName}",
                        Name = material.ToLowerInvariant(),
                        PhysicalConfigData = physics,
                        InitialSize = size,
                        DoubleSided = true,
                        Anchored = true,
                        Size = size,
                        CFrame = cf,
                    };
                    
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

                        if (!string.IsNullOrEmpty(diffuse))
                        {
                            string png = diffuse.Replace(".vtf", ".png");
                            surface.ColorMap = $"{rbxasset}/{png}";
                            vmt.SaveVTF(diffuse, sourceDir);
                        }
                        
                        if (!string.IsNullOrEmpty(bump))
                        {
                            string png = bump.Replace(".vtf", ".png");
                            surface.NormalMap = $"{rbxasset}/{png}";
                            vmt.SaveVTF(bump, sourceDir, true);
                        }
                    }

                    mesh.NumVerts = mesh.Verts.Count;
                    mesh.NumFaces = mesh.Faces.Count;

                    mesh.Save(meshStream);
                    meshPart.Parent = workspace;
                }
            }

            var staticProps = geometry.StaticProps;
            var detailProps = geometry.DetailProps;

            var modelNames = new HashSet<string>();
            var models = new Dictionary<string, Model>();
            Console.WriteLine("Collecting models...");

            if (staticProps != null)
            {
                var set = staticProps
                    .Select(prop => prop.Name)
                    .Distinct()
                    .ToList();

                set.ForEach(name => modelNames.Add(name));
            }

            if (detailProps != null)
            {
                var set = detailProps.Names;
                set.ForEach(name => modelNames.Add(name));
            }

            foreach (string modelName in modelNames)
            {
                string rbxModel = modelName.Replace(".mdl", ".rbxm");
                string modelPath = Path.Combine(sourceDir, rbxModel);
                Model model = null;

                if (!File.Exists(modelPath))
                {
                    Console.WriteLine($"Loading model: {modelName}");
                    var modelFile = new ModelFile(modelName, game);

                    Console.WriteLine("\tBuilding model...");
                    model = BakeMDL_RBXM(modelFile);
                }
                else
                {
                    var file = RobloxFile.Open(modelPath);
                    model = file.FindFirstChildOfClass<Model>();
                    Console.WriteLine($"Loading pre-built model: {rbxModel}");
                }

                Debug.Assert(model != null, $"Error fetching model: {modelName}!");
                models[modelName] = model;
            }

            string savePath = Path.Combine(mapsDir, bsp.Name + ".rbxl");
            map.Save(savePath);

            Process.Start(savePath);
        }
    }
}
