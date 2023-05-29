using System;
using System.Collections.Generic;

namespace PowerOfMind.Graphics.Shader
{
	public static partial class ShaderParser
	{
		private static class ShaderParserHelpers
		{
			public static TokenSourceRef? CollectInclude(string source, List<Token> tokens, TokenSourceRef start, List<KeyValuePair<TokenRange, string>> outList, params TryProcessTokensDelegate[] subProcessors)
			{
				var token = tokens[start.index];
				if(token.type == TokenType.Hash && token.size - start.offset == 1)
				{
					var from = start;
					start = start.Step(tokens);
					if(IsLetter(source, tokens, start, "include"))
					{
						start = start.Step(tokens);
						if(ProcessIntervalUntil(tokens, ref start, p => tokens[p.index].type == TokenType.Quotes, subProcessors))
						{
							start = start.Increment(tokens);
							int startOffset = start.sourceOffset;
							if(ProcessUntil(tokens, ref start, p => (tokens[p.index].type == TokenType.Quotes || tokens[p.index].type == TokenType.LineBreak), subProcessors))
							{
								if(tokens[start.index].type == TokenType.Quotes)
								{
									string include = source.Substring(startOffset, start.sourceOffset - startOffset);
									ProcessUntil(tokens, ref start, p => tokens[p.index].type == TokenType.LineBreak, subProcessors);
									outList.Add(new KeyValuePair<TokenRange, string>(new TokenRange(new TokenRef(from.index, from.offset), new TokenRef(start.index - 1, 0), start.sourceOffset - from.sourceOffset), include));
								}
							}
						}
					}
				}
				return null;
			}

			public static TokenSourceRef? CollectUniform(string source, List<Token> tokens, TokenSourceRef start, List<FieldInfo> outList, params TryProcessTokensDelegate[] subProcessors)
			{
				//[MVP_MATRIX]
				//uniform mat4 mvp;

				var position = start;
				bool hasAlias = TryTakeAlias(source, tokens, ref position, out var alias);
				if(!hasAlias) position = start;

				if(ProcessBreakUntil(tokens, ref position, p => IsLetter(source, tokens, p, "uniform"), subProcessors))
				{
					position = position.Step(tokens);
					if(ProcessIntervalUntil(tokens, ref position, p => IsIdentifierStart(tokens[p.index]), subProcessors))
					{
						if(TryExtractIdentifier(source, tokens, ref position, out var typeName))
						{
							if(ProcessIntervalUntil(tokens, ref position, p => IsIdentifierStart(tokens[p.index]), subProcessors))
							{
								if(TryExtractIdentifier(source, tokens, ref position, out var fieldName))
								{
									//There is no need to check for a semicolon as it could be an array, etc.
									outList.Add(new FieldInfo(-1, fieldName, hasAlias ? alias : null, typeName));
									return position.Increment(tokens);
								}
							}
						}
					}
				}

				return null;
			}

			public static TokenSourceRef? CollectInput(string source, List<Token> tokens, TokenSourceRef start, List<FieldInfo> outList, params TryProcessTokensDelegate[] subProcessors)
			{
				//[POSITION]
				//layout(location = 2) in vec3 pos;

				var position = start;
				bool hasAlias = TryTakeAlias(source, tokens, ref position, out var alias);
				if(!hasAlias) position = start;

				if(ProcessBreakUntil(tokens, ref position, p => IsLetter(source, tokens, p, "layout") || IsLetter(source, tokens, p, "in"), subProcessors))
				{
					int location = -1;
					if(IsLetter(source, tokens, position, "layout"))
					{
						position = position.Step(tokens);
						if(ProcessBreakUntil(tokens, ref position, p => tokens[p.index].type == TokenType.ParenthesesOpen, subProcessors))
						{
							if(tokens[position.index].size - position.offset == 1)
							{
								position = position.Step(tokens);
								if(ProcessBreakUntil(tokens, ref position, p => IsLetter(source, tokens, p, "location"), subProcessors))
								{
									position = position.Step(tokens);
									if(ProcessBreakUntil(tokens, ref position, p => tokens[p.index].type == TokenType.Equal, subProcessors))
									{
										if(tokens[position.index].size - position.offset == 1)
										{
											position = position.Step(tokens);
											if(ProcessBreakUntil(tokens, ref position, p => tokens[p.index].type == TokenType.Number, subProcessors))
											{
												location = int.Parse(source.Substring(position.sourceOffset, tokens[position.index].size - position.offset));
												position = position.Step(tokens);

												if(ProcessBreakUntil(tokens, ref position, p => tokens[p.index].type == TokenType.ParenthesesClose, subProcessors))
												{
													position = position.Increment(tokens);
												}
												else return null;
											}
											else return null;
										}
										else return null;
									}
									else return null;
								}
								else return null;
							}
							else return null;
						}
						else return null;

						ProcessBreakUntil(tokens, ref position, p => IsLetter(source, tokens, p, "in"), subProcessors);
					}

					if(IsLetter(source, tokens, position, "in"))
					{
						position = position.Step(tokens);
						if(ProcessIntervalUntil(tokens, ref position, p => IsIdentifierStart(tokens[p.index]), subProcessors))
						{
							if(TryExtractIdentifier(source, tokens, ref position, out var typeName))
							{
								if(ProcessIntervalUntil(tokens, ref position, p => IsIdentifierStart(tokens[p.index]), subProcessors))
								{
									if(TryExtractIdentifier(source, tokens, ref position, out var fieldName))
									{
										if(ProcessBreakUntil(tokens, ref position, p => tokens[p.index].type == TokenType.Semicolon, subProcessors))
										{
											outList.Add(new FieldInfo(location, fieldName, hasAlias ? alias : null, typeName));
											return position.Increment(tokens);
										}
									}
								}
							}
						}
					}
				}

				return null;
			}

