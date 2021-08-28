using System;
using System.Collections.Generic;
using System.Diagnostics;

using RobloxFiles.DataTypes;

namespace Source2Roblox.Octree
{
    public class OctreeRegion<T>
    {
        private const float EPSILON = 1e-6f;
        private const float SQRT_3_OVER_2 = 0.86602540378444f;

        private static readonly Vector3[] SUB_REGION_POSITION_OFFSET = new Vector3[8]
        {
            new Vector3(  0.25f,  0.25f, -0.25f ),
            new Vector3( -0.25f,  0.25f, -0.25f ),
            new Vector3(  0.25f,  0.25f,  0.25f ),
            new Vector3( -0.25f,  0.25f,  0.25f ),
            new Vector3(  0.25f, -0.25f, -0.25f ),
            new Vector3( -0.25f, -0.25f, -0.25f ),
            new Vector3(  0.25f, -0.25f,  0.25f ),
            new Vector3( -0.25f, -0.25f,  0.25f ),
        };

        public readonly Dictionary<int, OctreeRegion<T>> SubRegions;
        public readonly OctreeRegion<T> Parent;
        public readonly int ParentIndex = -1;
        
        public readonly Vector3 LowerBounds;
        public readonly Vector3 UpperBounds;

        public readonly Vector3 Position;
        public readonly Vector3 Size;

        public readonly int Depth = 1;
        public readonly HashSet<OctreeNode<T>> Nodes;
        
        public OctreeRegion(Vector3 pos, Vector3 size)
        {
            var halfSize = size / 2f;
            
            Size = size;
            Position = pos;

            LowerBounds = pos - halfSize;
            UpperBounds = pos + halfSize;

            Nodes = new HashSet<OctreeNode<T>>();
            SubRegions = new Dictionary<int, OctreeRegion<T>>();
        }

        public OctreeRegion(Vector3 pos, Vector3 size, OctreeRegion<T> parent, int parentIndex) : this(pos, size)
        {
            Debug.Assert(parentIndex > -1 && parentIndex < 8);
            Debug.Assert(parent != null);

            ParentIndex = parentIndex;
            Depth = parent.Depth + 1;
            Parent = parent;
        }

        public void AddNode(OctreeNode<T> node)
        {
            Nodes.Add(node);
            Parent?.AddNode(node);
        }

        public void MoveNode(OctreeRegion<T> target, OctreeNode<T> node)
        {
            Debug.Assert(Depth == target.Depth);
            Debug.Assert(this != target);

            // remove from current
            Nodes.Remove(node);

            // remove subregion!
            if (Nodes.Count <= 0 && ParentIndex >= 0)
            {
                Debug.Assert(Parent != null);
                Debug.Assert(Parent.SubRegions[ParentIndex] == this);
                Parent.SubRegions[ParentIndex] = null;
            }

            // add to new
            target.Nodes.Add(node);
            Parent?.MoveNode(target.Parent, node);
        }

        public void RemoveNode(OctreeNode<T> node)
        {
            Nodes.Remove(node);

            if (Nodes.Count <= 0 && ParentIndex >= 0)
            {
                Debug.Assert(Parent != null);
                Debug.Assert(Parent.SubRegions[ParentIndex] == this);
                Parent.SubRegions[ParentIndex] = null;
            }

            Parent?.RemoveNode(node);
        }

        public static float GetSearchRadiusSquared(float radius, float diameter, float epsilon = EPSILON)
        {
            var searchRadius = radius + (SQRT_3_OVER_2 * diameter);
            return searchRadius * searchRadius + epsilon;
        }

