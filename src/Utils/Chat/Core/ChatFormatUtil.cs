namespace VsQuest
{
    public static class ChatFormatUtil
    {
        public static string Font(string text, string hexColor)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (string.IsNullOrWhiteSpace(hexColor)) return text;

            return $"<font color=\"{hexColor}\">{text}</font>";
        }

        public static string PrefixAlert(string text)
        {
            return $"{Font("[!] ", "#ff5555")}{Font(text, "#ffffff")}";
        }
    }
}
