namespace Source2Roblox.Models
{
    public static class ModelConstants
    {
        public const int MaxLODs = 8;
        public const int MaxSkins = 32;
        public const int MaxBones = 128;
        public const int MaxVerts = 65536;

        public const int MaxFlexCtrls = 96;
        public const int MaxFlexDescs = 1024;
        public const int MaxFlexVerts = 10000;
        public const int MaxTriangles = 65536;

        public const int MaxBoneCtrls = 4;
        public const int MaxAnimBlocks = 256;

        public const int MaxBonesPerVert = 3;
        public const int MaxBonesPerStrip = 512;
        public const int MaxBonesPerTriangle = MaxBonesPerVert * 3;

        public const int MinModelVersion = 44;
        public const int MaxModelVersion = 49;
    }
}
