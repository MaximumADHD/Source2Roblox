using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Source2Roblox.FileSystem
{
    public class VPKDirectory : IEnumerable<VPKEntry>
    {
        public IReadOnlyDictionary<string, VPKEntry> Entries => EntriesImpl;
        private readonly Dictionary<string, VPKEntry> EntriesImpl = new Dictionary<string, VPKEntry>();

        public readonly uint Version;
        public readonly uint DirectorySize;

        public readonly uint EmbeddedChunkSize;
        public readonly uint ChunkHashesSize;
        public readonly uint SelfHashesSize;
        public readonly uint SignatureSize;

        private void ReadFiles(BinaryReader reader, string ext, string dir)
        {
            while (true)
            {
                string fileName = reader.ReadString(null);

                if (string.IsNullOrEmpty(fileName))
                    break;

                string dirPrefix = dir.Trim() == "" ? "" : $"{dir}/";

                string path = $"{dirPrefix}{fileName}.{ext}"
                    .ToLowerInvariant()
                    .Replace('\\', '/');

                var entry = new VPKEntry(path, reader);
                EntriesImpl.Add(path, entry);
            }
        }

        private void ReadDirectories(BinaryReader reader, string ext)
        {
            while (true)
            {
                string dir = reader.ReadString(null);

                if (dir.Length == 0)
                    break;

                ReadFiles(reader, ext, dir);
            }
        }

        public VPKDirectory(BinaryReader reader)
        {
            uint magic = reader.ReadUInt32();
            Debug.Assert(magic == 0x55AA1234, "Not a VPK file!");

            Version = reader.ReadUInt32();
            DirectorySize = reader.ReadUInt32();

            if (Version == 2)
            {
                EmbeddedChunkSize = reader.ReadUInt32();
                ChunkHashesSize = reader.ReadUInt32();
                SelfHashesSize = reader.ReadUInt32();
                SignatureSize = reader.ReadUInt32();
            }
            else
            {
                Debug.Assert(Version == 1, "Unsupported VPK version!");
            }

            while (true)
            {
                string ext = reader.ReadString(null);

                if (ext.Length == 0)
                    break;

                ReadDirectories(reader, ext);
            }
        }

        public IEnumerator<VPKEntry> GetEnumerator()
        {
            return Entries.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Entries.Values.GetEnumerator();
        }
    }
}
