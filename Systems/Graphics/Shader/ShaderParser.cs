using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PowerOfMind.Graphics.Shader
{
	public static partial class ShaderParser
	{
		/// <summary>
		/// Provides source code for the given source id.
		/// </summary>
		/// <param name="sourceContent">Shader source code</param>
		/// <param name="isGenericSource">
		/// Must be set to true if the source is generic, i.e. it will not be split into shader blocks.
		/// Otherwise, the source will be divided and used block by block.
		/// I.e. if the include was in a vertex block, then the code from the source vertex block will be taken.
		/// If the include was in the global environment, then the code will be copied block by block.
		/// </param>
		public delegate void SourceCodeProvider(int sourceId, out string sourceContent, out bool isGenericSource);

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
			//TODO: so main can be also generic, so need to add ShaderType.Generic to enum, and return as single block with ShaderType.Generic type
		}

		private static void ProcessShaderBlockAndRemoveComments(string source, List<Token> tokens, TokenSourceRef start, List<TokenRange> outRanges, List<FieldInfo> outInputs, List<FieldInfo> outUniforms)
		{
			//TODO: same as ExtractBlockAndRemoveComments but also should handle includes, aliases & etc. by the way, aliases should look like:
			//[POSITION]//doesn't matter on new line or same line
			//layout(location = 2) in vec3 pos;

			ProcessBlockTokens(tokens, start,
				s => ShaderParserHelpers.SkipVertexLayout(source, tokens, s),
				s => ShaderParserHelpers.SkipBlock(tokens, s),
				s => ShaderParserHelpers.CollectUniform(source, tokens, s, outUniforms),
				s => ShaderParserHelpers.CollectInput(source, tokens, s, outInputs)
			);
		}

		private static TokenSourceRef ProcessBlockTokens(List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] processors)
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
					start = new TokenSourceRef(start.index + 1, 0, start.sourceOffset + (tokens[start.index].size - start.offset));
				}
			}

			return start;
		}

		private static List<Token> TokenizeSource(string source)
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
					case '{':
						currentType = TokenType.BlockOpen;
						currentSize = 1;
						break;
					case '}':
						currentType = TokenType.BlockClose;
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
					currentSize = 0;
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
		private readonly struct Token
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
		private readonly struct TokenRef
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
		private readonly struct TokenSourceRef
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
		}

		[StructLayout(LayoutKind.Auto, Pack = 4)]
		private readonly struct TokenRange
		{
			public readonly TokenRef start;
			public readonly TokenRef end;
			public readonly int sourceOffset;

			public TokenRange(TokenRef start, TokenRef end, int sourceOffset)
			{
				this.start = start;
				this.end = end;
				this.sourceOffset = sourceOffset;
			}
		}

		private enum TokenType
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
			Hash
		}

		private struct FieldInfo
		{
			public string name;
			public string alias;
			public string typeName;
		}

		private delegate TokenSourceRef? TryProcessTokensDelegate(TokenSourceRef start);

		public enum ParseFlags
		{
			RemoveComments,
			AddSourceLines
		}
	}
}