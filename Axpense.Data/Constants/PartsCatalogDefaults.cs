namespace Axpense.Data.Constants
{
    /// <summary>The starter parts catalogue every new organization receives (7 groups, 36 parts).</summary>
    public static class PartsCatalogDefaults
    {
        public sealed record DefaultPart(string Name, string NameAr);
        public sealed record DefaultCategory(string Name, string NameAr, DefaultPart[] Parts);

        public static readonly DefaultCategory[] Categories =
        [
            new("Engine Group", "مجموعة المحرك",
            [
                new("Engine Block", "كتلة المحرك"), new("Cylinder Head", "رأس السلندر"), new("Piston", "المكبس"),
                new("Crankshaft", "عمود الكرنك"), new("Timing Belt / Chain", "سير التايمينج / جنزير المحرك"),
                new("Spark Plugs", "البواجي / شمعات الاحتراق"), new("Fuel Injectors", "الرشاشات / حقن الوقود")
            ]),
            new("Cooling & Intake", "نظام التبريد والتهوية",
            [
                new("Radiator", "الردياتير"), new("Water Pump", "طلمبة المياه"), new("Thermostat", "ترموستات الحرارة"),
                new("Air Filter", "فلتر الهواء"), new("Turbocharger", "التيربو")
            ]),
            new("Transmission & Drivetrain", "ناقل الحركة والدفع",
            [
                new("Gearbox", "صندوق التروس / القير"), new("Clutch Kit", "طقم الدبرياج"), new("Drive Shaft", "عمود الكردان"),
                new("CV Joint", "الكوبلن"), new("Differential", "الديفرانس / الكورونا")
            ]),
            new("Suspension & Steering", "نظام التعليق والتوجيه",
            [
                new("Shock Absorbers", "المساعدين"), new("Control Arms", "المقصات"), new("Ball Joints", "البيضاوي / الجوزات"),
                new("Steering Rack", "علبة الدركسيون"), new("Power Steering Pump", "طلمبة الباور")
            ]),
            new("Braking System", "نظام الفرامل",
            [
                new("Brake Pads", "تيل الفرامل"), new("Brake Discs", "الطنابير / ديسكات الفرامل"),
                new("Master Cylinder", "مستر الفرامل الرئيسي"), new("Brake Hoses", "خراطيم الفرامل")
            ]),
            new("Electrical & Sensors", "الكهرباء والحساسات",
            [
                new("Battery", "البطارية"), new("Alternator", "الدينامو"), new("Starter Motor", "المارش"),
                new("Oxygen Sensor", "حساس الأكسجين"), new("ECU", "كمبيوتر السيارة")
            ]),
            new("Body & Consumables", "الاستهلاكيات والهيكل",
            [
                new("Tyres", "الإطارات"), new("Wheels", "الجنوط"), new("Oil Filter", "فلتر الزيت"),
                new("Serpentine Belt", "سيور المحرك الخارجية"), new("Wipers", "المساحات")
            ])
        ];

        public static string PartCode(int categoryNumber, int sequence) => $"P{categoryNumber}{sequence:00}";
    }
}
