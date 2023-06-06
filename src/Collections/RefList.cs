using System;
using System.Collections.Generic;

namespace PowerOfMind.Collections
{
	public class RefList<T>
	{
		public int Capacity => elements.Length;
		public int Count => size;

		public ref T this[int index] => ref elements[index];

		private T[] elements;
		private int size;

		public RefList(int capacity = 4)
		{
			elements = new T[capacity];
			size = 0;
		}

		public void Add(in T value)
		{
			EnsureCapacity();
			elements[size] = value;
			size++;
		}

		public void AddRange(IReadOnlyList<T> collection)
		{
			int count = collection.Count;
			EnsureCapacity(count);
			for(int i = 0; i < count; i++)
			{
				elements[size] = collection[i];
				size++;
			}
		}

		public void RemoveAt(int index)
		{
			size--;
			if(index < size)
			{
				Array.Copy(elements, index + 1, elements, index, size - index);
			}
			elements[size] = default;
		}

		public void RemoveRange(int index, int count)
		{
			size -= count;
			if(index < size)
			{
				Array.Copy(elements, index + count, elements, index, size - index);
			}
			Array.Clear(elements, size, count);
		}

		public void Clear()
		{
			if(size == 0) return;
			Array.Clear(elements, 0, size);
			size = 0;
		}

		public T[] ToArray()
		{
			var arr = new T[size];
			Array.Copy(elements, arr, size);
			return arr;
		}

		private void EnsureCapacity(int count = 1)
		{
			if(size + count > elements.Length)
			{
				var tmp = elements;
				elements = new T[Math.Max(elements.Length * 2, size + count)];
				Array.Copy(tmp, elements, size);
			}
		}
	}
}