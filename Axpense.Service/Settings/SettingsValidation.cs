namespace Axpense.Service.Settings
{
    internal static class SettingsValidation
    {
        public static Dictionary<string, string> Name(string? value, string label, int max, out string name)
        {
            var e = new Dictionary<string, string>();
            name = value?.Trim() ?? "";
            if (name.Length == 0) e["name"] = $"{label} is required.";
            else if (name.Length > max) e["name"] = $"{label} must be {max} characters or fewer.";
            return e;
        }
    }
}
