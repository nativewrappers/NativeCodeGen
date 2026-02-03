namespace NativeCodeGen.Core.Utilities;

/// <summary>
/// Shared utility methods for lexers.
/// </summary>
public static class LexerUtilities
{
    /// <summary>
    /// Checks if a character is a valid hexadecimal digit (0-9, a-f, A-F).
    /// </summary>
    public static bool IsHexDigit(char c) =>
        char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

    /// <summary>
    /// Checks if a character can start an identifier (letter or underscore).
    /// </summary>
    public static bool IsIdentifierStart(char c) =>
        char.IsLetter(c) || c == '_';

    /// <summary>
    /// Checks if a character can be part of an identifier (letter, digit, or underscore).
    /// </summary>
    public static bool IsIdentifierPart(char c) =>
        char.IsLetterOrDigit(c) || c == '_';
}
