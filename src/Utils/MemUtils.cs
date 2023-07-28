namespace PowerOfMind.Utils
{
	public static class MemUtils
	{
		public unsafe static bool MemEquals(byte* a, byte* b, int length)
		{
			for(int i = length / 8; i > 0; i--)
			{
				if(*(long*)a != *(long*)b)
				{
					return false;
				}
				a += 8;
				b += 8;
			}

			if((length & 4) != 0)
			{
				if(*((int*)a) != *((int*)b))
				{
					return false;
				}
				a += 4;
				b += 4;
			}

			if((length & 2) != 0)
			{
				if(*((short*)a) != *((short*)b))
				{
					return false;
				}
				a += 2;
				b += 2;
			}

			if((length & 1) != 0)
			{
				if(*a != *b)
				{
					return false;
				}
			}

			return true;
		}

		public unsafe static bool MemIsZero(byte* a, int length)
		{
			for(int i = length / 8; i > 0; i--)
			{
				if(*(long*)a != 0)
				{
					return false;
				}
				a += 8;
			}

			if((length & 4) != 0)
			{
				if(*((int*)a) != 0)
				{
					return false;
				}
				a += 4;
			}

			if((length & 2) != 0)
			{
				if(*((short*)a) != 0)
				{
					return false;
				}
				a += 2;
			}

			if((length & 1) != 0)
			{
				if(*a != 0)
				{
					return false;
				}
			}

			return true;
		}
	}
}