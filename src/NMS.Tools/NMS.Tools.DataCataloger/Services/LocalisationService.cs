using System.Reflection;
using libMBIN;

namespace NMS.Tools.DataCataloger.Services;

public static class LocalisationService
{
    /// <summary>
    /// Reflectively walks a decoded localisation-table-shaped object (anything with a
    /// public List&lt;T&gt; field whose elements have "Id"/"ID" + "English" fields) into a
    /// flat lookup dictionary. Done generically rather than casting to TkLocalisationTable
    /// directly so this doesn't break if libMBIN's exact class/field names shift between
    /// versions - only the *shape* (list of id/text pairs) needs to hold.
    /// </summary>
    public static Dictionary<string, string> BuildEnglishLookup(NMSTemplate locTableTemplate)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var tableField = ReflectionUtil.FindListField(locTableTemplate);
        if (tableField == null) return result;

        if (tableField.GetValue(locTableTemplate) is not System.Collections.IEnumerable list)
            return result;

        foreach (var row in list)
        {
            if (row == null) continue;

            var idField = row.GetType().GetField("Id") ?? row.GetType().GetField("ID");
            string? id = idField != null ? ReflectionUtil.ExtractString(idField.GetValue(row)) : null;
            if (string.IsNullOrEmpty(id)) continue;

            // TkLocalisationEntry has BOTH "English" and "USEnglish" as separate fields
            // on the same row (not either/or) - and some entries ship with "English"
            // deliberately blank while the real text only lives in "USEnglish". So we
            // fall back based on the VALUE being empty, not just the field being absent.
            string? text = null;

            var englishField = row.GetType().GetField("English");
            if (englishField != null)
                text = ReflectionUtil.ExtractString(englishField.GetValue(row));

            if (string.IsNullOrEmpty(text))
            {
                var usEnglishField = row.GetType().GetField("USEnglish");
                if (usEnglishField != null)
                    text = ReflectionUtil.ExtractString(usEnglishField.GetValue(row));
            }

            if (!string.IsNullOrEmpty(text))
                result[id] = text;
        }

        return result;
    }
}