using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace PowerOfMind.Graphics.Shader
{
	public static partial class ShaderParser
	{
		/// <summary>
		/// Provides source code for the given source id.
		/// </summary>
		public delegate string SourceCodeProvider(int sourceId);

		/// <summary>
		/// Provides the unique id of the source, must be the same if requested multiple times for the same source.
		/// If the <see cref="ParseFlags.AddSourceLines"/> flag is specified, it will be specified as the debug source.
		/// </summary>
		/// <param name="referenceId">The id of the source where this <paramref name="sourceName"/> was found</param>
		/// <param name="sourceName">Shader source name or path whose source code is to be provided</param>
		/// <returns><see langword="true"/> if the source is available</returns>
		public delegate bool SourceIdProvider(int referenceId, string sourceName, out int sourceId);

		/// <summary>
		/// 
		/// </summary>
		/// <param name="provideSourceCode">
		/// Provides the source code for the given source id.
		/// See <see cref="SourceCodeProvider"/> for more info.
		/// Will be called with id 0 for the source code of the main shader.
		/// </param>
		/// <param name="provideSourceId">
		/// Provides the unique id of the source.
		/// See <see cref="SourceIdProvider"/> for more info.
		/// </param>
		public static void ParseShader(SourceCodeProvider provideSourceCode, SourceIdProvider provideSourceId)
		{
			var sb = new StringBuilder();
			var includedSources = new HashSet<int>();

			BuildShader(sb, 0, provideSourceCode, provideSourceId, includedSources);
		}

		private static void BuildShader(StringBuilder sb, int sourceId, SourceCodeProvider provideSourceCode, SourceIdProvider provideSourceId, HashSet<int> includedSources)
		{
			if(!includedSources.Add(sourceId)) return;

			var code = provideSourceCode(0);
			var tokens = TokenizeSource(code);
			var skipRanges = new List<TokenRange>();
			var includes = new List<TokenRange>();
			var outInputs = new List<FieldInfo>();
			var outUniforms = new List<FieldInfo>();
			ProcessShaderBlockAndRemoveComments(code, tokens, new TokenSourceRef(0, 0, 0), skipRanges, includes, outInputs, outUniforms);

			int skipIndex = 0;
			int nextSkipToken = skipRanges.Count > 0 ? skipRanges[0].start.index : -1;
			int includeIndex = 0;
			int nextIncludeToken = includes.Count > 0 ? includes[0].start.index : -1;
			int prevSourceOffset = 0;
			int sourceOffset = 0;
			for(int i = 0; i < tokens.Count; i++)
			{
				if(nextSkipToken == i)
				{
					var range = skipRanges[skipIndex];
					if(range.start.offset != 0)
					{
						sourceOffset += range.start.offset;
					}
					if(prevSourceOffset != sourceOffset)
					{
						sb.Append(code, prevSourceOffset, sourceOffset - prevSourceOffset);
					}
					sb.Append(' ');//For safety, if the block is between words

					sourceOffset += range.length;
					prevSourceOffset = sourceOffset;
					if(range.end.offset != 0)
					{
						prevSourceOffset -= tokens[range.end.index].size - range.end.offset;
					}

					skipIndex++;
					nextSkipToken = skipRanges.Count > skipIndex ? skipRanges[skipIndex].start.index : -1;

					i += range.end.index - range.start.index;
					continue;
				}
				if(nextIncludeToken == i)
				{
					var range = includes[includeIndex];
					if(range.start.offset != 0)
					{
						sourceOffset += range.start.offset;
					}
					if(prevSourceOffset != sourceOffset)
					{
						sb.Append(code, prevSourceOffset, sourceOffset - prevSourceOffset);
					}

					//TODO: include

					sourceOffset += range.length;
					prevSourceOffset = sourceOffset;
					if(range.end.offset != 0)
					{
						prevSourceOffset -= tokens[range.end.index].size - range.end.offset;
					}

					includeIndex++;
					nextIncludeToken = includes.Count > includeIndex ? includes[includeIndex].start.index : -1;

					i += range.end.index - range.start.index;
					continue;
				}
				sourceOffset += tokens[i].size;
			}
			if(prevSourceOffset != sourceOffset)
			{
				sb.Append(code, prevSourceOffset, sourceOffset - prevSourceOffset);
			}
		}

		internal static void ProcessShaderBlockAndRemoveComments(string source, List<Token> tokens, TokenSourceRef start, List<TokenRange> skipRanges, List<TokenRange> includes, List<FieldInfo> outInputs, List<FieldInfo> outUniforms)
		{
			//TODO: same as ExtractBlockAndRemoveComments but also should handle includes, aliases & etc. by the way, aliases should look like:
			//[POSITION]//doesn't matter on new line or same line
			//layout(location = 2) in vec3 pos;

			var skipComments = new TryProcessTokensDelegate[] {
				s=>ShaderParserHelpers.SkipCommentBlock(tokens, s, skipRanges),
				s=>ShaderParserHelpers.SkipCommentLine(tokens, s, skipRanges)
			};

			ProcessBlockTokens(tokens, start,//TODO: skip comments
				s => ShaderParserHelpers.ProcessMethod(tokens, s, skipComments),
				s => ShaderParserHelpers.ProcessBlock(tokens, s, skipComments),
				s => ShaderParserHelpers.CollectUniform(source, tokens, s, outUniforms, skipComments),
				s => ShaderParserHelpers.CollectInput(source, tokens, s, outInputs, skipComments),
				s => ShaderParserHelpers.CollectInclude(source, tokens, s, includes),
				s => ShaderParserHelpers.SkipCommentBlock(tokens, s, skipRanges),
				s => ShaderParserHelpers.SkipCommentLine(tokens, s, skipRanges)
			);
		}

		internal static TokenSourceRef ProcessBlockTokens(List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] processors)
		{
			while(start.index < tokens.Count)
			{
				if(tokens[start.index].type == TokenType.BlockClose) break;
				bool skip = true;
				foreach(var proc in processors)
				{
					var result = proc(start);
					if(result.HasValue)
					{
						skip = false;
						start = result.Value;
						break;
					}
				}
				if(skip)
				{
					start = start.Increment(tokens);
				}
			}

			return start;
		}

		internal static List<Token> TokenizeSource(string source)
		{
			int len = source.Length;
			var tokens = new List<Token>();

			TokenType tokenType = default;
			int tokenSize = 0;
			int index = 0;
			while(index < len)
			{
				TokenType currentType;
				int currentSize;
				switch(source[index])
				{
					case '0':
					case '1':
					case '2':
					case '3':
					case '4':
					case '5':
					case '6':
					case '7':
					case '8':
					case '9':
						currentType = TokenType.Number;
						currentSize = 1;
						break;
					case '\r':
					case '\n':
						currentType = TokenType.LineBreak;
						currentSize = 1;
						break;
					case '_':
						currentType = TokenType.Underscore;
						currentSize = 1;
						break;
					case '#':
						currentType = TokenType.Hash;
						currentSize = 1;
						break;
					case '/':
						currentType = TokenType.Slash;
						currentSize = 1;
						break;
					case '*':
						currentType = TokenType.Asterisk;
						currentSize = 1;
						break;
					case ';':
						currentType = TokenType.Semicolon;
						currentSize = 1;
						break;
					case '=':
						currentType = TokenType.Equal;
						currentSize = 1;
						break;
					case '{':
						currentType = TokenType.BlockOpen;
						currentSize = 1;
						break;
					case '}':
						currentType = TokenType.BlockClose;
						currentSize = 1;
						break;
					case '[':
						currentType = TokenType.BracketOpen;
						currentSize = 1;
						break;
					case ']':
						currentType = TokenType.BracketClose;
						currentSize = 1;
						break;
					case '(':
						currentType = TokenType.ParenthesesOpen;
						currentSize = 1;
						break;
					case ')':
						currentType = TokenType.ParenthesesClose;
						currentSize = 1;
						break;
					default:
						if(index + 1 < len && char.IsSurrogatePair(source, index))
						{
							currentSize = 2;
						}
						else
						{
							currentSize = 1;
						}
						if(char.IsWhiteSpace(source, index))
						{
							currentType = TokenType.Whitespace;
						}
						else if(char.IsLetter(source, index))
						{
							currentType = TokenType.Letter;
						}
						else
						{
							currentType = TokenType.Text;
						}
						break;
				}
				if(currentType != tokenType)
				{
					if(tokenSize > 0)
					{
						tokens.Add(new Token(tokenType, tokenSize));
					}
					tokenType = currentType;
					tokenSize = 0;
				}
				tokenSize += currentSize;
				index += currentSize;
			}
			if(tokenSize > 0)
			{
				tokens.Add(new Token(tokenType, tokenSize));
			}

			return tokens;
		}

		[StructLayout(LayoutKind.Auto, Pack = 4)]
		internal readonly struct Token
		{
			public readonly TokenType type;
			public readonly int size;

			public Token(TokenType type, int size)
			{
				this.type = type;
				this.size = size;
			}
		}

		[StructLayout(LayoutKind.Auto, Pack = 4)]
		internal readonly struct TokenRef
		{
			/// <summary>
			/// Index in tokens list
			/// </summary>
			public readonly int index;
			/// <summary>
			/// Internal token offset.
			/// If zero, then defined as the entire token
			/// </summary>
			public readonly int offset;

			public TokenRef(int index, int offset)
			{
				this.index = index;
				this.offset = offset;
			}
		}

		[StructLayout(LayoutKind.Auto, Pack = 4)]
		internal readonly struct TokenSourceRef
		{
			/// <summary>
			/// Index in tokens list
			/// </summary>
			public readonly int index;
			/// <summary>
			/// Internal token offset.
			/// If zero, then defined as the entire token
			/// </summary>
			public readonly int offset;
			public readonly int sourceOffset;

			public TokenSourceRef(int index, int offset, int sourceOffset)
			{
				this.index = index;
				this.offset = offset;
				this.sourceOffset = sourceOffset;
			}

			public TokenSourceRef Increment(List<Token> tokens)
			{
				if(offset + 1 < tokens[index].size)
				{
					return new TokenSourceRef(index, offset + 1, sourceOffset + 1);
				}
				return new TokenSourceRef(index + 1, 0, sourceOffset + 1);
			}

			public TokenSourceRef AddOffset(List<Token> tokens, int count)
			{
				if(tokens[index].size == offset + count)
				{
					return Step(tokens);
				}
				return new TokenSourceRef(index, offset + count, sourceOffset + count);
			}

			public TokenSourceRef Step(List<Token> tokens)
			{
				return new TokenSourceRef(index + 1, 0, sourceOffset + tokens[index].size - offset);
			}
		}

		[StructLayout(LayoutKind.Auto, Pack = 4)]
		internal readonly struct TokenRange
		{
			public readonly TokenRef start;
			public readonly TokenRef end;
			public readonly int length;

			public TokenRange(TokenRef start, TokenRef end, int length)
			{
				this.start = start;
				this.end = end;
				this.length = length;
			}
		}

		internal enum TokenType
		{
			Text,
			Letter,
			Whitespace,
			LineBreak,
			Number,
			Underscore,
			Asterisk,
			Backslach,
			Slash,
			BlockOpen,
			BlockClose,
			BracketOpen,
			BracketClose,
			ParenthesesOpen,
			ParenthesesClose,
			Hash,
			Equal,
			Semicolon
		}

		internal struct FieldInfo
		{
			public int location;
			public string name;
			public string alias;
			public string typeName;

			public FieldInfo(int location, string name, string alias, string typeName)
			{
				this.location = location;
				this.name = name;
				this.alias = alias;
				this.typeName = typeName;
			}
		}

		internal delegate TokenSourceRef? TryProcessTokensDelegate(TokenSourceRef start);

		public enum ParseFlags
		{
			RemoveComments,
			AddSourceLines
		}
	}
}