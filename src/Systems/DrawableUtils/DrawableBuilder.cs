using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Drawable;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vintagestory.API.Client;

namespace PowerOfMind.Systems.DrawableUtils
{
	public class DrawableBuilder<TVertex> where TVertex : unmanaged
	{
		public bool IsEmpty => totalVertices == 0;

		private readonly List<TVertex[]> vertexChunks = new List<TVertex[]>();
		private readonly List<uint[]> indexChunks = new List<uint[]>();

		private readonly int chunkSize;

		private uint totalVertices;
		private uint totalIndices;
		private int chunkVertexCounter;
		private int chunkIndexCounter;
		private int currentVertexChunk;
		private int currentIndexChunk;
		private TVertex[] currentVertexBuffer;
		private uint[] currentIndexBuffer;

		private DrawableData dataHelper = null;
		private int[] tmpVerticesOffset = null;

		public DrawableBuilder(int chunkSize)
		{
			this.chunkSize = chunkSize;

			indexChunks.Add(new uint[chunkSize]);
			vertexChunks.Add(new TVertex[chunkSize]);

			Clear();
		}

		public void AddVertex(in TVertex vertex)
		{
			if(chunkVertexCounter == chunkSize)
			{
				chunkVertexCounter = 0;
				currentVertexChunk++;
				if(currentVertexChunk < vertexChunks.Count)
				{
					currentVertexBuffer = vertexChunks[currentVertexChunk];
				}
				else
				{
					currentVertexBuffer = new TVertex[chunkSize];
					vertexChunks.Add(currentVertexBuffer);
				}
			}
			currentVertexBuffer[chunkVertexCounter] = vertex;
			chunkVertexCounter++;
			totalVertices++;
		}

		public void AddIndex(uint index)
		{
			if(chunkIndexCounter == chunkSize)
			{
				chunkIndexCounter = 0;
				currentIndexChunk++;
				if(currentIndexChunk < indexChunks.Count)
				{
					currentIndexBuffer = indexChunks[currentIndexChunk];
				}
				else
				{
					currentIndexBuffer = new uint[chunkSize];
					indexChunks.Add(currentIndexBuffer);
				}
			}
			currentIndexBuffer[chunkIndexCounter] = index;
			chunkIndexCounter++;
			totalIndices++;
		}

		public void Clear()
		{
			totalIndices = 0;
			currentIndexChunk = 0;
			chunkIndexCounter = 0;

			totalVertices = 0;
			currentVertexChunk = 0;
			chunkVertexCounter = 0;

			currentVertexBuffer = vertexChunks[0];
			currentIndexBuffer = indexChunks[0];
		}

		public IDrawableHandle UploadData(IRenderAPI rapi, EnumDrawMode drawMode, VertexDeclaration vertexDeclaration)
		{
			if(IsEmpty) return null;

			if(dataHelper == null) dataHelper = new DrawableData();
			dataHelper.verticesCount = totalVertices;
			dataHelper.indicesCount = totalIndices;
			dataHelper.vertexDeclaration = vertexDeclaration;
			dataHelper.vertices = currentVertexBuffer;
			dataHelper.indices = currentIndexBuffer;
			dataHelper.mode = drawMode;

			IDrawableHandle handle;
			if(currentIndexChunk + currentVertexChunk == 0)
			{
				handle = rapi.UploadDrawable(dataHelper);
			}
			else
			{
				if(currentIndexChunk != 0)
				{
					dataHelper.indices = null;
				}
				if(currentVertexChunk != 0)
				{
					dataHelper.vertices = null;
				}
				handle = rapi.UploadDrawable(dataHelper);

				UploadParts(rapi, handle);
			}

			dataHelper.Clear();
			return handle;
		}

		public void ReuploadData(IRenderAPI rapi, IDrawableHandle handle)
		{
			if(dataHelper == null) dataHelper = new DrawableData();
			dataHelper.verticesCount = totalVertices;
			dataHelper.indicesCount = totalIndices;
			dataHelper.vertexDeclaration = default;
			dataHelper.vertices = currentVertexBuffer;
			dataHelper.indices = currentIndexBuffer;
			dataHelper.mode = default;

			if(currentIndexChunk + currentVertexChunk == 0)
			{
				rapi.ReuploadDrawable(handle, dataHelper);
			}
			else
			{
				if(currentIndexChunk != 0)
				{
					dataHelper.indices = null;
				}
				if(currentVertexChunk != 0)
				{
					dataHelper.vertices = null;
				}
				rapi.ReuploadDrawable(handle, dataHelper);

				UploadParts(rapi, handle);
			}

			dataHelper.Clear();
		}

