using System.Reflection;

namespace NMS.Tools.DataCataloger.Services;

public static class ReflectionUtil
{
    /// <summary>Finds the first public instance field whose type is a generic List&lt;T&gt;.</summary>
    public static FieldInfo? FindListField(object obj)
    {
        foreach (var field in obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType.IsGenericType &&
                field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return field;
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts a plain string from a field value that's either a real System.String, or one
    /// of libMBIN's NMSTemplate-derived string wrapper types (NMSString0x10, NMSString0x20A,
    /// VariableSizeString, etc.) - all of which expose a public "Value" field of type string
    /// rather than being a System.String themselves. Returns null if the value isn't
    /// string-shaped at all (so callers can distinguish "not a string field" from "empty string").
    /// </summary>
    public static string? ExtractString(object? fieldValue)
    {
        if (fieldValue == null) return null;
        if (fieldValue is string s) return s;

        var valueField = fieldValue.GetType().GetField("Value");
        if (valueField != null && valueField.FieldType == typeof(string))
            return valueField.GetValue(fieldValue) as string;

        return null;
    }
}