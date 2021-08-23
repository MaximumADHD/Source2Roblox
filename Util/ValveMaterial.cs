using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using ValveKeyValue;
using Source2Roblox.FileSystem;
using Source2Roblox.Textures;
using System.Linq;

namespace Source2Roblox.Util
{

    public class ValveMaterial
    {
        private static readonly KVSerializer vmtHelper = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
        private static readonly Dictionary<string, Image> handledFiles = new Dictionary<string, Image>();

        private readonly GameMount Game;
        public string DiffusePath;
        public string BumpPath;
        public string IrisPath;
        public bool NoAlpha;

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
                    var preload = Image.FromFile(filePath);
                    handledFiles.Add(filePath, preload);

                    Console.WriteLine($"\tPreloaded {path}");
                    return preload;
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

            if (key == ">=dx90_20b")
            {
                foreach (var child in entry.Children)
                    ReadEntry(child);

                return;
            }

            if (key == "$basetexture")
            {
                DiffusePath = $"materials/{value}.vtf";
                return;
            }

            if (key == "$bumpmap")
            {
                BumpPath = $"materials/{value}.vtf";
                return;
            }

            if (key == "$translucent")
            {
                NoAlpha = (value != "1");
                return;
            }

            if (key == "$iris")
            {
                IrisPath = $"materials/{value}.vtf";
                return;
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
                var vmt = vmtHelper
                    .Deserialize(stream)
                    .ToList();

                vmt.ForEach(ReadEntry);
            }
        }
    }

}
