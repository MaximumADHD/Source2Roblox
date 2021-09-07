using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Source2Roblox.FileSystem
{
    public class VPKFile : IDisposable
    {
        public readonly FileInfo BasePath;
        public readonly VPKDirectory Root;
        public readonly bool Mounted = false;

        private Dictionary<string, byte[]> Binaries = new Dictionary<string, byte[]>();
        private Dictionary<int, FileStream> Buckets = new Dictionary<int, FileStream>();

        private bool Disposed;
        public override string ToString() => BasePath.Name;

        public VPKFile(string basePath)
        {
            string dir = $"{basePath}_dir.vpk";
            BasePath = new FileInfo(basePath);

            if (File.Exists(dir))
            {
                using (var stream = File.OpenRead(dir))
                using (var reader = new BinaryReader(stream))
                    Root = new VPKDirectory(reader);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\tMounted!");

                Mounted = true;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\tCould not find file!");
            }

            Console.ForegroundColor = ConsoleColor.Gray;
        }
        
        public VPKFile(ZipArchive archive)
        {
            Root = new VPKDirectory(archive);
        }

        public bool HasFile(string path)
        {
            path = Program.CleanPath(path);

            if (Binaries.Keys.Contains(path))
                return true;

            var entry = Root.FindEntry(path);
            return (entry != null);
        }

        public MemoryStream OpenRead(string path)
        {
            path = Program.CleanPath(path);

            if (!Binaries.TryGetValue(path, out byte[] buffer))
            {
                var entry = Root.FindEntry(path);

                if (entry == null)
                    throw new FileNotFoundException("File not found in VPK:", path);

                int bucketId = entry.Index;

                if (bucketId == 0x7FFF)
                {
                    var embed = entry.EmbeddedContent ?? Array.Empty<byte>();
                    return new MemoryStream(embed);
                }

                if (!Buckets.TryGetValue(bucketId, out FileStream bucket) || !bucket.CanRead)
                {
                    string bucketPath = BasePath.FullName + string.Format("_{0,3:D3}.vpk", bucketId);
                    bucket = File.OpenRead(bucketPath);
                    Buckets[bucketId] = bucket;
                }

                lock (bucket)
                {
                    bucket.Position = entry.Offset;

                    using (var reader = new BinaryReader(bucket, Encoding.UTF8, true))
                        buffer = reader.ReadBytes((int)entry.Size);

                    Binaries[path] = buffer;
                }
            }

            return new MemoryStream(buffer);
        }

        public void Dispose()
        {
            if (!Disposed)
            {
                foreach (int bucketId in Buckets.Keys)
                {
                    var file = Buckets[bucketId];
                    file.Dispose();
                }

                Buckets.Clear();
                Buckets = null;

                Binaries.Clear();
                Binaries = null;
            }

            Disposed = true;
        }
    }
}
