using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MortgageSim.Python
{
    /// <summary>
    /// Интерпретатор безопасного подмножества Python, достаточного для всех 12 задач.
    /// Он не импортирует модули и не имеет доступа к системе — только математика,
    /// строки, списки, словари, множества, условия, циклы и функции.
    /// </summary>
    internal sealed class Interpreter
    {
        public const int MaxSteps = 500000;
        public const int MaxCallDepth = 160;

        internal readonly Env Globals;
        public readonly StringBuilder Output = new StringBuilder();

        private int _steps;
        private int _callDepth;

        public Interpreter()
        {
            Globals = new Env();
            RegisterBuiltins();
        }

        public List<Stmt> Execute(string code)
        {
            var tokens = new Lexer(code).Tokenize();
            var program = new Parser(tokens).ParseModule();
            foreach (var stmt in program)
            {
                ExecuteStmt(stmt, Globals);
            }
            return program;
        }

        public object GetGlobal(string name)
        {
            return Globals.TryGet(name, out object value) ? value : null;
        }

        public object CallFunction(object function, List<object> args)
        {
            return CallValue(function, args, 0);
        }

        // ------------------------------------------------------------------
        // Statements
        // ------------------------------------------------------------------

        private void ExecuteStmt(Stmt stmt, Env env)
        {
            Tick(stmt.Line);

            if (stmt is FuncDefStmt def)
            {
                env.Set(def.Name, new PyFunction
                {
                    Name = def.Name,
                    Parameters = def.Parameters,
                    Body = def.Body,
                    Closure = env
                });
                return;
            }

            if (stmt is ExprStmt exprStmt)
            {
                Eval(exprStmt.Expr, env);
                return;
            }

            if (stmt is AssignStmt assign)
            {
                object value = Eval(assign.Value, env);
                if (assign.Op != "=")
                {
                    object current = ReadTarget(assign.Target, env);
                    string op = assign.Op.Substring(0, assign.Op.Length - 1);
                    value = ApplyBinary(op, current, value, assign.Line);
                }
                AssignTarget(assign.Target, value, env, assign.Line);
                return;
            }

            if (stmt is ReturnStmt ret)
            {
                object value = ret.Value == null ? null : Eval(ret.Value, env);
                throw new ReturnSignal(value);
            }

            if (stmt is IfStmt ifStmt)
            {
                for (int i = 0; i < ifStmt.Conditions.Count; i++)
                {
                    if (Truthy(Eval(ifStmt.Conditions[i], env)))
                    {
                        ExecuteBlock(ifStmt.Bodies[i], env);
                        return;
                    }
                }
                if (ifStmt.ElseBody != null) ExecuteBlock(ifStmt.ElseBody, env);
                return;
            }

            if (stmt is ForStmt forStmt)
            {
                object iterable = Eval(forStmt.Iterable, env);
                foreach (object item in Iterate(iterable, forStmt.Line))
                {
                    Tick(forStmt.BodyLine);
                    env.Set(forStmt.Var, item);
                    try
                    {
                        ExecuteBlock(forStmt.Body, env);
                    }
                    catch (BreakSignal)
                    {
                        break;
                    }
                    catch (ContinueSignal)
                    {
                        continue;
                    }
                }
                return;
            }

            if (stmt is WhileStmt whileStmt)
            {
                while (Truthy(Eval(whileStmt.Condition, env)))
                {
                    Tick(whileStmt.BodyLine);
                    try
                    {
                        ExecuteBlock(whileStmt.Body, env);
                    }
                    catch (BreakSignal)
                    {
                        break;
                    }
                    catch (ContinueSignal)
                    {
                        continue;
                    }
                }
                return;
            }

            if (stmt is BreakStmt) throw new BreakSignal();
            if (stmt is ContinueStmt) throw new ContinueSignal();
            if (stmt is PassStmt) return;
        }

        private void ExecuteBlock(List<Stmt> block, Env env)
        {
            for (int i = 0; i < block.Count; i++) ExecuteStmt(block[i], env);
        }

        private object ReadTarget(Expr target, Env env)
        {
            if (target is NameExpr name) return env.Get(name.Name, target.Line);
            if (target is IndexExpr index) return GetIndex(Eval(index.Target, env), Eval(index.Index, env), index.Line);
            throw new PythonRuntimeException("Недопустимая цель присваивания", target.Line);
        }

        private void AssignTarget(Expr target, object value, Env env, int line)
        {
            if (target is NameExpr name)
            {
                env.Set(name.Name, value);
                return;
            }

            if (target is IndexExpr index)
            {
                object container = Eval(index.Target, env);
                object key = Eval(index.Index, env);
                SetIndex(container, key, value, line);
                return;
            }

            throw new PythonRuntimeException("Недопустимая цель присваивания", line);
        }

        // ------------------------------------------------------------------
        // Expressions
        // ------------------------------------------------------------------

        private object Eval(Expr expr, Env env)
        {
            if (expr is LiteralExpr literal) return literal.Value;
            if (expr is NameExpr name) return env.Get(name.Name, name.Line);

            if (expr is FStringExpr fstring) return EvalFString(fstring, env);

            if (expr is ListExpr list)
            {
                var result = new List<object>();
                foreach (var item in list.Items) result.Add(Eval(item, env));
                return result;
            }

            if (expr is TupleExpr tuple)
            {
                var result = new List<object>();
                foreach (var item in tuple.Items) result.Add(Eval(item, env));
                return result;
            }

            if (expr is DictExpr dict)
            {
                var result = new Dictionary<string, object>();
                for (int i = 0; i < dict.Keys.Count; i++)
                {
                    object key = Eval(dict.Keys[i], env);
                    result[PyFormat.Str(key)] = Eval(dict.Values[i], env);
                }
                return result;
            }

            if (expr is SetExpr setExpr)
            {
                var set = new PySet();
                foreach (var item in setExpr.Items) set.Add(Eval(item, env));
                return set;
            }

            if (expr is UnaryExpr unary)
            {
                object value = Eval(unary.Value, env);
                if (unary.Op == "not") return !Truthy(value);
                if (unary.Op == "-")
                {
                    if (PyFormat.IsNumber(value)) return PyFormat.ToDouble(value, unary.Line) * -1;
                    throw new PythonRuntimeException("Нельзя применить '-' к " + PyFormat.TypeName(value), unary.Line);
                }
                if (unary.Op == "+") return value;
            }

            if (expr is BoolOpExpr boolOp)
            {
                object last = null;
                foreach (var item in boolOp.Values)
                {
                    last = Eval(item, env);
                    if (boolOp.Op == "and" && !Truthy(last)) return last;
                    if (boolOp.Op == "or" && Truthy(last)) return last;
                }
                return last;
            }

            if (expr is BinaryExpr binary)
            {
                object left = Eval(binary.Left, env);
                object right = Eval(binary.Right, env);
                return ApplyBinary(binary.Op, left, right, binary.Line);
            }

            if (expr is CallExpr call)
            {
                if (call.Callee is AttrExpr attr)
                {
                    object target = Eval(attr.Target, env);
                    var args = new List<object>();
                    foreach (var arg in call.Args) args.Add(Eval(arg, env));
                    return CallMethod(target, attr.Name, args, call.Line);
                }

                object callee = Eval(call.Callee, env);
                var callArgs = new List<object>();
                foreach (var arg in call.Args) callArgs.Add(Eval(arg, env));
                return CallValue(callee, callArgs, call.Line);
            }

            if (expr is IndexExpr index) return GetIndex(Eval(index.Target, env), Eval(index.Index, env), index.Line);

            if (expr is SliceExpr slice)
            {
                object target = Eval(slice.Target, env);
                object start = slice.Start == null ? null : Eval(slice.Start, env);
                object stop = slice.Stop == null ? null : Eval(slice.Stop, env);
                object step = slice.Step == null ? null : Eval(slice.Step, env);
                return GetSlice(target, start, stop, step, slice.Line);
            }

            if (expr is AttrExpr attrExpr)
            {
                throw new PythonRuntimeException("Атрибут '" + attrExpr.Name + "' можно использовать только как вызов метода", attrExpr.Line);
            }

            throw new PythonRuntimeException("Неизвестное выражение", expr.Line);
        }

        private string EvalFString(FStringExpr fstring, Env env)
        {
            string raw = fstring.Raw;
            var sb = new StringBuilder();
            int i = 0;

            while (i < raw.Length)
            {
                char c = raw[i];
                if (c == '{')
                {
                    if (i + 1 < raw.Length && raw[i + 1] == '{')
                    {
                        sb.Append('{');
                        i += 2;
                        continue;
                    }

                    int close = FindFStringClose(raw, i + 1);
                    if (close < 0) throw new PythonRuntimeException("Незакрытая скобка в f-строке", fstring.Line);
                    string inner = raw.Substring(i + 1, close - i - 1).Trim();
                    string format = null;

                    int formatPos = inner.LastIndexOf(':');
                    if (formatPos > 0)
                    {
                        format = inner.Substring(formatPos + 1);
                        inner = inner.Substring(0, formatPos).Trim();
                    }

                    if (inner.EndsWith("!r", StringComparison.Ordinal))
                        inner = inner.Substring(0, inner.Length - 2).Trim();

                    var tokens = new Lexer(inner).Tokenize();
                    var parsed = new Parser(tokens).ParseExpressionOnly();
                    object value = Eval(parsed, env);
                    sb.Append(FormatValue(value, format));
                    i = close + 1;
                    continue;
                }

                if (c == '}' && i + 1 < raw.Length && raw[i + 1] == '}')
                {
                    sb.Append('}');
                    i += 2;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        private static int FindFStringClose(string raw, int start)
        {
            int depth = 0;
            for (int i = start; i < raw.Length; i++)
            {
                if (raw[i] == '{') depth++;
                else if (raw[i] == '}')
                {
                    if (depth == 0) return i;
                    depth--;
                }
            }
            return -1;
        }

        private static string FormatValue(object value, string format)
        {
            if (string.IsNullOrEmpty(format)) return PyFormat.Str(value);
            if (format.EndsWith("f", StringComparison.OrdinalIgnoreCase))
            {
                int dot = format.IndexOf('.');
                int digits = 2;
                if (dot >= 0 && dot + 1 < format.Length)
                {
                    int.TryParse(format.Substring(dot + 1, format.Length - dot - 2), out digits);
                }
                return PyFormat.ToDouble(value, 0).ToString("F" + digits, CultureInfo.InvariantCulture);
            }
            return PyFormat.Str(value);
        }

        // ------------------------------------------------------------------
        // Operators
        // ------------------------------------------------------------------

        private object ApplyBinary(string op, object left, object right, int line)
        {
            switch (op)
            {
                case "+":
                    if (left is string ls && right is string rs) return ls + rs;
                    if (left is List<object> ll && right is List<object> rl)
                    {
                        var combined = new List<object>(ll);
                        combined.AddRange(rl);
                        return combined;
                    }
                    if (PyFormat.IsNumber(left) && PyFormat.IsNumber(right))
                        return NumericResult(PyFormat.ToDouble(left, line) + PyFormat.ToDouble(right, line));
                    break;

                case "-":
                    if (PyFormat.IsNumber(left) && PyFormat.IsNumber(right))
                        return NumericResult(PyFormat.ToDouble(left, line) - PyFormat.ToDouble(right, line));
                    break;

                case "*":
                    if (left is string mulStr && PyFormat.IsNumber(right)) return Repeat(mulStr, (int)PyFormat.ToLong(right, line));
                    if (right is string mulStr2 && PyFormat.IsNumber(left)) return Repeat(mulStr2, (int)PyFormat.ToLong(left, line));
                    if (left is List<object> mulList && PyFormat.IsNumber(right)) return RepeatList(mulList, (int)PyFormat.ToLong(right, line));
                    if (PyFormat.IsNumber(left) && PyFormat.IsNumber(right))
                        return NumericResult(PyFormat.ToDouble(left, line) * PyFormat.ToDouble(right, line));
                    break;

                case "/":
                    if (PyFormat.IsNumber(left) && PyFormat.IsNumber(right))
                    {
                        double divisor = PyFormat.ToDouble(right, line);
                        if (Math.Abs(divisor) < 0.0000000001) throw new PythonRuntimeException("Деление на ноль", line);
                        return PyFormat.ToDouble(left, line) / divisor;
                    }
                    break;

                case "//":
                    if (PyFormat.IsNumber(left) && PyFormat.IsNumber(right))
                    {
                        double divisor = PyFormat.ToDouble(right, line);
                        if (Math.Abs(divisor) < 0.0000000001) throw new PythonRuntimeException("Деление на ноль", line);
                        return (long)Math.Floor(PyFormat.ToDouble(left, line) / divisor);
                    }
                    break;

                case "%":
                    if (PyFormat.IsNumber(left) && PyFormat.IsNumber(right))
                    {
                        long a = PyFormat.ToLong(left, line);
                        long b = PyFormat.ToLong(right, line);
                        if (b == 0) throw new PythonRuntimeException("Деление на ноль", line);
                        long mod = a % b;
                        if (mod != 0 && ((mod < 0) != (b < 0))) mod += b;
                        return mod;
                    }
                    break;

                case "**":
                    if (PyFormat.IsNumber(left) && PyFormat.IsNumber(right))
                    {
                        double result = Math.Pow(PyFormat.ToDouble(left, line), PyFormat.ToDouble(right, line));
                        return NumericResult(result);
                    }
                    break;

                case "==": return Equal(left, right);
                case "!=": return !Equal(left, right);
                case "<": return Compare(left, right, line) < 0;
                case "<=": return Compare(left, right, line) <= 0;
                case ">": return Compare(left, right, line) > 0;
                case ">=": return Compare(left, right, line) >= 0;
                case "in": return Contains(right, left, line);
                case "not in": return !Contains(right, left, line);
                case "is": return ReferenceEquals(left, right) || (left == null && right == null) || (left is bool b1 && right is bool b2 && b1 == b2);
                case "is not": return !(ReferenceEquals(left, right) || (left == null && right == null) || (left is bool bb1 && right is bool bb2 && bb1 == bb2));
            }

            throw new PythonRuntimeException("Операция '" + op + "' не поддерживается для " + PyFormat.TypeName(left) + " и " + PyFormat.TypeName(right), line);
        }

        private static object NumericResult(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 0.0000001 && Math.Abs(value) < 9.0e18)
                return (long)Math.Round(value);
            return value;
        }

        private static string Repeat(string value, int count)
        {
            if (count <= 0) return string.Empty;
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++) sb.Append(value);
            return sb.ToString();
        }

        private static List<object> RepeatList(List<object> value, int count)
        {
            var result = new List<object>();
            if (count <= 0) return result;
            for (int i = 0; i < count; i++) result.AddRange(value);
            return result;
        }

        public static bool Equal(object left, object right)
        {
            if (left == null && right == null) return true;
            if (left == null || right == null) return false;

            if (PyFormat.IsNumber(left) && PyFormat.IsNumber(right))
                return Math.Abs(PyFormat.ToDouble(left, 0) - PyFormat.ToDouble(right, 0)) < 0.0000001;

            if (left is string ls && right is string rs) return ls == rs;
            if (left is bool lb && right is bool rb) return lb == rb;

            if (left is List<object> ll && right is List<object> rl)
            {
                if (ll.Count != rl.Count) return false;
                for (int i = 0; i < ll.Count; i++)
                {
                    if (!Equal(ll[i], rl[i])) return false;
                }
                return true;
            }

            if (left is Dictionary<string, object> ld && right is Dictionary<string, object> rd)
            {
                if (ld.Count != rd.Count) return false;
                foreach (var pair in ld)
                {
                    if (!rd.TryGetValue(pair.Key, out object value)) return false;
                    if (!Equal(pair.Value, value)) return false;
                }
                return true;
            }

            if (left is PySet lset && right is PySet rset)
            {
                if (lset.Items.Count != rset.Items.Count) return false;
                foreach (var item in lset.Items)
                {
                    bool found = false;
                    foreach (var other in rset.Items)
                    {
                        if (Equal(item, other)) { found = true; break; }
                    }
                    if (!found) return false;
                }
                return true;
            }

            return left.Equals(right);
        }

        private static int Compare(object left, object right, int line)
        {
            if (PyFormat.IsNumber(left) && PyFormat.IsNumber(right))
                return PyFormat.ToDouble(left, line).CompareTo(PyFormat.ToDouble(right, line));
            if (left is string ls && right is string rs) return string.CompareOrdinal(ls, rs);
            if (left is bool lb && right is bool rb) return lb.CompareTo(rb);
            throw new PythonRuntimeException("Нельзя сравнить " + PyFormat.TypeName(left) + " и " + PyFormat.TypeName(right), line);
        }

        private static bool Contains(object container, object item, int line)
        {
            if (container is string s && item is string sub) return s.IndexOf(sub, StringComparison.Ordinal) >= 0;
            if (container is List<object> list)
            {
                foreach (var entry in list) if (Equal(entry, item)) return true;
                return false;
            }
            if (container is Dictionary<string, object> dict) return dict.ContainsKey(PyFormat.Str(item));
            if (container is PySet set)
            {
                foreach (var entry in set.Items) if (Equal(entry, item)) return true;
                return false;
            }
            throw new PythonRuntimeException("Оператор 'in' не поддерживается для " + PyFormat.TypeName(container), line);
        }

        private static bool Truthy(object value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            if (value is long l) return l != 0;
            if (value is int i) return i != 0;
            if (value is double d) return Math.Abs(d) > 0.0000000001;
            if (value is string s) return s.Length > 0;
            if (value is List<object> list) return list.Count > 0;
            if (value is Dictionary<string, object> dict) return dict.Count > 0;
            if (value is PySet set) return set.Items.Count > 0;
            return true;
        }

        // ------------------------------------------------------------------
        // Call / methods
        // ------------------------------------------------------------------

        private object CallValue(object callee, List<object> args, int line)
        {
            if (callee is BuiltinFunction builtin) return builtin.Call(args);

            if (callee is PyFunction function)
            {
                if (args.Count != function.Parameters.Count)
                    throw new PythonRuntimeException(function.Name + "() ожидает " + function.Parameters.Count + " аргументов, получено " + args.Count, line);

                if (++_callDepth > MaxCallDepth)
                    throw new PythonRuntimeException("Слишком глубокая рекурсия", line);

                var local = new Env(function.Closure);
                for (int i = 0; i < function.Parameters.Count; i++) local.Set(function.Parameters[i], args[i]);

                try
                {
                    ExecuteBlock(function.Body, local);
                    return null;
                }
                catch (ReturnSignal signal)
                {
                    return signal.Value;
                }
                finally
                {
                    _callDepth--;
                }
            }

            throw new PythonRuntimeException("Это не функция: " + PyFormat.Repr(callee), line);
        }

        private object CallMethod(object target, string name, List<object> args, int line)
        {
            if (target is string s) return StringMethod(s, name, args, line);
            if (target is List<object> list) return ListMethod(list, name, args, line);
            if (target is Dictionary<string, object> dict) return DictMethod(dict, name, args, line);
            if (target is PySet set) return SetMethod(set, name, args, line);
            throw new PythonRuntimeException("У " + PyFormat.TypeName(target) + " нет метода " + name + "()", line);
        }

        private object StringMethod(string s, string name, List<object> args, int line)
        {
            switch (name)
            {
                case "lower": return s.ToLowerInvariant();
                case "upper": return s.ToUpperInvariant();
                case "strip": return args.Count == 0 ? s.Trim() : s.Trim((args[0] as string ?? "").ToCharArray());
                case "lstrip": return args.Count == 0 ? s.TrimStart() : s.TrimStart((args[0] as string ?? "").ToCharArray());
                case "rstrip": return args.Count == 0 ? s.TrimEnd() : s.TrimEnd((args[0] as string ?? "").ToCharArray());
                case "split":
                    if (args.Count == 0) return s.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList<object>();
                    return s.Split(new[] { args[0] as string ?? " " }, StringSplitOptions.None).ToList<object>();
                case "join":
                {
                    var parts = Iterate(args[0], line).Select(x => PyFormat.Str(x)).ToArray();
                    return string.Join(s, parts);
                }
                case "replace":
                {
                    if (args.Count < 2) throw new PythonRuntimeException("replace() ожидает 2 аргумента", line);
                    string oldValue = args[0] as string ?? "";
                    string newValue = args[1] as string ?? "";
                    if (args.Count < 3) return s.Replace(oldValue, newValue);
                    int limit = (int)PyFormat.ToLong(args[2], line);
                    if (limit <= 0) return s;
                    var replaced = new System.Text.StringBuilder();
                    int from = 0;
                    int done = 0;
                    while (done < limit)
                    {
                        int found = s.IndexOf(oldValue, from, StringComparison.Ordinal);
                        if (found < 0) break;
                        replaced.Append(s, from, found - from);
                        replaced.Append(newValue);
                        from = found + oldValue.Length;
                        done++;
                    }
                    replaced.Append(s, from, s.Length - from);
                    return replaced.ToString();
                }
                case "startswith": return s.StartsWith(args[0] as string ?? "", StringComparison.Ordinal);
                case "endswith": return s.EndsWith(args[0] as string ?? "", StringComparison.Ordinal);
                case "count": return (long)CountSubstring(s, args[0] as string ?? "");
                case "find": return (long)s.IndexOf(args[0] as string ?? "", StringComparison.Ordinal);
                case "isalpha": return s.Length > 0 && s.All(char.IsLetter);
                case "isdigit": return s.Length > 0 && s.All(char.IsDigit);
                case "isalnum": return s.Length > 0 && s.All(char.IsLetterOrDigit);
                case "isspace": return s.Length > 0 && s.All(char.IsWhiteSpace);
                case "capitalize": return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();
                case "title": return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
            }
            throw new PythonRuntimeException("У строки нет метода " + name + "()", line);
        }

        private static int CountSubstring(string source, string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        private object ListMethod(List<object> list, string name, List<object> args, int line)
        {
            switch (name)
            {
                case "append":
                    list.Add(args[0]);
                    return null;
                case "extend":
                    foreach (var item in Iterate(args[0], line)) list.Add(item);
                    return null;
                case "insert":
                    list.Insert((int)PyFormat.ToLong(args[0], line), args[1]);
                    return null;
                case "pop":
                    if (list.Count == 0) throw new PythonRuntimeException("pop из пустого списка", line);
                    int index = args.Count == 0 ? list.Count - 1 : (int)PyFormat.ToLong(args[0], line);
                    if (index < 0) index += list.Count;
                    object removed = list[index];
                    list.RemoveAt(index);
                    return removed;
                case "remove":
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (Equal(list[i], args[0]))
                        {
                            list.RemoveAt(i);
                            return null;
                        }
                    }
                    throw new PythonRuntimeException("remove: элемент не найден", line);
                case "sort":
                    list.Sort((a, b) => Compare(a, b, line));
                    return null;
                case "reverse":
                    list.Reverse();
                    return null;
                case "count":
                    return (long)list.Count(x => Equal(x, args[0]));
                case "index":
                    for (int i = 0; i < list.Count; i++) if (Equal(list[i], args[0])) return (long)i;
                    throw new PythonRuntimeException("index: элемент не найден", line);
                case "clear":
                    list.Clear();
                    return null;
            }
            throw new PythonRuntimeException("У списка нет метода " + name + "()", line);
        }

        private object DictMethod(Dictionary<string, object> dict, string name, List<object> args, int line)
        {
            switch (name)
            {
                case "get":
                {
                    string key = PyFormat.Str(args[0]);
                    if (dict.TryGetValue(key, out object value)) return value;
                    return args.Count > 1 ? args[1] : null;
                }
                case "keys": return dict.Keys.Cast<object>().ToList();
                case "values": return dict.Values.ToList();
                case "items":
                {
                    var result = new List<object>();
                    foreach (var pair in dict) result.Add(new List<object> { pair.Key, pair.Value });
                    return result;
                }
                case "setdefault":
                {
                    string key = PyFormat.Str(args[0]);
                    if (dict.TryGetValue(key, out object value)) return value;
                    object fallback = args.Count > 1 ? args[1] : null;
                    dict[key] = fallback;
                    return fallback;
                }
                case "pop":
                {
                    string key = PyFormat.Str(args[0]);
                    if (dict.TryGetValue(key, out object value))
                    {
                        dict.Remove(key);
                        return value;
                    }
                    if (args.Count > 1) return args[1];
                    throw new PythonRuntimeException("pop: ключ не найден", line);
                }
                case "clear":
                    dict.Clear();
                    return null;
            }
            throw new PythonRuntimeException("У словаря нет метода " + name + "()", line);
        }

        private object SetMethod(PySet set, string name, List<object> args, int line)
        {
            switch (name)
            {
                case "add":
                    set.Add(args[0]);
                    return null;
                case "remove":
                    for (int i = 0; i < set.Items.Count; i++)
                    {
                        if (Equal(set.Items[i], args[0]))
                        {
                            set.Items.RemoveAt(i);
                            return null;
                        }
                    }
                    throw new PythonRuntimeException("remove: элемента нет в множестве", line);
                case "discard":
                    for (int i = 0; i < set.Items.Count; i++)
                    {
                        if (Equal(set.Items[i], args[0]))
                        {
                            set.Items.RemoveAt(i);
                            return null;
                        }
                    }
                    return null;
            }
            throw new PythonRuntimeException("У множества нет метода " + name + "()", line);
        }

        // ------------------------------------------------------------------
        // Indexing / slicing
        // ------------------------------------------------------------------

        private object GetIndex(object target, object index, int line)
        {
            if (target is string s)
            {
                int i = NormalizeIndex((int)PyFormat.ToLong(index, line), s.Length, line);
                return s[i].ToString();
            }
            if (target is List<object> list)
            {
                int i = NormalizeIndex((int)PyFormat.ToLong(index, line), list.Count, line);
                return list[i];
            }
            if (target is Dictionary<string, object> dict) return dict[PyFormat.Str(index)];
            throw new PythonRuntimeException("Индексация не поддерживается для " + PyFormat.TypeName(target), line);
        }

        private void SetIndex(object target, object index, object value, int line)
        {
            if (target is List<object> list)
            {
                int i = NormalizeIndex((int)PyFormat.ToLong(index, line), list.Count, line);
                list[i] = value;
                return;
            }
            if (target is Dictionary<string, object> dict)
            {
                dict[PyFormat.Str(index)] = value;
                return;
            }
            throw new PythonRuntimeException("Нельзя изменить элемент " + PyFormat.TypeName(target), line);
        }

        private static int NormalizeIndex(int index, int count, int line)
        {
            if (index < 0) index += count;
            if (index < 0 || index >= count)
                throw new PythonRuntimeException("Индекс вне диапазона: " + index, line);
            return index;
        }

        private object GetSlice(object target, object startObj, object stopObj, object stepObj, int line)
        {
            int step = stepObj == null ? 1 : (int)PyFormat.ToLong(stepObj, line);
            if (step == 0) throw new PythonRuntimeException("Шаг среза не может быть 0", line);

            if (target is string s)
            {
                var indexes = SliceIndexes(s.Length, startObj, stopObj, step, line);
                var sb = new StringBuilder();
                foreach (int i in indexes) sb.Append(s[i]);
                return sb.ToString();
            }

            if (target is List<object> list)
            {
                var result = new List<object>();
                foreach (int i in SliceIndexes(list.Count, startObj, stopObj, step, line)) result.Add(list[i]);
                return result;
            }

            throw new PythonRuntimeException("Срез не поддерживается для " + PyFormat.TypeName(target), line);
        }

        private static List<int> SliceIndexes(int count, object startObj, object stopObj, int step, int line)
        {
            var result = new List<int>();
            if (step > 0)
            {
                int start = startObj == null ? 0 : (int)PyFormat.ToLong(startObj, line);
                int stop = stopObj == null ? count : (int)PyFormat.ToLong(stopObj, line);
                if (start < 0) start += count;
                if (stop < 0) stop += count;
                start = Math.Max(0, Math.Min(count, start));
                stop = Math.Max(0, Math.Min(count, stop));
                for (int i = start; i < stop; i += step) result.Add(i);
            }
            else
            {
                int start = startObj == null ? count - 1 : (int)PyFormat.ToLong(startObj, line);
                if (startObj != null && start < 0) start += count;
                int stop;
                if (stopObj == null) stop = -1;
                else
                {
                    stop = (int)PyFormat.ToLong(stopObj, line);
                    if (stop < 0) stop += count;
                }
                start = Math.Max(-1, Math.Min(count - 1, start));
                stop = Math.Max(-1, Math.Min(count - 1, stop));
                for (int i = start; i > stop; i += step) result.Add(i);
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Builtins
        // ------------------------------------------------------------------

        private void RegisterBuiltins()
        {
            Add("print", args =>
            {
                Output.AppendLine(string.Join(" ", args.Select(PyFormat.Str)));
                return null;
            });

            Add("len", args => (long)Length(args[0], 0));

            Add("range", args =>
            {
                if (args.Count == 1) return new PyRange(0, PyFormat.ToLong(args[0], 0), 1);
                if (args.Count == 2) return new PyRange(PyFormat.ToLong(args[0], 0), PyFormat.ToLong(args[1], 0), 1);
                if (args.Count == 3) return new PyRange(PyFormat.ToLong(args[0], 0), PyFormat.ToLong(args[1], 0), PyFormat.ToLong(args[2], 0));
                throw new PythonRuntimeException("range() ожидает 1–3 аргумента", 0);
            });

            Add("str", args => args.Count == 0 ? "" : PyFormat.Str(args[0]));

            Add("int", args =>
            {
                if (args.Count == 0) return 0L;
                object value = args[0];
                if (value is string s)
                {
                    if (long.TryParse(s.Trim(), out long parsed)) return parsed;
                    if (double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedDouble)) return (long)parsedDouble;
                    throw new PythonRuntimeException("int(): не удалось преобразовать '" + s + "'", 0);
                }
                return PyFormat.ToLong(value, 0);
            });

            Add("float", args =>
            {
                if (args.Count == 0) return 0.0;
                if (args[0] is string s && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return parsed;
                return PyFormat.ToDouble(args[0], 0);
            });

            Add("bool", args => args.Count > 0 && Truthy(args[0]));

            Add("abs", args => PyFormat.IsNumber(args[0]) ? NumericResult(Math.Abs(PyFormat.ToDouble(args[0], 0))) : args[0]);

            Add("sum", args =>
            {
                double total = 0;
                foreach (var item in Iterate(args[0], 0)) total += PyFormat.ToDouble(item, 0);
                return NumericResult(total);
            });

            Add("min", args => MinMax(args, true));
            Add("max", args => MinMax(args, false));

            Add("sorted", args =>
            {
                var list = Iterate(args[0], 0).ToList();
                list.Sort((a, b) => Compare(a, b, 0));
                return list;
            });

            Add("reversed", args =>
            {
                var list = Iterate(args[0], 0).ToList();
                list.Reverse();
                return list;
            });

            Add("set", args =>
            {
                var set = new PySet();
                if (args.Count > 0)
                {
                    foreach (var item in Iterate(args[0], 0)) set.Add(item);
                }
                return set;
            });

            Add("list", args =>
            {
                var result = new List<object>();
                if (args.Count > 0) result.AddRange(Iterate(args[0], 0));
                return result;
            });

            Add("enumerate", args =>
            {
                var result = new List<object>();
                long index = 0;
                foreach (var item in Iterate(args[0], 0))
                {
                    result.Add(new List<object> { index, item });
                    index++;
                }
                return result;
            });

            Add("round", args =>
            {
                double value = PyFormat.ToDouble(args[0], 0);
                int digits = args.Count > 1 ? (int)PyFormat.ToLong(args[1], 0) : 0;
                double rounded = Math.Round(value, digits, MidpointRounding.AwayFromZero);
                return digits == 0 ? (object)(long)rounded : rounded;
            });
        }

        private void Add(string name, Func<List<object>, object> fn)
        {
            Globals.Set(name, new BuiltinFunction { Name = name, Call = fn });
        }

        private static int Length(object value, int line)
        {
            if (value is string s) return s.Length;
            if (value is List<object> list) return list.Count;
            if (value is Dictionary<string, object> dict) return dict.Count;
            if (value is PySet set) return set.Items.Count;
            if (value is PyRange range) return (int)Math.Max(0, (range.Stop - range.Start + (range.Step > 0 ? range.Step - 1 : range.Step + 1)) / range.Step);
            throw new PythonRuntimeException("len() не поддерживается для " + PyFormat.TypeName(value), line);
        }

        private static object MinMax(List<object> args, bool min)
        {
            var values = args.Count == 1 ? Iterate(args[0], 0).ToList() : args;
            if (values.Count == 0) throw new PythonRuntimeException((min ? "min" : "max") + "() от пустой последовательности", 0);
            object best = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                int cmp = Compare(values[i], best, 0);
                if (min ? cmp < 0 : cmp > 0) best = values[i];
            }
            return best;
        }

        private static IEnumerable<object> Iterate(object value, int line)
        {
            if (value is List<object> list) return list;
            if (value is PyRange range) return range;
            if (value is PySet set) return set.Items;
            if (value is string s) return EnumerateString(s);
            if (value is Dictionary<string, object> dict) return dict.Keys.Cast<object>();
            throw new PythonRuntimeException("Объект " + PyFormat.TypeName(value) + " не является последовательностью", line);
        }

        private static IEnumerable<object> EnumerateString(string s)
        {
            for (int i = 0; i < s.Length; i++) yield return s[i].ToString();
        }

        private void Tick(int line)
        {
            _steps++;
            if (_steps > MaxSteps)
                throw new PythonRuntimeException("Превышено время выполнения (слишком долгий цикл?)", line);
        }
    }
}
