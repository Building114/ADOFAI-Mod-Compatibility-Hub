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
    [TagDesc("计算数学表达式并返回数字，可直接使用无参数标签名\n支持ABS、ROUND、MIN、MAX、CLAMP、SQRT、POW和IF等函数\n示例:{Expr:OVE+OVL}或{Expr:@PlayerHit:VeryEarly:2+@PlayerHit:VeryLate:2}")]
    public static double Expr(string expression, int digits = -1) => EvaluateNumber(expression).Round(digits);

    [Tag("Calc")]
    [TagDesc("Expr的简写，计算数学表达式并返回数字\n示例:{Calc:1+2*3}返回7")]
    public static double Calc(string expression, int digits = -1) => Expr(expression, digits);

    [Tag]
    [TagDesc("计算数学表达式并按指定格式输出文本\n第二个参数使用数字格式，默认0.###\n示例:{ExprText:10/3:0.00}返回3.33")]
    public static string ExprText(string expression, string format = "0.###")
    {
        double value = EvaluateNumber(expression);
        return string.IsNullOrWhiteSpace(format)
            ? value.ToString(CultureInfo.InvariantCulture)
            : value.ToString(format, CultureInfo.InvariantCulture);
    }

    [Tag]
    [TagDesc("根据数学表达式选择文本，结果非0时返回第二个参数，否则返回第三个参数\n返回文本中可以继续使用标签\n示例:{IfExpr:OVE>0:偏早:正常}")]
    public static string IfExpr(string expression, string trueText = "1", string falseText = "0")
    {
        return Math.Abs(EvaluateNumber(expression)) > double.Epsilon
            ? ResolveInlineTags(trueText)
            : ResolveInlineTags(falseText);
    }

    [Tag]
    [TagDesc("比较两段文本或数字并选择结果\n支持=、==、!=、<>、>、>=、<、<=、contains、starts和ends\n示例:{IfText:{DifficultyRaw}:=:Strict:严格:其他}")]
    public static string IfText(string left, string op, string right, string trueText = "1", string falseText = "0")
    {
        bool ok = Compare(ResolveInlineTags(left), op, ResolveInlineTags(right));
        return ok ? ResolveInlineTags(trueText) : ResolveInlineTags(falseText);
    }

    [Tag]
    [TagDesc("从左到右返回第一个有效内容，空文本、0和False会继续检查下一个参数\n适合给可能缺失的标签设置备用文本\n示例:{Coalesce:{XPerfectLastJudge}:{CHit}:无判定}")]
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

                                                                       
                          
                                                                    
        result = BareColonTagRef.Replace(result, match => ReplaceOneTagReference(match, injectedValues, false));

        return result;
    }

    private static string ReplaceOneTagReference(Match match, Dictionary<string, double> injectedValues, bool explicitReference)
    {
        string name = match.Groups["name"].Value;
        string[] tagArgs = ParseColonArgs(match.Groups["args"].Value);

        if (!TryReadTagAsDouble(name, tagArgs, out double value))
        {
                                                                           
                                                                                
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
