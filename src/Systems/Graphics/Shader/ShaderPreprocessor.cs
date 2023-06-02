using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace PowerOfMind.Graphics.Shader
{
	public class ShaderPreprocessor
	{
		private static readonly HashSet<char> invalidPathCharacters = new HashSet<char>(Path.GetInvalidFileNameChars().Union(Path.GetInvalidPathChars()).Distinct());

		private readonly GraphicsSystem graphics;

		private StringBuilder sb = null;
		private HashSet<int> processedSources = null;

		internal Dictionary<string, string> inputAliasMap = null;
		internal Dictionary<string, string> uniformAliasMap = null;
		internal Dictionary<AssetLocation, int> locationToId = null;
		internal List<KeyValuePair<AssetLocation, string>> sources = null;

		internal ShaderPreprocessor(GraphicsSystem graphics)
		{
			this.graphics = graphics;
		}

		/// <summary>
		/// Preprocess shader code:
		///- Adds preprocessor definitions
		///- Includes sources
		///- Changes the shader version for the current platform
		///- Collects inputs and uniforms
		/// </summary>
		/// <param name="getSourceId">Should return a unique id for inclusion. The main source has id 0</param>
		/// <param name="getSourceCode">Returns the source code for the given id. The main source has id 0</param>
		/// <param name="outInputAliasMap">Dictionary where input aliases will be written. Can be null if no aliases need to be looked up</param>
		/// <param name="outUniformAliasMap">Dictionary where uniform aliases will be written. Can be null if no aliases need to be looked up</param>
		/// <param name="shaderDefinitionsProvider">Provider of custom definitions that will be added after the version and standard definitions</param>
		/// <returns>Prepared shader code</returns>
		public string PreprocessShaderCode(EnumShaderType type, SourceIdProvider getSourceId, SourceCodeProvider getSourceCode,
			out string version, IDictionary<string, string> outInputAliasMap = null, IDictionary<string, string> outUniformAliasMap = null,
			System.Func<EnumShaderType, string> shaderDefinitionsProvider = null)
		{
			var code = getSourceCode(0);
			int length = code.Length;

			if(sb == null) sb = new StringBuilder();
			else sb.Clear();
			if(processedSources == null) processedSources = new HashSet<int>();
			else processedSources.Clear();

			version = null;

			int index = 0;
			while(index < length)
			{
				if(!char.IsWhiteSpace(code, index))
				{
					if(code[index] == '#')
					{
						if(IsWordWithSpace(code, index + 1, "version"))
						{
							index += 8;
							while(index < length)
							{
								if(char.IsWhiteSpace(code, index))
								{
									if(char.IsControl(code, index))
									{
										throw new Exception("Invalid version format");
									}
								}
								else break;
								index++;
							}
							int numberSize = 0;
							while(index < length)
							{
								if(!char.IsNumber(code, index))
								{
									break;
								}
								numberSize++;
								index++;
							}
							if(numberSize == 0)
							{
								throw new Exception("Invalid version format");
							}
							sb.Append(code, 0, index - numberSize);
							version = graphics.GetPlatformShaderVersion(code.Substring(index - numberSize, numberSize));
							sb.Append(version);

							int fromIndex = index;
							GoToNewLine(code, ref index);
							sb.Append(code, fromIndex, index - fromIndex);
							sb.AppendLine();
							break;
						}
					}
					index = 0;
					break;
				}
				index++;
			}

			if(type == EnumShaderType.FragmentShader)
			{
				sb.AppendLine(graphics.FragmentShaderDefines);
			}
			else if(type == EnumShaderType.VertexShader)
			{
				sb.AppendLine(graphics.VertexShaderDefines);
			}

			string defs = shaderDefinitionsProvider?.Invoke(type);
			if(!string.IsNullOrEmpty(defs))
			{
				sb.AppendLine(defs);
			}

			PreprocessShaderCode(0, index, getSourceId, getSourceCode, outInputAliasMap, outUniformAliasMap);

			return sb.ToString();
		}

		public void ClearCache()
		{
			sb = null;
			processedSources = null;
			uniformAliasMap = null;
			inputAliasMap = null;
			locationToId = null;
			sources = null;
		}

		internal string PreprocessShaderAsset(EnumShaderType type, AssetLocation location, IAsset asset, System.Func<EnumShaderType, string> shaderDefinitionsProvider, out string version)
		{
			if(inputAliasMap == null) inputAliasMap = new Dictionary<string, string>();
			else inputAliasMap.Clear();
			if(uniformAliasMap == null) uniformAliasMap = new Dictionary<string, string>();
			else uniformAliasMap.Clear();
			if(locationToId == null) locationToId = new Dictionary<AssetLocation, int>();
			else locationToId.Clear();
			if(sources == null) sources = new List<KeyValuePair<AssetLocation, string>>();
			else sources.Clear();

			location = location.Clone();
			locationToId[location] = 0;
			sources.Add(new KeyValuePair<AssetLocation, string>(location, asset.ToText()));
			var code = PreprocessShaderCode(type, (refId, file) => {
				var refLoc = sources[refId].Key;
				var baseLoc = new AssetLocation(file);
				bool hasDomain = baseLoc.HasDomain();
				int id;
				AssetLocation loc;
				if(!hasDomain)
				{
					baseLoc.Domain = refLoc.Domain;
					loc = baseLoc.Clone().WithPathPrefixOnce(AssetShaderProgram.SHADERS_LOC);
					if(locationToId.TryGetValue(loc, out id)) return id;
					loc = baseLoc.Clone().WithPathPrefixOnce(AssetShaderProgram.SHADERINCLUDES_LOC);
					if(locationToId.TryGetValue(loc, out id)) return id;

					baseLoc.Domain = null;
				}
				loc = baseLoc.Clone().WithPathPrefixOnce(AssetShaderProgram.SHADERS_LOC);
				if(locationToId.TryGetValue(loc, out id)) return id;
				loc = baseLoc.Clone().WithPathPrefixOnce(AssetShaderProgram.SHADERINCLUDES_LOC);
				if(locationToId.TryGetValue(loc, out id)) return id;

				var assets = graphics.Api.Assets;
				IAsset subAsset;
				if(!hasDomain)
				{
					baseLoc.Domain = refLoc.Domain;
					loc = baseLoc.Clone().WithPathPrefixOnce(AssetShaderProgram.SHADERS_LOC);
					if((subAsset = assets.TryGet(loc, loadAsset: true)) != null)
					{
						id = sources.Count;
						sources.Add(new KeyValuePair<AssetLocation, string>(loc, subAsset.ToText()));
						locationToId[loc] = id;
						return id;
					}
					loc = baseLoc.Clone().WithPathPrefixOnce(AssetShaderProgram.SHADERINCLUDES_LOC);
					if((subAsset = assets.TryGet(loc, loadAsset: true)) != null)
					{
						id = sources.Count;
						sources.Add(new KeyValuePair<AssetLocation, string>(loc, subAsset.ToText()));
						locationToId[loc] = id;
						return id;
					}

					baseLoc.Domain = null;
				}
				loc = baseLoc.Clone().WithPathPrefixOnce(AssetShaderProgram.SHADERS_LOC);
				if((subAsset = assets.TryGet(loc, loadAsset: true)) != null)
				{
					id = sources.Count;
					sources.Add(new KeyValuePair<AssetLocation, string>(loc, subAsset.ToText()));
					locationToId[loc] = id;
					return id;
				}
				loc = baseLoc.Clone().WithPathPrefixOnce(AssetShaderProgram.SHADERINCLUDES_LOC);
				if((subAsset = assets.TryGet(loc, loadAsset: true)) != null)
				{
					id = sources.Count;
					sources.Add(new KeyValuePair<AssetLocation, string>(loc, subAsset.ToText()));
					locationToId[loc] = id;
					return id;
				}

				throw new FileNotFoundException(string.Format("Shader include '{0}' not found", file));
			}, id => sources[id].Value, out version, type == EnumShaderType.VertexShader ? inputAliasMap : null, uniformAliasMap, shaderDefinitionsProvider);

			sources.Clear();

			return code;
		}

		private void PreprocessShaderCode(int id, int index, SourceIdProvider getSourceId, SourceCodeProvider getSourceCode,
			IDictionary<string, string> outInputAliasMap, IDictionary<string, string> outUniformAliasMap)
		{
			if(!processedSources.Add(id)) return;

			var code = getSourceCode(id);
			int length = code.Length;

			int fromIndex = index;
			index = code.IndexOf('#', index);
			while(index >= 0)
			{
				if(IsWordWithSpace(code, index + 1, "include"))
				{
					if(!IsCommented(code, index - 1) && IsLine(code, index - 1))
					{
						sb.Append(code, fromIndex, index - fromIndex);

						int start = index;
						index += 8;
						SkipWhitespace(code, ref index);
						var name = ExtractInclude(code, ref index);
						int nextId = getSourceId(id, name.Trim());
						PreprocessShaderCode(nextId, 0, getSourceId, getSourceCode, outInputAliasMap, outUniformAliasMap);
						sb.AppendLine();

						fromIndex = index;
					}
					else index += 9;
				}
				else if((outInputAliasMap != null || outUniformAliasMap != null) && IsWordWithSpace(code, index + 1, "alias"))
				{
					if(IsCommented(code, index - 1, true))
					{
						index += 6;
						SkipWhitespace(code, ref index);
						var alias = ExtractIdentifier(code, ref index);
						GoToNewLine(code, ref index);
						SkipWhitespace(code, ref index);
						SkipLayoutDefinition(code, ref index);
						SkipWhitespace(code, ref index);
						if(IsWordWithSpace(code, index, "in"))
						{
							index += 2;
							SkipWhitespace(code, ref index);
							SkipIdentifier(code, ref index);
							SkipWhitespace(code, ref index);
							if(outInputAliasMap != null)
							{
								outInputAliasMap[ExtractIdentifier(code, ref index)] = alias;
							}
							else
							{
								SkipIdentifier(code, ref index);
							}
						}
						else if(IsWordWithSpace(code, index, "uniform"))
						{
							index += 7;
							SkipWhitespace(code, ref index);
							SkipIdentifier(code, ref index);
							SkipWhitespace(code, ref index);
							if(outUniformAliasMap != null)
							{
								outUniformAliasMap[ExtractIdentifier(code, ref index)] = alias;
							}
							else
							{
								SkipIdentifier(code, ref index);
							}
						}
					}
					else index += 7;
				}
				else index++;
				index = code.IndexOf('#', index);
			}

			sb.Append(code, fromIndex, length - fromIndex);
		}

		private static bool IsCommented(string code, int index, bool checkIsLine = false)
		{
			while(index >= 0)
			{
				if(char.IsWhiteSpace(code, index))
				{
					if(char.IsControl(code, index)) return false;
				}
				else
				{
					if((code[index] == '/' || code[index] == '*') && index > 0 && code[index - 1] == '/')//Check only line comments, because block comments are expensive and difficult to check
					{
						if(checkIsLine)
						{
							return IsLine(code, index - 2);
						}
						return true;
					}
					break;
				}
			}
			return false;
		}

		private static bool IsLine(string code, int index)
		{
			while(index >= 0)
			{
				if(char.IsWhiteSpace(code, index))
				{
					if(char.IsControl(code, index)) return true;
				}
				else return false;
			}
			return true;
		}

		private static bool IsWordWithSpace(string code, int index, string word)
		{
			if(string.Compare(code, index, word, 0, word.Length, StringComparison.InvariantCultureIgnoreCase) == 0)
			{
				index += word.Length;
				return index < code.Length && char.IsWhiteSpace(code, index) && !char.IsControl(code, index);
			}
			return false;
		}

		private static void SkipLayoutDefinition(string code, ref int index)
		{
			if(string.Compare(code, index, "layout", 0, 5, StringComparison.InvariantCultureIgnoreCase) == 0)
			{
				index += 5;
				int pCount = 0;
				int length = code.Length;
				while(index < length)
				{
					switch(code[index])
					{
						case '(':
							pCount++;
							index++;
							break;
						case ')':
							pCount--;
							index++;
							if(pCount == 0) return;
							break;
						case '/':
							if(!SkipComment(code, ref index))
							{
								index++;
							}
							break;
						default:
							if(char.IsSurrogate(code, index)) index += 2;
							else index++;
							break;
					}
				}
			}
		}

		private static bool SkipComment(string code, ref int index)
		{
			int length = code.Length;
			if(code[index] == '/' && index + 1 < length && (code[index] == '/' || code[index] == '*'))
			{
				index += 2;
				if(code[index - 1] == '*')
				{
					while(index < length)
					{
						switch(code[index])
						{
							case '*':
								index++;
								if(index < length && code[index] == '/')
								{
									index++;
									return true;
								}
								break;
							default:
								if(char.IsSurrogate(code, index)) index += 2;
								else index++;
								break;
						}
					}
				}
				else
				{
					while(index < length)
					{
						if(char.IsControl(code, index)) return true;

						if(char.IsSurrogate(code, index)) index += 2;
						else index++;
					}
				}
				return true;
			}
			return false;
		}

		private static void SkipIdentifier(string code, ref int index)
		{
			int length = code.Length;
			while(index < length)
			{
				if(!char.IsLetter(code, index) && !char.IsDigit(code, index) && code[index] != '_')
				{
					break;
				}
				if(char.IsSurrogate(code, index)) index += 2;
				else index++;
			}
		}

		private static string ExtractIdentifier(string code, ref int index)
		{
			int length = code.Length;
			int start = index;
			while(index < length)
			{
				if(!char.IsLetter(code, index) && !char.IsDigit(code, index) && code[index] != '_')
				{
					break;
				}
				if(char.IsSurrogate(code, index)) index += 2;
				else index++;
			}
			return code.Substring(start, index - start);
		}

		private static void SkipWhitespace(string code, ref int index)
		{
			int length = code.Length;
			while(index < length)
			{
				if(!char.IsWhiteSpace(code, index)) break;
				index++;
			}
		}

		private static void GoToNewLine(string code, ref int index)
		{
			int length = code.Length;
			while(index < length)
			{
				if(code[index] == '\n') break;
				index++;
			}
		}

		private static string ExtractInclude(string code, ref int index)
		{
			int length = code.Length;
			if(index >= length) return string.Empty;

			bool hasQuotes = false;
			if(code[index] == '"')
			{
				hasQuotes = true;
				index++;
			}
			int start = index;
			while(index < length)
			{
				switch(code[index])
				{
					case '"':
						if(hasQuotes)
						{
							hasQuotes = false;
							goto _result;
						}
						if(invalidPathCharacters.Contains(code[index]))
						{
							goto _result;
						}
						break;
					case '/':
						if(index + 1 < length && (code[index + 1] == '/' || code[index + 1] == '*'))
						{
							goto _result;
						}
						if(invalidPathCharacters.Contains(code[index]))
						{
							goto _result;
						}
						break;
					default:
						if(char.IsWhiteSpace(code, index) && char.IsControl(code, index))
						{
							goto _result;
						}
						if(invalidPathCharacters.Contains(code[index]))
						{
							goto _result;
						}
						break;
				}
				if(char.IsSurrogate(code, index)) index += 2;
				else index++;
			}
_result:
			if(hasQuotes)
			{
				throw new Exception("Missing closing quotes in include");
			}
			return code.Substring(start, index - start);
		}

		public delegate int SourceIdProvider(int refId, string includeName);

		public delegate string SourceCodeProvider(int sourceId);
	}
}