			public static TokenSourceRef? ProcessMethod(List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] subProcessors)
			{
				if(TrySkipIdentifier(tokens, ref start))
				{
					if(ProcessIntervalUntil(tokens, ref start, p => IsIdentifierStart(tokens[p.index]), subProcessors))
					{
						if(TrySkipIdentifier(tokens, ref start))
						{
							if(ProcessBreakUntil(tokens, ref start, p => tokens[p.index].type == TokenType.ParenthesesOpen, subProcessors))
							{
								if(tokens[start.index].size == 1)
								{
									start = start.Step(tokens);
									if(ProcessUntil(tokens, ref start, p => tokens[p.index].type == TokenType.ParenthesesClose, subProcessors))
									{
										if(tokens[start.index].size - start.offset > 1)
										{
											return null;
										}
										start = start.Step(tokens);

										if(ProcessBreakUntil(tokens, ref start, p => tokens[p.index].type == TokenType.BlockOpen, subProcessors))
										{
											start = start.Increment(tokens);
											if(ProcessUntil(tokens, ref start, p => tokens[p.index].type == TokenType.BlockClose, subProcessors))
											{
												if(tokens[start.index].size - start.offset > 1)
												{
													return null;
												}
												return start.Step(tokens);
											}
										}
									}
								}
							}
						}
					}
				}
				return null;
			}

			public static TokenSourceRef? ProcessBlock(List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] subProcessors)
			{
				var token = tokens[start.index];
				if(token.type == TokenType.BlockOpen)
				{
					start = start.Increment(tokens);
					if(subProcessors.Length == 0)
					{
						int offset = 0;
						int depth = 0;
						int sourceOffset = start.sourceOffset;
						for(int i = start.index; i < tokens.Count; i++)
						{
							token = tokens[i];
							if(token.type == TokenType.BlockOpen)
							{
								depth += token.size - offset;
								sourceOffset += token.size - offset;
								offset = 0;
							}
							else if(token.type == TokenType.BlockClose)
							{
								if(token.size >= depth)
								{
									sourceOffset += depth;
									if(token.size == depth)
									{
										return new TokenSourceRef(i + 1, 0, sourceOffset);
									}
									else
									{
										return new TokenSourceRef(i, token.size - depth, sourceOffset);
									}
								}
								depth -= token.size;
								sourceOffset += token.size;
							}
							else
							{
								sourceOffset += token.size;
							}
						}
						return new TokenSourceRef(tokens.Count - 1, 0, sourceOffset);
					}
					else
					{
						while(start.index < tokens.Count)
						{
							if(tokens[start.index].type == TokenType.BlockClose)
							{
								return start.Increment(tokens);
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
								start = start.Increment(tokens);
							}
						}
						return start;
					}
				}
				return null;
			}

