using PowerOfMind.Collections;
using PowerOfMind.Graphics;
using PowerOfMind.Graphics.Shader;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace PowerOfMind.Systems.RenderBatching
{
	public class WorldBatcher<TVertex, TUniform>
		where TVertex : unmanaged, IVertexStruct
		where TUniform : unmanaged, IUniformsData
	{
		private readonly Dictionary<int3, int> chunkToId = new Dictionary<int3, int>();
		private readonly HeapCollection<ChunkBuilder> chunks = new HeapCollection<ChunkBuilder>();

		private readonly HeapCollection<IBlockDataProvider> providers = new HeapCollection<IBlockDataProvider>();//TODO: check if provider is not null in separate thread, because can be removed when in work

		private readonly TVertex vertexStruct;
		private readonly TUniform uniformStruct;
		private readonly IExtendedShaderProgram shader;

		private readonly int3 chunkSize;
		private readonly int octreeDepth;

		private bool isInited = false;

		public int AddProvider(IBlockDataProvider provider)
		{
			return providers.Add(provider);
		}

		public void RemoveProvider(int id)
		{
			providers.Remove(id);
		}

		public void AddBlock(int3 pos, int builderId)
		{
			ChunkBlockCoords coords = default;
			coords.offset = pos / chunkSize;
			if(!chunkToId.TryGetValue(coords.offset, out var id))
			{
				id = chunks.Add(new ChunkBuilder());
				chunkToId[coords.offset] = id;
			}
			coords.offset = pos % chunkSize;
			coords.currentDepth = octreeDepth;
			chunks[id].AddBlock(ref coords, builderId);
		}

		public void RemoveBlock(int3 pos)
		{
			ChunkBlockCoords coords = default;
			coords.offset = pos / chunkSize;
			if(chunkToId.TryGetValue(coords.offset, out var id))
			{
				coords.offset = pos % chunkSize;
				coords.currentDepth = octreeDepth;
				if(chunks[id].RemoveBlock(ref coords))
				{
					chunkToId.Remove(pos / chunkSize);
					chunks.Remove(id);
				}
			}
		}

		//TODO: add bulk, remove bulk (int3 from, int3 to, ulong[] fillMap, int builderId)

		private class ChunkBuilder
		{
			private OctreeHeap octree;

			public ChunkBuilder()
			{
				octree = new OctreeHeap(8);
				octree.Add(ref OctreeNode.Empty);
			}

			public void AddBlock(ref ChunkBlockCoords coords, int builderId)
			{
				int index;
				ref OctreeNode nodeRef = ref octree[0];
				do
				{
					index = coords.GetIndex();
					var value = nodeRef[index];
					if(value < 0)
					{
						while(coords.Next())
						{
							var node = new OctreeNode(value);
							int id = octree.Add(ref node);
							nodeRef[index] = id;
							nodeRef = ref octree[id];
							index = coords.GetIndex();
						}
						nodeRef[index] = builderId | int.MinValue;
						break;
					}
					nodeRef = ref octree[value];
				}
				while(coords.Next());

				throw new Exception("Invalid octree handling");
			}

			public bool RemoveBlock(ref ChunkBlockCoords coords)//TODO: calc count to remove using node.EmptyExcept, then remove nodes & return true if there is no nodes in tree
			{
				int index;
				ref OctreeNode nodeRef = ref octree[0];
				do
				{
					index = coords.GetIndex();
					var value = nodeRef[index];
					if(value < 0)
					{
					}
					nodeRef = ref octree[value];
				}
				while(coords.Next());

				throw new Exception("Invalid octree handling");
			}
		}

		private unsafe struct OctreeNode
		{
			public const int EMPTY_NODE = int.MaxValue;

			public static OctreeNode Empty = new OctreeNode(EMPTY_NODE);

			public fixed int nodes[8];

			public ref int this[int index]
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get { return ref nodes[index]; }
			}

			public OctreeNode(int value)
			{
				Empty.nodes[0] = value;
				Empty.nodes[1] = value;
				Empty.nodes[2] = value;
				Empty.nodes[3] = value;
				Empty.nodes[4] = value;
				Empty.nodes[5] = value;
				Empty.nodes[6] = value;
				Empty.nodes[7] = value;
			}

			public bool EmptyExcept(int index)
			{
				for(int i = 0; i < 8; i++)
				{
					if((i == index) & (nodes[i] != EMPTY_NODE)) return false;
				}
				return true;
			}
		}

		private struct OctreeHeap
		{
			private OctreeNode[] nodes;
			private int lastFree;
			private int capacityUsed;

			public ref OctreeNode this[int id] => ref nodes[id];

			public OctreeHeap(int capacity)
			{
				nodes = new OctreeNode[capacity];
				lastFree = -1;
				capacityUsed = 0;
			}

			public int Add(ref OctreeNode value)
			{
				int id;
				if(lastFree >= 0)
				{
					id = lastFree;
					lastFree = nodes[lastFree][0];
					nodes[id] = value;
					return id;
				}

				if(capacityUsed >= nodes.Length)
				{
					Array.Resize(ref nodes, capacityUsed * 2);
				}

				id = capacityUsed;
				capacityUsed++;
				nodes[id] = value;
				return id;
			}

			public void Remove(int id)
			{
				nodes[id][0] = lastFree;
				lastFree = id;
			}
		}

		private struct ChunkBlockCoords
		{
			public int3 offset;
			public int currentDepth;

			public int GetIndex()
			{
				return (offset.x >> currentDepth) | (offset.y >> currentDepth) << 3 | (offset.z >> currentDepth) << 4;
			}

			public bool Next()
			{
				currentDepth--;
				return currentDepth >= 0;
			}
		}
	}

	public interface IBlockDataProvider
	{
		/// <summary>
		/// Builds the drawable data for the batcher.
		/// Sometimes can be called for invalid coordinates, as it is processed in a different thread.
		/// </summary>
		void AddData(int3 pos, IBatchBuildContext context);

		/// <summary>
		/// Returns mask of block sides that should be culled.
		/// Sometimes can be called for invalid coordinates, as it is processed in a different thread.
		/// </summary>
		int GetCullSides(int3 pos);
	}
}