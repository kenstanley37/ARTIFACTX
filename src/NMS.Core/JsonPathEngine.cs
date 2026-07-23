using NMS.Core.Models;
using System.Text.Json;

namespace NMS.Core;

public static class JsonPathEngine
{
    public static List<JsonNodeMatch> SearchAncestry(byte[] rawJsonBytes, string targetSearchKey)
    {
        var results = new List<JsonNodeMatch>();
        if (string.IsNullOrWhiteSpace(targetSearchKey) || rawJsonBytes == null || rawJsonBytes.Length == 0)
            return results;

        var readerOptions = new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        // Create an isolated secondary reader clone instance for deep value lookaheads
        var reader = new Utf8JsonReader(rawJsonBytes, readerOptions);
        var pathStack = new Stack<string>();
        string currentProperty = "Root";
        int matchCounter = 0;

        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        currentProperty = reader.GetString() ?? "";

                        // 📍 STICKY KEY MATCH FOUND
                        if (currentProperty.Equals(targetSearchKey, StringComparison.OrdinalIgnoreCase))
                        {
                            matchCounter++;
                            string extractedValueText = "[Complex Object/Array Block]";

                            // Clone the reader current state to peek at the value token without disrupting the primary stack walk loop
                            var peeker = reader;
                            if (peeker.Read())
                            {
                                extractedValueText = peeker.TokenType switch
                                {
                                    JsonTokenType.String => peeker.GetString() ?? "null",
                                    JsonTokenType.Number => peeker.TryGetInt64(out long lVal) ? lVal.ToString() : (peeker.TryGetDouble(out double dVal) ? dVal.ToString() : "0"),
                                    JsonTokenType.True => "true",
                                    JsonTokenType.False => "false",
                                    JsonTokenType.Null => "null",
                                    JsonTokenType.StartObject => "{ ... }",
                                    JsonTokenType.StartArray => "[ ... ]",
                                    _ => $"[{peeker.TokenType}]"
                                };
                            }

                            results.Add(BuildMatchRecord(matchCounter, pathStack, currentProperty, extractedValueText));
                        }
                        break;

                    case JsonTokenType.StartObject:
                        pathStack.Push(currentProperty);
                        currentProperty = "ObjectElement";
                        break;

                    case JsonTokenType.EndObject:
                        if (pathStack.Count > 0) pathStack.Pop();
                        break;

                    case JsonTokenType.StartArray:
                        pathStack.Push($"{currentProperty}[]");
                        break;

                    case JsonTokenType.EndArray:
                        if (pathStack.Count > 0) pathStack.Pop();
                        break;
                }
            }
        }
        catch (JsonException)
        {
            // Gracefully catch unexpected document terminations safely
        }

        return results;
    }

    private static JsonNodeMatch BuildMatchRecord(int index, Stack<string> stack, string property, string value)
    {
        var pathList = new List<string>(stack);
        pathList.Reverse();

        var lineageStrings = new List<string>();
        for (int i = 0; i < pathList.Count; i++)
        {
            if (pathList[i] != "Root" && pathList[i] != "ObjectElement")
            {
                string indent = new string(' ', i * 3);
                lineageStrings.Add($"{indent}└── {pathList[i]}");
            }
        }

        string targetIndent = new string(' ', lineageStrings.Count * 3);
        lineageStrings.Add($"{targetIndent}└── 📍 {property} (= {value})");

        var plainTextPath = string.Join(" -> ", pathList).Replace(" -> ObjectElement", "") + $" -> {property}";

        return new JsonNodeMatch
        {
            Index = index,
            TargetKey = property,
            ExtractedValue = value,
            FullPathDisplay = plainTextPath,
            TreeLineage = lineageStrings
        };
    }
}