namespace Axpense.Data.Constants
{
    public static class InspectionConstants
    {
        public const string StatusInProgress = "In progress";
        public const string StatusCompleted = "Completed";

        public const string ScopeAll = "All";

        public const string FieldPassFail = "passfail";
        public const string FieldGauge = "gauge";
        public const string FieldScale = "scale";
        public const string FieldPhoto = "photo";
        public static readonly string[] FieldTypes = [FieldPassFail, FieldGauge, FieldScale, FieldPhoto];
        public static string FieldLabel(string type) => type switch
        {
            FieldGauge => "Numeric / gauge",
            FieldScale => "Scale 1–5",
            FieldPhoto => "Photo only",
            _ => "Pass / Fail"
        };

        public const string ResultPass = "pass";
        public const string ResultFail = "fail";
        public const string ResultNa = "na";
        public static readonly string[] Results = [ResultPass, ResultFail, ResultNa];

        public const int MaxChecks = 60;

        // ---- Built-in hand-off template (created on first use; checks linked to catalogue parts by name)
        public const string SystemHandoff = "handoff";
        public const string HandoffTemplateName = "Handoff inspection";
        public sealed record DefaultCheck(string Label, string PartName, string FieldType, bool Critical);
        public static readonly DefaultCheck[] HandoffChecks =
        [
            new("Tyres — condition & pressure", "Tyres", FieldPassFail, true),
            new("Brakes — pedal feel & pads", "Brake Pads", FieldPassFail, true),
            new("Lights, indicators & wipers", "Wipers", FieldPassFail, false),
            new("Coolant level", "Radiator", FieldScale, false),
            new("Engine oil level", "Oil Filter", FieldScale, false),
            new("Battery & dashboard warning lights", "Battery", FieldPassFail, false),
            new("Body, mirrors & wheels — walk-around", "Wheels", FieldPhoto, false),
            new("Steering play", "Steering Rack", FieldPassFail, true)
        ];
        public const long MaxPhotoBytes = 5 * 1024 * 1024;
    }
}
