using NCalc;
using NCalcExpression = NCalc.Expression;
using Overlayer.Core;
using Overlayer.Core.TextReplacing;
using Overlayer.Tags.Attributes;
using Overlayer.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Overlayer.Tags;

/// <summary>
/// Small math expressions inside Overlayer text.
///
/// Examples:
///   {Expr(1 + 2 * 3)}
///   {Expr(OVE + OVL)}
///   {Expr(OVE:2 + OVL:2)}
///   {Expr(@PlayerHit:TooEarly:2 + @PlayerHit:TooLate:2)}
///
/// Bare tag names without arguments are handled by NCalc's parameter callback.
/// Tag calls with arguments are replaced before NCalc sees the expression,
/// because NCalc does not accept ':' inside variable names.
/// </summary>
public static class ExpressionTags
{
    private static readonly Regex ExplicitTagRef = new(
        @"@(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<args>(?::[^\s+\-*/%^<>=!&|(),]+)*)",
        RegexOptions.Compiled
    );

    private static readonly Regex BareColonTagRef = new(
        @"(?<![A-Za-z0-9_@])(?<name>[A-Za-z_][A-Za-z0-9_]*)(?<args>(?::[^\s+\-*/%^<>=!&|(),]+)+)",
        RegexOptions.Compiled
    );

    [Tag]
    public static double Expr(string expression, int digits = -1) => EvaluateNumber(expression).Round(digits);

    [Tag("Calc")]
    public static double Calc(string expression, int digits = -1) => Expr(expression, digits);

    [Tag]
    public static string ExprText(string expression, string format = "0.###")
    {
        double value = EvaluateNumber(expression);
        return string.IsNullOrWhiteSpace(format)
            ? value.ToString(CultureInfo.InvariantCulture)
            : value.ToString(format, CultureInfo.InvariantCulture);
    }

    [Tag]
    public static string IfExpr(string expression, string trueText = "1", string falseText = "0")
    {
        return Math.Abs(EvaluateNumber(expression)) > double.Epsilon
            ? ResolveInlineTags(trueText)
            : ResolveInlineTags(falseText);
    }

    [Tag]
    public static string IfText(string left, string op, string right, string trueText = "1", string falseText = "0")
    {
        bool ok = Compare(ResolveInlineTags(left), op, ResolveInlineTags(right));
        return ok ? ResolveInlineTags(trueText) : ResolveInlineTags(falseText);
    }

    [Tag]
    public static string Coalesce(string first, string second = "", string third = "")
    {
        string a = ResolveInlineTags(first);
        if (!string.IsNullOrWhiteSpace(a) && a != "0" && !a.Equals("False", StringComparison.OrdinalIgnoreCase))
        {
            return a;
        }

        string b = ResolveInlineTags(second);
        if (!string.IsNullOrWhiteSpace(b) && b != "0" && !b.Equals("False", StringComparison.OrdinalIgnoreCase))
        {
            return b;
        }

        return ResolveInlineTags(third);
    }

    public static double EvaluateNumber(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return 0;
        }

