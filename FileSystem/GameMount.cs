using RobloxFiles.DataTypes;
using System.Collections.Generic;
using System.IO;

namespace Source2Roblox.FileSystem
{
    public class GameMount
    {
        public readonly string GameDir;
        public IReadOnlyDictionary<string, VPKFile> Mounts => mounts;

        private readonly Dictionary<string, string> routing;
        private readonly Dictionary<string, VPKFile> mounts;
        
        public GameMount(string gameDir)
        {
            GameDir = gameDir;

            mounts = new Dictionary<string, VPKFile>();
            routing = new Dictionary<string, string>();

            foreach (string file in Directory.GetFiles(gameDir))
            {
                var info = new FileInfo(file);

                if (info.Extension != ".vpk")
                    continue;

                string path = info.FullName;

                if (path.EndsWith("_dir.vpk"))
                    path = path.Replace("_dir.vpk", "");
                else
                    continue;

                var vpk = new VPKFile(path);
                string name = vpk.ToString();

                mounts.Add(name, vpk);
            }
        }

        private Optional<string> GetRouting(string path)
        {
            if (routing.TryGetValue(path, out string route))
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

            routing[path] = route;

            if (route == null)
            {
                string fullPath = Path.Combine(GameDir, path);

                if (File.Exists(fullPath))
                {
                    route = fullPath;
                    routing[path] = route;
                }
            }
            
            return route;
        }

        public bool HasFile(string path)
        {
            var route = GetRouting(path);

            if (route == null)
                return false;

            if (mounts.ContainsKey(route))
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
            routing.Remove(path);

            // Try fetching again.
            return OpenRead(path);
        }

        public static Stream OpenRead(string path, GameMount game = null)
        {
            game = game ?? Program.GameMount;

            if (game != null)
                return game.OpenRead(path);

            return File.OpenRead(path);
        }
    }
}
