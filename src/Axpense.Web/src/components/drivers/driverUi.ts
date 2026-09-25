/** Presentation helpers shared by the Drivers list and profile. The day counts come from the API. */

export const EXPIRY_REMINDER_DAYS = 30;

export type BadgeClass = "ok" | "warn" | "bad" | "plain" | "info";

export function expiryTone(daysLeft: number | null | undefined): BadgeClass {
  if (daysLeft == null) return "plain";
  if (daysLeft < 0) return "bad";
  return daysLeft <= EXPIRY_REMINDER_DAYS ? "warn" : "ok";
}

export function statusTone(status: string): BadgeClass {
  return status === "Active" ? "ok" : status === "Suspended" ? "bad" : "plain";
}

export function ratingBarTone(rating: number) {
  return rating < 3.5 ? "bad" : rating < 4.2 ? "warn" : "";
}

export function documentStateLabel(state: string, daysLeft: number) {
  return state === "Expired" ? "Expired" : state === "Expiring" ? `${daysLeft} days left` : "Valid";
}

export function documentStateTone(state: string): BadgeClass {
  return state === "Expired" ? "bad" : state === "Expiring" ? "warn" : "ok";
}
