using System;
using System.Linq.Expressions;
using System.Reflection;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace PowerOfMind.Systems.ChunkBatchers
{
	public class BlockLightUtil
	{
		private delegate int GetOneBlockData(IChunkBlocks data, out ushort lightOut, out int lightSatOut, out int fluidBlockId, int index3d);

		private const int CENTER = 13;

		private const float occ = 0.67f;
		private const float halfoccInverted = 0.0196875f;

		private static readonly int3[] blockOffsets;

		private static readonly GetOneBlockData chunkGetOneBlockData;

		private static readonly int2[] axesByFacingLookup;
		private static readonly int4[] indexesByFacingLookup;

		static BlockLightUtil()
		{
			var clientChunkDataType = typeof(ClientChunk).Assembly.GetType("Vintagestory.Client.NoObf.ClientChunkData");
			var dataArg = Expression.Parameter(typeof(IChunkBlocks));
			var lightOutArg = Expression.Parameter(typeof(ushort).MakeByRefType());
			var lightSatOutArg = Expression.Parameter(typeof(int).MakeByRefType());
			var fluidBlockIdArg = Expression.Parameter(typeof(int).MakeByRefType());
			var index3dArg = Expression.Parameter(typeof(int));
			chunkGetOneBlockData = Expression.Lambda<GetOneBlockData>(
				Expression.Call(
					Expression.TypeAs(dataArg, clientChunkDataType),
					clientChunkDataType.GetMethod("GetOne", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
					lightOutArg,
					lightSatOutArg,
					fluidBlockIdArg,
					index3dArg
				),
				dataArg,
				lightOutArg,
				lightSatOutArg,
				fluidBlockIdArg,
				index3dArg
			).Compile();

			blockOffsets = new int3[27];
			for(int x = -1; x <= 1; x++)
			{
				for(int y = -1; y <= 1; y++)
				{
					for(int z = -1; z <= 1; z++)
					{
						blockOffsets[MapUtil.Index3d(x, y, z, 3, 3) + CENTER] = new int3(x, y, z);
					}
				}
			}

			axesByFacingLookup = new int2[6] { new int2(0, 1), new int2(1, 2), new int2(0, 1), new int2(1, 2), new int2(0, 2), new int2(0, 2) };
			indexesByFacingLookup = new int4[6] { new int4(3, 2, 1, 0), new int4(3, 1, 2, 0), new int4(2, 3, 0, 1), new int4(2, 0, 3, 1), new int4(3, 2, 1, 0), new int4(1, 0, 3, 2) };
		}

		public Unmanaged unmanaged;

		private Block block;
		private IBlockAccessor blockAccessor;
		private IGeometryTester geometryTester;
		private ColorUtil.LightUtil lightConverter;
		private readonly Block[] currentChunkFluidBlocksExt;
		private readonly Block[] currentChunkBlocksExt;

		public BlockLightUtil()
		{
			currentChunkFluidBlocksExt = new Block[27];
			currentChunkBlocksExt = new Block[27];
		}

		public void Init(IBlockAccessor blockAccessor, IGeometryTester geometryTester, ColorUtil.LightUtil lightConverter)
		{
			unmanaged.chunkSize = blockAccessor.ChunkSize;
			unmanaged.chunkSizeMask = unmanaged.chunkSize - 1;

			this.blockAccessor = blockAccessor;
			this.geometryTester = geometryTester;
			this.lightConverter = lightConverter;
		}

		public void Clear()
		{
			this.blockAccessor = null;
			this.geometryTester = null;
			this.lightConverter = null;
		}

		public unsafe struct Unmanaged
		{
#if DEBUG
			public int[] OutLightRGB
			{
				get
				{
					var value = new int[25];
					for(int i = 0; i < 25; i++)
					{
						value[i] = outLightRGB[i];
					}
					return value;
				}
			}

			public int[] CurrentChunkRgbsExt
			{
				get
				{
					var value = new int[27];
					for(int i = 0; i < 27; i++)
					{
						value[i] = currentChunkRgbsExt[i];
					}
					return value;
				}
			}

			public int[] NeighbourLightRGBS
			{
				get
				{
					var value = new int[9];
					for(int i = 0; i < 9; i++)
					{
						value[i] = neighbourLightRGBS[i];
					}
					return value;
				}
			}
#endif

			internal int chunkSize, chunkSizeMask;

			private fixed int outLightRGB[25];
			private fixed int currentChunkRgbsExt[27];
			private fixed int neighbourLightRGBS[9];
			private int4 currentLightRGBByCorner;

			//locals:
			int blockId, lightSat, fluidId, lightRGBindex, tileSide;
			ushort light;
			long totalLight;
			int3 pos;
			public long SetUpLightRGBs(BlockLightUtil managed, int3 blockPos)
			{
				ClientChunk chunk;
				BlockFacing face;

				for(int i = 0; i < 27; i++)
				{
					pos = blockOffsets[i] + blockPos;
					chunk = (ClientChunk)managed.blockAccessor.GetChunkAtBlockPos(pos.x, pos.y, pos.z);
					if(chunk != null)
					{
						blockId = chunkGetOneBlockData(chunk.Data, out light, out lightSat, out fluidId,
						   MapUtil.Index3d(pos.x & chunkSizeMask, pos.y & chunkSizeMask, pos.z & chunkSizeMask, chunkSize, chunkSize));
						managed.currentChunkBlocksExt[i] = managed.blockAccessor.GetBlock(blockId);
						managed.currentChunkFluidBlocksExt[i] = managed.blockAccessor.GetBlock(fluidId);
						currentChunkRgbsExt[i] = managed.lightConverter.ToRgba(light, lightSat);
					}
					else
					{
						managed.currentChunkFluidBlocksExt[i] = managed.blockAccessor.GetBlock(0);
						managed.currentChunkBlocksExt[i] = managed.blockAccessor.GetBlock(0);
						currentChunkRgbsExt[i] = managed.lightConverter.ToRgba(248u, 0);
					}
				}

				managed.block = managed.currentChunkBlocksExt[CENTER];
				blockLightAbsorption = managed.block.LightAbsorption;

				lightRGBindex = 0;
				totalLight = 0L;
				for(tileSide = 0; tileSide < 6; tileSide++)
				{
					face = BlockFacing.ALLFACES[tileSide];
					totalLight += CalcBlockFaceLight(managed, MapUtil.Index3d(face.Normali.X, face.Normali.Y, face.Normali.Z, 3, 3) + CENTER);
					outLightRGB[lightRGBindex++] = currentLightRGBByCorner[0];
					outLightRGB[lightRGBindex++] = currentLightRGBByCorner[1];
					outLightRGB[lightRGBindex++] = currentLightRGBByCorner[2];
					outLightRGB[lightRGBindex++] = currentLightRGBByCorner[3];
				}
				return totalLight + (outLightRGB[24] = currentChunkRgbsExt[CENTER]);
			}

			int colorTopLeft, colorTopRight, colorBottomLeft, colorBottomRight, baseIndex;
			int2 axesByFacing;
			int4 indexes;
			public void SetVerticesFace(int i)
			{
				axesByFacing = axesByFacingLookup[i];
				indexes = indexesByFacingLookup[i];
				baseIndex = i * 4;
				colorTopLeft = outLightRGB[baseIndex + indexes[0]];
				colorTopRight = outLightRGB[baseIndex + indexes[1]];
				colorBottomLeft = outLightRGB[baseIndex + indexes[2]];
				colorBottomRight = outLightRGB[baseIndex + indexes[3]];
			}

			float lx, ly;
			public int GetVertexColor(float3 vertPos)
			{
				lx = GameMath.Clamp(vertPos[axesByFacing[0]], 0f, 1f);
				ly = GameMath.Clamp(vertPos[axesByFacing[1]], 0f, 1f);
				return GameMath.BiLerpRgbaColor(lx, ly, colorTopLeft, colorTopRight, colorBottomLeft, colorBottomRight);
			}

			public int GetAverageColor()
			{
				return outLightRGB[24];
			}

			//locals(some shared with CornerAoRGB):
			int rgb, absorption, rgbExt, newSunLight, hsv, oldV, v, newR, newG, newB, blockRGB, neighbourLighter, i, dirExtIndex3d;
			bool thisIsALeaf, ao;
			float frontAo;
			ushort sunLight, r, g, b;
			int doBottomRight, doBottomLeft, doTopRight, doTopLeft, doRight, doLeft, doBottom, doTop;
			private long CalcBlockFaceLight(BlockLightUtil managed, int extNeibIndex3d)
			{
				Block block = managed.block;

				if(!ClientSettings.SmoothShadows || !block.SideAo[tileSide])
				{
					rgb = currentChunkRgbsExt[extNeibIndex3d];
					if(block.DrawType == EnumDrawType.JSON && !block.SideAo[tileSide])
					{
						absorption = (int)(GameMath.Clamp(((float?)managed.currentChunkBlocksExt[extNeibIndex3d]?.LightAbsorption / 32f).GetValueOrDefault(), 0f, 1f) * 255f);
						rgbExt = currentChunkRgbsExt[CENTER];
						newSunLight = Math.Max((byte)(rgbExt >> 24), (byte)(rgb >> 24) - absorption);
						hsv = ColorUtil.Rgb2HSv(rgb);
						oldV = hsv & 0xFF;
						v = Math.Max(0, oldV - absorption);
						if(v != oldV)
						{
							hsv = (hsv & 0xFFFF00) | v;
							rgb = ColorUtil.Hsv2Rgb(hsv);
						}
						newR = Math.Max((byte)(rgbExt >> 16), (byte)(rgb >> 16));
						newG = Math.Max((byte)(rgbExt >> 8), (byte)(rgb >> 8));
						newB = Math.Max((byte)rgbExt, (byte)rgb);
						rgb = (newSunLight << 24) | (newR << 16) | (newG << 8) | newB;
					}
					currentLightRGBByCorner = rgb;
					return rgb * 4;
				}
				var vNeighbors = CubeFaceVertices.blockFaceVerticesCentered[tileSide];
				blockRGB = currentChunkRgbsExt[extNeibIndex3d];
				thisIsALeaf = block.BlockMaterial == EnumBlockMaterial.Leaves;
				ao = managed.currentChunkFluidBlocksExt[extNeibIndex3d].LightAbsorption > 0 || managed.currentChunkBlocksExt[extNeibIndex3d].DoEmitSideAo(managed.geometryTester, BlockFacing.ALLFACES[tileSide].Opposite);
				frontAo = (ao ? occ : 1f);
				neighbourLighter = 0;
				i = 0;
				while(i < 8)
				{
					neighbourLighter <<= 1;
					var neibOffset = vNeighbors[i];
					dirExtIndex3d = MapUtil.Index3d(neibOffset.X, neibOffset.Y, neibOffset.Z, 3, 3) + CENTER;
					var nblock = managed.currentChunkFluidBlocksExt[dirExtIndex3d];
					if(nblock.LightAbsorption > 0)
					{
						ao = false;
					}
					else
					{
						nblock = managed.currentChunkBlocksExt[dirExtIndex3d];
						ao = nblock.DoEmitSideAoByFlag(managed.geometryTester, neibOffset) || (thisIsALeaf && nblock.BlockMaterial == EnumBlockMaterial.Leaves);
					}
					i++;
					if(ao)
					{
						neighbourLightRGBS[i] = blockRGB;
						continue;
					}
					neighbourLighter |= 1;
					neighbourLightRGBS[i] = currentChunkRgbsExt[dirExtIndex3d];
				}
				doBottomRight = 8 * (neighbourLighter & 1);
				doBottomLeft = 7 * ((neighbourLighter >>= 1) & 1);
				doTopRight = 6 * ((neighbourLighter >>= 1) & 1);
				doTopLeft = 5 * ((neighbourLighter >>= 1) & 1);
				doRight = 4 * ((neighbourLighter >>= 1) & 1);
				doLeft = 3 * ((neighbourLighter >>= 1) & 1);
				doBottom = neighbourLighter & 2;
				doTop = neighbourLighter >> 2;
				sunLight = (byte)(blockRGB >> 24);
				r = (byte)(blockRGB >> 16);
				g = (byte)(blockRGB >> 8);
				b = (byte)blockRGB;
				currentLightRGBByCorner[0] = CornerAoRGB(doTop, doLeft, doTopLeft);
				sunLight = (byte)(blockRGB >> 24);
				r = (byte)(blockRGB >> 16);
				g = (byte)(blockRGB >> 8);
				b = (byte)blockRGB;
				currentLightRGBByCorner[1] = CornerAoRGB(doTop, doRight, doTopRight);
				sunLight = (byte)(blockRGB >> 24);
				r = (byte)(blockRGB >> 16);
				g = (byte)(blockRGB >> 8);
				b = (byte)blockRGB;
				currentLightRGBByCorner[2] = CornerAoRGB(doBottom, doLeft, doBottomLeft);
				sunLight = (byte)(blockRGB >> 24);
				r = (byte)(blockRGB >> 16);
				g = (byte)(blockRGB >> 8);
				b = (byte)blockRGB;
				currentLightRGBByCorner[3] = CornerAoRGB(doBottom, doRight, doBottomRight);
				return (long)currentLightRGBByCorner[0] + (long)currentLightRGBByCorner[1] + (long)currentLightRGBByCorner[2] + (long)currentLightRGBByCorner[3];
			}

			//locals:
			float cornerAO;
			int facesconsidered, blockLightAbsorption;
			private int CornerAoRGB(int ndir1, int ndir2, int ndirbetween)
			{
				if(ndir1 + ndir2 == 0)
				{
					cornerAO = Math.Min(occ, 1f - (halfoccInverted * (float)GameMath.Clamp(blockLightAbsorption, 0, 32)));
				}
				else
				{
					cornerAO = ((ndir1 * ndir2 * ndirbetween == 0) ? occ : frontAo);
					facesconsidered = 1;
					if(ndir1 > 0)
					{
						blockRGB = neighbourLightRGBS[ndir1];
						sunLight = (ushort)(sunLight + (byte)(blockRGB >> 24));
						r = (ushort)(r + (byte)(blockRGB >> 16));
						g = (ushort)(g + (byte)(blockRGB >> 8));
						b = (ushort)(b + (byte)blockRGB);
						facesconsidered++;
					}
					if(ndir2 > 0)
					{
						blockRGB = neighbourLightRGBS[ndir2];
						sunLight = (ushort)(sunLight + (byte)(blockRGB >> 24));
						r = (ushort)(r + (byte)(blockRGB >> 16));
						g = (ushort)(g + (byte)(blockRGB >> 8));
						b = (ushort)(b + (byte)blockRGB);
						facesconsidered++;
					}
					if(ndirbetween > 0)
					{
						blockRGB = neighbourLightRGBS[ndirbetween];
						sunLight = (ushort)(sunLight + (byte)(blockRGB >> 24));
						r = (ushort)(r + (byte)(blockRGB >> 16));
						g = (ushort)(g + (byte)(blockRGB >> 8));
						b = (ushort)(b + (byte)blockRGB);
						facesconsidered++;
					}
					cornerAO /= (float)facesconsidered;
				}
				return ((int)((float)(int)sunLight * cornerAO) << 24) | ((int)((float)(int)r * cornerAO) << 16) | ((int)((float)(int)g * cornerAO) << 8) | (int)((float)(int)b * cornerAO);
			}
		}
	}
}