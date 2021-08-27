using System.Collections.Generic;
using System.Linq;
using RobloxFiles.DataTypes;

namespace Source2Roblox.Octree
{
    public class Octree<T>
    {
        private readonly Dictionary<int, List<OctreeRegion<T>>> RegionHashMap;
        private readonly Vector3 MaxRegionSize;
        private readonly int MaxDepth;

        public Octree()
        {
            RegionHashMap = new Dictionary<int, List<OctreeRegion<T>>>();
            MaxRegionSize = new Vector3(512, 512, 512);
            MaxDepth = 4;
        }

        public void ClearNodes()
        {
            RegionHashMap.Clear();
        }

        public List<OctreeNode<T>> GetAllNodes()
        {
            var options = RegionHashMap.Values
                .SelectMany(regionList => regionList)
                .SelectMany(region => region.Nodes)
                .ToList();

            return options;
        }

        public OctreeNode<T> CreateNode(Vector3 focus, T value)
        {
            return new OctreeNode<T>(this, value) { Position = focus };
        }

        public List<T> RadiusSearch(Vector3 pos, float radius)
        {
            var diameter = MaxRegionSize.X;
            var objectsFound = new List<T>();
            var searchRadiusSquared = OctreeRegion<T>.GetSearchRadiusSquared(radius, diameter, 1E-9F);
            
            foreach (var regionList in RegionHashMap.Values)
            {
                foreach (var region in regionList)
                {
                    var regionPos = region.Position;
                    var offset = pos - regionPos;

                    var dist = offset.Magnitude;
                    var dist2 = dist * dist;

                    if (dist2 > searchRadiusSquared)
                        continue;

                    region.GetNeighborsWithinRadius(radius, pos, ref objectsFound, MaxDepth);
                }
            }

            return objectsFound;
        }

        public OctreeRegion<T> GetOrCreateLowestSubRegion(Vector3 pos)
        {
            var region = GetOrCreateRegion(pos);
            return region.GetOrCreateSubRegionAtDepth(pos, MaxDepth);
        }

        public OctreeRegion<T> GetRegion(Vector3 pos)
        {
            return OctreeRegion<T>.FindRegion(RegionHashMap, MaxRegionSize, pos);
        }

        private OctreeRegion<T> GetOrCreateRegion(Vector3 pos)
        {
            return OctreeRegion<T>.GetOrCreateRegion(RegionHashMap, MaxRegionSize, pos);
        }
    }
}
