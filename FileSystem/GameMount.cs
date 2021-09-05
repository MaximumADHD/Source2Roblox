using System;
using System.Collections.Generic;
using System.IO;
using ValveKeyValue;

namespace Source2Roblox.FileSystem
{
    public class GameMount
    {
        private static readonly KVSerializer infoHelper = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);

        public readonly string GameDir;
        public readonly string GameName;

        private readonly Dictionary<string, string> Routing;
        private readonly Dictionary<string, VPKFile> Mounts;

        public GameMount(string gameInfoPath)
        {
            if (!gameInfoPath.EndsWith("gameinfo.txt"))
                gameInfoPath += "/gameinfo.txt";

            var gameFileInfo = new FileInfo(gameInfoPath);
            var dirInfo = gameFileInfo.Directory;

            var rootDir = dirInfo.Parent;
            var gameRoot = rootDir.FullName;

            GameName = dirInfo.Name;
            GameDir = dirInfo.FullName;
            
            Mounts = new Dictionary<string, VPKFile>();
            Routing = new Dictionary<string, string>();

            KVObject gameInfo;
            Console.WriteLine($"Loading game mount {gameInfoPath}...");

            using (var infoStream = File.OpenRead(gameInfoPath))
                gameInfo = infoHelper.Deserialize(infoStream);

            var fileSystem = gameInfo["FileSystem"];
            var vpkPaths = new List<string>();

            var searchPaths = fileSystem["SearchPaths"]
                as IEnumerable<KVObject>;

            foreach (var path in searchPaths)
            {
                string value = path.Value
                    .ToString()
                    .ToLowerInvariant();

                if (value.StartsWith("|all_source_engine_paths|"))
                    value = value.Replace("|all_source_engine_paths|", $"{gameRoot}/");
                else
                    value = $"{gameRoot}/{value}";

                if (value.EndsWith(".vpk"))
                {
                    vpkPaths.Add(value);
                    continue;
                }

                if (value.EndsWith("/*"))
                    value = value.Replace("/*", "");
                else
                    continue;

                if (Directory.Exists(value))
                {
                    foreach (var file in Directory.GetFiles(value))
                    {
                        if (!file.EndsWith(".vpk"))
                            continue;

                        vpkPaths.Add(file);
                    }
                }
            }

            foreach (string path in vpkPaths)
            {
                string localPath = path
                    .Replace(".vpk", "")
                    .Replace('\\', '/');

                Console.WriteLine($"\tMounting {localPath}...");
                var vpk = new VPKFile(localPath);

                if (!vpk.Mounted)
                    continue;

                string name = vpk.ToString();
                Mounts.Add(name, vpk);
            }

            Console.WriteLine("Ready!");
        }

        private string GetRouting(string path)
        {
            path = Program.CleanPath(path);

            if (Routing.TryGetValue(path, out string route))
                return route;

            foreach (string name in Mounts.Keys)
            {
                VPKFile vpk = Mounts[name];

                if (vpk.HasFile(path))
                {
                    route = name;
                    break;
                }
            }

            Routing[path] = route;

            if (route == null)
            {
                string fullPath = Path.Combine(GameDir, path);

                if (File.Exists(fullPath))
                {
                    route = fullPath;
                    Routing[path] = route;
                }
            }
            
            return route;
        }

        public bool HasFile(string path)
        {
            var route = GetRouting(path);

            if (route == null)
                return false;

            if (Mounts.ContainsKey(route))
                return true;

            if (File.Exists(route))
                return true;

            return false;
        }

        public Stream OpenRead(string path)
        {
            var route = GetRouting(path);

            if (route == null)
                throw new FileNotFoundException("Couldn't find file:", path);

            Console.WriteLine($"Opening {path}");

            if (Mounts.TryGetValue(route, out VPKFile vpk))
                return vpk.OpenRead(path);

            if (File.Exists(route))
                return File.OpenRead(route);

            // Clear invalid route.
            Routing.Remove(path);

            // Try fetching again.
            return OpenRead(path);
        }

        public static bool HasFile(string path, GameMount game = null)
        {
            game = game ?? Program.GameMount;

            if (game != null)
                return game.HasFile(path);

            return File.Exists(path);
        }

        public static Stream OpenRead(string path, GameMount game = null)
        {
            game = game ?? Program.GameMount;

            if (game != null)
                return game.OpenRead(path);

            Console.WriteLine($"Opening {path}");
            return File.OpenRead(path);
        }

        public static string GetGameName(GameMount game = null)
        {
            return (game ?? Program.GameMount)?.GameName;
        }
    }
}