		public void ReuploadData(IRenderAPI rapi, IDrawableHandle handle, VertexDeclaration vertexDeclaration)
		{
			if(dataHelper == null) dataHelper = new DrawableData();
			dataHelper.verticesCount = totalVertices;
			dataHelper.indicesCount = totalIndices;
			dataHelper.vertexDeclaration = vertexDeclaration;
			dataHelper.vertices = currentVertexBuffer;
			dataHelper.indices = currentIndexBuffer;
			dataHelper.mode = default;

			if(currentIndexChunk + currentVertexChunk == 0)
			{
				rapi.ReuploadDrawable(handle, dataHelper, true);
			}
			else
			{
				if(currentIndexChunk != 0)
				{
					dataHelper.indices = null;
				}
				if(currentVertexChunk != 0)
				{
					dataHelper.vertices = null;
				}
				rapi.ReuploadDrawable(handle, dataHelper, true);

				UploadParts(rapi, handle);
			}

			dataHelper.Clear();
		}

		private void UploadParts(IRenderAPI rapi, IDrawableHandle handle)
		{
			int offset, index;
			if(currentIndexChunk != 0)
			{
				dataHelper.vertices = null;
				dataHelper.verticesCount = 0;
				dataHelper.indicesCount = (uint)chunkSize;

				index = 0;
				offset = 0;
				while(index < currentIndexChunk)
				{
					dataHelper.indices = indexChunks[index];
					rapi.UpdateDrawablePart(handle, dataHelper, offset, null);
					offset += chunkSize;
					index++;
				}
				dataHelper.indices = currentIndexBuffer;
				dataHelper.indicesCount = (uint)chunkIndexCounter;
				rapi.UpdateDrawablePart(handle, dataHelper, offset, null);
			}
			if(currentVertexChunk != 0)
			{
				if(tmpVerticesOffset == null) tmpVerticesOffset = new int[1];
				dataHelper.indices = null;
				dataHelper.indicesCount = 0;
				dataHelper.verticesCount = (uint)chunkSize;

				index = 0;
				offset = 0;
				while(index < currentVertexChunk)
				{
					dataHelper.vertices = vertexChunks[index];
					tmpVerticesOffset[0] = offset;
					rapi.UpdateDrawablePart(handle, dataHelper, 0, tmpVerticesOffset);
					offset += chunkSize;
					index++;
				}
				dataHelper.vertices = currentVertexBuffer;
				dataHelper.verticesCount = (uint)chunkVertexCounter;
				tmpVerticesOffset[0] = offset;
				rapi.UpdateDrawablePart(handle, dataHelper, 0, tmpVerticesOffset);
			}
		}

		private class DrawableData : IDrawableData
		{
			EnumDrawMode IDrawableInfo.DrawMode => mode;
			uint IDrawableInfo.IndicesCount => indicesCount;
			uint IDrawableInfo.VerticesCount => verticesCount;
			int IDrawableInfo.VertexBuffersCount => 1;

			public EnumDrawMode mode;
			public TVertex[] vertices;
			public uint[] indices;
			public uint verticesCount;
			public uint indicesCount;
			public VertexDeclaration vertexDeclaration;

			public void Clear()
			{
				vertices = null;
				indices = null;
				indicesCount = 0;
				verticesCount = 0;
				vertexDeclaration = default;
			}

			IndicesMeta IDrawableInfo.GetIndicesMeta()
			{
				return new IndicesMeta(false);
			}

			unsafe VertexBufferMeta IDrawableInfo.GetVertexBufferMeta(int index)
			{
				return new VertexBufferMeta(vertexDeclaration, sizeof(TVertex), false);
			}

			ReadOnlySpan<uint> IDrawableData.GetIndicesData()
			{
				if(indices == null) return default;
				return indices;
			}

			ReadOnlySpan<byte> IDrawableData.GetVerticesData(int bufferIndex)
			{
				if(vertices == null) return default;
				return MemoryMarshal.AsBytes(vertices.AsSpan());
			}
		}
	}
}