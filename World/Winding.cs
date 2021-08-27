using RobloxFiles.DataTypes;
using RobloxFiles.Enums;
using Source2Roblox.World.Types;

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Source2Roblox.World
{
    public class Winding : List<Vector3>
    {
        private const byte MAX_POINTS_ON_WINDING = 128;
        private const float SPLIT_EPSILON = 0.01f;

        private const int MAX_COORD_INTEGER = 8192;
        private const int COORD_EXTENT = 2 * MAX_COORD_INTEGER;

        private const float SQRT_3 = 1.73205080757f;
        private const float MAX_TRACE_LENGTH = COORD_EXTENT * SQRT_3;

        private const byte SIDE_FRONT = 0;
        private const byte SIDE_BACK = 1;
        private const byte SIDE_ON = 2;

        public Winding(int maxPoints = 0) : base(maxPoints)
        {
            Debug.Assert(maxPoints <= MAX_POINTS_ON_WINDING);
        }

        public Winding(Plane plane)
        {
            var normal = plane.Normal;

            // find the major axis
            float max = 0;
            Axis? axis = null;

            float x = Math.Abs(normal.X),
                  y = Math.Abs(normal.Y),
                  z = Math.Abs(normal.Z);

            if (x > max)
            {
                max = x;
                axis = Axis.X;
            }

            if (y > max)
            {
                max = y;
                axis = Axis.Y;
            }

            if (z > max)
                axis = Axis.Z;

            if (axis == null)
                throw new Exception("No axis found!");

            var up = new Vector3(0, 0, 1);

            if (axis == Axis.Y)
                up = new Vector3(1, 0, 0);

            var scale = -up.Dot(normal);
            up = (up + normal * scale).Unit;

            var origin = normal * plane.Dist;
            var right = up.Cross(normal);

            up *= MAX_TRACE_LENGTH;
            right *= MAX_TRACE_LENGTH;

            // project a really big axis
            // aligned box onto the plane.

            Add(origin - right + up);
            Add(origin + right + up);
            Add(origin + right - up);
            Add(origin - right - up);
        }

        public void RemoveDuplicates(float fMinDist)
        {
            var dropList = new List<Vector3>();

            for (int i = 0; i < Count; i++)
            {
                for (int j = Count + 1; j < Count; j++)
                {
                    var edge = this[i] - this[j];

                    if (edge.Magnitude >= fMinDist)
                        continue;

                    dropList.Add(edge);
                }
            }

            dropList.ForEach(value => Remove(value));
        }

        public Winding Clip(Vector3 norm, float dist)
        {
            var dists  = new float[MAX_POINTS_ON_WINDING];
            var sides  = new int[MAX_POINTS_ON_WINDING];
            var counts = new int[3];

            for (int i = 0; i < Count; i++)
            {
                var point = this[i];
                var dot = point.Dot(norm);

                dot -= dist;
                dists[i] = dot;

                if (dot > SPLIT_EPSILON)
                    sides[i] = SIDE_FRONT;
                else if (dot < -SPLIT_EPSILON)
                    sides[i] = SIDE_BACK;
                else
                    sides[i] = SIDE_ON;

                var side = sides[i];
                counts[side] += 1;
            }

            var noFronts = (counts[SIDE_FRONT] == 0);
            var noBacks  = (counts[SIDE_BACK]  == 0);

            sides[Count] = sides[0];
            dists[Count] = dists[0];

            if (noFronts && noBacks)
                return this;
            else if (noFronts)
                return null;
            else if (noBacks)
                return this;

            var maxPoints = Count + 4;
            var clip = new Winding(maxPoints);

            for (int i = 0; i < Count; i++)
            {
                var p1 = this[i];

                if (sides[i] == SIDE_FRONT || sides[i] == SIDE_ON)
                {
                    clip.Add(p1);

                    if (sides[i] == SIDE_ON)
                    {
                        continue;
                    }
                }

                if (sides[i + 1] == SIDE_ON || sides[i + 1] == sides[i])
                    continue;

                // generate a split point
                var dot = dists[i] / (dists[i] - dists[i + 1]);
                var p2 = this[(i + 1) % Count];

                var mid = p1.Lerp(p2, dot);
                clip.Add(mid);
            }

            if (clip.Count > maxPoints)
                throw new Exception("points exceeded estimate");

            return clip;
        }
    }
}