			public static TokenSourceRef? SkipCommentBlock(List<Token> tokens, TokenSourceRef start, ICollection<TokenRange> outSkipList = null)
			{
				var token = tokens[start.index];
				if(token.type == TokenType.Slash && token.size - start.offset == 1)
				{
					int sourceOffset = start.sourceOffset + (token.size - start.offset);
					if(start.index + 1 < tokens.Count && tokens[start.index + 1].type == TokenType.Asterisk)
					{
						sourceOffset += tokens[start.index + 1].size;
						if(tokens[start.index + 1].size > 1)
						{
							if(start.index + 2 < tokens.Count && tokens[start.index + 2].type == TokenType.Slash)
							{
								sourceOffset++;
								if(tokens[start.index + 2].size == 1)
								{
									outSkipList?.Add(new TokenRange(new TokenRef(start.index, start.offset), new TokenRef(start.index + 2, 0), sourceOffset - start.sourceOffset));
									return new TokenSourceRef(start.index + 3, 0, sourceOffset);
								}
								else
								{
									outSkipList?.Add(new TokenRange(new TokenRef(start.index, start.offset), new TokenRef(start.index + 2, 1), sourceOffset - start.sourceOffset));
									return new TokenSourceRef(start.index + 2, 1, sourceOffset);
								}
							}
						}
						for(int i = start.index + 2; i < tokens.Count; i++)
						{
							sourceOffset += tokens[i].size;
							if(tokens[i].type == TokenType.Asterisk)
							{
								if(i + 1 < tokens.Count && tokens[i + 1].type == TokenType.Slash)
								{
									sourceOffset++;
									if(tokens[i + 1].size == 1)
									{
										outSkipList?.Add(new TokenRange(new TokenRef(start.index, start.offset), new TokenRef(i + 1, 0), sourceOffset - start.sourceOffset));
										return new TokenSourceRef(i + 2, 0, sourceOffset);
									}
									else
									{
										outSkipList?.Add(new TokenRange(new TokenRef(start.index, start.offset), new TokenRef(i + 1, 1), sourceOffset - start.sourceOffset));
										return new TokenSourceRef(i + 1, 1, sourceOffset);
									}
								}
							}
						}
						outSkipList?.Add(new TokenRange(new TokenRef(start.index, start.offset), new TokenRef(tokens.Count - 1, 0), sourceOffset - start.sourceOffset));
						return new TokenSourceRef(tokens.Count, 0, sourceOffset);
					}
				}
				return null;
			}

			public static TokenSourceRef? SkipCommentLine(List<Token> tokens, TokenSourceRef start, ICollection<TokenRange> outSkipList = null)
			{
				var token = tokens[start.index];
				if(token.type == TokenType.Slash && token.size - start.offset > 1)
				{
					int sourceOffset = start.sourceOffset + (token.size - start.offset);
					for(int i = start.index + 1; i < tokens.Count; i++)
					{
						sourceOffset += tokens[i].size;
						if(tokens[i].type == TokenType.LineBreak)
						{
							outSkipList?.Add(new TokenRange(new TokenRef(start.index, start.offset), new TokenRef(i, 0), sourceOffset - start.sourceOffset));
							return new TokenSourceRef(i + 1, 0, sourceOffset);
						}
					}
					outSkipList?.Add(new TokenRange(new TokenRef(start.index, start.offset), new TokenRef(tokens.Count - 1, 0), sourceOffset - start.sourceOffset));
					return new TokenSourceRef(tokens.Count, 0, sourceOffset);
				}
				return null;
			}

