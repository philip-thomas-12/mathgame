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

    /// <summary>
    /// Evaluates the expression and generates a step-by-step reduction sequence
    /// showing BODMAS order of operations resolving intermediate expressions.
    /// </summary>
    public static bool TryEvaluateWithSteps(string expression, out double value, out List<string> steps) {
        steps = new List<string>();
        if (!TryEvaluate(expression, out value)) return false;

        steps.Add(CleanDisplayString(expression));
        try {
            BuildStepwiseReduction(expression, steps);
        } catch {
            // Fallback if stepwise reduction hits unexpected token edge case:
            if (steps.Count == 1) steps.Add(FormatNumber(value));
        }

        string finalStr = FormatNumber(value);
        if (steps.Count == 0 || steps[steps.Count - 1] != finalStr) {
            steps.Add(finalStr);
        }
        return true;
    }

    static string CleanDisplayString(string s) {
        return s.Replace("*", "×").Replace("/", "÷").Trim();
    }

    static string FormatNumber(double n) {
        if (System.Math.Abs(n - System.Math.Round(n)) < 1e-6) {
            return ((long)System.Math.Round(n)).ToString();
        }
        return n.ToString("0.##");
    }

    static void BuildStepwiseReduction(string current, List<string> steps) {
        int maxIter = 10;
        while (maxIter-- > 0) {
            List<object> tokens;
            if (!Tokenize(current, out tokens)) break;

            // If it's already just a single number, we are done
            if (tokens.Count == 1 && (tokens[0] is int || tokens[0] is double)) break;

            int bestOpIdx = -1;
            int parenStart = -1, parenEnd = -1;

            // 1. Look for innermost parentheses
            for (int i = 0; i < tokens.Count; i++) {
                if (tokens[i] is char && (char)tokens[i] == '(') parenStart = i;
                else if (tokens[i] is char && (char)tokens[i] == ')') {
                    parenEnd = i;
                    break;
                }
            }

            int searchStart = (parenStart != -1) ? parenStart + 1 : 0;
            int searchEnd = (parenEnd != -1) ? parenEnd : tokens.Count;

            // If parentheses contain just a single number e.g. "( 5 )", simplify to "5"
            if (parenStart != -1 && parenEnd == parenStart + 2) {
                tokens.RemoveAt(parenEnd);
                tokens.RemoveAt(parenStart);
                current = TokensToString(tokens);
                AddUniqueStep(steps, current);
                continue;
            }

            // Find highest precedence operator within search range
            // Precedence: ^ (right-to-left) > *, / (left-to-right) > +, - (left-to-right)
            for (int i = searchEnd - 1; i >= searchStart; i--) {
                if (tokens[i] is char && (char)tokens[i] == '^') {
                    bestOpIdx = i;
                    break;
                }
            }

            if (bestOpIdx == -1) {
                for (int i = searchStart; i < searchEnd; i++) {
                    if (tokens[i] is char && ((char)tokens[i] == '*' || (char)tokens[i] == '/')) {
                        bestOpIdx = i;
                        break;
                    }
                }
            }

            if (bestOpIdx == -1) {
                for (int i = searchStart; i < searchEnd; i++) {
                    if (tokens[i] is char && ((char)tokens[i] == '+' || (char)tokens[i] == '-')) {
                        bestOpIdx = i;
                        break;
                    }
                }
            }

            if (bestOpIdx <= 0 || bestOpIdx >= tokens.Count - 1) break;

            double lhs = (tokens[bestOpIdx - 1] is int) ? (int)tokens[bestOpIdx - 1] : (double)tokens[bestOpIdx - 1];
            char op = (char)tokens[bestOpIdx];
            double rhs = (tokens[bestOpIdx + 1] is int) ? (int)tokens[bestOpIdx + 1] : (double)tokens[bestOpIdx + 1];
            double res = 0.0;

            if (op == '^') res = System.Math.Pow(lhs, rhs);
            else if (op == '*') res = lhs * rhs;
            else if (op == '/') {
                if (System.Math.Abs(rhs) < 1e-12) break;
                res = lhs / rhs;
            } else if (op == '+') res = lhs + rhs;
            else if (op == '-') res = lhs - rhs;

            // Replace lhs, op, rhs with res
            tokens[bestOpIdx - 1] = res;
            tokens.RemoveAt(bestOpIdx + 1);
            tokens.RemoveAt(bestOpIdx);

            // If this was inside parentheses and is now the only item in those parentheses, remove brackets
            if (parenStart != -1 && parenEnd != -1) {
                // Re-find parenthesis indices
                for (int i = 0; i < tokens.Count; i++) {
                    if (tokens[i] is char && (char)tokens[i] == '(') parenStart = i;
                    else if (tokens[i] is char && (char)tokens[i] == ')') {
                        parenEnd = i;
                        if (parenEnd == parenStart + 2) {
                            tokens.RemoveAt(parenEnd);
                            tokens.RemoveAt(parenStart);
                        }
                        break;
                    }
                }
            }

            current = TokensToString(tokens);
            AddUniqueStep(steps, current);
        }
    }

    static void AddUniqueStep(List<string> steps, string str) {
        string clean = CleanDisplayString(str);
        if (steps.Count == 0 || steps[steps.Count - 1] != clean) {
            steps.Add(clean);
        }
    }

    static string TokensToString(List<object> tokens) {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < tokens.Count; i++) {
            if (i > 0) sb.Append(" ");
            object t = tokens[i];
            if (t is double) sb.Append(FormatNumber((double)t));
            else if (t is int) sb.Append(((int)t).ToString());
            else if (t is char) {
                char c = (char)t;
                if (c == '*') sb.Append("×");
                else if (c == '/') sb.Append("÷");
                else sb.Append(c);
            }
        }
        return sb.ToString();
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
