using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MortgageSim.Python
{
    internal sealed class PyRange : IEnumerable<object>
    {
        public long Start;
        public long Stop;
        public long Step;

        public PyRange(long start, long stop, long step)
        {
            Start = start;
            Stop = stop;
            Step = step == 0 ? 1 : step;
        }

        public IEnumerator<object> GetEnumerator()
        {
            if (Step > 0)
            {
                for (long i = Start; i < Stop; i += Step) yield return i;
            }
            else
            {
                for (long i = Start; i > Stop; i += Step) yield return i;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class PySet : IEnumerable<object>
    {
        public readonly List<object> Items = new List<object>();

        public void Add(object value)
        {
            foreach (var item in Items)
            {
                if (Interpreter.Equal(item, value)) return;
            }
            Items.Add(value);
        }

        public IEnumerator<object> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class PyFunction
    {
        public string Name;
        public List<string> Parameters;
        public List<Stmt> Body;
        public Env Closure;
    }

    internal sealed class BuiltinFunction
    {
        public string Name;
        public Func<List<object>, object> Call;
    }

    internal sealed class Env
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
        public readonly Env Parent;

        public Env(Env parent = null)
        {
            Parent = parent;
        }

        public bool TryGet(string name, out object value)
        {
            if (_values.TryGetValue(name, out value)) return true;
            if (Parent != null) return Parent.TryGet(name, out value);
            value = null;
            return false;
        }

        public object Get(string name, int line)
        {
            if (TryGet(name, out object value)) return value;
            throw new PythonRuntimeException("Имя не определено: " + name, line);
        }

        public void Set(string name, object value)
        {
            _values[name] = value;
        }

        public bool ContainsLocal(string name) => _values.ContainsKey(name);
    }

    internal sealed class ReturnSignal : Exception
    {
        public readonly object Value;
        public ReturnSignal(object value) { Value = value; }
    }

    internal sealed class BreakSignal : Exception { }
    internal sealed class ContinueSignal : Exception { }

    internal static class PyFormat
    {
        public static string Str(object value)
        {
            if (value == null) return "None";
            if (value is string s) return s;
            if (value is bool b) return b ? "True" : "False";
            if (value is long l) return l.ToString(CultureInfo.InvariantCulture);
            if (value is int i) return i.ToString(CultureInfo.InvariantCulture);
            if (value is double d)
            {
                if (Math.Abs(d - Math.Round(d)) < 0.0000001)
                    return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
                return d.ToString("0.############", CultureInfo.InvariantCulture);
            }
            if (value is List<object> list)
            {
                var sb = new StringBuilder("[");
                for (int k = 0; k < list.Count; k++)
                {
                    if (k > 0) sb.Append(", ");
                    sb.Append(Repr(list[k]));
                }
                sb.Append(']');
                return sb.ToString();
            }
            if (value is Dictionary<string, object> dict)
            {
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (var pair in dict)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(Repr(pair.Key)).Append(": ").Append(Repr(pair.Value));
                }
                sb.Append('}');
                return sb.ToString();
            }
            if (value is PySet set)
            {
                var sb = new StringBuilder("{");
                for (int k = 0; k < set.Items.Count; k++)
                {
                    if (k > 0) sb.Append(", ");
                    sb.Append(Repr(set.Items[k]));
                }
                sb.Append('}');
                return sb.ToString();
            }
            if (value is PyRange range)
            {
                return "range(" + range.Start + ", " + range.Stop + ", " + range.Step + ")";
            }
            return value.ToString();
        }

        public static string Repr(object value)
        {
            if (value is string s) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            if (value == null) return "None";
            return Str(value);
        }

        public static bool IsNumber(object value)
        {
            return value is long || value is int || value is double;
        }

        public static double ToDouble(object value, int line)
        {
            if (value is long l) return l;
            if (value is int i) return i;
            if (value is double d) return d;
            if (value is bool b) return b ? 1 : 0;
            throw new PythonRuntimeException("Ожидалось число, получено: " + Repr(value), line);
        }

        public static long ToLong(object value, int line)
        {
            if (value is long l) return l;
            if (value is int i) return i;
            if (value is double d) return (long)d;
            if (value is bool b) return b ? 1 : 0;
            if (value is string s && long.TryParse(s, out long parsed)) return parsed;
            throw new PythonRuntimeException("Ожидалось целое число, получено: " + Repr(value), line);
        }

        public static string TypeName(object value)
        {
            if (value == null) return "NoneType";
            if (value is string) return "str";
            if (value is bool) return "bool";
            if (value is long || value is int) return "int";
            if (value is double) return "float";
            if (value is List<object>) return "list";
            if (value is Dictionary<string, object>) return "dict";
            if (value is PySet) return "set";
            if (value is PyFunction || value is BuiltinFunction) return "function";
            return value.GetType().Name;
        }
    }
}