        try
        {
            expression = ResolveInlineTags(expression);
            Dictionary<string, double> injectedValues = new();
            string prepared = ReplaceTagReferences(expression, injectedValues);
            if(HasUnresolvedInlineTag(prepared)) {
                return 0;
            }

            NCalcExpression ncalcExpression = new(prepared, EvaluateOptions.IgnoreCase);

            ncalcExpression.EvaluateParameter += (name, args) =>
            {
                if (injectedValues.TryGetValue(name, out double injected))
                {
                    args.Result = injected;
                    return;
                }

                switch (name.ToUpperInvariant())
                {
                    case "PI":
                        args.Result = Math.PI;
                        return;
                    case "E":
                        args.Result = Math.E;
                        return;
                }

                if (TryReadTagAsDouble(name, Array.Empty<string>(), out double tagValue))
                {
                    args.Result = tagValue;
                    return;
                }

                // Unknown variables should not crash a text replacement.
                args.Result = 0d;
            };

            ncalcExpression.EvaluateFunction += (name, args) =>
            {
                if (TryEvaluateBuiltInFunction(name, args, out double value))
                {
                    args.Result = value;
                }
            };

            object result = ncalcExpression.Evaluate();
            return ToDouble(result);
        }
        catch
        {
            return 0;
        }
    }

    private static bool HasUnresolvedInlineTag(string text)
    {
        return !string.IsNullOrEmpty(text) && (text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0);
    }

    private static string ReplaceTagReferences(string expression, Dictionary<string, double> injectedValues)
    {
        string result = ExplicitTagRef.Replace(expression, match => ReplaceOneTagReference(match, injectedValues, true));

        // Also allow the simpler style requested for multiplayer tags:
        //   OVE:2 + OVL:2
        // If the left side is not a real tag, leave the text alone.
        result = BareColonTagRef.Replace(result, match => ReplaceOneTagReference(match, injectedValues, false));

        return result;
    }

    private static string ReplaceOneTagReference(Match match, Dictionary<string, double> injectedValues, bool explicitReference)
    {
        string name = match.Groups["name"].Value;
        string[] tagArgs = ParseColonArgs(match.Groups["args"].Value);

        if (!TryReadTagAsDouble(name, tagArgs, out double value))
        {
            // Keep unknown explicit references as 0, because '@NotFound:1'
            // cannot be parsed by NCalc anyway. Bare unknown text is preserved.
            if (explicitReference)
            {
                value = 0;
            }
            else
            {
                return match.Value;
            }
        }

        string variableName = "__ol_tag_" + injectedValues.Count.ToString(CultureInfo.InvariantCulture);
        injectedValues[variableName] = value;
        return variableName;
    }

    private static string[] ParseColonArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return Array.Empty<string>();
        }

        return args.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(arg => arg.Trim())
            .Where(arg => arg.Length > 0)
            .ToArray();
    }

    private static bool TryReadTagAsDouble(string tagName, string[] args, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return false;
        }

        // Avoid accidental self-recursion.
        if (tagName.Equals(nameof(Expr), StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals(nameof(Calc), StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals(nameof(ExprText), StringComparison.OrdinalIgnoreCase) ||
            tagName.Equals(nameof(IfExpr), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        OverlayerTag overlayerTag = TagManager.GetTag(tagName) ??
            TagManager.All.FirstOrDefault(tag => tag.Name.Equals(tagName, StringComparison.OrdinalIgnoreCase));
        if (overlayerTag == null)
        {
            return false;
        }

        try
        {
            object result = InvokeTag(overlayerTag, args ?? Array.Empty<string>());
            value = ToDouble(result);
            return true;
        }
        catch
        {
            value = 0;
            return false;
        }
    }

    private static object InvokeTag(OverlayerTag overlayerTag, string[] args)
    {
        ParameterInfo[] parameters = overlayerTag.Tag.GetterOriginal.GetParameters();
        object[] callArgs = new object[overlayerTag.Tag.ArgumentCount];

        for (int i = 0; i < callArgs.Length; i++)
        {
            if (i < args.Length)
            {
                callArgs[i] = args[i];
            }
            else
            {
                object defaultValue = i < parameters.Length ? parameters[i].DefaultValue : null;
                callArgs[i] = defaultValue?.ToString() ?? string.Empty;
            }
        }

        return overlayerTag.Tag.GetterDelegate.DynamicInvoke(callArgs);
    }


    private static bool Compare(string left, string op, string right)
    {
        op = (op ?? string.Empty).Trim();
        if (TryNumber(left, out double leftNumber) && TryNumber(right, out double rightNumber))
        {
            switch (op)
            {
                case ">": return leftNumber > rightNumber;
                case ">=": return leftNumber >= rightNumber;
                case "<": return leftNumber < rightNumber;
                case "<=": return leftNumber <= rightNumber;
                case "==":
                case "=": return Math.Abs(leftNumber - rightNumber) <= double.Epsilon;
                case "!=":
                case "<>": return Math.Abs(leftNumber - rightNumber) > double.Epsilon;
            }
        }

        int cmp = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        switch (op)
        {
            case "==":
            case "=": return cmp == 0;
            case "!=":
            case "<>": return cmp != 0;
            case "contains": return (left ?? string.Empty).IndexOf(right ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
            case "starts":
            case "startswith": return (left ?? string.Empty).StartsWith(right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            case "ends":
            case "endswith": return (left ?? string.Empty).EndsWith(right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            default: return false;
        }
    }

    private static bool TryNumber(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    [ThreadStatic]
    private static int inlineDepth;

    private static string ResolveInlineTags(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0 || inlineDepth > 8 || !TagManager.Initialized)
        {
            return text ?? string.Empty;
        }

        try
        {
            inlineDepth++;
            using ReplaceableText replaceable = ReplaceableText.Create(text, TagManager.All.Select(tag => tag.Tag));
            return replaceable.Replace() ?? text;
        }
        catch
        {
            return text;
        }
        finally
        {
            inlineDepth--;
        }
    }

    private static bool TryEvaluateBuiltInFunction(string name, FunctionArgs args, out double value)
    {
        value = 0;
        string upper = name.ToUpperInvariant();
        double Arg(int index) => index < args.Parameters.Length ? ToDouble(args.Parameters[index].Evaluate()) : 0d;

        switch (upper)
        {
            case "ABS":
                value = Math.Abs(Arg(0));
                return true;
            case "CEIL":
            case "CEILING":
                value = Math.Ceiling(Arg(0));
                return true;
            case "FLOOR":
                value = Math.Floor(Arg(0));
                return true;
            case "ROUND":
                value = args.Parameters.Length > 1 ? Math.Round(Arg(0), Convert.ToInt32(Arg(1))) : Math.Round(Arg(0));
                return true;
            case "SQRT":
                value = Math.Sqrt(Arg(0));
                return true;
            case "POW":
                value = Math.Pow(Arg(0), Arg(1));
                return true;
            case "MIN":
                value = args.Parameters.Length == 0 ? 0 : args.Parameters.Select(p => ToDouble(p.Evaluate())).Min();
                return true;
            case "MAX":
                value = args.Parameters.Length == 0 ? 0 : args.Parameters.Select(p => ToDouble(p.Evaluate())).Max();
                return true;
            case "CLAMP":
                value = Math.Max(Arg(1), Math.Min(Arg(2), Arg(0)));
                return true;
            case "SIGN":
                value = Math.Sign(Arg(0));
                return true;
            case "IF":
                value = Math.Abs(Arg(0)) > double.Epsilon ? Arg(1) : Arg(2);
                return true;
            default:
                return false;
        }
    }

    private static double ToDouble(object value)
    {
        try
        {
            if (value == null)
            {
                return 0;
            }

            if (value is bool boolValue)
            {
                return boolValue ? 1 : 0;
            }

            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            try
            {
                return Convert.ToDouble(Convert.ToString(value), CultureInfo.CurrentCulture);
            }
            catch
            {
                return 0;
            }
        }
    }
}