        public void GetNeighborsWithinRadius(float radius, Vector3 pos, ref List<T> objectsFound, int maxDepth)
        {
            float childDiameter = Size.X / 2f;
            float radiusSquared = radius * radius;

            float searchRadiusSquared = GetSearchRadiusSquared(radius, childDiameter);
            var subRegions = SubRegions.Values;

            foreach (var childRegion in subRegions)
            {
                var childPos = childRegion.Position;
                var offset = pos - childPos;

                var dist = offset.Magnitude;
                var dist2 = dist * dist;

                if (dist2 > searchRadiusSquared)
                    continue;

                if (childRegion.Depth != maxDepth)
                {
                    childRegion.GetNeighborsWithinRadius(radius, pos, ref objectsFound, maxDepth);
                    return;
                }

                foreach (var node in childRegion.Nodes)
                {
                    var nodePos = node.Position;
                    var nodeOffset = pos - nodePos;

                    var nodeDist = nodeOffset.Magnitude;
                    var nodeDist2 = nodeDist * nodeDist;

                    if (nodeDist2 > radiusSquared)
                        continue;

                    objectsFound.Add(node.Object);
                }
            }
        }

        public OctreeRegion<T> GetOrCreateSubRegionAtDepth(Vector3 pos, int maxDepth)
        {
            var current = this;

            for (int i = Depth; i < maxDepth; i++)
            {
                int index = current.GetSubRegionIndex(pos);
                
                if (!current.SubRegions.TryGetValue(index, out var next))
                {
                    next = current.CreateSubRegion(index);
                    current.SubRegions[index] = next;
                }

                current = next;
            }

            return current;
        }

        public OctreeRegion<T> CreateSubRegion(int parentIndex)
        {
            var multiplier = SUB_REGION_POSITION_OFFSET[parentIndex];
            var pos = Position + (multiplier * Size);
            var size = Size / 2f;

            return new OctreeRegion<T>(pos, size, this, parentIndex);
        }

        public bool InRegionBounds(Vector3 pos)
        {
            if (pos.X > UpperBounds.X || pos.X < LowerBounds.X)
                return false;

            if (pos.Y > UpperBounds.Y || pos.Y < LowerBounds.Y)
                return false;

            if (pos.Z > UpperBounds.Z || pos.Z < LowerBounds.Z)
                return false;

            return true;
        }

        public int GetSubRegionIndex(Vector3 pos)
        {
            var index = (pos.X > Position.X) ? 0 : 1;

            if (pos.Y <= Position.Y)
                index += 4;

            if (pos.Z >= Position.Z)
                index += 2;

            return index;
        }

        public static int GetTopLevelRegionHash(Vector3 cell)
        {
            int cx = (int)cell.X,
                cy = (int)cell.Y,
                cz = (int)cell.Z;

            return cx * 73856093 + cy * 19351301 + cz * 83492791;
        }

        public static Vector3 GetTopLevelRegionCell(Vector3 size, Vector3 pos)
        {
            float x = (float)Math.Round(pos.X / size.X),
                  y = (float)Math.Round(pos.Y / size.Y),
                  z = (float)Math.Round(pos.Z / size.Z);

            return new Vector3(x, y, z);
        }

        public static OctreeRegion<T> FindRegion(Dictionary<int, List<OctreeRegion<T>>> regionHashMap, Vector3 size, Vector3 pos)
        {
            var cell = GetTopLevelRegionCell(size, pos);
            int hash = GetTopLevelRegionHash(cell);

            if (!regionHashMap.TryGetValue(hash, out var regionList))
                return null;

            var regionPos = size * cell;

            foreach (var region in regionList)
                if (region.Position == regionPos)
                    return region;

            return null;
        }

        public static OctreeRegion<T> GetOrCreateRegion(Dictionary<int, List<OctreeRegion<T>>> regionHashMap, Vector3 size, Vector3 pos)
        {
            var cell = GetTopLevelRegionCell(size, pos);
            int hash = GetTopLevelRegionHash(cell);

            if (!regionHashMap.TryGetValue(hash, out var regionList))
            {
                regionList = new List<OctreeRegion<T>>();
                regionHashMap[hash] = regionList;
            }
            
            var regionPos = size * cell;

            foreach (var region in regionList)
                if (region.Position == regionPos)
                    return region;

            var newRegion = new OctreeRegion<T>(regionPos, size);
            regionList.Add(newRegion);

            return newRegion;
        }
    }
}
