using System;
using System.Collections.Generic;

namespace MortgageSim.Python
{
    internal sealed class PythonSyntaxException : Exception
    {
        public int Line;
        public PythonSyntaxException(string message, int line) : base(message)
        {
            Line = line;
        }
    }

    internal sealed class PythonRuntimeException : Exception
    {
        public int Line;
        public PythonRuntimeException(string message, int line) : base(message)
        {
            Line = line;
        }
    }

    internal abstract class Stmt
    {
        public int Line;
    }

    internal sealed class AssignStmt : Stmt
    {
        public Expr Target;
        public Expr Value;
        public string Op;
    }

    internal sealed class ExprStmt : Stmt
    {
        public Expr Expr;
    }

    internal sealed class ReturnStmt : Stmt
    {
        public Expr Value;
    }

    internal sealed class IfStmt : Stmt
    {
        public List<Expr> Conditions = new List<Expr>();
        public List<List<Stmt>> Bodies = new List<List<Stmt>>();
        public List<Stmt> ElseBody;
    }

    internal sealed class ForStmt : Stmt
    {
        public string Var;
        public Expr Iterable;
        public List<Stmt> Body;
        public int BodyLine;
    }

    internal sealed class WhileStmt : Stmt
    {
        public Expr Condition;
        public List<Stmt> Body;
        public int BodyLine;
    }

    internal sealed class FuncDefStmt : Stmt
    {
        public string Name;
        public List<string> Parameters = new List<string>();
        public List<Stmt> Body;
    }

    internal sealed class BreakStmt : Stmt { }
    internal sealed class ContinueStmt : Stmt { }
    internal sealed class PassStmt : Stmt { }

    internal abstract class Expr
    {
        public int Line;
    }

    internal sealed class LiteralExpr : Expr
    {
        public object Value;
    }

    internal sealed class NameExpr : Expr
    {
        public string Name;
    }

    internal sealed class FStringExpr : Expr
    {
        public string Raw;
    }

    internal sealed class ListExpr : Expr
    {
        public List<Expr> Items = new List<Expr>();
    }

    internal sealed class DictExpr : Expr
    {
        public List<Expr> Keys = new List<Expr>();
        public List<Expr> Values = new List<Expr>();
    }

    internal sealed class SetExpr : Expr
    {
        public List<Expr> Items = new List<Expr>();
    }

    internal sealed class TupleExpr : Expr
    {
        public List<Expr> Items = new List<Expr>();
    }

    internal sealed class UnaryExpr : Expr
    {
        public string Op;
        public Expr Value;
    }

    internal sealed class BinaryExpr : Expr
    {
        public Expr Left;
        public string Op;
        public Expr Right;
    }

    internal sealed class BoolOpExpr : Expr
    {
        public string Op;
        public List<Expr> Values = new List<Expr>();
    }

    internal sealed class CallExpr : Expr
    {
        public Expr Callee;
        public List<Expr> Args = new List<Expr>();
    }

    internal sealed class IndexExpr : Expr
    {
        public Expr Target;
        public Expr Index;
    }

    internal sealed class SliceExpr : Expr
    {
        public Expr Target;
        public Expr Start;
        public Expr Stop;
        public Expr Step;
    }

    internal sealed class AttrExpr : Expr
    {
        public Expr Target;
        public string Name;
    }

    internal sealed class TupleTargetExpr : Expr
    {
        public List<Expr> Items = new List<Expr>();
    }
}
