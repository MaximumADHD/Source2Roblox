namespace Source2Roblox.World
{
    public enum LumpType
    {
        Entities,
        Planes,
        TexData,
        Vertices,
        Visibility,
        TreeNodes,
        TexInfo,
        Faces,
        Lighting,
        Occlusion,
        Leaves,
        FaceIds,
        Edges,
        SurfEdges,
        Models,
        WorldLights,
        LeafFaces,
        LeafBrushes,
        Brushes,
        BrushSides,
        Areas,
        AreaPortals,
        Unused0,
        Unused1,
        Unused2,
        Unused3,
        Displacements,
        OriginalFaces,
        PhysDisp,
        PhysCollide,
        VertNormals,
        VertNormalIndices,
        DispLightmapAlphas,
        DispVerts,
        DispLightmapSamplePositions,
        GameLump,
        LeafWaterData,
        Primitives,
        PrimVerts,
        PrimIndices,
        PakFile,
        ClipPortalVerts,
        CubeMaps,
        TexDataStringData,
        TexDataStringTable,
        Overlays,
        LeafMinDistToWater,
        FaceMacroTextureInfo,
        DispTris,
        PhysCollideSurface,
        WaterOverlays,
        LeafAmbientIndexHDR,
        LeafAmbientIndex,
        LightingHDR,
        WorldLightsHDR,
        LeafAmbientLightingHDR,
        LeafAmbientLighting,
        XZipPakFile,
        FacesHDR,
        MapFlags,
        OverlayFades,
        OverlaySystemLevels,
        PhysLevel,
        DispMultiblend
    }

    public class Lump
    {
        public LumpType Type;

        public int Offset;
        public int Length;

        public int Version;
        public int Uncompressed;

        public override string ToString()
        {
            return $"{Type}";
        }
    }
}
