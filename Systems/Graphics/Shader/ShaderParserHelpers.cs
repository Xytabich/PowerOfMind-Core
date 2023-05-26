using System;
using System.Collections.Generic;

namespace PowerOfMind.Graphics.Shader
{
	public static partial class ShaderParser
	{
		private static class ShaderParserHelpers
		{
			public static TokenSourceRef? SkipVertexLayout(string source, List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] subProcessors)
			{
				throw new NotImplementedException();
			}

			public static TokenSourceRef? CollectUniform(string source, List<Token> tokens, TokenSourceRef start, List<FieldInfo> outList, params TryProcessTokensDelegate[] subProcessors)
			{
				throw new NotImplementedException();
			}

			public static TokenSourceRef? CollectInput(string source, List<Token> tokens, TokenSourceRef start, List<FieldInfo> outList, params TryProcessTokensDelegate[] subProcessors)
			{
				throw new NotImplementedException();
			}

			public static TokenSourceRef? SkipBlock(List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] subProcessors)
			{
				throw new NotImplementedException();
			}

			public static TokenSourceRef? SkipCommentBlock(List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] subProcessors)
			{
				throw new NotImplementedException();
			}

			public static TokenSourceRef? SkipCommentLine(List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] subProcessors)
			{
				var token = tokens[start.index];
				if(token.type == TokenType.Slash && token.size - start.offset > 1)
				{
					if(subProcessors.Length == 0)
					{
						int sourceOffset = start.sourceOffset + (token.size - start.offset);
						for(int i = start.index + 1; i < tokens.Count; i++)
						{
							sourceOffset += tokens[i].size;
							if(tokens[i].type == TokenType.LineBreak)
							{
								return new TokenSourceRef(i + 1, 0, sourceOffset);
							}
						}
						return new TokenSourceRef(tokens.Count, 0, sourceOffset);
					}
					else
					{
						if(start.offset + 2 == token.size)
						{
							start = new TokenSourceRef(start.index + 1, 0, start.sourceOffset + 2);
						}
						while(start.index < tokens.Count)
						{
							if(tokens[start.index].type == TokenType.LineBreak)
							{
								return new TokenSourceRef(start.index + 1, 0, start.sourceOffset + tokens[start.index].size);
							}
							bool skip = true;
							foreach(var proc in subProcessors)
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
				}
				return null;
			}
		}
	}
}