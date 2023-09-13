using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PowerOfMind.Collections
{
	[DebuggerDisplay("Capacity Used: {valuesCount}")]
	public class HeapCollection<T>
	{
		public int Count
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get { return valuesCount; }
		}

		public int Capacity => values.Length;
		public int UsedCapacity => valuesCount;

		private int lastFreeIndex;
		private int[] indices;

		private int valuesCount;
		private T[] values;

		public HeapCollection(int capacity = 4)
		{
			lastFreeIndex = -1;
			indices = new int[capacity];

			valuesCount = 0;
			values = new T[capacity];
		}

		public ref T this[int id]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				if(id >= valuesCount || id == lastFreeIndex || indices[id] != -1)
				{
					throw new InvalidOperationException("Heap value reference is invalid");
				}
				return ref values[id];
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Add(T value)
		{
			int id = Allocate();
			values[id] = value;
			return id;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Allocate()
		{
			int index;
			if(lastFreeIndex < 0)
			{
				index = valuesCount;
				if(valuesCount >= values.Length)
				{
					int newCount = valuesCount * 2;
					Array.Resize(ref values, newCount);
					Array.Resize(ref indices, newCount);
				}
			}
			else
			{
				index = lastFreeIndex;
				lastFreeIndex = indices[lastFreeIndex];
			}
			valuesCount++;
			indices[index] = -1;
			return index;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Remove(int id)
		{
			Remove(id, default);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Remove(int id, T defValue)
		{
			if(id == lastFreeIndex || indices[id] != -1)
			{
				throw new InvalidOperationException();
			}

			values[id] = defValue;
			indices[id] = lastFreeIndex;
			lastFreeIndex = id;
			valuesCount--;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsAllocated(int id)
		{
			return id < valuesCount && id != lastFreeIndex && indices[id] == -1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGet(int id, out T value)
		{
			value = values[id];
			return id < valuesCount && id != lastFreeIndex && indices[id] == -1;
		}

		public Readonly AsReadonly()
		{
			return new Readonly(this);
		}

		public readonly struct Readonly
		{
			private readonly int valuesCount;
			private readonly int lastFreeIndex;
			private readonly int[] indices;
			private readonly T[] values;

			public Readonly(HeapCollection<T> heapCollection)
			{
				valuesCount = heapCollection.valuesCount;
				lastFreeIndex = heapCollection.lastFreeIndex;
				indices = heapCollection.indices;
				values = heapCollection.values;
			}

			public ref readonly T this[int id]
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get
				{
					if(id >= valuesCount || id == lastFreeIndex || indices[id] != -1)
					{
						throw new InvalidOperationException("Heap value reference is invalid");
					}
					return ref values[id];
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool IsAllocated(int id)
			{
				return id < valuesCount && id != lastFreeIndex && indices[id] == -1;
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool TryGet(int id, out T value)
			{
				value = values[id];
				return id < valuesCount && id != lastFreeIndex && indices[id] == -1;
			}

			public static implicit operator Readonly(HeapCollection<T> heapCollection)
			{
				return new Readonly(heapCollection);
			}
		}
	}
}