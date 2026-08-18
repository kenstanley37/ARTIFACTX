using System.Text;
using System.Text.Json;

namespace ArtifactX.Core;

public static class StructureAnalyzer
{
    /// <summary>
    /// Searches for a target key or string value and prints out the exact parent-child structural layout hierarchy.
    /// </summary>
    public static void HuntJsonKeyAncestry(byte[] rawUncompressedJson, string searchTarget)
    {
        var reader = new Utf8JsonReader(rawUncompressedJson);
        // Using Stack<string> correctly provides Push() and Pop() functionality
        var pathStack = new Stack<string>();
        string currentProperty = "Root";
        int matchCount = 0;

        Console.WriteLine($"\n[ArtifactX-STRUCTURE-ANALYZER] Initiating strict ancestry hunt for target: '{searchTarget}'");

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    currentProperty = reader.GetString() ?? "";
                    break;

                case JsonTokenType.StartObject:
                    pathStack.Push(currentProperty);
                    currentProperty = "ObjectElement"; // Placeholder for unnamed elements inside objects
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

                case JsonTokenType.String:
                    string stringValue = reader.GetString() ?? "";
                    if (currentProperty.Equals(searchTarget, StringComparison.OrdinalIgnoreCase) ||
                        stringValue.Equals(searchTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        matchCount++;
                        PrintAncestryMatch(matchCount, pathStack, currentProperty, stringValue);
                    }
                    break;

                case JsonTokenType.Number:
                    if (currentProperty.Equals(searchTarget, StringComparison.OrdinalIgnoreCase))
                    {
                        matchCount++;
                        PrintAncestryMatch(matchCount, pathStack, currentProperty, reader.GetDouble().ToString());
                    }
                    break;
            }
        }

        Console.WriteLine($"[ArtifactX-STRUCTURE-ANALYZER] Completed scan. Total strict parent instances discovered: {matchCount}\n");
    }

    private static void PrintAncestryMatch(int index, Stack<string> stack, string property, string value)
    {
        var builder = new StringBuilder();
        builder.Append("ROOT -> ");

        // Stack enumeration goes from top-of-stack to bottom, so we reverse it to print chronologically
        var chronologicalPath = new List<string>(stack);
        chronologicalPath.Reverse();

        foreach (var parent in chronologicalPath)
        {
            if (parent != "Root" && parent != "ObjectElement")
            {
                builder.Append(parent).Append(" -> ");
            }
        }

        builder.Append(property);

        System.Diagnostics.Debug.WriteLine($"[ANCESTRY MATCH #{index}]");
        System.Diagnostics.Debug.WriteLine($"   📍 Node Path: {builder.ToString()}");
        System.Diagnostics.Debug.WriteLine($"   📄 Node Value: {value}");
        System.Diagnostics.Debug.WriteLine(new string('-', 60));
    }
}