/** Flat side-view illustration of a vehicle by category (used in lists, panels and profiles). */

const COLORS: Record<string, string> = {
  Truck: "#E0533D",
  Van: "#16A085",
  Bus: "#1F6FD1",
  Sedan: "#2E86DE",
  Pickup: "#8E6CD8",
  Machinery: "#F2B418",
  Motorcycle: "#C2410C",
  Trailer: "#64748B",
};

export function vehicleColor(category: string) {
  return COLORS[category] ?? "#5B7A99";
}

const Wheel = ({ x }: { x: number }) => (
  <g>
    <circle cx={x} cy={31} r={4.6} fill="#1C2733" />
    <circle cx={x} cy={31} r={1.8} fill="#C9D3DD" />
  </g>
);

export function VehicleArt({ category, className }: { category: string; className?: string }) {
  const c = vehicleColor(category);
  const glass = "#CFE6F7";
  let body;
  switch (category) {
    case "Truck":
      body = (
        <>
          <rect x="3" y="8" width="38" height="20" rx="2" fill={c} />
          <path d="M42 14h9l6 7v7H42z" fill={c} />
          <path d="M45 16h5l4 5h-9z" fill={glass} />
          <Wheel x={12} />
          <Wheel x={22} />
          <Wheel x={51} />
        </>
      );
      break;
    case "Bus":
      body = (
        <>
          <rect x="3" y="9" width="56" height="19" rx="4" fill={c} />
          {[8, 17, 26, 35, 44].map((x) => (
            <rect key={x} x={x} y="12" width="7" height="6" rx="1" fill={glass} />
          ))}
          <rect x="52" y="12" width="5" height="9" rx="1" fill={glass} />
          <rect x="3" y="21" width="56" height="2" fill="#fff" opacity=".6" />
          <Wheel x={14} />
          <Wheel x={48} />
        </>
      );
      break;
    case "Sedan":
      body = (
        <>
          <path d="M5 26v-5l7-2 7-7h19l9 7 9 2v5z" fill={c} />
          <path d="M21 13h8v6H15zM31 13h7l6 6H31z" fill={glass} />
          <Wheel x={16} />
          <Wheel x={47} />
        </>
      );
      break;
    case "Pickup":
      body = (
        <>
          <path d="M4 18h28v10H4z" fill={c} />
          <path d="M32 10h14l7 8h5v10H32z" fill={c} />
          <path d="M35 12h9l5 6H35z" fill={glass} />
          <Wheel x={14} />
          <Wheel x={48} />
        </>
      );
      break;
    case "Machinery":
      body = (
        <>
          <rect x="6" y="26" width="30" height="7" rx="3.5" fill="#39434E" />
          <rect x="8" y="14" width="24" height="12" rx="2" fill={c} />
          <rect x="22" y="9" width="10" height="10" rx="1.5" fill={c} />
          <rect x="24" y="11" width="6" height="5" fill={glass} />
          <path d="M32 16l14-8 10 10-3 3-8-7-11 7z" fill={c} />
          <path d="M53 19l4 6h-8z" fill="#39434E" />
        </>
      );
      break;
    case "Motorcycle":
      body = (
        <>
          <circle cx={14} cy={28} r={7} fill="none" stroke="#1C2733" strokeWidth={3} />
          <circle cx={48} cy={28} r={7} fill="none" stroke="#1C2733" strokeWidth={3} />
          <path d="M14 28l10-10h14l10 10" fill="none" stroke={c} strokeWidth={3} strokeLinejoin="round" />
          <path d="M22 16h16l4 5H24z" fill={c} />
          <path d="M40 13l6-3" stroke="#39434E" strokeWidth={2.4} strokeLinecap="round" />
          <rect x="26" y="12" width="10" height="4" rx="2" fill="#39434E" />
        </>
      );
      break;
    case "Trailer":
      body = (
        <>
          <rect x="10" y="8" width="48" height="19" rx="2" fill={c} />
          <rect x="10" y="22" width="48" height="2" fill="#fff" opacity=".5" />
          <path d="M3 25h8" stroke="#39434E" strokeWidth={2.4} strokeLinecap="round" />
          <Wheel x={40} />
          <Wheel x={50} />
        </>
      );
      break;
    default: // Van
      body = (
        <>
          <path d="M4 10h38l10 8h6v10H4z" fill={c} />
          <path d="M43 12l7 6h-7z" fill={glass} />
          <rect x="30" y="12" width="9" height="6" rx="1" fill={glass} />
          <Wheel x={15} />
          <Wheel x={47} />
        </>
      );
  }
  return (
    <svg viewBox="0 0 62 38" className={className} aria-hidden="true">
      {body}
    </svg>
  );
}
