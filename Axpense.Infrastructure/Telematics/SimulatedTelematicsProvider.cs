using Axpense.Infrastructure.Intertfaces;

namespace Axpense.Infrastructure.Telematics
{
    /// <summary>
    /// Deterministic telemetry for demos and development (Greater Cairo). Each vehicle gets a
    /// stable scenario from its id: some drive along real corridors, others idle or park at real
    /// addresses. Positions advance with the clock, so polling shows vehicles moving.
    /// Replace with a real adapter via DI — nothing else changes.
    /// </summary>
    public sealed class SimulatedTelematicsProvider : ITelematicsProvider
    {
        private sealed record Waypoint(double Lat, double Lng, string Address);
        private sealed record Route(string Origin, string Destination, int AverageSpeedKmh, Waypoint[] Points);
        private sealed record Spot(double Lat, double Lng, string Address, string Area);

        private static readonly Route[] Routes =
        [
            new("Cairo hub — Badr City", "Alexandria hub — Borg El Arab", 72,
            [
                new(30.1374, 31.7108, "Cairo–Suez Road, Badr City, Cairo"),
                new(30.1580, 31.6000, "Cairo–Ismailia Road, El Shorouk, Cairo"),
                new(30.1890, 31.4800, "Cairo–Ismailia Road, Obour interchange, Cairo"),
                new(30.1310, 31.3700, "Ring Road, Cairo Airport, Cairo"),
                new(30.1180, 31.2400, "Ring Road, Shubra El Kheima, Qalyubia"),
                new(30.0950, 31.1600, "Ring Road, Imbaba, Giza"),
                new(30.0600, 31.0400, "Ring Road, Sheikh Zayed exit, Giza"),
                new(30.0650, 30.9500, "Cairo–Alexandria Desert Road, Sheikh Zayed, Giza"),
                new(30.2200, 30.7500, "Cairo–Alexandria Desert Road, km 60, Giza"),
                new(30.4200, 30.4500, "Cairo–Alexandria Desert Road, Wadi El Natrun, Beheira")
            ]),
            new("Giza depot", "6th of October Industrial Zone", 58,
            [
                new(29.9950, 31.1700, "Pyramids Road, Giza"),
                new(29.9990, 31.1300, "26th of July Corridor, Haram, Giza"),
                new(30.0150, 31.0400, "26th of July Corridor, Sheikh Zayed, Giza"),
                new(29.9800, 30.9600, "Central Axis, 6th of October, Giza"),
                new(29.9300, 30.9300, "Industrial Zone 3, 6th of October, Giza")
            ]),
            new("New Cairo depot", "Downtown office", 46,
            [
                new(30.0290, 31.4700, "90th Street, 5th Settlement, New Cairo"),
                new(30.0450, 31.4000, "Cairo–Suez Road, New Cairo"),
                new(30.0560, 31.3300, "Mostafa El Nahas St, Nasr City, Cairo"),
                new(30.0620, 31.2800, "6th of October Bridge, Abbasiya, Cairo"),
                new(30.0500, 31.2400, "Ramses St, Downtown, Cairo")
            ]),
            new("Cairo HQ — Maadi", "Heliopolis branch", 42,
            [
                new(29.9600, 31.2570, "Corniche El Nil, Maadi, Cairo"),
                new(30.0070, 31.2320, "Corniche El Nil, Old Cairo, Cairo"),
                new(30.0444, 31.2357, "Tahrir Square, Downtown, Cairo"),
                new(30.0750, 31.2850, "Salah Salem Road, Abbasiya, Cairo"),
                new(30.0900, 31.3220, "El Korba, Heliopolis, Cairo")
            ]),
            new("10th of Ramadan plant", "Obour warehouse", 64,
            [
                new(30.2920, 31.7420, "Industrial Zone A1, 10th of Ramadan, Sharqia"),
                new(30.2500, 31.6000, "Cairo–Ismailia Road, Badr exit, Cairo"),
                new(30.2270, 31.4750, "Obour Industrial Zone, Obour City, Qalyubia")
            ])
        ];

        private static readonly Spot[] Spots =
        [
            new(30.0490, 31.2400, "24 Kasr El Nile St, Downtown, Cairo", "Downtown"),
            new(30.0600, 31.3450, "Makram Ebeid St, Nasr City, Cairo", "Nasr City"),
            new(30.1169, 31.7353, "Industrial Zone A2, Badr City, Cairo", "Obour"),
            new(29.9650, 31.2520, "12 Nile Corniche, Maadi, Cairo", "Maadi"),
            new(29.9950, 31.1700, "Giza depot, Pyramids Road, Giza", "Giza"),
            new(30.0100, 31.4300, "Al-Amal Workshop, 5th Settlement, New Cairo", "New Cairo"),
            new(30.0900, 31.3400, "El Merghany St, Heliopolis, Cairo", "Heliopolis"),
            new(29.9300, 30.9300, "Industrial Zone 3, 6th of October, Giza", "6th of October")
        ];

        private const int WorkshopSpot = 5;

        public bool IsSimulated => true;

        public Task<IReadOnlyList<VehicleTelemetry>> GetLatestAsync(Guid organizationId, IReadOnlyList<TelemetryVehicle> vehicles, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            IReadOnlyList<VehicleTelemetry> result = vehicles.Select(v => Simulate(v, now)).ToList();
            return Task.FromResult(result);
        }