			public static TokenSourceRef? ProcessCommentBlock(List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] subProcessors)
			{
				var token = tokens[start.index];
				if(token.type == TokenType.Slash && token.size - start.offset == 1)
				{
					start = new TokenSourceRef(start.index + 1, 1, start.sourceOffset + 2);
					if(start.index < tokens.Count && tokens[start.index].type == TokenType.Asterisk)
					{
						if(tokens[start.index].size == 1)
						{
							start = new TokenSourceRef(start.index + 1, 0, start.sourceOffset);
						}
						while(start.index < tokens.Count)
						{
							if(tokens[start.index].type == TokenType.Asterisk && tokens[start.index].size - start.offset == 1)
							{
								if(start.index + 1 < tokens.Count && tokens[start.index + 1].type == TokenType.Slash)
								{
									if(tokens[start.index + 1].size == 1)
									{
										return new TokenSourceRef(start.index + 2, 0, start.sourceOffset + 2);
									}
									else
									{
										return new TokenSourceRef(start.index + 1, 1, start.sourceOffset + 2);
									}
								}
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
								start = start.Increment(tokens);
							}
						}
						return start;
					}
				}
				return null;
			}

			public static TokenSourceRef? ProcessCommentLine(List<Token> tokens, TokenSourceRef start, params TryProcessTokensDelegate[] subProcessors)
			{
				var token = tokens[start.index];
				if(token.type == TokenType.Slash && token.size - start.offset > 1)
				{
					start = start.AddOffset(tokens, 2);
					while(start.index < tokens.Count)
					{
						if(tokens[start.index].type == TokenType.LineBreak)
						{
							return start.Step(tokens);
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
							start = start.Increment(tokens);
						}
					}
					return start;
				}
				return null;
			}

			private static bool TryTakeAlias(string source, List<Token> tokens, ref TokenSourceRef position, out string alias)
			{
				var token = tokens[position.index];
				if(token.type == TokenType.BracketOpen && token.size - position.offset == 1)
				{
					position = position.Increment(tokens);
					if(position.index < tokens.Count)
					{
						if(tokens[position.index].type == TokenType.Whitespace)
						{
							position = position.Step(tokens);
						}
						if(TryExtractIdentifier(source, tokens, ref position, out alias))
						{
							if(tokens[position.index].type == TokenType.Whitespace)
							{
								position = position.Step(tokens);
								return true;
							}
							if(tokens[position.index].type == TokenType.BracketClose)
							{
								position = position.Increment(tokens);
								return true;
							}
						}
					}
				}

				alias = null;
				return false;
			}

			private static bool TryExtractIdentifier(string source, List<Token> tokens, ref TokenSourceRef position, out string identifier)
			{
				if(IsIdentifierStart(tokens[position.index]))
				{
					int from = position.sourceOffset;
					while(position.index < tokens.Count)
					{
						switch(tokens[position.index].type)
						{
							case TokenType.Letter:
							case TokenType.Underscore:
							case TokenType.Number:
								break;
							default:
								identifier = source.Substring(from, position.sourceOffset - from);
								return true;
						}
						position = position.Step(tokens);
					}

					identifier = source.Substring(from, position.sourceOffset - from);
					return true;
				}

				identifier = null;
				return false;
			}

			private static bool TrySkipIdentifier(List<Token> tokens, ref TokenSourceRef position)
			{
				if(IsIdentifierStart(tokens[position.index]))
				{
					while(position.index < tokens.Count)
					{
						switch(tokens[position.index].type)
						{
							case TokenType.Letter:
							case TokenType.Underscore:
							case TokenType.Number:
								break;
							default: return true;
						}
						position = position.Step(tokens);
					}
					return true;
				}
				return false;
			}

			private static bool IsLetter(string source, List<Token> tokens, in TokenSourceRef position, string letter)
			{
				if(tokens[position.index].type == TokenType.Letter)
				{
					return string.CompareOrdinal(source, position.sourceOffset, letter, 0, letter.Length) == 0;
				}
				return false;
			}

			/// <summary>
			/// Skips whitespaces & line breaks & runs subprocessors.
			/// Will try to make at least one pass before returning.
			/// </summary>
			/// <returns><see langword="true"/> if at least one pass was made</returns>
			private static bool ProcessIntervalUntil(List<Token> tokens, ref TokenSourceRef position, Predicate<TokenSourceRef> predicate, params TryProcessTokensDelegate[] subProcessors)
			{
				bool isNext = false;
				while(position.index < tokens.Count)
				{
					if(isNext && predicate(position))
					{
						break;
					}

					bool skip = true;
					foreach(var proc in subProcessors)
					{
						var result = proc(position);
						if(result.HasValue)
						{
							skip = false;
							position = result.Value;
							break;
						}
					}
					if(skip)
					{
						if(tokens[position.index].type == TokenType.Whitespace || tokens[position.index].type == TokenType.LineBreak)
						{
							position = position.Step(tokens);
						}
						else break;
					}

					isNext = true;
				}
				return isNext;
			}

			/// <summary>
			/// Skips whitespaces & line breaks & runs subprocessors.
			/// But returns immediately if the predicate returns true, without additional loop passes.
			/// </summary>
			private static bool ProcessBreakUntil(List<Token> tokens, ref TokenSourceRef position, Predicate<TokenSourceRef> predicate, params TryProcessTokensDelegate[] subProcessors)
			{
				while(position.index < tokens.Count)
				{
					if(predicate(position))
					{
						return true;
					}

					bool skip = true;
					foreach(var proc in subProcessors)
					{
						var result = proc(position);
						if(result.HasValue)
						{
							skip = false;
							position = result.Value;
							break;
						}
					}
					if(skip)
					{
						if(tokens[position.index].type == TokenType.Whitespace || tokens[position.index].type == TokenType.LineBreak)
						{
							position = position.Step(tokens);
						}
						else break;
					}
				}
				return false;
			}

			private static bool ProcessUntil(List<Token> tokens, ref TokenSourceRef position, Predicate<TokenSourceRef> predicate, params TryProcessTokensDelegate[] subProcessors)
			{
				while(position.index < tokens.Count)
				{
					if(predicate(position))
					{
						return true;
					}

					bool skip = true;
					foreach(var proc in subProcessors)
					{
						var result = proc(position);
						if(result.HasValue)
						{
							skip = false;
							position = result.Value;
							break;
						}
					}
					if(skip)
					{
						position = position.Step(tokens);
					}
				}
				return false;
			}

			private static bool IsIdentifierStart(Token token)
			{
				return token.type == TokenType.Letter || token.type == TokenType.Underscore;
			}
		}
	}
}