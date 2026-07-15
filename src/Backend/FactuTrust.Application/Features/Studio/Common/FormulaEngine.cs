using System.Globalization;

namespace FactuTrust.Application.Features.Studio.Common;

/// <summary>
/// A small, fully sandboxed expression evaluator for Studio "Formula" fields. No code execution, no
/// reflection, no external dependency — a recursive-descent parser over a fixed grammar with a closed
/// whitelist of scalar functions. Identifiers resolve only to other field values passed in by the caller.
/// Used both at design time (validate syntax / references / functions, detect cycles) and at write time.
/// </summary>
public static class FormulaEngine
{
    public const int MaxLength = 1000;
    public const int MaxDepth = 64;

    /// <summary>Scalar functions the grammar accepts (case-insensitive). Anything else is rejected at parse time.</summary>
    public static readonly IReadOnlySet<string> AllowedFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ROUND", "ABS", "FLOOR", "CEIL", "MIN", "MAX", "IF", "COALESCE", "CONCAT", "LEN", "LOWER", "UPPER", "TRIM"
    };

    public sealed class FormulaException : Exception
    {
        public FormulaException(string message) : base(message) { }
    }

    // ---- Public surface ----

    /// <summary>Parses and checks the expression. Returns (ok, error, distinct referenced field keys).</summary>
    public static (bool Ok, string? Error, IReadOnlyCollection<string> References) Validate(string? expression)
    {
        try
        {
            var ast = Parse(expression);
            var refs = new HashSet<string>(StringComparer.Ordinal);
            Collect(ast, refs);
            return (true, null, refs);
        }
        catch (FormulaException ex)
        {
            return (false, ex.Message, Array.Empty<string>());
        }
    }

    /// <summary>Distinct field keys referenced by the expression. Empty if it does not parse.</summary>
    public static IReadOnlyCollection<string> GetReferences(string? expression)
    {
        try
        {
            var refs = new HashSet<string>(StringComparer.Ordinal);
            Collect(Parse(expression), refs);
            return refs;
        }
        catch (FormulaException) { return Array.Empty<string>(); }
    }

    /// <summary>Evaluates the expression against the provided field values. Returns false on any error.</summary>
    public static bool TryEvaluate(string? expression, IReadOnlyDictionary<string, object?> values, out object? result)
    {
        result = null;
        try
        {
            result = Eval(Parse(expression), values);
            return true;
        }
        catch (FormulaException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (OverflowException) { return false; }
        catch (DivideByZeroException) { return false; }
    }

    // ---- Tokenizer ----

    private enum Tk { Number, String, Ident, Op, End }

    private readonly record struct Token(Tk Kind, string Text);

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0, n = s.Length;
        while (i < n)
        {
            var c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || (c == '.' && i + 1 < n && char.IsDigit(s[i + 1])))
            {
                int start = i;
                while (i < n && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                tokens.Add(new Token(Tk.Number, s[start..i]));
                continue;
            }

            if (c == '\'' || c == '"')
            {
                var quote = c; i++;
                int start = i;
                while (i < n && s[i] != quote) i++;
                if (i >= n) throw new FormulaException("Chaîne non terminée.");
                tokens.Add(new Token(Tk.String, s[start..i]));
                i++; // closing quote
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                tokens.Add(new Token(Tk.Ident, s[start..i]));
                continue;
            }

            // Multi-char operators first.
            if (i + 1 < n)
            {
                var two = s.Substring(i, 2);
                if (two is "==" or "!=" or "<>" or "<=" or ">=" or "&&" or "||")
                {
                    tokens.Add(new Token(Tk.Op, two));
                    i += 2;
                    continue;
                }
            }

            if ("+-*/%(),<>!".IndexOf(c) >= 0)
            {
                tokens.Add(new Token(Tk.Op, c.ToString()));
                i++;
                continue;
            }

            throw new FormulaException($"Caractère invalide : « {c} ».");
        }
        tokens.Add(new Token(Tk.End, string.Empty));
        return tokens;
    }

    // ---- AST ----

    private abstract record Node;
    private sealed record NumNode(decimal Value) : Node;
    private sealed record StrNode(string Value) : Node;
    private sealed record BoolNode(bool Value) : Node;
    private sealed record ParamNode(string Name) : Node;
    private sealed record UnaryNode(string Op, Node Operand) : Node;
    private sealed record BinaryNode(string Op, Node Left, Node Right) : Node;
    private sealed record CallNode(string Name, IReadOnlyList<Node> Args) : Node;

    // ---- Parser (recursive descent) ----

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _pos;
        private int _depth;

        public Parser(List<Token> tokens) => _tokens = tokens;

        private Token Cur => _tokens[_pos];
        private bool IsOp(string op) => Cur.Kind == Tk.Op && Cur.Text == op;
        private void Advance() => _pos++;

        private void Enter()
        {
            if (++_depth > MaxDepth) throw new FormulaException("Expression trop imbriquée.");
        }
        private void Leave() => _depth--;

        public Node ParseExpression()
        {
            var node = ParseOr();
            if (Cur.Kind != Tk.End) throw new FormulaException("Expression mal formée.");
            return node;
        }

        private Node ParseOr()
        {
            Enter();
            var left = ParseAnd();
            while (IsOp("||") || IsKeyword("OR")) { Advance(); left = new BinaryNode("||", left, ParseAnd()); }
            Leave();
            return left;
        }

        private Node ParseAnd()
        {
            var left = ParseNot();
            while (IsOp("&&") || IsKeyword("AND")) { Advance(); left = new BinaryNode("&&", left, ParseNot()); }
            return left;
        }

        private Node ParseNot()
        {
            if (IsOp("!") || IsKeyword("NOT")) { Advance(); return new UnaryNode("!", ParseNot()); }
            return ParseComparison();
        }

        private Node ParseComparison()
        {
            var left = ParseAdditive();
            if (Cur.Kind == Tk.Op && Cur.Text is "==" or "!=" or "<>" or "<" or "<=" or ">" or ">=")
            {
                var op = Cur.Text; Advance();
                return new BinaryNode(op == "<>" ? "!=" : op, left, ParseAdditive());
            }
            return left;
        }

        private Node ParseAdditive()
        {
            var left = ParseMultiplicative();
            while (IsOp("+") || IsOp("-")) { var op = Cur.Text; Advance(); left = new BinaryNode(op, left, ParseMultiplicative()); }
            return left;
        }

        private Node ParseMultiplicative()
        {
            var left = ParseUnary();
            while (IsOp("*") || IsOp("/") || IsOp("%")) { var op = Cur.Text; Advance(); left = new BinaryNode(op, left, ParseUnary()); }
            return left;
        }

        private Node ParseUnary()
        {
            if (IsOp("-")) { Advance(); return new UnaryNode("-", ParseUnary()); }
            if (IsOp("+")) { Advance(); return ParseUnary(); }
            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            Enter();
            try
            {
                if (IsOp("("))
                {
                    Advance();
                    var inner = ParseOr();
                    Expect(")");
                    return inner;
                }

                switch (Cur.Kind)
                {
                    case Tk.Number:
                    {
                        if (!decimal.TryParse(Cur.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                            throw new FormulaException($"Nombre invalide : « {Cur.Text} ».");
                        Advance();
                        return new NumNode(d);
                    }
                    case Tk.String:
                    {
                        var str = Cur.Text; Advance();
                        return new StrNode(str);
                    }
                    case Tk.Ident:
                    {
                        var name = Cur.Text;
                        Advance();
                        if (IsOp("(")) return ParseCall(name);
                        if (string.Equals(name, "true", StringComparison.OrdinalIgnoreCase)) return new BoolNode(true);
                        if (string.Equals(name, "false", StringComparison.OrdinalIgnoreCase)) return new BoolNode(false);
                        if (IsKeywordName(name)) throw new FormulaException($"Mot-clé inattendu : « {name} ».");
                        return new ParamNode(name);
                    }
                    default:
                        throw new FormulaException("Expression incomplète.");
                }
            }
            finally { Leave(); }
        }

        private Node ParseCall(string name)
        {
            if (!AllowedFunctions.Contains(name))
                throw new FormulaException($"Fonction non autorisée : « {name} ».");
            Expect("(");
            var args = new List<Node>();
            if (!IsOp(")"))
            {
                args.Add(ParseOr());
                while (IsOp(",")) { Advance(); args.Add(ParseOr()); }
            }
            Expect(")");
            return new CallNode(name.ToUpperInvariant(), args);
        }

        private bool IsKeyword(string kw) => Cur.Kind == Tk.Ident && string.Equals(Cur.Text, kw, StringComparison.OrdinalIgnoreCase);
        private static bool IsKeywordName(string name) =>
            name is "and" or "or" or "not" || string.Equals(name, "AND", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "OR", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "NOT", StringComparison.OrdinalIgnoreCase);

        private void Expect(string op)
        {
            if (!IsOp(op)) throw new FormulaException($"« {op} » attendu.");
            Advance();
        }
    }

    private static Node Parse(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            throw new FormulaException("L'expression est vide.");
        if (expression.Length > MaxLength)
            throw new FormulaException($"L'expression dépasse {MaxLength} caractères.");
        return new Parser(Tokenize(expression)).ParseExpression();
    }

    // ---- Reference collection ----

    private static void Collect(Node node, HashSet<string> into)
    {
        switch (node)
        {
            case ParamNode p: into.Add(p.Name); break;
            case UnaryNode u: Collect(u.Operand, into); break;
            case BinaryNode b: Collect(b.Left, into); Collect(b.Right, into); break;
            case CallNode c: foreach (var a in c.Args) Collect(a, into); break;
        }
    }

    // ---- Evaluation ----

    private static object? Eval(Node node, IReadOnlyDictionary<string, object?> values)
    {
        switch (node)
        {
            case NumNode n: return n.Value;
            case StrNode s: return s.Value;
            case BoolNode b: return b.Value;
            case ParamNode p: return values.TryGetValue(p.Name, out var v) ? Normalize(v) : null;
            case UnaryNode u: return EvalUnary(u, values);
            case BinaryNode b: return EvalBinary(b, values);
            case CallNode c: return EvalCall(c, values);
            default: throw new FormulaException("Nœud inconnu.");
        }
    }

    private static object? EvalUnary(UnaryNode u, IReadOnlyDictionary<string, object?> values)
    {
        var v = Eval(u.Operand, values);
        return u.Op switch
        {
            "-" => -ToNumber(v),
            "!" => !ToBool(v),
            _ => throw new FormulaException("Opérateur unaire inconnu.")
        };
    }

    private static object? EvalBinary(BinaryNode b, IReadOnlyDictionary<string, object?> values)
    {
        // Short-circuit logical operators.
        if (b.Op == "&&") return ToBool(Eval(b.Left, values)) && ToBool(Eval(b.Right, values));
        if (b.Op == "||") return ToBool(Eval(b.Left, values)) || ToBool(Eval(b.Right, values));

        var l = Eval(b.Left, values);
        var r = Eval(b.Right, values);

        switch (b.Op)
        {
            case "+":
                if (l is string || r is string) return ToStr(l) + ToStr(r);
                return ToNumber(l) + ToNumber(r);
            case "-": return ToNumber(l) - ToNumber(r);
            case "*": return ToNumber(l) * ToNumber(r);
            case "/":
            {
                var d = ToNumber(r);
                if (d == 0) throw new DivideByZeroException();
                return ToNumber(l) / d;
            }
            case "%":
            {
                var d = ToNumber(r);
                if (d == 0) throw new DivideByZeroException();
                return ToNumber(l) % d;
            }
            case "==": return AreEqual(l, r);
            case "!=": return !AreEqual(l, r);
            case "<": return ToNumber(l) < ToNumber(r);
            case "<=": return ToNumber(l) <= ToNumber(r);
            case ">": return ToNumber(l) > ToNumber(r);
            case ">=": return ToNumber(l) >= ToNumber(r);
            default: throw new FormulaException("Opérateur inconnu.");
        }
    }

    private static object? EvalCall(CallNode c, IReadOnlyDictionary<string, object?> values)
    {
        switch (c.Name)
        {
            case "IF":
                Require(c, 3);
                return ToBool(Eval(c.Args[0], values)) ? Eval(c.Args[1], values) : Eval(c.Args[2], values);

            case "COALESCE":
                if (c.Args.Count == 0) throw new FormulaException("COALESCE attend au moins un argument.");
                foreach (var arg in c.Args)
                {
                    var v = Eval(arg, values);
                    if (v is not null) return v;
                }
                return null;
        }

        // Eager-evaluated functions.
        var a = c.Args.Select(arg => Eval(arg, values)).ToList();
        switch (c.Name)
        {
            case "ROUND":
            {
                if (a.Count is < 1 or > 2) throw new FormulaException("ROUND attend 1 ou 2 arguments.");
                var digits = a.Count == 2 ? (int)ToNumber(a[1]) : 0;
                if (digits is < 0 or > 15) throw new FormulaException("ROUND : nombre de décimales invalide.");
                return Math.Round(ToNumber(a[0]), digits, MidpointRounding.AwayFromZero);
            }
            case "ABS": Require(c, 1); return Math.Abs(ToNumber(a[0]));
            case "FLOOR": Require(c, 1); return Math.Floor(ToNumber(a[0]));
            case "CEIL": Require(c, 1); return Math.Ceiling(ToNumber(a[0]));
            case "MIN":
                if (a.Count == 0) throw new FormulaException("MIN attend au moins un argument.");
                return a.Select(ToNumber).Min();
            case "MAX":
                if (a.Count == 0) throw new FormulaException("MAX attend au moins un argument.");
                return a.Select(ToNumber).Max();
            case "CONCAT": return string.Concat(a.Select(ToStr));
            case "LEN": Require(c, 1); return (decimal)ToStr(a[0]).Length;
            case "LOWER": Require(c, 1); return ToStr(a[0]).ToLowerInvariant();
            case "UPPER": Require(c, 1); return ToStr(a[0]).ToUpperInvariant();
            case "TRIM": Require(c, 1); return ToStr(a[0]).Trim();
            default: throw new FormulaException($"Fonction non autorisée : « {c.Name} ».");
        }
    }

    private static void Require(CallNode c, int count)
    {
        if (c.Args.Count != count) throw new FormulaException($"{c.Name} attend {count} argument(s).");
    }

    // ---- Value coercion ----

    private static object? Normalize(object? v) => v switch
    {
        null => null,
        bool b => b,
        string s => s,
        decimal d => d,
        sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToDecimal(v, CultureInfo.InvariantCulture),
        float or double => Convert.ToDecimal(v, CultureInfo.InvariantCulture),
        _ => v.ToString()
    };

    private static decimal ToNumber(object? v) => v switch
    {
        decimal d => d,
        bool b => b ? 1m : 0m,
        string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) => d,
        null => throw new FormulaException("Valeur nulle utilisée comme nombre."),
        _ => throw new FormulaException("Valeur non numérique.")
    };

    private static bool ToBool(object? v) => v switch
    {
        bool b => b,
        decimal d => d != 0,
        null => false,
        string s => !string.IsNullOrEmpty(s),
        _ => throw new FormulaException("Valeur non booléenne.")
    };

    private static string ToStr(object? v) => v switch
    {
        null => string.Empty,
        bool b => b ? "true" : "false",
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        string s => s,
        _ => v.ToString() ?? string.Empty
    };

    private static bool AreEqual(object? l, object? r)
    {
        if (l is null || r is null) return l is null && r is null;
        if (l is decimal || r is decimal)
        {
            try { return ToNumber(l) == ToNumber(r); }
            catch (FormulaException) { return false; }
        }
        if (l is bool || r is bool) return ToBool(l) == ToBool(r);
        return string.Equals(ToStr(l), ToStr(r), StringComparison.Ordinal);
    }
}