        private static VehicleTelemetry Simulate(TelemetryVehicle v, DateTime now)
        {
            var seed = StableSeed(v.VehicleId);
            var electric = v.FuelType == "Electric";
            var perKm = ConsumptionPerKm(v.Category, electric);
            var cairoNow = now.AddHours(2); // Egypt local time for the "today" window

            // Machinery never drives on public corridors; others: 45% moving, 20% idle, 35% parked.
            var roll = seed % 100;
            var motion = v.Category == "Machinery"
                ? (roll < 50 ? TelemetryMotion.Idle : TelemetryMotion.Parked)
                : roll < 45 ? TelemetryMotion.Moving : roll < 65 ? TelemetryMotion.Idle : TelemetryMotion.Parked;

            if (v.FleetStatus == Axpense.Data.Constants.VehicleConstants.StatusUnderMaintenance) motion = TelemetryMotion.Parked;

            if (motion == TelemetryMotion.Moving)
            {
                var route = Routes[seed % Routes.Length];
                var lengths = SegmentLengths(route.Points);
                var total = lengths.Sum();
                var speed = route.AverageSpeedKmh + (int)Math.Round(8 * Math.Sin(now.Ticks / TimeSpan.TicksPerSecond / 20.0 + seed));
                var tripSeconds = total / route.AverageSpeedKmh * 3600;
                var progress = ((now - now.Date).TotalSeconds / tripSeconds + (seed % 1000) / 1000.0) % 1.0;
                progress = 0.05 + progress * 0.9; // never exactly at a terminal

                var (pos, heading, address) = PointAlong(route.Points, lengths, total * progress);
                var remaining = total * (1 - progress);
                var distanceToday = Math.Round(40 + seed % 90 + total * progress, 1);
                var path = route.Points.Select(p => new GeoPoint(p.Lat, p.Lng)).ToList();

                return new VehicleTelemetry(v.VehicleId, pos, heading, Math.Max(15, speed), true, motion, address,
                    distanceToday, Math.Round(distanceToday * perKm, 1), 5 + seed % 15,
                    new TelemetryTrip(route.Origin, route.Destination, Math.Round(progress * 100, 1), Math.Round(remaining, 1),
                        now.AddHours(remaining / Math.Max(30, route.AverageSpeedKmh)), path),
                    null, now);
            }

            var spot = v.FleetStatus == Axpense.Data.Constants.VehicleConstants.StatusUnderMaintenance ? Spots[WorkshopSpot] : Spots[seed % Spots.Length];
            // Small stable offset so vehicles sharing a spot don't overlap exactly.
            var jitter = ((seed / 7) % 9 - 4) * 0.0012;
            var position = new GeoPoint(spot.Lat + jitter, spot.Lng - jitter);
            var km = motion == TelemetryMotion.Idle ? 20 + seed % 40 : seed % 3 == 0 ? 0 : 10 + seed % 45;
            var lastAt = cairoNow.Date.AddHours(6 + seed % 5).AddMinutes(seed % 60);
            var lastTrip = km == 0
                ? $"Returned to {spot.Area} · yesterday {17 + seed % 3}:{seed % 60:00}"
                : $"Depot → {spot.Area} · today {lastAt:HH:mm}";

            return new VehicleTelemetry(v.VehicleId, position, 0, 0, motion == TelemetryMotion.Idle, motion, spot.Address,
                km, Math.Round(km * perKm, 1), km == 0 ? 0 : 4 + seed % 20, null, lastTrip, now);
        }

        private static double ConsumptionPerKm(string category, bool electric) =>
            electric
                ? (category == "Sedan" ? 0.16 : 0.17)
                : category switch
                {
                    "Truck" => 0.32,
                    "Bus" => 0.22,
                    "Machinery" => 0.45,
                    "Sedan" => 0.08,
                    _ => 0.12
                };

        private static int StableSeed(Guid id)
        {
            var bytes = id.ToByteArray();
            var h = 17;
            foreach (var b in bytes) h = unchecked(h * 31 + b);
            return Math.Abs(h % 100_000);
        }

        private static double[] SegmentLengths(Waypoint[] pts)
        {
            var l = new double[pts.Length - 1];
            for (var i = 0; i < l.Length; i++) l[i] = HaversineKm(pts[i].Lat, pts[i].Lng, pts[i + 1].Lat, pts[i + 1].Lng);
            return l;
        }

        private static (GeoPoint Position, double Heading, string Address) PointAlong(Waypoint[] pts, double[] lengths, double km)
        {
            for (var i = 0; i < lengths.Length; i++)
            {
                if (km <= lengths[i] || i == lengths.Length - 1)
                {
                    var t = Math.Clamp(km / lengths[i], 0, 1);
                    var a = pts[i];
                    var b = pts[i + 1];
                    var pos = new GeoPoint(a.Lat + (b.Lat - a.Lat) * t, a.Lng + (b.Lng - a.Lng) * t);
                    return (pos, Bearing(a.Lat, a.Lng, b.Lat, b.Lng), t < 0.5 ? a.Address : b.Address);
                }
                km -= lengths[i];
            }
            var last = pts[^1];
            return (new GeoPoint(last.Lat, last.Lng), 0, last.Address);
        }

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double r = 6371;
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * r * Math.Asin(Math.Sqrt(a));
        }

        private static double Bearing(double lat1, double lon1, double lat2, double lon2)
        {
            var y = Math.Sin(ToRad(lon2 - lon1)) * Math.Cos(ToRad(lat2));
            var x = Math.Cos(ToRad(lat1)) * Math.Sin(ToRad(lat2)) - Math.Sin(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Cos(ToRad(lon2 - lon1));
            return (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360;
        }

        private static double ToRad(double deg) => deg * Math.PI / 180;
    }
}
