using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using ValveKeyValue;
using Source2Roblox.FileSystem;
using System.Linq;
using RobloxFiles.Enums;

namespace Source2Roblox.Textures
{

    public class ValveMaterial
    {
        private static readonly KVSerializer vmtHelper = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        private static readonly Dictionary<string, Image> handledFiles = new Dictionary<string, Image>();

        private readonly GameMount Game;
        public string DiffusePath;
        public string DetailPath;
        public string BumpPath;
        public string IrisPath;
        public string Shader;

        public bool EnvMap = false;
        public bool NoAlpha = true;
        public bool Additive = false;
        public bool SelfIllum = false;
        public bool SelfShadowedBump = false;
        public Material Material = Material.Plastic;

        public Image SaveVTF(string path, string rootDir, bool? noAlpha = null)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var info = new FileInfo(path);

            string fileName = info.Name;
            string matDir = path.Replace(fileName, "");
            rootDir = Path.Combine(rootDir, matDir);

            if (!Directory.Exists(rootDir))
                Directory.CreateDirectory(rootDir);

            string filePath = Path
                .Combine(rootDir, fileName)
                .Replace(".vtf", ".png");

            if (handledFiles.ContainsKey(filePath))
                return handledFiles[filePath];

            if (!handledFiles.TryGetValue(path, out Image bitmap))
            {
                if (File.Exists(filePath))
                {
                    bool canPreload = true;

                    if (SelfShadowedBump)
                    {
                        string sourcePath = filePath.Replace("ssbump", "ssbump-source");
                        canPreload = File.Exists(sourcePath);
                    }

                    if (canPreload)
                    {
                        var preload = Image.FromFile(filePath);
                        handledFiles.Add(filePath, preload);

                        Console.WriteLine($"\tPreloaded {path}");
                        return preload;
                    }
                }

                // TODO: Implement Pakfile Lump.
                Console.WriteLine($"\tReading {path}");

                if (!GameMount.HasFile(path, Game))
                {
                    Console.WriteLine($"\t\tImage not found!");
                    return null;
                }

                using (var vtfStream = GameMount.OpenRead(path, Game))
                using (var vtfReader = new BinaryReader(vtfStream))
                {
                    var vtf = new VTFFile(vtfReader, noAlpha ?? NoAlpha);
                    bitmap = vtf.HighResImage;

                    if (SelfShadowedBump)
                    {
                        var normalMap = SSBump.ToNormalMap(bitmap);
                        string sourcePath = filePath.Replace("ssbump", "ssbump-source");

                        bitmap.Save(sourcePath);
                        bitmap = normalMap;
                    }

                    handledFiles.Add(path, bitmap);
                }
            }

            bitmap.Save(filePath);
            handledFiles[filePath] = bitmap;

            Console.WriteLine($"\tWrote {filePath}");
            return bitmap;
        }

