namespace NorthWind.Sales.Entities.Dtos;

public static class TextInputRules
{
    public static bool IsLetter(char value) => char.IsLetter(value) && value <= '\u024f' && value is not 'º' and not 'ª';
    public static bool IsNameCharacter(char value) => IsLetter(value) || value is ' ' or '-' or '.' or '\'' or '’';
    public static bool IsPlaceCharacter(char value) => IsLetter(value) || value is ' ' or '-' or '\'' or '’';
    public static bool IsAddressCharacter(char value) => IsLetter(value) || value is >= '0' and <= '9' || " .,-'#/ºª°():’&".Contains(value);
    public static bool IsName(string value) => !string.IsNullOrWhiteSpace(value) && value.Any(IsLetter) && value.All(IsNameCharacter);
    public static bool IsPlace(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.All(IsPlaceCharacter)) return false;
        string name = value.Trim(' ');
        if (!IsLetter(name[0]) || !IsLetter(name[^1])) return false;
        for (int index = 1; index < name.Length; index++)
            if (!IsLetter(name[index]) && !IsLetter(name[index - 1])) return false;
        return true;
    }
    public static bool IsAddress(string value) => !string.IsNullOrWhiteSpace(value) && value.Any(c => IsLetter(c) || c is >= '0' and <= '9') && value.All(IsAddressCharacter);
    public static bool IsBusinessNameCharacter(char value) => IsNameCharacter(value) || value is >= '0' and <= '9' || "&,()/".Contains(value);
    public static bool IsProductNameCharacter(char value) => IsBusinessNameCharacter(value) || ":;%+ºª°#[]\"".Contains(value);
    public static bool IsProductName(string value) => !string.IsNullOrWhiteSpace(value) &&
        value.Any(c => IsLetter(c) || c is >= '0' and <= '9') && value.All(IsProductNameCharacter) &&
        value.Trim(' ') == CleanProductName(value.Trim(' '));

    public static string CleanProductName(string value)
    {
        var result = new System.Text.StringBuilder();
        char significant = '\0';
        static bool Alphanumeric(char c) => IsLetter(c) || c is >= '0' and <= '9';
        foreach (char character in value.Normalize())
        {
            if (!IsProductNameCharacter(character)) continue;
            if (Alphanumeric(character) || (result.Length > 0 && (character == ' '
                ? result[result.Length - 1] != ' '
                : Alphanumeric(significant) || (")]\"".Contains(character) && character != significant && "%ºª°)]".Contains(significant)))))
            {
                result.Append(character);
                if (character != ' ') significant = character;
            }
        }
        return result.ToString();
    }
}
