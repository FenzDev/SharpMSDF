using System.Text;

namespace SharpMSDF.Atlas;

/// <summary>
/// Static utility class for parsing charset files and strings
/// </summary>
public static class CharsetParser
{
	private enum State
	{
		Clear,
		Tight,
		RangeBracket,
		RangeStart,
		RangeSeparator,
		RangeEnd
	}

	public delegate int ReadCharFunc<T>(ref T userData);

	public unsafe struct ParseUserData
	{
		public char* Cur;
		public char* End;

		public ParseUserData(char* cur, char* end)
		{
			Cur = cur;
			End = end;
		}

		public static int ReadChar(ref ParseUserData ud)
		{
			return ud.Cur < ud.End ? *ud.Cur++ : -1;
		}
	}

	public readonly struct LoadUserData
	{
		public readonly string Filename;
		public readonly bool DisableCharLiterals;
		public readonly FileStream File;

		public LoadUserData(string filename, bool disableCharLiterals, FileStream file)
		{
			Filename = filename;
			DisableCharLiterals = disableCharLiterals;
			File = file;
		}

		public static int ReadChar(ref LoadUserData ud)
		{
			return ud.File.ReadByte();
		}
	}

	/// <summary>
	/// Loads a charset from a file
	/// </summary>
	public static bool LoadFromFile(ref Charset charset, string filename, bool disableCharLiterals = false)
	{
		using var fs = File.OpenRead(filename);
		var userData = new LoadUserData(filename, disableCharLiterals, fs);
		return Parse(ref charset, ref userData, LoadUserData.ReadChar, disableCharLiterals, false);
	}

	/// <summary>
	/// Parses a charset from a string
	/// </summary>
	public static unsafe bool ParseFromString(ref Charset charset, string str, bool disableCharLiterals = false)
	{
		fixed (char* chars = str)
		{
			var userData = new ParseUserData(&chars[0], &chars[str.Length]);
			return Parse(ref charset, ref userData, ParseUserData.ReadChar, disableCharLiterals, true);
		}
	}

	/// <summary>
	/// Core parsing logic
	/// </summary>
	private static bool Parse<T>(
		ref Charset charset,
		ref T userData,
		ReadCharFunc<T> readChar,
		bool disableCharLiterals,
		bool disableInclude)
	{
		var state = State.Clear;
		var buffer = new StringBuilder();
		var unicodeBuffer = new List<uint>();
		uint rangeStart = 0;
		bool start = true;

		for (int c = readChar(ref userData); c >= 0; start = false, c = readChar(ref userData))
		{
			switch (c)
			{
				// --- Number literal ---
				case >= '0' and <= '9':
					if (state is not (State.Clear or State.RangeBracket or State.RangeSeparator))
					{
						return false;
					}

					buffer.Append((char)c);
					c = ReadWord(readChar, ref userData, buffer);
					if (!ParseInt(buffer.ToString(), out int cp))
					{
						return false;
					}

					switch (state)
					{
						case State.Clear:
							if (cp >= 0)
							{
								charset.Add((uint)cp);
							}
							state = State.Tight;
							break;
						case State.RangeBracket:
							rangeStart = (uint)cp;
							state = State.RangeStart;
							break;
						case State.RangeSeparator:
							for (uint u = rangeStart; u <= (uint)cp; ++u)
							{
								charset.Add(u);
							}
							state = State.RangeEnd;
							break;
					}
					buffer.Clear();
					continue; // already have next c

				// --- Single char literal ---
				case '\'':
					if (!(state is State.Clear or State.RangeBracket or State.RangeSeparator) || disableCharLiterals)
					{
						return false;
					}

					if (!ReadString(readChar, ref userData, buffer, '\''))
					{
						return false;
					}

					Utf8.Utf8Decode(unicodeBuffer, buffer.ToString());
					if (unicodeBuffer.Count != 1)
					{
						return false;
					}

					uint uc = unicodeBuffer[0];
					switch (state)
					{
						case State.Clear:
							if (uc > 0)
							{
								charset.Add(uc);
							}
							state = State.Tight;
							break;
						case State.RangeBracket:
							rangeStart = uc;
							state = State.RangeStart;
							break;
						case State.RangeSeparator:
							for (uint u = rangeStart; u <= uc; ++u)
							{
								charset.Add(u);
							}
							state = State.RangeEnd;
							break;
					}
					unicodeBuffer.Clear();
					buffer.Clear();
					break;

				// --- String literal ---
				case '"':
					if (state != State.Clear || disableCharLiterals)
					{
						return false;
					}

					if (!ReadString(readChar, ref userData, buffer, '"'))
					{
						return false;
					}

					Utf8.Utf8Decode(unicodeBuffer, buffer.ToString());
					foreach (uint cp2 in unicodeBuffer)
					{
						charset.Add(cp2);
					}

					unicodeBuffer.Clear();
					buffer.Clear();
					state = State.Tight;
					break;

				// --- Range brackets ---
				case '[':
					if (state != State.Clear)
					{
						return false;
					}
					state = State.RangeBracket;
					break;
				case ']':
					if (state == State.RangeEnd)
					{
						state = State.Tight;
					}
					else
					{
						return false;
					}
					break;

				// --- Include directive ---
				case '@':
					if (state != State.Clear)
					{
						return false;
					}

					c = ReadWord(readChar, ref userData, buffer);
					if (buffer.ToString() == "include")
					{
						// skip whitespace
						while (c is ' ' or '\t' or '\n' or '\r')
						{
							c = readChar(ref userData);
						}

						if (c != '"')
						{
							return false;
						}

						buffer.Clear();
						if (!ReadString(readChar, ref userData, buffer, '"'))
						{
							return false;
						}

						if (!disableInclude && userData is LoadUserData loadData)
						{
							string fullPath = CombinePath(loadData.Filename, buffer.ToString());
							LoadFromFile(ref charset, fullPath, loadData.DisableCharLiterals);
						}

						state = State.Tight;
					}
					else
					{
						return false;
					}

					buffer.Clear();
					break;

				// --- Separators & whitespace ---
				case ',':
				case ';':
					if (state is State.RangeStart)
					{
						state = State.RangeSeparator;
					}
					else if (state is not (State.Clear or State.Tight))
					{
						return false;
					}
					goto case ' ';
				case ' ':
				case '\n':
				case '\r':
				case '\t':
					if (state == State.Tight)
					{
						state = State.Clear;
					}
					break;

				// --- BOM at start ---
				case 0xEF:
					if (start &&
						readChar(ref userData) == 0xBB &&
						readChar(ref userData) == 0xBF)
					{
						break;
					}
					return false;

				// --- Anything else is error ---
				default:
					return false;
			}
		}

		return state is State.Clear or State.Tight;
	}

