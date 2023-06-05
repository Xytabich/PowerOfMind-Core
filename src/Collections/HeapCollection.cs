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
				if(id == lastFreeIndex || indices[id] != -1)
				{
					throw new InvalidOperationException();
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
	}
}