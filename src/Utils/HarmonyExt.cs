using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;

namespace PowerOfMind.Utils
{
	public static class HarmonyExt
	{
		private static readonly Dictionary<OpCode, Func<CodeInstruction, IList<LocalVariableInfo>, LocalVariableInfo>> loadVarCodes = new() {
			{OpCodes.Ldloc_0, (inst, list) => list[0]},
			{OpCodes.Ldloc_1, (inst, list) => list[1]},
			{OpCodes.Ldloc_2, (inst, list) => list[2]},
			{OpCodes.Ldloc_3, (inst, list) => list[3]},
			{OpCodes.Ldloc, (inst, list) => list[(inst.operand as LocalBuilder)?.LocalIndex ?? Convert.ToInt32(inst.operand)]},
			{OpCodes.Ldloca, (inst, list) => list[(inst.operand as LocalBuilder)?.LocalIndex ?? Convert.ToInt32(inst.operand)]},
			{OpCodes.Ldloc_S, (inst, list) => list[(inst.operand as LocalBuilder)?.LocalIndex ?? Convert.ToInt32(inst.operand)]},
			{OpCodes.Ldloca_S, (inst, list) => list[(inst.operand as LocalBuilder)?.LocalIndex ?? Convert.ToInt32(inst.operand)]}
		};

		public static HarmonyMethod GetHarmonyMethod(LambdaExpression expression)
		{
			return new HarmonyMethod(SymbolExtensions.GetMethodInfo(expression));
		}

		public static HarmonyMethod GetHarmonyMethod<T>(string name)
		{
			return new HarmonyMethod(typeof(T).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
		}

		public static MethodInfo GetMethodInfo<T>(string name, BindingFlags flags)
		{
			return typeof(T).GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | flags);
		}

		public static bool IsLdloc(this CodeInstruction inst, IList<LocalVariableInfo> locals, Type type)
		{
			if(loadVarCodes.TryGetValue(inst.opcode, out var getLoc))
			{
				return getLoc(inst, locals).LocalType.Equals(type);
			}
			return false;
		}

		public static bool TryGetLdloc(this CodeInstruction inst, IList<LocalVariableInfo> locals, Type type, out LocalVariableInfo local)
		{
			if(loadVarCodes.TryGetValue(inst.opcode, out var getLoc))
			{
				local = getLoc(inst, locals);
				return local.LocalType.Equals(type);
			}

			local = null;
			return false;
		}

		public static bool Match(this CodeMatcher matcher, params Func<CodeInstruction, bool>[] matches)
		{
			while(matcher.IsValid)
			{
				bool prevFound = false;
				int offset = 0;
				for(int i = 1; i < matches.Length; i++)
				{
					while(matcher.Pos + offset < matcher.Length)
					{
						var inst = matcher.InstructionAt(offset);
						offset++;

						if(prevFound && matches[i](inst))
						{
							break;
						}
						else
						{
							prevFound |= matches[i - 1](inst);
						}
					}
				}
				if(matcher.Pos + offset < matcher.Length)
				{
					return true;
				}
				matcher.Advance(1);
			}
			return false;
		}
	}
}