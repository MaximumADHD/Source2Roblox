using System.Globalization;
using System.Linq;

public static class Format
{
    private const string DecimalFmt = "0.00";
    private static CultureInfo Invariant => CultureInfo.InvariantCulture;

    private static string FilterNan(string value, string replace = DecimalFmt)
    {
        if (value.ToLower() == "nan")
            value = replace;

        return value;
    }

    public static string ToInvariantString(this float value)
    {
        string result = value.ToString(DecimalFmt, Invariant);
        return FilterNan(result);
    }

    public static string ToInvariantString(this double value)
    {
        string result = value.ToString(DecimalFmt, Invariant);
        return FilterNan(result);
    }

    public static string ToInvariantString(this int value)
    {
        return value.ToString(Invariant);
    }

    public static string ToInvariantString(this object value)
    {
        if (value is float f)
            return f.ToInvariantString();
        else if (value is double d)
            return d.ToInvariantString();
        else if (value is int i)
            return i.ToInvariantString();
        
        // Unhandled
        return value.ToString();
    }

    public static float ParseFloat(string s)
    {
        return float.Parse(s, Invariant);
    }

    public static double ParseDouble(string s)
    {
        return double.Parse(s, Invariant);
    }

    public static int ParseInt(string s)
    {
        return int.Parse(s, Invariant);
    }

    public static string FormatFloats(params float[] values)
    {
        string[] results = values
            .Select(value => value.ToInvariantString())
            .ToArray();

        for (int i = 0; i < results.Length; i++)
        {
            string result = results[i];

            while (result.Contains(".") && (result.EndsWith("0") || result.EndsWith(".")))
                result = result.Substring(0, result.Length - 1);

            results[i] = result;
        }

        return '[' + string.Join("][", results) + ']';
    }
}