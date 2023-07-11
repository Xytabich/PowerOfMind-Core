using PowerOfMind.Systems.RenderBatching;
using System;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Systems.ChunkBatchers.GroupBatch
{
	public abstract class WireBuilder<TVertex> : IBlockGroupBuilder where TVertex : unmanaged
	{
		private static readonly float faceUv = (float)Math.Sqrt(0.5);
		private static readonly float3[] segmentPoses = new float3[] {
			new float3(0, 0.5f, 0), new float3(0.5f, 0, 0), new float3(0, -0.5f, 0), new float3(-0.5f, 0, 0), new float3(0, 0.5f, 0),
			new float3(0, 0.5f, 1), new float3(0.5f, 0, 1), new float3(0, -0.5f, 1), new float3(-0.5f, 0, 1), new float3(0, 0.5f, 1)
		};
		private static readonly float2[] segmentUvs = new float2[] {
			new float2(0, 0), new float2(0, faceUv), new float2(0, faceUv * 2f), new float2(0, faceUv * 3f), new float2(0, faceUv * 4f),
			new float2(1, 0), new float2(1, faceUv), new float2(1, faceUv * 2f), new float2(1, faceUv * 3f), new float2(1, faceUv * 4f)
		};
		private static readonly int[] segmentIndices = new int[] {
			0, 5, 6, 1, 0, 6,
			1, 6, 7, 2, 1, 7,
			2, 7, 8, 3, 2, 8,
			3, 8, 9, 4, 3, 9
		};

		protected abstract float SegmentLength { get; }
		protected abstract float SegmentWidth { get; }

		protected readonly ICoreClientAPI api;
		protected internal readonly int3 fromBlock, toBlock;
		protected readonly float3 fromOffset, toOffset;

		public WireBuilder(ICoreClientAPI api, int3 fromBlock, int3 toBlock, float3 fromOffset, float3 toOffset)
		{
			this.api = api;
			this.fromBlock = fromBlock;
			this.toBlock = toBlock;
			this.fromOffset = fromOffset;
			this.toOffset = toOffset;
		}

		protected abstract void InitVertex(ref TVertex vertex, float3 position, float2 uv, int color);
		protected abstract void AddMesh(TVertex[] vertices, int[] indices, IBatchBuildContext batcher);

		unsafe void IBlockGroupBuilder.BuildChunk(int3 chunkIndex, IBlockAccessor blockAccessor, BlockLightUtil lightUtil, IBatchBuildContext batcher)
		{
			var chunkSize = api.World.BlockAccessor.ChunkSize;
			var chunkOrigin = chunkIndex * chunkSize;
			var rayPos = fromBlock - chunkOrigin + fromOffset;
			var rayDir = (toBlock - chunkOrigin + toOffset) - rayPos;
			BatchUtil.RayCubeIntersection(rayPos, rayDir, chunkSize, out float tMin, out float tMax);
			tMin = Math.Max(0, tMin);
			tMax = Math.Min(1, tMax);

			var rotation = quaternion.LookRotationSafe(rayDir, new float3(0, 1, 0));

			float rayLen = math.length(rayDir);
			float overallLen = tMin * rayLen;
			float lenLeft = (tMax - tMin) * rayLen;
			rayPos += rayDir * tMin;
			rayDir /= rayLen;
			float maxYOffset = (1f - Math.Abs(math.dot(rayDir, new float3(0, 1, 0)))) * Math.Min(rayLen * 0.05f, 0.5f);

			float segmentWidth = SegmentWidth;
			float segmentLength = SegmentLength;
			int segmentsPerUnit = (int)(1f / segmentLength);
			int segmentCount = (int)Math.Ceiling(lenLeft / segmentLength);
			var vertices = new TVertex[segmentCount * 10];
			var triangles = new int[segmentCount * 24];
			int vertIndex = 0;
			int triIndex = 0;
			int unitSegmentsCounter = 0;
			while(lenLeft > 0f && segmentCount > 0)
			{
				float partLen = Math.Min(lenLeft, segmentLength);
				lightUtil.unmanaged.SetUpLightRGBs(lightUtil, chunkOrigin + (int3)(rayPos + rayDir * partLen * 0.5f));
				int color = lightUtil.unmanaged.GetAverageColor();

				int startVertex = vertIndex;
				for(int i = 0; i < 10; i++)
				{
					var tmpCoords = segmentPoses[i];
					tmpCoords.xy *= segmentWidth;
					tmpCoords.z *= partLen;
					float yOffset = CalcYOffset(overallLen + tmpCoords.z, rayLen, maxYOffset);
					tmpCoords = rayPos + math.rotate(rotation, tmpCoords);
					tmpCoords.y += yOffset;
					var uv = segmentUvs[i];
					uv.y *= segmentWidth;
					uv.x *= partLen;
					uv.x += unitSegmentsCounter * segmentLength;
					InitVertex(ref vertices[vertIndex], tmpCoords, uv, color);
					vertIndex++;
				}

				unitSegmentsCounter++;
				if(unitSegmentsCounter == segmentsPerUnit)
				{
					unitSegmentsCounter = 0;
				}

				for(int i = 0; i < 24; i++)
				{
					triangles[triIndex] = startVertex + segmentIndices[i];
					triIndex++;
				}

				lenLeft -= segmentLength;
				overallLen += segmentLength;
				rayPos += rayDir * segmentLength;
				segmentCount--;
			}

			AddMesh(vertices, triangles, batcher);
		}

		private static float CalcYOffset(float len, float fullLen, float maxOffset)
		{
			float t = len / fullLen;
			return -GameMath.Lerp(GameMath.Lerp(0, 1, t), GameMath.Lerp(1, 0, t), t) * maxOffset;
		}
	}

	public static class WireBuilderExtension
	{
		public static int AddWireBuilder<TVertex>(this BlockGroupBatcher batcher, WireBuilder<TVertex> builder)
			where TVertex : unmanaged
		{
			var enumerator = new BatchUtil.LineEnumerator(builder.fromBlock, builder.toBlock);
			return batcher.AddGroup(ref enumerator, builder);
		}
	}
}