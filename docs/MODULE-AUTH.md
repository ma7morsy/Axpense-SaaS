# Sign in, register, onboarding and password reset

## Screens

All auth screens share one split-screen layout (`components/auth/AuthLayout.tsx`, `auth.css`):

- **Brand panel:** Axpense logo, headline, four value points (safety, costs, efficiency, sustainability), and the skyline/road art from the app sidebar.
- **Form side:** a single card, plus an **English / العربية** toggle.
  - Arabic switches the page to right-to-left and the Noto Sans Arabic font.
  - The choice is remembered per browser (`localStorage` key `axpense_lang`).
- Below 900 px the brand panel is hidden and the logo moves above the form.

| Route | Screen |
|---|---|
| `/login` | Email or username, password with show/hide, **Keep me signed in**, **Forgot password?**, link to register. The demo-account shortcut is shown only in `npm run dev`. |
| `/register` | Company name, first and last name, work email, password with a strength meter. Progress strip: Account → Company → Ready. |
| `/onboarding` | Wizard with three steps: company details (name, legal name, industry, size) → location & preferences (country, city, phone, time zone, currency, fiscal year start, date format, distance unit) → done, with next-step links (add vehicle, invite team, set budget). **Skip for now** is available on every step. |
| `/forgot-password` | Email → "Check your email" (with a 30-second resend cooldown). |
| `/reset-password?email=&token=` | New password + confirmation. Handles an expired, invalid or incomplete link with a **Request a new link** action. Success returns to sign-in with a confirmation. |

- Validation (required fields, email format, 6+ characters, passwords match) runs in the browser first.
- Server errors come back as codes and are shown translated:
  - `INVALID_CREDENTIALS`
  - `LOCKED_OUT`
  - `EMAIL_TAKEN`
  - `USERNAME_TAKEN`
  - `PASSWORD_INVALID`
  - `INVALID_RESET_TOKEN`

## API changes

- **Auth responses:** `AuthResponse` now carries `code` and `fieldErrors`.
- **Login:**
  - Trims the input.
  - Returns `LOCKED_OUT` on the attempt that triggers the lockout (5 failures → 5 minutes, unchanged).
- **Register:**
  - `userName` is optional. When it is blank, the username is taken from the email's local part, and a number is added when that name is already taken (`sara.ali`, `sara.ali2`).
  - The email is stored in lowercase.
  - Registering creates the company-settings row with `OnboardingCompletedAt = null`.
- **`GET /api/auth/me`** returns `onboardingRequired`. It is true only for an Owner or Admin of a workspace that has not finished or skipped the wizard. The app then redirects every page to `/onboarding`.
- **Onboarding endpoints (Owner/Admin):**
  - `POST /api/company/onboarding` saves the profile, with the same validation as Administration → Company, and marks onboarding as done.
  - `POST /api/company/onboarding/skip` marks it as done without saving anything.
  - Workspaces that existed before this change are treated as already onboarded.
- **Forgot password:**
  - Answers the same way whether or not the email exists.
  - The reset token is returned **only in the Development environment**. The UI then shows a "Development only — open reset link" box. In any other environment the token is never returned.
- **Reset password:** an invalid or used token returns `INVALID_RESET_TOKEN`. A successful reset clears any lockout.
- **"Keep me signed in":**
  - Checked: the token is stored in `localStorage`.
  - Unchecked: it is stored in `sessionStorage`, so it is cleared when the browser closes.

## Decisions to confirm ⚑

- ⚑ **No email service yet.** Reset links are not delivered outside Development. Before production you need an `IEmailService` (SMTP or a provider) to send `/reset-password?email=…&token=…`.
- ⚑ **The password policy is unchanged** (6+ characters, no other rules). The meter only advises. Consider raising it to 8+ characters with a digit before launch.
- ⚑ **Registering creates a new workspace** where the user is the Owner. There is no email verification and no terms-of-service checkbox; add both once the terms pages exist.
- ⚑ **The dropdown values are stored data** (industries, sizes, time zones) and stay in English on the Arabic screens.

## Verification

- **Executed:**
  - `atest.py`, 18/18: username derivation and collision, duplicate email code, short password, onboarding required / complete / skip / legacy workspace, login code, lockout, forgot password for known and unknown emails, invalid token, reset, login with the new password, single-use token.
  - `tsc --noEmit` is clean.
  - Playwright walk-through with no page errors: login (empty, wrong password, Arabic), register (validation, email taken, success) → onboarding steps 1–3 → dashboard; forgot → check email → reset (mismatch, success) → sign in; expired link; Arabic register; mobile login at 390 px.
- **NOT VERIFIED:**
  - Real email delivery (there is none yet).
  - Screen-reader walkthrough.
  - Safari.
