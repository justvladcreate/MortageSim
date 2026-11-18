using System;
using System.Collections.Generic;
using System.Globalization;

namespace MortgageSim.Python
{
    internal sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _index;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens;
        }

        public List<Stmt> ParseModule()
        {
            var result = new List<Stmt>();
            SkipNewLines();
            while (!Check(TokenKind.Eof))
            {
                result.Add(ParseStatement());
                SkipNewLines();
            }
            return result;
        }

        /// <summary>Парсит одно выражение (используется для f-строк).</summary>
        public Expr ParseExpressionOnly()
        {
            SkipNewLines();
            return ParseExpression();
        }

        private Stmt ParseStatement()
        {
            Token t = Current;
            if (t.Kind == TokenKind.Name)
            {
                switch (t.Text)
                {
                    case "def": return ParseFunctionDef();
                    case "if": return ParseIf();
                    case "for": return ParseFor();
                    case "while": return ParseWhile();
                    case "return": return ParseReturn();
                    case "break":
                        Advance();
                        ConsumeLineEnd();
                        return new BreakStmt { Line = t.Line };
                    case "continue":
                        Advance();
                        ConsumeLineEnd();
                        return new ContinueStmt { Line = t.Line };
                    case "pass":
                        Advance();
                        ConsumeLineEnd();
                        return new PassStmt { Line = t.Line };
                }
            }

            return ParseSimpleStatement();
        }

        private Stmt ParseFunctionDef()
        {
            int line = Current.Line;
            Advance(); // def
            string name = ExpectName();
            ExpectOp("(");
            var parameters = new List<string>();

            if (!CheckOp(")"))
            {
                while (true)
                {
                    string p = ExpectName();
                    parameters.Add(p);
                    // Дефолтные значения поддерживаем синтаксически, но игнорируем.
                    if (CheckOp("="))
                    {
                        Advance();
                        ParseExpression();
                    }
                    if (CheckOp(","))
                    {
                        Advance();
                        if (CheckOp(")")) break;
                        continue;
                    }
                    break;
                }
            }
            ExpectOp(")");
            var body = ParseBlock();

            return new FuncDefStmt
            {
                Line = line,
                Name = name,
                Parameters = parameters,
                Body = body
            };
        }

        private Stmt ParseIf()
        {
            int line = Current.Line;
            var stmt = new IfStmt { Line = line };
            Advance(); // if
            stmt.Conditions.Add(ParseExpression());
            stmt.Bodies.Add(ParseBlock());

            while (CheckName("elif"))
            {
                Advance();
                stmt.Conditions.Add(ParseExpression());
                stmt.Bodies.Add(ParseBlock());
            }

            if (CheckName("else"))
            {
                Advance();
                stmt.ElseBody = ParseBlock();
            }

            return stmt;
        }

        private Stmt ParseFor()
        {
            int line = Current.Line;
            Advance(); // for
            var targets = new List<string>();
            targets.Add(ExpectName());
            while (CheckOp(","))
            {
                Advance();
                targets.Add(ExpectName());
            }
            if (targets.Count != 1)
                throw new PythonSyntaxException("Поддерживается только одна переменная цикла", line);
            if (!CheckName("in")) throw new PythonSyntaxException("Ожидалось 'in'", Current.Line);
            Advance();
            var iterable = ParseExpression();
            var body = ParseBlock();
            return new ForStmt
            {
                Line = line,
                Var = targets[0],
                Iterable = iterable,
                Body = body,
                BodyLine = line
            };
        }

        private Stmt ParseWhile()
        {
            int line = Current.Line;
            Advance(); // while
            var condition = ParseExpression();
            var body = ParseBlock();
            return new WhileStmt
            {
                Line = line,
                Condition = condition,
                Body = body,
                BodyLine = line
            };
        }

        private Stmt ParseReturn()
        {
            int line = Current.Line;
            Advance();
            Expr value = null;
            if (!Check(TokenKind.NewLine) && !Check(TokenKind.Eof))
                value = ParseExpression();
            ConsumeLineEnd();
            return new ReturnStmt { Line = line, Value = value };
        }

        private Stmt ParseSimpleStatement()
        {
            int line = Current.Line;
            Expr left = ParseExpression();

            if (CheckOp("=") || CheckOp("+=") || CheckOp("-=") || CheckOp("*=") || CheckOp("/=") || CheckOp("%="))
            {
                string op = Current.Text;
                Advance();
                Expr right = ParseExpression();
                ConsumeLineEnd();
                return new AssignStmt
                {
                    Line = line,
                    Target = left,
                    Value = right,
                    Op = op
                };
            }

            ConsumeLineEnd();
            return new ExprStmt { Line = line, Expr = left };
        }

        private List<Stmt> ParseBlock()
        {
            if (!CheckOp(":")) throw new PythonSyntaxException("Ожидалось ':'", Current.Line);
            Advance();

            if (!Check(TokenKind.NewLine))
            {
                // Однострочная форма: if x: return y
                var single = new List<Stmt>();
                single.Add(ParseSimpleStatement());
                return single;
            }

            Advance(); // newline
            SkipNewLines();
            if (!Check(TokenKind.Indent))
                throw new PythonSyntaxException("Ожидался блок с отступом", Current.Line);

            Advance(); // indent
            var body = new List<Stmt>();
            SkipNewLines();

            while (!Check(TokenKind.Dedent) && !Check(TokenKind.Eof))
            {
                body.Add(ParseStatement());
                SkipNewLines();
            }

            if (Check(TokenKind.Dedent)) Advance();
            return body;
        }

        // ---- expressions ----

        private Expr ParseExpression()
        {
            return ParseOr();
        }

        private Expr ParseOr()
        {
            Expr left = ParseAnd();
            if (!CheckName("or")) return left;

            var values = new List<Expr> { left };
            while (CheckName("or"))
            {
                Advance();
                values.Add(ParseAnd());
            }
            return new BoolOpExpr { Line = left.Line, Op = "or", Values = values };
        }

        private Expr ParseAnd()
        {
            Expr left = ParseNot();
            if (!CheckName("and")) return left;

            var values = new List<Expr> { left };
            while (CheckName("and"))
            {
                Advance();
                values.Add(ParseNot());
            }
            return new BoolOpExpr { Line = left.Line, Op = "and", Values = values };
        }

        private Expr ParseNot()
        {
            if (CheckName("not"))
            {
                int line = Current.Line;
                Advance();
                return new UnaryExpr { Line = line, Op = "not", Value = ParseNot() };
            }
            return ParseCompare();
        }

        private Expr ParseCompare()
        {
            Expr left = ParseAdd();

            while (true)
            {
                string op = null;
                if (CheckOp("==") || CheckOp("!=") || CheckOp("<") || CheckOp("<=") || CheckOp(">") || CheckOp(">="))
                {
                    op = Current.Text;
                    Advance();
                }
                else if (CheckName("in"))
                {
                    op = "in";
                    Advance();
                }
                else if (CheckName("not") && Peek(1).Kind == TokenKind.Name && Peek(1).Text == "in")
                {
                    op = "not in";
                    Advance();
                    Advance();
                }
                else if (CheckName("is"))
                {
                    op = "is";
                    Advance();
                    if (CheckName("not"))
                    {
                        op = "is not";
                        Advance();
                    }
                }

                if (op == null) break;
                Expr right = ParseAdd();
                left = new BinaryExpr { Line = left.Line, Left = left, Op = op, Right = right };
            }

            return left;
        }

        private Expr ParseAdd()
        {
            Expr left = ParseMul();
            while (CheckOp("+") || CheckOp("-"))
            {
                string op = Current.Text;
                int line = Current.Line;
                Advance();
                Expr right = ParseMul();
                left = new BinaryExpr { Line = line, Left = left, Op = op, Right = right };
            }
            return left;
        }

        private Expr ParseMul()
        {
            Expr left = ParseUnary();
            while (CheckOp("*") || CheckOp("/") || CheckOp("//") || CheckOp("%"))
            {
                string op = Current.Text;
                int line = Current.Line;
                Advance();
                Expr right = ParseUnary();
                left = new BinaryExpr { Line = line, Left = left, Op = op, Right = right };
            }
            return left;
        }

        private Expr ParseUnary()
        {
            if (CheckOp("-") || CheckOp("+"))
            {
                string op = Current.Text;
                int line = Current.Line;
                Advance();
                return new UnaryExpr { Line = line, Op = op, Value = ParseUnary() };
            }
            return ParsePower();
        }

        private Expr ParsePower()
        {
            Expr left = ParsePostfix();
            if (CheckOp("**"))
            {
                int line = Current.Line;
                Advance();
                Expr right = ParseUnary();
                return new BinaryExpr { Line = line, Left = left, Op = "**", Right = right };
            }
            return left;
        }

        private Expr ParsePostfix()
        {
            Expr expr = ParsePrimary();

            while (true)
            {
                if (CheckOp("("))
                {
                    int line = Current.Line;
                    Advance();
                    var call = new CallExpr { Line = line, Callee = expr };
                    if (!CheckOp(")"))
                    {
                        while (true)
                        {
                            call.Args.Add(ParseExpression());
                            if (CheckOp(","))
                            {
                                Advance();
                                if (CheckOp(")")) break;
                                continue;
                            }
                            break;
                        }
                    }
                    ExpectOp(")");
                    expr = call;
                    continue;
                }

                if (CheckOp("["))
                {
                    int line = Current.Line;
                    Advance();
                    expr = ParseIndexOrSlice(expr, line);
                    continue;
                }

                if (CheckOp("."))
                {
                    int line = Current.Line;
                    Advance();
                    string name = ExpectName();
                    expr = new AttrExpr { Line = line, Target = expr, Name = name };
                    continue;
                }

                break;
            }

            return expr;
        }

        private Expr ParseIndexOrSlice(Expr target, int line)
        {
            Expr first = null;
            if (!CheckOp(":")) first = ParseExpression();

            if (CheckOp(":"))
            {
                var slice = new SliceExpr { Line = line, Target = target, Start = first };
                Advance();

                if (!CheckOp(":") && !CheckOp("]"))
                    slice.Stop = ParseExpression();

                if (CheckOp(":"))
                {
                    Advance();
                    if (!CheckOp("]"))
                        slice.Step = ParseExpression();
                }

                ExpectOp("]");
                return slice;
            }

            ExpectOp("]");
            if (first == null) throw new PythonSyntaxException("Пустой индекс", line);
            return new IndexExpr { Line = line, Target = target, Index = first };
        }

        private Expr ParsePrimary()
        {
            Token t = Current;

            if (t.Kind == TokenKind.Number)
            {
                Advance();
                if (t.Text.IndexOf('.') >= 0 || t.Text.IndexOf('e') >= 0 || t.Text.IndexOf('E') >= 0)
                {
                    double dv;
                    double.TryParse(t.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out dv);
                    return new LiteralExpr { Line = t.Line, Value = dv };
                }
                long lv;
                long.TryParse(t.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out lv);
                return new LiteralExpr { Line = t.Line, Value = lv };
            }

            if (t.Kind == TokenKind.String)
            {
                Advance();
                return new LiteralExpr { Line = t.Line, Value = t.Text };
            }

            if (t.Kind == TokenKind.FString)
            {
                Advance();
                return new FStringExpr { Line = t.Line, Raw = t.Text };
            }

            if (t.Kind == TokenKind.Name)
            {
                switch (t.Text)
                {
                    case "True":
                        Advance();
                        return new LiteralExpr { Line = t.Line, Value = true };
                    case "False":
                        Advance();
                        return new LiteralExpr { Line = t.Line, Value = false };
                    case "None":
                        Advance();
                        return new LiteralExpr { Line = t.Line, Value = null };
                }

                Advance();
                return new NameExpr { Line = t.Line, Name = t.Text };
            }

            if (CheckOp("("))
            {
                int line = t.Line;
                Advance();
                var items = new List<Expr>();
                bool trailingComma = false;
                if (!CheckOp(")"))
                {
                    while (true)
                    {
                        items.Add(ParseExpression());
                        if (CheckOp(","))
                        {
                            trailingComma = true;
                            Advance();
                            if (CheckOp(")")) break;
                            continue;
                        }
                        trailingComma = false;
                        break;
                    }
                }
                ExpectOp(")");
                if (items.Count == 1 && !trailingComma) return items[0];
                return new TupleExpr { Line = line, Items = items };
            }

            if (CheckOp("["))
            {
                int line = t.Line;
                Advance();
                var list = new ListExpr { Line = line };
                if (!CheckOp("]"))
                {
                    while (true)
                    {
                        list.Items.Add(ParseExpression());
                        if (CheckOp(","))
                        {
                            Advance();
                            if (CheckOp("]")) break;
                            continue;
                        }
                        break;
                    }
                }
                ExpectOp("]");
                return list;
            }

            if (CheckOp("{"))
            {
                int line = t.Line;
                Advance();
                if (CheckOp("}"))
                {
                    Advance();
                    return new DictExpr { Line = line };
                }

                Expr firstKey = ParseExpression();
                if (CheckOp(":"))
                {
                    Advance();
                    var dict = new DictExpr { Line = line };
                    dict.Keys.Add(firstKey);
                    dict.Values.Add(ParseExpression());
                    while (CheckOp(","))
                    {
                        Advance();
                        if (CheckOp("}")) break;
                        dict.Keys.Add(ParseExpression());
                        ExpectOp(":");
                        dict.Values.Add(ParseExpression());
                    }
                    ExpectOp("}");
                    return dict;
                }

                var set = new SetExpr { Line = line };
                set.Items.Add(firstKey);
                while (CheckOp(","))
                {
                    Advance();
                    if (CheckOp("}")) break;
                    set.Items.Add(ParseExpression());
                }
                ExpectOp("}");
                return set;
            }

            throw new PythonSyntaxException("Неожиданный символ: " + t.Text, t.Line);
        }

        // ---- token helpers ----

        private Token Current => _tokens[Math.Min(_index, _tokens.Count - 1)];

        private Token Peek(int offset)
        {
            int i = Math.Min(_index + offset, _tokens.Count - 1);
            return _tokens[i];
        }

        private void Advance()
        {
            if (_index < _tokens.Count - 1) _index++;
        }

        private bool Check(TokenKind kind) => Current.Kind == kind;

        private bool CheckOp(string text) => Current.Kind == TokenKind.Op && Current.Text == text;

        private bool CheckName(string text) => Current.Kind == TokenKind.Name && Current.Text == text;

        private void ExpectOp(string text)
        {
            if (!CheckOp(text))
                throw new PythonSyntaxException("Ожидалось '" + text + "', получено '" + Current.Text + "'", Current.Line);
            Advance();
        }

        private string ExpectName()
        {
            if (Current.Kind != TokenKind.Name)
                throw new PythonSyntaxException("Ожидалось имя", Current.Line);
            string name = Current.Text;
            Advance();
            return name;
        }

        private void ConsumeLineEnd()
        {
            if (Check(TokenKind.NewLine))
            {
                Advance();
                return;
            }
            if (Check(TokenKind.Eof) || Check(TokenKind.Dedent)) return;
            throw new PythonSyntaxException("Лишний текст в строке: " + Current.Text, Current.Line);
        }

        private void SkipNewLines()
        {
            while (Check(TokenKind.NewLine)) Advance();
        }
    }
}
