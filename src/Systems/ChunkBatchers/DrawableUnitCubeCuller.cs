using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace PowerOfMind.Systems.ChunkBatchers
{
	public class DrawableUnitCubeCuller : IDrawableData
	{
		EnumDrawMode IDrawableInfo.DrawMode => EnumDrawMode.Triangles;
		uint IDrawableInfo.IndicesCount => indicesCount;
		uint IDrawableInfo.VerticesCount => verticesCount;
		int IDrawableInfo.VertexBuffersCount => original.VertexBuffersCount;

		private IDrawableData original;
		private uint indicesCount;
		private uint verticesCount;
		private uint[] indices = null;

		private uint[] culledIndices = null;
		private byte[] culledVerticesData = null;

		private MeshTopology topology;

		private uint posDataOffset;

		private bool cullBackfaces = false;
		private int cullSides = 0;

		public bool BuildData(IDrawableData original, int cullSides, bool cullBackfaces)
		{
			this.original = original;
			this.cullSides = cullSides;
			this.cullBackfaces = cullBackfaces;
			indicesCount = original.IndicesCount;
			verticesCount = original.VerticesCount;
			if(indices == null) indices = new uint[Math.Max(256, (int)original.IndicesCount)];
			else if(indices.Length < original.IndicesCount)
			{
				indices = new uint[((original.IndicesCount - 1) / indices.Length + 1) * indices.Length];
			}

			topology = MeshTopology.Generic;
			if((indicesCount / 6) * 6 == indicesCount && (indicesCount / 6) * 4 == verticesCount)
			{
				topology = MeshTopology.Quad;
			}
			else if((indicesCount / 3) * 3 == verticesCount)
			{
				topology = MeshTopology.Triangle;
			}

			var indData = original.GetIndicesData();
			if(indData.IsEmpty) return false;
			indData.Slice(0, (int)original.IndicesCount).CopyTo(indices);

			var vBuffCount = original.VertexBuffersCount;
			for(int i = 0; i < vBuffCount; i++)
			{
				var meta = original.GetVertexBufferMeta(i);
				if(meta.IsDynamic) continue;
				var attributes = meta.Declaration.Attributes;
				for(int j = 0; j < attributes.Length; j++)
				{
					ref readonly var attr = ref attributes[j];
					if(attr.Alias == VertexAttributeAlias.POSITION && attr.Type == EnumShaderPrimitiveType.Float && attr.Size >= 3)
					{
						posDataOffset = attr.Offset;
						CullIndices(original.GetVerticesData(i), original.GetVertexBufferMeta(i).Stride);
						switch(topology)
						{
							case MeshTopology.Quad:
								verticesCount = (indicesCount / 6) * 4;
								break;
							case MeshTopology.Triangle:
								verticesCount = indicesCount;
								break;
						}
						return indicesCount > 0;
					}
				}
			}
			return false;
		}

		public void Clear()
		{
			original = null;
		}

		ReadOnlySpan<uint> IDrawableData.GetIndicesData()
		{
			if(topology == MeshTopology.Generic)
			{
				return indices;
			}
			else
			{
				return culledIndices;
			}
		}

		ReadOnlySpan<byte> IDrawableData.GetVerticesData(int bufferIndex)
		{
			if(verticesCount == original.VerticesCount)
			{
				return original.GetVerticesData(bufferIndex);
			}
			else
			{
				return CullVertices(original.GetVerticesData(bufferIndex), original.GetVertexBufferMeta(bufferIndex).Stride);
			}
		}

		IndicesMeta IDrawableInfo.GetIndicesMeta()
		{
			return new IndicesMeta(false);
		}

		VertexBufferMeta IDrawableInfo.GetVertexBufferMeta(int index)
		{
			return original.GetVertexBufferMeta(index);
		}

		private void EnsureVerticesDataBuffer(int size)
		{
			if(culledVerticesData == null || culledVerticesData.Length < size)
			{
				culledVerticesData = new byte[size];
			}
		}

		private unsafe ReadOnlySpan<byte> CullVertices(ReadOnlySpan<byte> data, int stride)
		{
			if(data.IsEmpty) return default;
			EnsureVerticesDataBuffer((int)verticesCount * stride);
			fixed(byte* dPtr = data)
			{
				fixed(byte* vPtr = culledVerticesData)
				{
					fixed(uint* indPtr = indices)
					{
						fixed(uint* cindPtr = culledIndices)
						{
							for(uint i = 0; i < indicesCount; i++)
							{
								Buffer.MemoryCopy(dPtr + indPtr[i] * stride, vPtr + cindPtr[i] * (uint)stride, stride, stride);
							}
						}
					}
				}
			}
			return culledVerticesData;
		}

		private unsafe void CullIndices(ReadOnlySpan<byte> data, int stride)
		{
			if(data.IsEmpty) return;
			fixed(byte* ptr = data)
			{
				CullIndicesImpl(ptr + posDataOffset, (uint)stride);
			}
		}

		private unsafe void CullIndicesImpl(byte* vertices, uint stride)
		{
			FaceCullHelper helper = default;
			helper.vertices = vertices;
			helper.stride = stride;
			helper.cullBackfaces = cullBackfaces;
			helper.visibleSides = ~cullSides;
			fixed(uint* ptr = indices)
			{
				//TODO: should be generic vertices culled too? i.e. add some Dictionary<uint, uint> origVertIndexToOutputIndex; & then only collect vertices from this list

				helper.indices = ptr;
				uint* moveTo = ptr;
				uint counter = 0;
				if(topology == MeshTopology.Quad)
				{
					for(uint i = 0; i < indicesCount; i += 6)
					{
						if(helper.IsVisible(i) || helper.IsVisible(i + 3))
						{
							*moveTo = ptr[i];
							moveTo++;
							*moveTo = ptr[i + 1];
							moveTo++;
							*moveTo = ptr[i + 2];
							moveTo++;
							*moveTo = ptr[i + 3];
							moveTo++;
							*moveTo = ptr[i + 4];
							moveTo++;
							*moveTo = ptr[i + 5];
							moveTo++;

							counter += 6;
						}
					}
					if(culledIndices == null || culledIndices.Length < counter)
					{
						culledIndices = new uint[counter];
					}
					uint vertIndex = 0;
					fixed(uint* cptr = culledIndices)
					{
						for(uint i = 0; i < counter; i += 6)
						{
							AddMappedQuad(cptr + i, ptr + i, vertIndex);
							vertIndex += 4;
						}
					}
				}
				else
				{
					for(uint i = 0; i < indicesCount; i += 3)
					{
						if(helper.IsVisible(i))
						{
							*moveTo = ptr[i];
							moveTo++;
							*moveTo = ptr[i + 1];
							moveTo++;
							*moveTo = ptr[i + 2];
							moveTo++;

							counter += 3;
						}
					}
					if(topology == MeshTopology.Triangle)
					{
						if(culledIndices == null || culledIndices.Length < counter)
						{
							culledIndices = new uint[counter];
						}
						for(uint i = 0; i < counter; i++)
						{
							culledIndices[i] = i;
						}
					}
				}
				indicesCount = counter;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static unsafe void AddMappedQuad(uint* outIndices, uint* indices, uint vertIndex)
		{
			*outIndices = vertIndex;
			outIndices++;
			*outIndices = vertIndex + 1;
			outIndices++;
			*outIndices = vertIndex + 2;
			outIndices++;

			if(indices[3] == indices[0])
			{
				*outIndices = vertIndex;
			}
			else if(indices[3] == indices[1])
			{
				*outIndices = vertIndex + 1;
			}
			else if(indices[3] == indices[2])
			{
				*outIndices = vertIndex + 2;
			}
			else
			{
				*outIndices = vertIndex + 3;
			}
			outIndices++;

			if(indices[4] == indices[0])
			{
				*outIndices = vertIndex;
			}
			else if(indices[4] == indices[1])
			{
				*outIndices = vertIndex + 1;
			}
			else if(indices[4] == indices[2])
			{
				*outIndices = vertIndex + 2;
			}
			else
			{
				*outIndices = vertIndex + 3;
			}
			outIndices++;

			if(indices[5] == indices[0])
			{
				*outIndices = vertIndex;
			}
			else if(indices[5] == indices[1])
			{
				*outIndices = vertIndex + 1;
			}
			else if(indices[5] == indices[2])
			{
				*outIndices = vertIndex + 2;
			}
			else
			{
				*outIndices = vertIndex + 3;
			}
			outIndices++;
		}

		private unsafe struct FaceCullHelper
		{
			public byte* vertices;
			public uint stride;
			public bool cullBackfaces;
			public int visibleSides;
			public uint* indices;

			private TriangleCalcUtil util;

			//locals:
			uint a, b, c;
			float4 plane;
			float3 p;
			public bool IsVisible(uint triIndex)
			{
				a = indices[triIndex];
				b = indices[triIndex + 1];
				c = indices[triIndex + 2];

				util.a = *(float3*)(vertices + a * stride);
				util.b = *(float3*)(vertices + b * stride);
				util.c = *(float3*)(vertices + c * stride);
				plane = new float4(math.cross(util.b - util.a, util.c - util.a), 0);

				if(!cullBackfaces || CubeBoundsHelper.TriInView(plane.xyz, visibleSides))
				{
					plane.xyz = math.normalize(plane.xyz);
					plane.w = -math.dot(util.a, plane.xyz);

					p = CubeBoundsHelper.cubeCenter - plane.xyz * (math.dot(plane.xyz, CubeBoundsHelper.cubeCenter) + plane.w);

					if(CubeBoundsHelper.PointInView(p, visibleSides))
					{
						if(util.ClosestPointOnTriangleToPoint(ref p) || CubeBoundsHelper.PointInView(p, visibleSides))
						{
							return true;
						}
					}
				}
				return false;
			}
		}

		[StructLayout(LayoutKind.Auto, Pack = 4)]
		private struct TriangleCalcUtil
		{
			public float3 a, b, c;

			private float3 ab, ac, ap, bp, cp;
			private float v, d1, d2, d3, d4, d5, d6;

			public bool ClosestPointOnTriangleToPoint(ref float3 p)
			{
				//https://github.com/StudioTechtrics/closestPointOnMesh/blob/master/closestPointOnMesh/Assets/KDTreeData.cs
				//Source: Real-Time Collision Detection by Christer Ericson
				//Reference: Page 136

				//Check if P in vertex region outside A
				ab = b - a;
				ac = c - a;
				ap = p - a;

				d1 = math.dot(ab, ap);
				d2 = math.dot(ac, ap);
				if(d1 <= 0.0f && d2 <= 0.0f)
				{
					p = a; //Barycentric coordinates (1,0,0)
					return false;
				}

				//Check if P in vertex region outside B
				float3 bp = p - b;
				d3 = math.dot(ab, bp);
				d4 = math.dot(ac, bp);
				if(d3 >= 0.0f && d4 <= d3)
				{
					p = b; //Barycentric coordinates (1,0,0)
					return false;
				}

				//Check if P in edge region of AB, if so return projection of P onto AB
				v = d1 * d4 - d3 * d2;
				if(v <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
				{
					v = d1 / (d1 - d3);
					p = (1 - v) * a + v * b; //Barycentric coordinates (1-v,v,0)
					return false;
				}

				//Check if P in vertex region outside C
				cp = p - c;
				d5 = math.dot(ab, cp);
				d6 = math.dot(ac, cp);
				if(d6 >= 0.0f && d5 <= d6)
				{
					p = c; //Barycentric coordinates (1,0,0)
					return false;
				}

				//Check if P in edge region of AC, if so return projection of P onto AC
				v = d5 * d2 - d1 * d6;
				if(v <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
				{
					v = d2 / (d2 - d6);
					p = (1 - v) * a + v * c; //Barycentric coordinates (1-w,0,w)
					return false;
				}

				//Check if P in edge region of BC, if so return projection of P onto BC
				v = d3 * d6 - d5 * d4;
				if(v <= 0.0f && d4 - d3 >= 0.0f && d5 - d6 >= 0.0f)
				{
					v = (d4 - d3) / (d4 - d3 + (d5 - d6));
					p = (1 - v) * b + v * c; //Barycentric coordinates (0,1-w,w)
					return false;
				}

				//P inside face region
				return true;
			}
		}

		private enum MeshTopology
		{
			Generic,
			Triangle,
			Quad
		}

		private static unsafe class CubeBoundsHelper
		{
			private const float EPSILON_MIN = -0.0001f;
			private const float EPSILON_MAX = 1.0001f;
			private const float CONE_MAX = 0.6f;

			private const int FLAG_NORTH = 1 << BlockFacing.indexNORTH;
			private const int FLAG_EAST = 1 << BlockFacing.indexEAST;
			private const int FLAG_SOUTH = 1 << BlockFacing.indexSOUTH;
			private const int FLAG_WEST = 1 << BlockFacing.indexWEST;
			private const int FLAG_UP = 1 << BlockFacing.indexUP;
			private const int FLAG_DOWN = 1 << BlockFacing.indexDOWN;

			public static readonly float3 cubeCenter = new float3(0.5f, 0.5f, 0.5f);

#pragma warning disable CS0169 // The field is never used
			private static Float6x3 sideNormals;
#pragma warning restore CS0169

			static CubeBoundsHelper()
			{
				fixed(float* sideNormals = CubeBoundsHelper.sideNormals.values)
				{
					foreach(var face in BlockFacing.ALLFACES)
					{
						int i = face.Index * 3;
						sideNormals[i] = face.Normalf.X;
						sideNormals[i + 1] = face.Normalf.Y;
						sideNormals[i + 2] = face.Normalf.Z;
					}
				}
			}

			public static bool PointInView(float3 v, int sidesMask)
			{
				if(v.x > EPSILON_MIN & v.x < EPSILON_MAX & v.y > EPSILON_MIN & v.y < EPSILON_MAX & v.z > EPSILON_MIN & v.z < EPSILON_MAX) return true;
				v = math.normalizesafe(v - cubeCenter);
				fixed(float* sideNormals = CubeBoundsHelper.sideNormals.values)
				{
					if((sidesMask & FLAG_NORTH) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexNORTH]) > CONE_MAX) return true;
					if((sidesMask & FLAG_EAST) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexEAST]) > CONE_MAX) return true;
					if((sidesMask & FLAG_SOUTH) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexSOUTH]) > CONE_MAX) return true;
					if((sidesMask & FLAG_WEST) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexWEST]) > CONE_MAX) return true;
					if((sidesMask & FLAG_UP) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexUP]) > CONE_MAX) return true;
					if((sidesMask & FLAG_DOWN) != 0 && math.dot(v, ((float3*)sideNormals)[BlockFacing.indexDOWN]) > CONE_MAX) return true;
				}
				return false;
			}

			public static bool TriInView(float3 triNormal, int sidesMask)
			{
				fixed(float* sideNormals = CubeBoundsHelper.sideNormals.values)
				{
					if((sidesMask & FLAG_NORTH) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexNORTH]) > math.EPSILON) return true;
					if((sidesMask & FLAG_EAST) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexEAST]) > math.EPSILON) return true;
					if((sidesMask & FLAG_SOUTH) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexSOUTH]) > math.EPSILON) return true;
					if((sidesMask & FLAG_WEST) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexWEST]) > math.EPSILON) return true;
					if((sidesMask & FLAG_UP) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexUP]) > math.EPSILON) return true;
					if((sidesMask & FLAG_DOWN) != 0 && math.dot(triNormal, ((float3*)sideNormals)[BlockFacing.indexDOWN]) > math.EPSILON) return true;
				}
				return false;
			}

			[StructLayout(LayoutKind.Auto, Pack = 4)]
			private struct Float6x3
			{
				public fixed float values[6 * 3];
			}
		}
	}
}