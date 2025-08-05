using System.Collections;

namespace SharpMSDF.Atlas;
/// <summary>
/// Represents a set of Unicode codepoints (characters)
/// </summary>
public struct Charset
{
	private SortedSet<uint> _codepoints;

	/// <summary>
	/// The set of the 95 printable ASCII characters
	/// </summary>
	public static readonly SortedSet<uint> ASCII = CreateAsciiCharset();

	private static SortedSet<uint> CreateAsciiCharset()
	{
		var ascii = new SortedSet<uint>();
		for (uint cp = 0x20; cp < 0x7f; ++cp)
		{
			ascii.Add(cp);
		}
		return ascii;
	}

	public Charset()
	{
		_codepoints = new SortedSet<uint>();
	}

	public Charset(SortedSet<uint> codepoints)
	{
		_codepoints = codepoints ?? new SortedSet<uint>();
	}

	/// <summary>
	/// Adds a codepoint
	/// </summary>
	public void Add(uint cp)
	{
		_codepoints.Add(cp);
	}

	/// <summary>
	/// Removes a codepoint
	/// </summary>
	public void Remove(uint cp)
	{
		_codepoints.Remove(cp);
	}

	public int Size()
	{
		return _codepoints.Count;
	}

	public bool Empty()
	{
		return _codepoints.Count == 0;
	}

	public SortedSet<uint>.Enumerator GetEnumerator()
	{
		return _codepoints.GetEnumerator();
	}

	public SortedSet<uint> GetCodepoints()
	{
		return _codepoints;
	}

	// Implicit conversion from ReadOnlySpan<char> to Charset
	public static implicit operator Charset(ReadOnlySpan<char> chars)
	{
		var charset = new Charset();
		foreach (char ch in chars)
			charset.Add(ch);
		return charset;
	}

	// Implicit conversion from ReadOnlySpan<uint> to Charset
	public static implicit operator Charset(ReadOnlySpan<uint> codepoints)
	{
		var charset = new Charset();
		foreach (uint cp in codepoints)
			charset.Add(cp);
		return charset;
	}
}
