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
			public int3 Current => g;
			object IEnumerator.Current => g;

			private bool isFirst;

			private int3 p0, p1, g, s, err, serr, derr;

			public LineEnumerator(int3 p0, int3 p1)
			{
				this.p0 = p0;
				this.p1 = p1;

				s.x = p1.x > p0.x ? 1 : (p1.x < p0.x ? -1 : 0);
				s.y = p1.y > p0.y ? 1 : (p1.y < p0.y ? -1 : 0);
				s.z = p1.z > p0.z ? 1 : (p1.z < p0.z ? -1 : 0);

				//Error is normalized to vx * vy * vz so we only have to multiply up
				g.x = (p1.x == p0.x ? 1 : p1.x - p0.x) * (p1.y == p0.y ? 1 : p1.y - p0.y);
				g.y = (p1.x == p0.x ? 1 : p1.x - p0.x) * (p1.z == p0.z ? 1 : p1.z - p0.z);
				g.z = (p1.y == p0.y ? 1 : p1.y - p0.y) * (p1.z == p0.z ? 1 : p1.z - p0.z);

				//Error from the next plane accumulators, scaled up by vx*vy*vz
				// gx0 + vx * rx == gxp
				// vx * rx == gxp - gx0
				// rx == (gxp - gx0) / vx
				serr.x = p1.x > p0.x ? g.z : 0;
				serr.y = p1.y > p0.y ? g.y : 0;
				serr.z = p1.z > p0.z ? g.x : 0;

				derr.x = s.x * g.z;
				derr.y = s.y * g.y;
				derr.z = s.z * g.x;

				g = p0;
				err = serr;
				isFirst = true;
			}

			public bool MoveNext()
			{
				if(isFirst)
				{
					isFirst = false;
					return true;
				}

				if(g.x == p1.x && g.y == p1.y && g.z == p1.z) return false;

				//Which plane do we cross first?
				var xr = Math.Abs(err.x);
				var yr = Math.Abs(err.y);
				var zr = Math.Abs(err.z);

				if(s.x != 0 && (s.y == 0 || xr < yr) && (s.z == 0 || xr < zr))
				{
					g.x += s.x;
					err.x += derr.x;
				}
				else if(s.y != 0 && (s.z == 0 || yr < zr))
				{
					g.y += s.y;
					err.y += derr.y;
				}
				else if(s.z != 0)
				{
					g.z += s.z;
					err.z += derr.z;
				}
				return true;
			}

			public void Reset()
			{
				g = p0;
				err = serr;
				isFirst = true;
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