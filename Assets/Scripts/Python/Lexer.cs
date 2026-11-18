using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MortgageSim.Python
{
    internal enum TokenKind
    {
        Number,
        String,
        FString,
        Name,
        Op,
        NewLine,
        Indent,
        Dedent,
        Eof
    }

    internal sealed class Token
    {
        public TokenKind Kind;
        public string Text;
        public int Line;

        public Token(TokenKind kind, string text, int line)
        {
            Kind = kind;
            Text = text;
            Line = line;
        }

        public override string ToString()
        {
            return Kind + " '" + Text + "' @" + Line;
        }
    }

    /// <summary>
    /// Небольшой лексер Python-подобного языка. Поддерживает отступы,
    /// комментарии, обычные и f-строки, числа и основные операторы.
    /// </summary>
    internal sealed class Lexer
    {
        private readonly string _src;
        private readonly List<Token> _tokens = new List<Token>();
        private readonly Stack<int> _indents = new Stack<int>();
        private int _pos;
        private int _line = 1;
        private bool _atLineStart = true;

        public Lexer(string source)
        {
            _src = (source ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            _indents.Push(0);
        }

        public List<Token> Tokenize()
        {
            while (_pos < _src.Length)
            {
                if (_atLineStart && !HandleIndentation()) break;

                char c = _src[_pos];

                if (c == ' ' || c == '\t')
                {
                    _pos++;
                    continue;
                }

                if (c == '#')
                {
                    SkipComment();
                    continue;
                }

                if (c == '\n')
                {
                    _tokens.Add(new Token(TokenKind.NewLine, "\n", _line));
                    _pos++;
                    _line++;
                    _atLineStart = true;
                    continue;
                }

                if (char.IsDigit(c) || (c == '.' && _pos + 1 < _src.Length && char.IsDigit(_src[_pos + 1])))
                {
                    ReadNumber();
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    ReadString(false);
                    continue;
                }

                if (char.IsLetter(c) || c == '_')
                {
                    // f"..." / r"..." / fr"..."
                    int prefixLen = StringPrefixLength();
                    if (prefixLen > 0)
                    {
                        bool isF = false;
                        for (int i = 0; i < prefixLen; i++)
                        {
                            if (_src[_pos + i] == 'f' || _src[_pos + i] == 'F') isF = true;
                        }
                        _pos += prefixLen;
                        ReadString(isF);
                        continue;
                    }

                    ReadName();
                    continue;
                }

                ReadOperator();
            }

            if (_tokens.Count == 0 || _tokens[_tokens.Count - 1].Kind != TokenKind.NewLine)
                _tokens.Add(new Token(TokenKind.NewLine, "\n", _line));

            while (_indents.Peek() > 0)
            {
                _indents.Pop();
                _tokens.Add(new Token(TokenKind.Dedent, string.Empty, _line));
            }

            _tokens.Add(new Token(TokenKind.Eof, string.Empty, _line));
            return _tokens;
        }

        private bool HandleIndentation()
        {
            int start = _pos;
            int spaces = 0;
            while (_pos < _src.Length && _src[_pos] == ' ')
            {
                spaces++;
                _pos++;
            }

            if (_pos >= _src.Length) return false;

            if (_src[_pos] == '\n')
            {
                _pos++;
                _line++;
                return true;
            }

            if (_src[_pos] == '#')
            {
                SkipComment();
                return true;
            }

            if (spaces > _indents.Peek())
            {
                _indents.Push(spaces);
                _tokens.Add(new Token(TokenKind.Indent, string.Empty, _line));
            }
            else if (spaces < _indents.Peek())
            {
                while (_indents.Peek() > spaces)
                {
                    _indents.Pop();
                    _tokens.Add(new Token(TokenKind.Dedent, string.Empty, _line));
                }
                if (_indents.Peek() != spaces)
                    throw new PythonSyntaxException("Неправильный отступ", _line);
            }

            _atLineStart = false;
            return true;
        }

        private void SkipComment()
        {
            while (_pos < _src.Length && _src[_pos] != '\n') _pos++;
            if (_pos < _src.Length)
            {
                _pos++;
                _line++;
                _atLineStart = true;
            }
        }

        private int StringPrefixLength()
        {
            int i = _pos;
            int count = 0;
            while (i < _src.Length && count < 2)
            {
                char ch = char.ToLowerInvariant(_src[i]);
                if (ch == 'f' || ch == 'r')
                {
                    i++;
                    count++;
                }
                else
                {
                    break;
                }
            }

            if (count == 0) return 0;
            if (i < _src.Length && (_src[i] == '"' || _src[i] == '\'')) return count;
            return 0;
        }

        private void ReadNumber()
        {
            int start = _pos;
            bool isFloat = false;

            while (_pos < _src.Length && char.IsDigit(_src[_pos])) _pos++;
            if (_pos < _src.Length && _src[_pos] == '.')
            {
                isFloat = true;
                _pos++;
                while (_pos < _src.Length && char.IsDigit(_src[_pos])) _pos++;
            }
            if (_pos < _src.Length && (_src[_pos] == 'e' || _src[_pos] == 'E'))
            {
                isFloat = true;
                _pos++;
                if (_pos < _src.Length && (_src[_pos] == '+' || _src[_pos] == '-')) _pos++;
                while (_pos < _src.Length && char.IsDigit(_src[_pos])) _pos++;
            }

            string text = _src.Substring(start, _pos - start);
            if (!isFloat)
            {
                long lv;
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out lv))
                {
                    _tokens.Add(new Token(TokenKind.Number, text, _line));
                    return;
                }
            }

            _tokens.Add(new Token(TokenKind.Number, text, _line));
        }

        private void ReadString(bool isFString)
        {
            char quote = _src[_pos];
            bool triple = _pos + 2 < _src.Length && _src[_pos + 1] == quote && _src[_pos + 2] == quote;
            int startLine = _line;
            _pos += triple ? 3 : 1;
            var sb = new StringBuilder();

            while (_pos < _src.Length)
            {
                if (triple)
                {
                    if (_pos + 2 < _src.Length && _src[_pos] == quote && _src[_pos + 1] == quote && _src[_pos + 2] == quote)
                    {
                        _pos += 3;
                        EmitString(sb.ToString(), isFString, startLine);
                        return;
                    }
                }
                else if (_src[_pos] == quote)
                {
                    _pos++;
                    EmitString(sb.ToString(), isFString, startLine);
                    return;
                }

                char c = _src[_pos];
                if (c == '\\' && _pos + 1 < _src.Length)
                {
                    _pos++;
                    char esc = _src[_pos];
                    switch (esc)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case '\'': sb.Append('\''); break;
                        default: sb.Append(esc); break;
                    }
                    _pos++;
                    continue;
                }

                if (c == '\n')
                {
                    if (!triple) throw new PythonSyntaxException("Незакрытая строка", startLine);
                    _line++;
                }

                sb.Append(c);
                _pos++;
            }

            throw new PythonSyntaxException("Незакрытая строка", startLine);
        }

        private void EmitString(string value, bool isFString, int line)
        {
            _tokens.Add(new Token(isFString ? TokenKind.FString : TokenKind.String, value, line));
        }

        private void ReadName()
        {
            int start = _pos;
            while (_pos < _src.Length && (char.IsLetterOrDigit(_src[_pos]) || _src[_pos] == '_')) _pos++;
            _tokens.Add(new Token(TokenKind.Name, _src.Substring(start, _pos - start), _line));
        }

        private void ReadOperator()
        {
            string[] multi = { "**", "//", "==", "!=", "<=", ">=", "+=", "-=", "*=", "/=", "%=" };
            for (int i = 0; i < multi.Length; i++)
            {
                if (_pos + multi[i].Length <= _src.Length && _src.Substring(_pos, multi[i].Length) == multi[i])
                {
                    _tokens.Add(new Token(TokenKind.Op, multi[i], _line));
                    _pos += multi[i].Length;
                    return;
                }
            }

            char c = _src[_pos];
            if ("+-*/%<>=()[]{}.,:!".IndexOf(c) >= 0)
            {
                _tokens.Add(new Token(TokenKind.Op, c.ToString(), _line));
                _pos++;
                return;
            }

            throw new PythonSyntaxException("Неизвестный символ: " + c, _line);
        }
    }
}
