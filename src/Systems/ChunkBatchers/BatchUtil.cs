using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

namespace PowerOfMind.Systems.ChunkBatchers
{
	public static class BatchUtil
	{
		public struct LineEnumerator : IEnumerator<int3>
		{
			public int3 Current => current;
			object IEnumerator.Current => current;

			private int3 p0, current;
			private float3 step;
			private int index, stepCount;

			public LineEnumerator(int3 p0, int3 p1)
			{
				this.p0 = p0;
				stepCount = Math.Max(Math.Max(Math.Abs(p0.x - p1.x), Math.Abs(p0.y - p1.y)), Math.Abs(p0.z - p1.z));
				if(stepCount > 0) step = (float3)(p1 - p0) / (float)stepCount;
				else step = default;
				index = 0;
			}

			public bool MoveNext()
			{
				if(index > stepCount) return false;

				current = p0 + (int3)(index * step);
				index++;

				return true;
			}

			public void Reset()
			{
				index = 0;
			}

			public void Dispose() { }
		}

		public static void RayCubeIntersection(float3 origin, float3 dir, float size, out float tmin, out float tmax)
		{
			tmin = float.NegativeInfinity;
			tmax = float.PositiveInfinity;

			float t0, t1;

			if(dir.y != 0.0)
			{
				t0 = (-origin.y) / dir.y;
				t1 = (size - origin.y) / dir.y;

				tmin = Math.Max(tmin, Math.Min(t0, t1));
				tmax = Math.Min(tmax, Math.Max(t0, t1));
			}

			if(dir.x != 0.0)
			{
				t0 = (-origin.x) / dir.x;
				t1 = (size - origin.x) / dir.x;

				tmin = Math.Max(tmin, Math.Min(t0, t1));
				tmax = Math.Min(tmax, Math.Max(t0, t1));
			}

			if(dir.z != 0.0)
			{
				t0 = (-origin.z) / dir.z;
				t1 = (size - origin.z) / dir.z;

				tmin = Math.Max(tmin, Math.Min(t0, t1));
				tmax = Math.Min(tmax, Math.Max(t0, t1));
			}
		}
	}
}