using System.Collections.Generic;
using System.IO;

namespace Source2Roblox.FileSystem
{
    public class GameMount
    {
        public readonly string GameDir;
        public readonly string GameName;

        private readonly Dictionary<string, string> Routing;
        private readonly Dictionary<string, VPKFile> Mounts;

        public GameMount(string gameDir)
        {
            var dirInfo = new DirectoryInfo(gameDir);
            GameName = dirInfo.Name;
            GameDir = gameDir;
            
            Mounts = new Dictionary<string, VPKFile>();
            Routing = new Dictionary<string, string>();

            foreach (string file in Directory.GetFiles(gameDir))
            {
                var fileInfo = new FileInfo(file);

                if (fileInfo.Extension != ".vpk")
                    continue;

                string path = fileInfo.FullName;

                if (path.EndsWith("_dir.vpk"))
                    path = path.Replace("_dir.vpk", "");
                else
                    continue;

                var vpk = new VPKFile(path);
                string name = vpk.ToString();

                Mounts.Add(name, vpk);
            }
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

            return File.OpenRead(path);
        }

        public static string GetGameName(GameMount game = null)
        {
            return (game ?? Program.GameMount)?.GameName;
        }
    }
}
