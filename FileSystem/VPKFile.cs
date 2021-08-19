using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Source2Roblox.FileSystem
{
    public class VPKFile : IEnumerable<VPKEntry>, IDisposable
    {
        public readonly FileInfo BasePath;
        public readonly VPKDirectory Root;

        private Dictionary<string, byte[]> binaries;
        private Dictionary<int, FileStream> buckets;

        private bool disposed;
        
        public IReadOnlyDictionary<string, VPKEntry> Entries => Root.Entries;
        public IEnumerable<string> Files => Entries.Keys.AsEnumerable();
        public override string ToString() => BasePath.Name;

        public VPKFile(string basePath)
        {
            string dir = $"{basePath}_dir.vpk";
            BasePath = new FileInfo(basePath);

            using (var stream = File.OpenRead(dir))
            using (var reader = new BinaryReader(stream))
                Root = new VPKDirectory(reader);

            buckets = new Dictionary<int, FileStream>();
            binaries = new Dictionary<string, byte[]>();
        }

        public bool HasFile(string path)
        {
            path = Program.CleanPath(path);

            if (binaries.Keys.Contains(path))
                return true;

            if (Root.Entries.Keys.Contains(path))
                return true;

            return false;
        }

        public MemoryStream OpenRead(string path)
        {
            path = Program.CleanPath(path);

            if (!binaries.TryGetValue(path, out byte[] buffer))
            {
                if (!Root.Entries.TryGetValue(path, out VPKEntry entry))
                    throw new FileNotFoundException("File not found in VPK:", path);

                string basePath = BasePath.FullName;
                int bucketId = entry.Index;

                if (!buckets.TryGetValue(bucketId, out FileStream bucket) || !bucket.CanRead)
                {
                    string bucketPath = basePath + string.Format("_{0,3:D3}.vpk", bucketId);
                    bucket = File.OpenRead(bucketPath);
                    buckets[bucketId] = bucket;
                }

                lock (bucket)
                {
                    bucket.Position = entry.Offset;

                    using (var reader = new BinaryReader(bucket, Encoding.UTF8, true))
                        buffer = reader.ReadBytes((int)entry.Size);

                    binaries[path] = buffer;
                }
            }

            return new MemoryStream(buffer);
        }

        public IEnumerator<VPKEntry> GetEnumerator()
        {
            return Entries.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Entries.Values.GetEnumerator();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                foreach (int bucketId in buckets.Keys)
                {
                    var file = buckets[bucketId];
                    file.Dispose();
                }

                buckets.Clear();
                buckets = null;

                binaries.Clear();
                binaries = null;
            }

            disposed = true;
        }
    }
}
