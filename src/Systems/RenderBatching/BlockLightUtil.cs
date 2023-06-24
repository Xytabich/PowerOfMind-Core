using System;
using System.Linq.Expressions;
using System.Reflection;
using Unity.Mathematics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;
using VintagestoryAPI.Math.Vector;

namespace PowerOfMind.Systems.RenderBatching
{
	public unsafe ref struct BlockLightUtil
	{
		private delegate int GetOneBlockData(IChunkBlocks data, out ushort lightOut, out int lightSatOut, out int fluidBlockId, int index3d);

		private const int CENTER = 13;

		private const float occ = 0.67f;
		private const float halfoccInverted = 0.0196875f;

		private static readonly int3[] blockOffsets;

		private static readonly GetOneBlockData chunkGetOneBlockData;

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
			for(int i = 0; i < 6; i++)
			{
				var faceOffsets = CubeFaceVertices.blockFaceVerticesCentered[i];
				for(int j = 0; j < 9; j++)
				{
					var offset = faceOffsets[j];
					blockOffsets[j] = new int3(offset.X, offset.Y, offset.Z);
				}
			}
		}

		public fixed int outLightRGB[25];

		private readonly Block[] currentChunkFluidBlocksExt;
		private readonly Block[] currentChunkBlocksExt;
		private fixed int currentChunkRgbsExt[27];
		private fixed int neighbourLightRGBS[9];

		private Block block;
		private int4 currentLightRGBByCorner;

		private IBlockAccessor blockAccessor;
		private IGeometryTester geometryTester;
		private ColorUtil.LightUtil lightConverter;
		private int chunkSize, chunkSizeMask;

		public BlockLightUtil(int _) : this()
		{
			currentChunkFluidBlocksExt = new Block[27];
			currentChunkBlocksExt = new Block[27];
		}

		public void Init(IBlockAccessor blockAccessor, IGeometryTester geometryTester, ColorUtil.LightUtil lightConverter)
		{
			chunkSize = blockAccessor.ChunkSize;
			chunkSizeMask = chunkSize - 1;

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

		//locals:
		ClientChunk chunk;
		int blockId, lightSat, fluidId, lightRGBindex, tileSide;
		ushort light;
		long totalLight;
		BlockFacing face;
		int3 pos;
		public long SetUpLightRGBs(int3 blockPos)
		{
			for(int i = 0; i < 27; i++)
			{
				pos = blockOffsets[i] + blockPos;
				chunk = (ClientChunk)blockAccessor.GetChunkAtBlockPos(pos.x, pos.y, pos.z);
				if(chunk != null)
				{
					blockId = chunkGetOneBlockData(chunk.Data, out light, out lightSat, out fluidId,
					   MapUtil.Index3d(pos.x & chunkSizeMask, pos.y & chunkSizeMask, pos.z & chunkSizeMask, chunkSize, chunkSize));
					currentChunkBlocksExt[i] = blockAccessor.GetBlock(blockId);
					currentChunkFluidBlocksExt[i] = blockAccessor.GetBlock(fluidId);
					currentChunkRgbsExt[i] = lightConverter.ToRgba(light, lightSat);
				}
				else
				{
					currentChunkFluidBlocksExt[i] = blockAccessor.GetBlock(0);
					currentChunkBlocksExt[i] = blockAccessor.GetBlock(0);
					currentChunkRgbsExt[i] = lightConverter.ToRgba(248u, 0);
				}
			}

			lightRGBindex = 0;
			totalLight = 0L;
			block = currentChunkBlocksExt[CENTER];
			for(tileSide = 0; tileSide < 6; tileSide++)
			{
				face = BlockFacing.ALLFACES[tileSide];
				totalLight += CalcBlockFaceLight(tileSide, MapUtil.Index3d(face.Normali.X, face.Normali.Y, face.Normali.Z, 3, 3));
				outLightRGB[lightRGBindex++] = currentLightRGBByCorner[0];
				outLightRGB[lightRGBindex++] = currentLightRGBByCorner[1];
				outLightRGB[lightRGBindex++] = currentLightRGBByCorner[2];
				outLightRGB[lightRGBindex++] = currentLightRGBByCorner[3];
			}
			return totalLight + (outLightRGB[24] = currentChunkRgbsExt[CENTER]);
		}

		//locals(some shared with CornerAoRGB):
		int rgb, absorption, rgbExt, newSunLight, hsv, oldV, v, newR, newG, newB, blockRGB, neighbourLighter, i, dirExtIndex3d;
		bool thisIsALeaf, ao;
		float frontAo;
		Vec3iAndFacingFlags[] vNeighbors;
		Vec3iAndFacingFlags neibOffset;
		Block nblock;
		ushort sunLight, r, g, b;
		int doBottomRight, doBottomLeft, doTopRight, doTopLeft, doRight, doLeft, doBottom, doTop;
		private long CalcBlockFaceLight(int tileSide, int extNeibIndex3d)
		{
			if(!ClientSettings.SmoothShadows || !block.SideAo[tileSide])
			{
				rgb = currentChunkRgbsExt[extNeibIndex3d];
				if(block.DrawType == EnumDrawType.JSON && !block.SideAo[tileSide])
				{
					absorption = (int)(GameMath.Clamp(((float?)currentChunkBlocksExt[extNeibIndex3d]?.LightAbsorption / 32f).GetValueOrDefault(), 0f, 1f) * 255f);
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
			vNeighbors = CubeFaceVertices.blockFaceVerticesCentered[tileSide];
			blockRGB = currentChunkRgbsExt[extNeibIndex3d];
			thisIsALeaf = block.BlockMaterial == EnumBlockMaterial.Leaves;
			ao = currentChunkFluidBlocksExt[extNeibIndex3d].LightAbsorption > 0 || currentChunkBlocksExt[extNeibIndex3d].DoEmitSideAo(geometryTester, BlockFacing.ALLFACES[tileSide].Opposite);
			frontAo = (ao ? occ : 1f);
			neighbourLighter = 0;
			i = 0;
			while(i < 8)
			{
				neighbourLighter <<= 1;
				neibOffset = vNeighbors[i];
				dirExtIndex3d = CENTER + MapUtil.Index3d(neibOffset.X, neibOffset.Y, neibOffset.Z, 3, 3);
				nblock = currentChunkFluidBlocksExt[dirExtIndex3d];
				if(nblock.LightAbsorption > 0)
				{
					ao = false;
				}
				else
				{
					nblock = currentChunkBlocksExt[dirExtIndex3d];
					ao = nblock.DoEmitSideAoByFlag(geometryTester, neibOffset) || (thisIsALeaf && nblock.BlockMaterial == EnumBlockMaterial.Leaves);
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
			return (long)(currentLightRGBByCorner[0] = CornerAoRGB(doTop, doLeft, doTopLeft)) + (long)(currentLightRGBByCorner[1] = CornerAoRGB(doTop, doRight, doTopRight)) + (currentLightRGBByCorner[2] = CornerAoRGB(doBottom, doLeft, doBottomLeft)) + (currentLightRGBByCorner[3] = CornerAoRGB(doBottom, doRight, doBottomRight));
		}

		//locals:
		float cornerAO;
		int facesconsidered;
		private int CornerAoRGB(int ndir1, int ndir2, int ndirbetween)
		{
			if(ndir1 + ndir2 == 0)
			{
				cornerAO = Math.Min(occ, 1f - (halfoccInverted * (float)GameMath.Clamp(block.LightAbsorption, 0, 32)));
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