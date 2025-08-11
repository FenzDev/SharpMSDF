using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpMSDF.Atlas
{
    public partial struct Charset
    {

        public bool Load(string filename, bool disableCharLiterals = false)
        {
            using FileStream fs = File.OpenRead(filename);
            CharsetUserData userData = new(this, filename, disableCharLiterals, fs);
            return CharsetParse(userData, CharsetUserData.ReadChar, CharsetUserData .Add, CharsetUserData.Include, disableCharLiterals, false);
        }

        public unsafe bool Parse(string str, bool disableCharLiterals = false)
        {
            fixed (char* chars = str)
            {
                CharsetUserData userData = new(this, &chars[0], &chars[str.Length]);
                return CharsetParse(userData, CharsetUserData.ReadChar, CharsetUserData.Add, CharsetUserData.Include, disableCharLiterals, true);
            }
        }

        public static char EscapedChar(char c) => c switch
        {
            '0' => '\0',
            'n' or 'N' => '\n',
            'r' or 'R' => '\r',
            's' or 'S' => ' ',
            't' or 'T' => '\t',
            _ => c
        };

        public static bool ParseInt(string str, out int result)
        {
            result = 0;
            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 2; i < str.Length; ++i)
                {
                    char c = str[i];
                    if (c is >= '0' and <= '9')
                        result = (result << 4) + (c - '0');
                    else if (c is >= 'A' and <= 'F')
                        result = (result << 4) + (c - 'A' + 10);
                    else if (c is >= 'a' and <= 'f')
                        result = (result << 4) + (c - 'a' + 10);
                    else
                        return false;
                }
                return true;
            }
            else
            {
                foreach (char c in str)
                {
                    if (c is >= '0' and <= '9')
                        result = result * 10 + (c - '0');
                    else
                        return false;
                }
                return true;
            }
        }

        private enum State
        {
            Clear,
            Tight,
            RangeBracket,
            RangeStart,
            RangeSeparator,
            RangeEnd
        }

        public static bool CharsetParse(
           CharsetUserData userData,
           ReadCharFunc readChar,
           Action<CharsetUserData, uint> add,
           Func<CharsetUserData, string, bool> include,
           bool disableCharLiterals,
           bool disableInclude
       )
        {
            State state = State.Clear;
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
                            return false;
                        buffer.Append((char)c);
                        c = ReadWord(readChar, userData, buffer);
                        if (!ParseInt(buffer.ToString(), out int cp))
                            return false;
                        switch (state)
                        {
                            case State.Clear:
                                if (cp >= 0) add(userData, (uint)cp);
                                state = State.Tight;
                                break;
                            case State.RangeBracket:
                                rangeStart = (uint)cp;
                                state = State.RangeStart;
                                break;
                            case State.RangeSeparator:
                                for (uint u = rangeStart; u <= (uint)cp; ++u)
                                    add(userData, u);
                                state = State.RangeEnd;
                                break;
                        }
                        buffer.Clear();
                        continue; // already have next c

                    // --- Single char literal ---
                    case '\'':
                        if (!(state is State.Clear or State.RangeBracket or State.RangeSeparator) || disableCharLiterals)
                            return false;
                        if (!ReadString(readChar, userData, buffer, '\''))
                            return false;
                        Utf8.Utf8Decode(unicodeBuffer, buffer.ToString());
                        if (unicodeBuffer.Count != 1)
                            return false;
                        uint uc = unicodeBuffer[0];
                        switch (state)
                        {
                            case State.Clear:
                                if (uc > 0) add(userData, uc);
                                state = State.Tight;
                                break;
                            case State.RangeBracket:
                                rangeStart = uc;
                                state = State.RangeStart;
                                break;
                            case State.RangeSeparator:
                                for (uint u = rangeStart; u <= uc; ++u)
                                    add(userData, u);
                                state = State.RangeEnd;
                                break;
                        }
                        unicodeBuffer.Clear();
                        buffer.Clear();
                        break;

                    // --- String literal ---
                    case '"':
                        if (state != State.Clear || disableCharLiterals)
                            return false;
                        if (!ReadString(readChar, userData, buffer, '"'))
                            return false;
                        Utf8.Utf8Decode(unicodeBuffer, buffer.ToString());
                        foreach (var cp2 in unicodeBuffer)
                            add(userData, cp2);
                        unicodeBuffer.Clear();
                        buffer.Clear();
                        state = State.Tight;
                        break;

                    // --- Range brackets ---
                    case '[':
                        if (state != State.Clear) return false;
                        state = State.RangeBracket;
                        break;
                    case ']':
                        if (state == State.RangeEnd) state = State.Tight;
                        else return false;
                        break;

                    // --- Include directive ---
                    case '@':
                        if (state != State.Clear) return false;
                        c = ReadWord(readChar, userData, buffer);
                        if (buffer.ToString() == "include")
                        {
                            // skip whitespace
                            while (c is ' ' or '\t' or '\n' or '\r')
                                c = readChar(ref userData);
                            if (c != '"') return false;
                            buffer.Clear();
                            if (!ReadString(readChar, userData, buffer, '"'))
                                return false;
                            if (!disableInclude)
                                include(userData, buffer.ToString());
                            state = State.Tight;
                        }
                        else return false;
                        buffer.Clear();
                        break;

                    // --- Separators & whitespace ---
                    case ',':
                    case ';':
                        if (state is State.RangeStart)
                            state = State.RangeSeparator;
                        else if (state is not (State.Clear or State.Tight))
                            return false;
                        goto case ' ';
                    case ' ':
                    case '\n':
                    case '\r':
                    case '\t':
                        if (state == State.Tight)
                            state = State.Clear;
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

            return state == State.Clear || state == State.Tight;
        }


        private static bool ReadString(ReadCharFunc readChar, CharsetUserData userData, StringBuilder buffer, char terminator)
        {
            bool escape = false;
            while (true)
            {
                int ci = readChar(ref userData);
                if (ci < 0) return false;
                char c = (char)ci;
                if (escape)
                {
                    buffer.Append(EscapedChar(c));
                    escape = false;
                }
                else
                {
                    if (c == terminator) return true;
                    if (c == '\\') { escape = true; }
                    else buffer.Append(c);
                }
            }
        }

        public static string CombinePath(string basePath, string relPath)
        {
            if (Path.IsPathRooted(relPath))
                return relPath;

            string? dir = Path.GetDirectoryName(basePath);
            return dir != null ? Path.Combine(dir, relPath) : relPath;
        }

        private static int ReadWord(ReadCharFunc readChar, CharsetUserData userData, StringBuilder buffer)
        {
            while (true)
            {
                int c = readChar(ref userData);
                if (char.IsLetterOrDigit((char)c) || c == '_')
                    buffer.Append((char)c);
                else
                    return c;
            }
        }
    }

    public delegate int ReadCharFunc(ref CharsetUserData charset);

    public unsafe struct CharsetUserData
    {
        public Charset Charset { get; set; }
        
        public readonly bool IsParseNotLoad;

        // Parse
        public char* Cur;
        public char* End;

        // Load
        public string Filename;
        public bool DisableCharLiterals;
        public FileStream File;

        public CharsetUserData(Charset charset, char* cur, char* end)
        {
            IsParseNotLoad = true;
            Charset = charset;
            Cur = cur;
            End = end;
        }
        public CharsetUserData(Charset charset, string filename, bool disableCharLiterals, FileStream file)
        {
            IsParseNotLoad = false;
            Charset = charset;
            Filename = filename;
            DisableCharLiterals = disableCharLiterals;
            File = file;
        }

        public static int ReadChar(ref CharsetUserData ud)
        {
            if (ud.IsParseNotLoad)
            {
                // sus
                return ud.Cur < ud.End ? *ud.Cur++ : -1;
            }

            return ud.File.ReadByte();
        }

        public static void Add(CharsetUserData ud, uint codepoint)
        {
            ud.Charset.Add(codepoint);
        }

        public static bool Include(CharsetUserData ud, string path)
        {
            if (ud.IsParseNotLoad)
                return false;

            string fullPath = Charset.CombinePath(ud.Filename, path);
            return ud.Charset.Load(fullPath, ud.DisableCharLiterals);
        }
    }
}