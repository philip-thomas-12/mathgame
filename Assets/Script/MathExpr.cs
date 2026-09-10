using System.Collections.Generic;

/// <summary>
/// Small recursive-descent parser that evaluates arithmetic expressions
/// using BODMAS / operator precedence:
///   Brackets  -> ( )
///   Orders    -> ^            (right associative)
///   Division / Multiplication
///   Addition / Subtraction
/// Used by GameManager to turn a player-built chain of falling number
/// and operator pickups into a single attack value.
/// </summary>
public static class MathExpr {

    /// <summary>Evaluates an expression string. Returns false on malformed input or division by zero.</summary>
    public static bool TryEvaluate(string expression, out double value) {
        value = 0.0;
        if (string.IsNullOrEmpty(expression)) return false;

        List<object> tokens;
        if (!Tokenize(expression, out tokens)) return false;

        int pos = 0;
        double result;
        if (!ParseAddition(tokens, ref pos, out result)) return false;
        if (pos != tokens.Count) return false; // trailing garbage

        value = result;
        return true;
    }

    // --- Tokenizer -------------------------------------------------------

    static bool Tokenize(string s, out List<object> tokens) {
        tokens = new List<object>();
        int i = 0;
        while (i < s.Length) {
            char c = s[i];
            if (c == ' ') { i++; continue; }

            if (c >= '0' && c <= '9') {
                int n = 0;
                while (i < s.Length && s[i] >= '0' && s[i] <= '9') {
                    n = n * 10 + (s[i] - '0');
                    i++;
                }
                tokens.Add(n);
                continue;
            }

            char op = '\0';
            if (c == '+' || c == '-' || c == '*' || c == '/' || c == '^' || c == '(' || c == ')') op = c;
            else if (c == '×' || c == '·') op = '*';
            else if (c == '÷' || c == ':') op = '/';
            else if (c == '−' || c == '–') op = '-';

            if (op == '\0') return false; // unknown character
            tokens.Add(op);
            i++;
        }
        return tokens.Count > 0;
    }

    static bool IsOp(List<object> tokens, int pos, char op) {
        return pos < tokens.Count && tokens[pos] is char && (char)tokens[pos] == op;
    }

    // --- Grammar ---------------------------------------------------------
    // addition  := multiplication (('+' | '-') multiplication)*
    // multiplication := power (('*' | '/') power)*
    // power     := primary ('^' power)?          (right associative)
    // primary   := NUMBER | '(' addition ')'

    static bool ParseAddition(List<object> tokens, ref int pos, out double value) {
        value = 0.0;
        double lhs;
        if (!ParseMultiplication(tokens, ref pos, out lhs)) return false;

        while (IsOp(tokens, pos, '+') || IsOp(tokens, pos, '-')) {
            char op = (char)tokens[pos];
            pos++;
            double rhs;
            if (!ParseMultiplication(tokens, ref pos, out rhs)) return false;
            lhs = (op == '+') ? lhs + rhs : lhs - rhs;
        }
        value = lhs;
        return true;
    }

    static bool ParseMultiplication(List<object> tokens, ref int pos, out double value) {
        value = 0.0;
        double lhs;
        if (!ParsePower(tokens, ref pos, out lhs)) return false;

        while (IsOp(tokens, pos, '*') || IsOp(tokens, pos, '/')) {
            char op = (char)tokens[pos];
            pos++;
            double rhs;
            if (!ParsePower(tokens, ref pos, out rhs)) return false;
            if (op == '/') {
                if (System.Math.Abs(rhs) < 1e-12) return false; // division by zero (exact float compare is unreliable)
                lhs = lhs / rhs;
            } else {
                lhs = lhs * rhs;
            }
        }
        value = lhs;
        return true;
    }

    static bool ParsePower(List<object> tokens, ref int pos, out double value) {
        value = 0.0;
        double baseVal;
        if (!ParsePrimary(tokens, ref pos, out baseVal)) return false;

        if (IsOp(tokens, pos, '^')) {
            pos++;
            double exponent;
            if (!ParsePower(tokens, ref pos, out exponent)) return false; // right associative
            value = System.Math.Pow(baseVal, exponent);
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            return true;
        }

        value = baseVal;
        return true;
    }

    static bool ParsePrimary(List<object> tokens, ref int pos, out double value) {
        value = 0.0;
        if (pos >= tokens.Count) return false;

        object token = tokens[pos];
        if (token is int) {
            value = (int)token;
            pos++;
            return true;
        }
        if (token is char && (char)token == '(') {
            pos++;
            double inner;
            if (!ParseAddition(tokens, ref pos, out inner)) return false;
            if (!IsOp(tokens, pos, ')')) return false; // missing closing bracket
            pos++;
            value = inner;
            return true;
        }
        return false;
    }
}
