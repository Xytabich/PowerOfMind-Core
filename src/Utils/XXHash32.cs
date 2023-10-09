/*
//https://github.com/shibox/XXHash

BSD License

For XXHash software

Copyright (c) 2016-2017, shibox. All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

 * Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

 * Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

 * Neither the name SKB Kontur nor the names of its contributors may be used to
   endorse or promote products derived from this software without specific
   prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace XXHash
{
	public static class XXHash32
	{
		private const uint PRIME32_1 = 2654435761U;
		private const uint PRIME32_2 = 2246822519U;
		private const uint PRIME32_3 = 3266489917U;
		private const uint PRIME32_4 = 668265263U;
		private const uint PRIME32_5 = 374761393U;

		public unsafe static uint Hash(string input, Encoding encoder, uint seed = 0)
		{
			var buf = encoder.GetBytes(input);
			fixed(byte* buffer = buf)
			{
				return Hash(buffer, buf.Length, seed);
			}
		}

		public unsafe static uint Hash(string input, uint seed = 0)
		{
			fixed(char* buffer = input)
			{
				return Hash((byte*)buffer, input.Length * sizeof(char), seed);
			}
		}

		public unsafe static uint Hash(string input, int offset, int count, uint seed = 0)
		{
			fixed(char* @in = input)
			{
				char* pd = @in;
				pd += offset;
				return Hash((byte*)pd, count * sizeof(char), seed);
			}
		}

		public unsafe static uint Hash(this Char[] input, int offset, int count, uint seed = 0)
		{
			if(input == null || input.Length == 0 || count == 0)
				return 0;
			fixed(Char* @in = &input[offset])
			{
				return Hash((byte*)@in, count * sizeof(Char), seed);
			}
		}

		public unsafe static uint Hash(this Char[] input, uint seed = 0)
		{
			if(input == null || input.Length == 0)
				return 0;
			fixed(Char* @in = &input[0])
			{
				return Hash((byte*)@in, input.Length * sizeof(Char), seed);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe uint Hash(byte* buffer, int len, uint seed = 0)
		{
			unchecked
			{
				uint h32;

				byte* bEnd = buffer + len;

				if(len >= 16)
				{
					byte* limit = bEnd - 16;

					uint v1 = seed + PRIME32_1 + PRIME32_2;
					uint v2 = seed + PRIME32_2;
					uint v3 = seed + 0;
					uint v4 = seed - PRIME32_1;

					do
					{
						v1 += *((uint*)buffer) * PRIME32_2;
						buffer += sizeof(uint);
						v2 += *((uint*)buffer) * PRIME32_2;
						buffer += sizeof(uint);
						v3 += *((uint*)buffer) * PRIME32_2;
						buffer += sizeof(uint);
						v4 += *((uint*)buffer) * PRIME32_2;
						buffer += sizeof(uint);

						v1 = rol13(v1);
						v2 = rol13(v2);
						v3 = rol13(v3);
						v4 = rol13(v4);

						v1 *= PRIME32_1;
						v2 *= PRIME32_1;
						v3 *= PRIME32_1;
						v4 *= PRIME32_1;
					}
					while(buffer <= limit);

					h32 = rol1(v1) + rol7(v2) + rol12(v3) + rol18(v4);
				}
				else
				{
					h32 = seed + PRIME32_5;
				}

				h32 += (uint)len;


				while(buffer + 4 <= bEnd)
				{
					h32 += *((uint*)buffer) * PRIME32_3;
					h32 = rol17(h32) * PRIME32_4;
					buffer += 4;
				}

				while(buffer < bEnd)
				{
					h32 += (uint)(*buffer) * PRIME32_5;
					h32 = rol11(h32) * PRIME32_1;
					buffer++;
				}

				h32 ^= h32 >> 15;
				h32 *= PRIME32_2;
				h32 ^= h32 >> 13;
				h32 *= PRIME32_3;
				h32 ^= h32 >> 16;

				return h32;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint rol1(uint x) { return (x << 1) | (x >> (32 - 1)); }
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint rol7(uint x) { return (x << 7) | (x >> (32 - 7)); }
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint rol11(uint x) { return (x << 11) | (x >> (32 - 11)); }
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint rol12(uint x) { return (x << 12) | (x >> (32 - 12)); }
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint rol18(uint x) { return (x << 18) | (x >> (32 - 18)); }
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint rol13(uint x) { return (x << 13) | (x >> (32 - 13)); }
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static uint rol17(uint x) { return (x << 17) | (x >> (32 - 17)); }
	}
}