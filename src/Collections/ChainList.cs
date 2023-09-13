using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PowerOfMind.Collections
{
	[DebuggerDisplay("Capacity Used: {count}")]
	public class ChainList<T>
	{
		public int CapacityUsed => count;

		private LinkedNode[] nodes;
		private int lastFree;
		private int count;

		public ChainList()
		{
			nodes = new LinkedNode[4];
			lastFree = -1;
			count = 0;
		}

		public ref T this[int id] => ref nodes[id].value;

		/// <summary>
		/// Create node with value and add to chain
		/// </summary>
		/// <returns>Node id</returns>
		public int Add(int chain, in T value)
		{
			int id = Allocate();
			ref var node = ref nodes[id];
			if(chain < 0)
			{
				node.prev = chain = id;
			}
			else
			{
				node.prev = nodes[chain].prev;
				nodes[nodes[chain].prev].next = id;
				nodes[chain].prev = id;
			}
			node.value = value;
			node.next = chain;
			return id;
		}

		/// <summary>
		/// Append value to chain
		/// </summary>
		/// <returns>Chain id</returns>
		public int Append(int chain, in T value)
		{
			int id = Add(chain, value);
			return chain < 0 ? id : chain;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetPrevId(int chain, int id, out int prevId)
		{
			if(chain == id)
			{
				prevId = default;
				return false;
			}

			prevId = nodes[id].prev;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetNextId(int chain, int id, out int nextId)
		{
			if(id < 0)
			{
				nextId = chain;
				return chain >= 0;
			}
			nextId = nodes[id].next;
			return nextId != chain;
		}

		public int Remove(int chain, int id)
		{
			ref var node = ref nodes[id];

			if(chain < 0 || id == chain && nodes[id].prev == chain)
			{
				node.value = default;
				node.prev = lastFree;
				node.next = -1;
				lastFree = id;
				return -1;
			}

			int next = nodes[id].next;
			int prev = nodes[id].prev;
			node.value = default;

			nodes[next].prev = prev;
			nodes[prev].next = next;

			node.prev = lastFree;
			node.next = -1;
			lastFree = id;
			return id == chain ? next : chain;
		}

		public void RemoveChain(int chain)
		{
			int id = chain;
			ref var node = ref nodes[id];
			do
			{
				node.value = default;
				node.prev = lastFree;
				lastFree = id;
				id = node.next;
				node.next = -1;
				node = ref nodes[id];
			}
			while(id != chain);
		}

		public int AppendBetween(int chain, int id0, int id1, in T value)
		{
			int id = Add(-1, value);
			ref var node = ref nodes[id];
			ref var node0 = ref nodes[id0];
			ref var node1 = ref nodes[id1];
			if(node0.next == id1)
			{
				node.next = id1;
				node.prev = id0;
				node0.next = id;
				node1.prev = id;
			}
			else
			{
				node.next = id0;
				node.prev = id1;
				node0.prev = id;
				node1.next = id;
			}
			return chain;
		}

		public Readonly AsReadonly()
		{
			return new Readonly(this);
		}

		public ChainEnumerable<RemoveChainEnumerator, T> RemoveEnumerated(int chain)
		{
			return new ChainEnumerable<RemoveChainEnumerator, T>(chain, this);
		}

		public ChainReadonlyEnumerable<ChainEnumerator, T> GetEnumerable(int chain)
		{
			return new ChainReadonlyEnumerable<ChainEnumerator, T>(chain, new Readonly(this));
		}

		public ChainReadonlyEnumerable<NodeEnumerator, int> GetNodeEnumerable(int chain)
		{
			return new ChainReadonlyEnumerable<NodeEnumerator, int>(chain, new Readonly(this));
		}

		private int Allocate()
		{
			if(lastFree < 0)
			{
				if(count >= nodes.Length)
				{
					Array.Resize(ref nodes, count * 2);
				}
				return count++;
			}
			int id = lastFree;
			lastFree = nodes[id].prev;
			return id;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		[DebuggerDisplay("{prev}<-[{value}]->{next}")]
		private struct LinkedNode
		{
			public int prev;
			public int next;
			public T value;
		}

		public readonly struct Readonly
		{
			private readonly LinkedNode[] nodes;

			public Readonly(ChainList<T> chainList)
			{
				nodes = chainList.nodes;
			}

			public ref readonly T this[int id] => ref nodes[id].value;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool TryGetPrevId(int chain, int id, out int prevId)
			{
				if(chain == id)
				{
					prevId = default;
					return false;
				}

				prevId = nodes[id].prev;
				return true;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool TryGetNextId(int chain, int id, out int nextId)
			{
				if(id < 0)
				{
					nextId = chain;
					return chain >= 0;
				}
				nextId = nodes[id].next;
				return nextId != chain;
			}

			public ChainReadonlyEnumerable<ChainEnumerator, T> GetEnumerable(int chain)
			{
				return new ChainReadonlyEnumerable<ChainEnumerator, T>(chain, this);
			}

			public ChainReadonlyEnumerable<NodeEnumerator, int> GetNodeEnumerable(int chain)
			{
				return new ChainReadonlyEnumerable<NodeEnumerator, int>(chain, this);
			}

			public static implicit operator Readonly(ChainList<T> chainList)
			{
				return new Readonly(chainList);
			}
		}

		public readonly struct ChainEnumerable<TEnumerator, TValue> : IEnumerable<TValue> where TEnumerator : IEnumerator<TValue>, IChainEnumerator
		{
			private readonly int chainId;
			private readonly ChainList<T> chainList;

			public ChainEnumerable(int chainId, ChainList<T> chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
			}

			public TEnumerator GetEnumerator()
			{
				var e = default(TEnumerator);
				e.Init(chainId, chainList);
				return e;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		public readonly struct ChainReadonlyEnumerable<TEnumerator, TValue> : IEnumerable<TValue> where TEnumerator : IEnumerator<TValue>, IChainReadonlyEnumerator
		{
			private readonly int chainId;
			private readonly Readonly chainList;

			public ChainReadonlyEnumerable(int chainId, Readonly chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
			}

			public TEnumerator GetEnumerator()
			{
				var e = default(TEnumerator);
				e.Init(chainId, chainList);
				return e;
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
			{
				return GetEnumerator();
			}
		}

		public struct ChainEnumerator : IEnumerator<T>, IChainReadonlyEnumerator
		{
			private int chainId;
			private Readonly chainList;

			private int current;

			public T Current => chainList[current];
			object IEnumerator.Current => chainList[current];

			public ChainEnumerator(int chainId, Readonly chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
				current = -1;
			}

			void IChainReadonlyEnumerator.Init(int chainId, Readonly chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
				current = -1;
			}

			public bool MoveNext()
			{
				if(current < 0)
				{
					current = chainId;
					return chainId >= 0;
				}
				int next;
				if(chainList.TryGetNextId(chainId, current, out next))
				{
					current = next;
					return true;
				}
				return false;
			}

			void IEnumerator.Reset()
			{
				current = -1;
			}

			void IDisposable.Dispose()
			{
			}
		}

		public struct NodeEnumerator : IEnumerator<int>, IChainReadonlyEnumerator
		{
			private int chainId;
			private Readonly chainList;

			private int current;

			public int Current => current;
			object IEnumerator.Current => current;

			public NodeEnumerator(int chainId, Readonly chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
				current = -1;
			}

			void IChainReadonlyEnumerator.Init(int chainId, Readonly chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
				current = -1;
			}

			public bool MoveNext()
			{
				if(current < 0)
				{
					current = chainId;
					return chainId >= 0;
				}
				int next;
				if(chainList.TryGetNextId(chainId, current, out next))
				{
					current = next;
					return true;
				}
				return false;
			}

			void IEnumerator.Reset()
			{
				current = -1;
			}

			void IDisposable.Dispose()
			{
			}
		}

		public struct RemoveChainEnumerator : IEnumerator<T>, IChainEnumerator
		{
			private int chainId;
			private ChainList<T> chainList;

			private int current;

			public T Current => chainList[current];
			object IEnumerator.Current => chainList[current];

			public RemoveChainEnumerator(int chainId, ChainList<T> chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
				current = -1;
			}

			void IChainEnumerator.Init(int chainId, ChainList<T> chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
				current = -1;
			}

			public bool MoveNext()
			{
				if(current < 0)
				{
					current = chainId;
					return chainId >= 0;
				}
				current = chainList.Remove(current, current);
				if(current < 0)
				{
					chainId = -1;
					return false;
				}
				return true;
			}

			void IEnumerator.Reset()
			{
				current = -1;
			}

			void IDisposable.Dispose()
			{
			}
		}

		public interface IChainEnumerator
		{
			void Init(int chainId, ChainList<T> chainList);
		}

		public interface IChainReadonlyEnumerator
		{
			void Init(int chainId, Readonly chainList);
		}
	}

	[DebuggerDisplay("Capacity Used: {count}")]
	public class ChainList
	{
		private LinkedNode[] nodes;
		private int lastFree;
		private int count;

		public ChainList()
		{
			nodes = new LinkedNode[4];
			lastFree = -1;
			count = 0;
		}

		/// <summary>
		/// Create node add to chain
		/// </summary>
		/// <returns>Node id</returns>
		public int Add(int chain)
		{
			int id = Allocate();
			ref var node = ref nodes[id];
			if(chain < 0)
			{
				node.prev = chain = id;
			}
			else
			{
				node.prev = nodes[chain].prev;
				nodes[nodes[chain].prev].next = id;
				nodes[chain].prev = id;
			}
			node.next = chain;
			return id;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetPrevId(int chain, int id, out int prevId)
		{
			if(chain == id)
			{
				prevId = default;
				return false;
			}

			prevId = nodes[id].prev;
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetNextId(int chain, int id, out int nextId)
		{
			nextId = nodes[id].next;
			return nextId != chain;
		}

		public int Remove(int chain, int id)
		{
			ref var node = ref nodes[id];

			if(chain < 0 || id == chain && nodes[id].prev == chain)
			{
				node.prev = lastFree;
				node.next = -1;
				lastFree = id;
				return -1;
			}

			int next = nodes[id].next;
			int prev = nodes[id].prev;

			nodes[next].prev = prev;
			nodes[prev].next = next;

			node.prev = lastFree;
			node.next = -1;
			lastFree = id;
			return id == chain ? next : chain;
		}

		public void RemoveChain(int chain)
		{
			int id = chain;
			ref var node = ref nodes[id];
			do
			{
				node.prev = lastFree;
				lastFree = id;
				id = node.next;
				node.next = -1;
				node = ref nodes[id];
			}
			while(id != chain);
		}

		public int AppendBetween(int chain, int id0, int id1)
		{
			int id = Allocate();
			ref var node = ref nodes[id];
			ref var node0 = ref nodes[id0];
			ref var node1 = ref nodes[id1];
			if(node0.next == id1)
			{
				node.next = id1;
				node.prev = id0;
				node0.next = id;
				node1.prev = id;
			}
			else
			{
				node.next = id0;
				node.prev = id1;
				node0.prev = id;
				node1.next = id;
			}
			return chain;
		}

		public Readonly AsReadonly()
		{
			return new Readonly(this);
		}

		public RemoveChainEnumerable RemoveEnumerated(int chain)
		{
			return new RemoveChainEnumerable(chain, this);
		}

		public NodeEnumerable GetEnumerable(int chain)
		{
			return new NodeEnumerable(chain, new Readonly(this));
		}

		private int Allocate()
		{
			if(lastFree < 0)
			{
				if(count >= nodes.Length)
				{
					Array.Resize(ref nodes, count * 2);
				}
				return count++;
			}
			int id = lastFree;
			lastFree = nodes[id].prev;
			return id;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		[DebuggerDisplay("{prev}<->{next}")]
		private struct LinkedNode
		{
			public int prev;
			public int next;
		}

		public readonly struct Readonly
		{
			private readonly LinkedNode[] nodes;

			public Readonly(ChainList chainList)
			{
				nodes = chainList.nodes;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool TryGetPrevId(int chain, int id, out int prevId)
			{
				if(chain == id)
				{
					prevId = default;
					return false;
				}

				prevId = nodes[id].prev;
				return true;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool TryGetNextId(int chain, int id, out int nextId)
			{
				if(id < 0)
				{
					nextId = chain;
					return chain >= 0;
				}
				nextId = nodes[id].next;
				return nextId != chain;
			}

			public NodeEnumerable GetEnumerable(int chain)
			{
				return new NodeEnumerable(chain, this);
			}

			public static implicit operator Readonly(ChainList chainList)
			{
				return new Readonly(chainList);
			}
		}

		public readonly struct NodeEnumerable : IEnumerable<int>
		{
			private readonly int chainId;
			private readonly Readonly chainList;

			public NodeEnumerable(int chainId, Readonly chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
			}

			public NodeEnumerator GetEnumerator()
			{
				return new NodeEnumerator(chainId, chainList);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return new NodeEnumerator(chainId, chainList);
			}

			IEnumerator<int> IEnumerable<int>.GetEnumerator()
			{
				return new NodeEnumerator(chainId, chainList);
			}
		}

		public readonly struct RemoveChainEnumerable : IEnumerable<int>
		{
			private readonly int chainId;
			private readonly ChainList chainList;

			public RemoveChainEnumerable(int chainId, ChainList chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
			}

			public RemoveChainEnumerator GetEnumerator()
			{
				return new RemoveChainEnumerator(chainId, chainList);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return new RemoveChainEnumerator(chainId, chainList);
			}

			IEnumerator<int> IEnumerable<int>.GetEnumerator()
			{
				return new RemoveChainEnumerator(chainId, chainList);
			}
		}

		public struct NodeEnumerator : IEnumerator<int>
		{
			private int chainId;
			private Readonly chainList;

			private int current;

			public int Current => current;
			object IEnumerator.Current => current;

			public NodeEnumerator(int chainId, Readonly chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
				current = -1;
			}

			public bool MoveNext()
			{
				if(current < 0)
				{
					current = chainId;
					return chainId >= 0;
				}
				int next;
				if(chainList.TryGetNextId(chainId, current, out next))
				{
					current = next;
					return true;
				}
				return false;
			}

			void IEnumerator.Reset()
			{
				current = -1;
			}

			void IDisposable.Dispose()
			{
			}
		}

		public struct RemoveChainEnumerator : IEnumerator<int>
		{
			private int chainId;
			private ChainList chainList;

			private int current;

			public int Current => current;
			object IEnumerator.Current => current;

			public RemoveChainEnumerator(int chainId, ChainList chainList)
			{
				this.chainId = chainId;
				this.chainList = chainList;
				current = -1;
			}

			public bool MoveNext()
			{
				if(current < 0)
				{
					current = chainId;
					return chainId >= 0;
				}
				current = chainList.Remove(current, current);
				if(current < 0)
				{
					chainId = -1;
					return false;
				}
				return true;
			}

			void IEnumerator.Reset()
			{
				current = -1;
			}

			void IDisposable.Dispose()
			{
			}
		}
	}

	public static class ChainListExtensions
	{
		/// <summary>
		/// Create node with value and add to chain.
		/// Assigns a <paramref name="chain"/> if not initialized.
		/// </summary>
		/// <returns>Node id</returns>
		public static int Add<T>(this ChainList<T> chainList, ref int chain, in T value)
		{
			int id = chainList.Add(chain, value);
			if(chain < 0) chain = id;
			return id;
		}

		/// <summary>
		/// Append value to chain.
		/// Assigns a <paramref name="chain"/> if not initialized.
		/// </summary>
		public static void Append<T>(this ChainList<T> chainList, ref int chain, in T value)
		{
			chain = chainList.Append(chain, value);
		}

		public static void Remove<T>(this ChainList<T> chainList, ref int chain, int id)
		{
			chain = chainList.Remove(chain, id);
		}
	}
}