	public static char EscapedChar(char c)
	{
		return c switch
		{
			'0' => '\0',
			'n' or 'N' => '\n',
			'r' or 'R' => '\r',
			's' or 'S' => ' ',
			't' or 'T' => '\t',
			_ => c
		};
	}

	public static bool ParseInt(string str, out int result)
	{
		result = 0;
		if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			for (int i = 2; i < str.Length; ++i)
			{
				char c = str[i];
				if (c is >= '0' and <= '9')
				{
					result = (result << 4) + (c - '0');
				}
				else if (c is >= 'A' and <= 'F')
				{
					result = (result << 4) + c - 'A' + 10;
				}
				else if (c is >= 'a' and <= 'f')
				{
					result = (result << 4) + c - 'a' + 10;
				}
				else
				{
					return false;
				}
			}
			return true;
		}
		else
		{
			foreach (char c in str)
			{
				if (c is >= '0' and <= '9')
				{
					result = (result * 10) + (c - '0');
				}
				else
				{
					return false;
				}
			}
			return true;
		}
	}

	private static bool ReadString<T>(ReadCharFunc<T> readChar, ref T userData, StringBuilder buffer, char terminator)
	{
		bool escape = false;
		while (true)
		{
			int ci = readChar(ref userData);
			if (ci < 0)
			{
				return false;
			}

			char c = (char)ci;
			if (escape)
			{
				buffer.Append(EscapedChar(c));
				escape = false;
			}
			else
			{
				if (c == terminator)
				{
					return true;
				}

				if (c == '\\')
				{
					escape = true;
				}
				else
				{
					buffer.Append(c);
				}
			}
		}
	}

	public static string CombinePath(string basePath, string relPath)
	{
		if (Path.IsPathRooted(relPath))
		{
			return relPath;
		}

		string? dir = Path.GetDirectoryName(basePath);
		return dir != null ? Path.Combine(dir, relPath) : relPath;
	}

	private static int ReadWord<T>(ReadCharFunc<T> readChar, ref T userData, StringBuilder buffer)
	{
		while (true)
		{
			int c = readChar(ref userData);
			if (char.IsLetterOrDigit((char)c) || c == '_')
			{
				buffer.Append((char)c);
			}
			else
			{
				return c;
			}
		}
	}
}