        private void ReadEntry(KVObject entry)
        {
            string key = entry.Name.ToLowerInvariant();
            var value = entry.Value.ToString();

            switch (key)
            {
                case ">=dx90_20b":
                {
                    foreach (var child in entry.Children)
                        ReadEntry(child);

                    break;
                }
                case "$basetexture":
                {
                    DiffusePath = $"materials/{value}.vtf";
                    break;
                }
                case "$detail":
                {
                    DetailPath = $"materials/{value}.vtf";
                    break;
                }
                case "$detailblendmode":
                {
                    if (value == "4")
                    {
                        var temp = DetailPath;
                        DetailPath = DiffusePath;
                        DiffusePath = temp;
                    }

                    break;
                }
                case "$bumpmap":
                {
                    BumpPath = $"materials/{value}.vtf";
                    break;
                }
                case "$selfillum":
                {
                    SelfIllum = (value == "1");
                    break;
                }
                case "$envmap":
                {
                    EnvMap = true;
                    break;
                }
                case "$ssbump":
                {
                    SelfShadowedBump = true;
                    break;
                }
                case "$alphatest":
                case "$translucent":
                {
                    NoAlpha = (value != "1");
                    break;
                }
                case "$additive":
                {
                    Additive = (value == "1");
                    break;
                }
                case "$iris":
                {
                    IrisPath = $"materials/{value}.vtf";
                    break;
                }
                case "$surfaceprop":
                {
                    switch (value.ToLowerInvariant())
                    {
                        case "brick":
                        {
                            Material = Material.Brick;
                            break;
                        }

                        case "concrete":
                        case "concrete_block":
                        {
                            Material = Material.Concrete;
                            break;
                        }

                        case "baserock":
                        case "boulder":
                        case "gravel":
                        case "rock":
                        {
                            Material = Material.Slate;
                            break;
                        }

                        case "canister":
                        case "chain":
                        case "chainlink":
                        case "combine_metal":
                        case "crowbar":
                        case "floating_metal_barrel":
                        case "grenade":
                        case "metal":
                        case "metal_barrel":
                        case "metal_bouncy":
                        case "metal_box":
                        case "metal_seafloorcar":
                        case "metalgrate":
                        case "metalpanel":
                        case "metalvent":
                        case "metalvehicle":
                        case "paintcan":
                        case "popcan":
                        case "roller":
                        case "slipperymetal":
                        case "solidmetal":
                        case "strider":
                        case "weapon":
                        {
                            Material = Material.Metal;
                            break;
                        }

                        case "wood":
                        case "wood_box":
                        case "wood_crate":
                        case "wood_furniture":
                        case "wood_lowdensity":
                        case "wood_plank":
                        case "wood_panel":
                        case "wood_solid":
                        {
                            Material = Material.Wood;
                            break;
                        }

                        case "glass":
                        case "glassbottle":
                        case "combine_glass":
                        {
                            Material = Material.Glass;
                            break;
                        }

                        case "slime":
                        case "water":
                        case "wade":
                        case "puddle":
                        case "slipperyslime":
                        {
                            Material = Material.Water;
                            break;
                        }

                        case "ice":
                        {
                            Material = Material.Glacier;
                            break;
                        }

                        case "snow":
                        {
                            Material = Material.Snow;
                            break;
                        }

                        case "grass":
                        {
                            Material = Material.Grass;
                            break;
                        }

                        case "dirt":
                        case "mud":
                        {
                            Material = Material.Mud;
                            break;
                        }

                        case "sand":
                        case "quicksand":
                        {
                            Material = Material.Sand;
                            break;
                        }

                        case "ceiling_tile":
                        case "computer":
                        case "pottery":
                        case "tile":
                        {
                            Material = Material.Marble;
                            break;
                        }

                        case "carpet":
                        case "paper":
                        case "papercup":
                        case "cardboard":
                        case "rubber":
                        case "rubbertire":
                        case "slidingrubbertire":
                        case "slidingrubbertire_front":
                        case "slidingrubbertire_rear":
                        case "jeeptire":
                        case "brakingrubbertire":
                        {
                            Material = Material.Fabric;
                            break;
                        }

                        case "asphalt":
                        {
                            Material = Material.Asphalt;
                            break;
                        }
                    }

                    break;
                }
            }   
        }

        public ValveMaterial(string path, GameMount game = null)
        {
            if (!path.StartsWith("materials"))
                path = "materials/" + path;

            if (!path.EndsWith(".vmt"))
                path += ".vmt";

            Game = game;

            if (!GameMount.HasFile(path, game))
            {
                Console.WriteLine($"Failed to bind ValveMaterial for: {path}");
                return;
            }

            using (var stream = GameMount.OpenRead(path, game))
            {
                var vmt = vmtHelper.Deserialize(stream);
                Shader = vmt.Name.ToLowerInvariant();

                if (Shader == "LightmappedGeneric")
                    NoAlpha = true;

                var keys = vmt.ToList();
                keys.ForEach(ReadEntry);
            }
        }
    }

}
