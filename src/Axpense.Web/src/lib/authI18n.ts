import { useCallback, useEffect, useState } from "react";

/**
 * English / Arabic strings for the sign-in, register, password-reset and onboarding screens.
 * The choice is remembered per browser (localStorage "axpense_lang"); Arabic switches the layout to right-to-left.
 */
export type Lang = "en" | "ar";
const KEY = "axpense_lang";
const EVENT = "axpense-lang";

const en = {
  // brand panel
  brandHeadline: "Smarter fleet operations, lower costs, greater control.",
  brandSub: "Vehicles, maintenance, inspections, drivers, fuel and expenses — one workspace for your entire fleet.",
  vpSafety: "Improve safety",
  vpSafetyD: "Inspections, hand-offs and issue tracking",
  vpCosts: "Reduce costs",
  vpCostsD: "Fuel, expenses and budgets in one place",
  vpEfficiency: "Increase efficiency",
  vpEfficiencyD: "Preventive maintenance that plans itself",
  vpGreen: "Drive sustainability",
  vpGreenD: "Know what every kilometre really costs",
  tagline: "Your Fleet. Our Priority.",
  rights: "All rights reserved.",
  language: "العربية",

  // common
  email: "Email",
  emailPh: "name@company.com",
  password: "Password",
  show: "Show",
  hide: "Hide",
  required: "This field is required.",
  invalidEmail: "Enter a valid email address.",
  pleaseWait: "Please wait…",
  backToSignIn: "Back to sign in",
  genericError: "Something went wrong. Please try again.",
  network: "Can't reach the server. Check your connection and try again.",

  // login
  loginTitle: "Welcome back",
  loginSub: "Sign in to manage your fleet.",
  loginId: "Email or username",
  loginIdPh: "name@company.com or username",
  remember: "Keep me signed in",
  forgotLink: "Forgot password?",
  signIn: "Sign in",
  noAccount: "New to Axpense?",
  createAccount: "Create an account",
  demo: "Demo account",
  useDemo: "Use demo",
  INVALID_CREDENTIALS: "The email/username or password is incorrect.",
  LOCKED_OUT: "Too many failed attempts. Your account is locked for 5 minutes — try again later or reset your password.",
  sessionEnded: "Your session has ended. Sign in again to continue.",

  // register
  regTitle: "Create your workspace",
  regSub: "Start managing your fleet in minutes. You'll set up your company next.",
  company: "Company name",
  companyPh: "e.g. Nile Logistics",
  firstName: "First name",
  lastName: "Last name",
  workEmail: "Work email",
  createPassword: "Create a password",
  pwHint: "At least 6 characters. Longer, with numbers and symbols, is stronger.",
  pwTooShort: "Password must be at least 6 characters.",
  strength: "Strength",
  weak: "Weak",
  fair: "Fair",
  good: "Good",
  strong: "Strong",
  createBtn: "Create account",
  haveAccount: "Already have an account?",
  EMAIL_TAKEN: "An account with this email already exists.",
  USERNAME_TAKEN: "This username is already taken.",
  PASSWORD_INVALID: "This password doesn't meet the requirements.",
  stepAccount: "Account",
  stepCompany: "Company",
  stepReady: "Ready",

  // forgot
  forgotTitle: "Forgot your password?",
  forgotSub: "Enter the email you use for Axpense and we'll send you a link to reset it.",
  sendLink: "Send reset link",
  checkTitle: "Check your email",
  checkSub: "If an account exists for {email}, you'll receive a link to reset your password. The link expires in 24 hours.",
  resend: "Didn't get it? Send again",
  devLinkTitle: "Development only",
  devLinkBody: "Email isn't configured yet, so here is the reset link:",
  openLink: "Open reset link",

  // reset
  resetTitle: "Set a new password",
  resetSub: "Choose a new password for {email}.",
  newPassword: "New password",
  confirmPassword: "Confirm new password",
  mismatch: "Passwords don't match.",
  resetBtn: "Reset password",
  resetDoneTitle: "Password updated",
  resetDoneSub: "Your password has been reset. Sign in with your new password.",
  INVALID_RESET_TOKEN: "This reset link is invalid or has expired. Request a new one.",
  requestNew: "Request a new link",
  badLink: "This reset link is incomplete. Request a new one.",

  // onboarding
  obWelcome: "Welcome to Axpense, {name}!",
  obSub: "Let's set up your company. It takes about a minute — you can change everything later in Administration → Company.",
  obStep1: "Company details",
  obStep1D: "Who you are",
  obStep2: "Location & preferences",
  obStep2D: "Where and how you work",
  obStep3: "All set",
  obStep3D: "Start using Axpense",
  legalName: "Legal name",
  industry: "Industry",
  size: "Company size",
  select: "Select…",
  country: "Country",
  city: "City",
  phone: "Phone",
  timeZone: "Time zone",
  currency: "Currency",
  fiscal: "Fiscal year starts",
  dateFormat: "Date format",
  distance: "Distance unit",
  next: "Continue",
  back: "Back",
  finish: "Finish setup",
  skip: "Skip for now",
  doneTitle: "Your workspace is ready",
  doneSub: "Here are a few good first steps:",
  nextVehicle: "Add your first vehicle",
  nextVehicleD: "Plates, odometer and PM schedule",
  nextTeam: "Invite your team",
  nextTeamD: "Give dispatchers and technicians access",
  nextBudget: "Set your annual budget",
  nextBudgetD: "Track spend against plan",
  goDashboard: "Go to dashboard",
  fixFields: "Fix the highlighted fields to continue.",
  stepOf: "Step {n} of {total}",
};

type Dict = typeof en;

const ar: Dict = {
  brandHeadline: "تشغيل أذكى لأسطولك، تكاليف أقل، وتحكّم أكبر.",
  brandSub: "المركبات والصيانة والفحوصات والسائقون والوقود والمصروفات — مساحة عمل واحدة لأسطولك بالكامل.",
  vpSafety: "سلامة أعلى",
  vpSafetyD: "فحوصات وتسليم ومتابعة للأعطال",
  vpCosts: "تكاليف أقل",
  vpCostsD: "الوقود والمصروفات والميزانيات في مكان واحد",
  vpEfficiency: "كفاءة أكبر",
  vpEfficiencyD: "صيانة وقائية تخطّط لنفسها",
  vpGreen: "استدامة",
  vpGreenD: "اعرف التكلفة الحقيقية لكل كيلومتر",
  tagline: "أسطولك. أولويتنا.",
  rights: "جميع الحقوق محفوظة.",
  language: "English",

  email: "البريد الإلكتروني",
  emailPh: "name@company.com",
  password: "كلمة المرور",
  show: "إظهار",
  hide: "إخفاء",
  required: "هذا الحقل مطلوب.",
  invalidEmail: "أدخل بريدًا إلكترونيًا صحيحًا.",
  pleaseWait: "برجاء الانتظار…",
  backToSignIn: "العودة لتسجيل الدخول",
  genericError: "حدث خطأ ما. حاول مرة أخرى.",
  network: "تعذّر الاتصال بالخادم. تحقّق من اتصالك وحاول مرة أخرى.",

  loginTitle: "مرحبًا بعودتك",
  loginSub: "سجّل الدخول لإدارة أسطولك.",
  loginId: "البريد الإلكتروني أو اسم المستخدم",
  loginIdPh: "name@company.com أو اسم المستخدم",
  remember: "تذكّرني",
  forgotLink: "نسيت كلمة المرور؟",
  signIn: "تسجيل الدخول",
  noAccount: "جديد على Axpense؟",
  createAccount: "أنشئ حسابًا",
  demo: "حساب تجريبي",
  useDemo: "استخدمه",
  INVALID_CREDENTIALS: "البريد الإلكتروني/اسم المستخدم أو كلمة المرور غير صحيحة.",
  LOCKED_OUT: "محاولات فاشلة كثيرة. تم قفل الحساب لمدة 5 دقائق — حاول لاحقًا أو أعد تعيين كلمة المرور.",
  sessionEnded: "انتهت جلستك. سجّل الدخول مرة أخرى للمتابعة.",

  regTitle: "أنشئ مساحة عملك",
  regSub: "ابدأ إدارة أسطولك في دقائق. ستُكمل بيانات شركتك في الخطوة التالية.",
  company: "اسم الشركة",
  companyPh: "مثال: النيل للنقل",
  firstName: "الاسم الأول",
  lastName: "اسم العائلة",
  workEmail: "البريد الإلكتروني للعمل",
  createPassword: "أنشئ كلمة مرور",
  pwHint: "6 أحرف على الأقل. كلما كانت أطول وبها أرقام ورموز كانت أقوى.",
  pwTooShort: "يجب ألا تقل كلمة المرور عن 6 أحرف.",
  strength: "القوة",
  weak: "ضعيفة",
  fair: "مقبولة",
  good: "جيدة",
  strong: "قوية",
  createBtn: "إنشاء الحساب",
  haveAccount: "لديك حساب بالفعل؟",
  EMAIL_TAKEN: "يوجد حساب مسجّل بهذا البريد الإلكتروني.",
  USERNAME_TAKEN: "اسم المستخدم مستخدم بالفعل.",
  PASSWORD_INVALID: "كلمة المرور لا تستوفي الشروط.",
  stepAccount: "الحساب",
  stepCompany: "الشركة",
  stepReady: "جاهز",

  forgotTitle: "نسيت كلمة المرور؟",
  forgotSub: "أدخل البريد الإلكتروني الذي تستخدمه في Axpense وسنرسل لك رابطًا لإعادة تعيينها.",
  sendLink: "إرسال رابط إعادة التعيين",
  checkTitle: "تحقّق من بريدك",
  checkSub: "إذا كان هناك حساب مسجّل بـ {email} فستصلك رسالة برابط لإعادة تعيين كلمة المرور. صلاحية الرابط 24 ساعة.",
  resend: "لم يصلك؟ أرسل مرة أخرى",
  devLinkTitle: "بيئة التطوير فقط",
  devLinkBody: "خدمة البريد لم تُفعَّل بعد، وهذا رابط إعادة التعيين:",
  openLink: "افتح الرابط",

  resetTitle: "عيّن كلمة مرور جديدة",
  resetSub: "اختر كلمة مرور جديدة لـ {email}.",
  newPassword: "كلمة المرور الجديدة",
  confirmPassword: "تأكيد كلمة المرور الجديدة",
  mismatch: "كلمتا المرور غير متطابقتين.",
  resetBtn: "إعادة تعيين كلمة المرور",
  resetDoneTitle: "تم تحديث كلمة المرور",
  resetDoneSub: "تمت إعادة تعيين كلمة المرور. سجّل الدخول بكلمة المرور الجديدة.",
  INVALID_RESET_TOKEN: "رابط إعادة التعيين غير صالح أو انتهت صلاحيته. اطلب رابطًا جديدًا.",
  requestNew: "اطلب رابطًا جديدًا",
  badLink: "رابط إعادة التعيين غير مكتمل. اطلب رابطًا جديدًا.",

  obWelcome: "مرحبًا بك في Axpense يا {name}!",
  obSub: "هيا نُجهّز بيانات شركتك. يستغرق ذلك دقيقة تقريبًا — ويمكنك تعديل كل شيء لاحقًا من الإدارة ← الشركة.",
  obStep1: "بيانات الشركة",
  obStep1D: "من أنتم",
  obStep2: "الموقع والتفضيلات",
  obStep2D: "أين وكيف تعملون",
  obStep3: "تم",
  obStep3D: "ابدأ استخدام Axpense",
  legalName: "الاسم القانوني",
  industry: "مجال العمل",
  size: "حجم الشركة",
  select: "اختر…",
  country: "الدولة",
  city: "المدينة",
  phone: "الهاتف",
  timeZone: "المنطقة الزمنية",
  currency: "العملة",
  fiscal: "بداية السنة المالية",
  dateFormat: "صيغة التاريخ",
  distance: "وحدة المسافة",
  next: "متابعة",
  back: "رجوع",
  finish: "إنهاء الإعداد",
  skip: "تخطَّ الآن",
  doneTitle: "مساحة عملك جاهزة",
  doneSub: "بعض الخطوات الأولى المقترحة:",
  nextVehicle: "أضف أول مركبة",
  nextVehicleD: "اللوحة والعدّاد وجدول الصيانة",
  nextTeam: "ادعُ فريقك",
  nextTeamD: "امنح المشرفين والفنيين صلاحية الدخول",
  nextBudget: "حدّد ميزانيتك السنوية",
  nextBudgetD: "تابع الإنفاق مقابل الخطة",
  goDashboard: "الذهاب للوحة التحكم",
  fixFields: "صحّح الحقول المحددة للمتابعة.",
  stepOf: "الخطوة {n} من {total}",
};

const DICTS: Record<Lang, Dict> = { en, ar };

function readLang(): Lang {
  try {
    return localStorage.getItem(KEY) === "ar" ? "ar" : "en";
  } catch {
    return "en";
  }
}

/** Current language, translate function (with {placeholders}) and a toggle shared across auth screens. */
export function useAuthLang() {
  const [lang, setLang] = useState<Lang>(readLang);
  useEffect(() => {
    const sync = () => setLang(readLang());
    window.addEventListener(EVENT, sync);
    return () => window.removeEventListener(EVENT, sync);
  }, []);
  const toggle = useCallback(() => {
    const next: Lang = readLang() === "ar" ? "en" : "ar";
    try {
      localStorage.setItem(KEY, next);
    } catch {
      /* private mode: language is not remembered */
    }
    window.dispatchEvent(new Event(EVENT));
  }, []);
  const t = useCallback(
    (key: keyof Dict, vars?: Record<string, string | number>) => {
      let s: string = DICTS[lang][key] ?? en[key];
      if (vars) for (const [k, v] of Object.entries(vars)) s = s.replace(`{${k}}`, String(v));
      return s;
    },
    [lang]
  );
  return { lang, dir: lang === "ar" ? ("rtl" as const) : ("ltr" as const), t, toggle };
}

export type T = ReturnType<typeof useAuthLang>["t"];

/** Translates a server error code when we know it; otherwise falls back to the server message. */
export function codeMessage(t: T, code: string | undefined, fallback: string) {
  const known = ["INVALID_CREDENTIALS", "LOCKED_OUT", "EMAIL_TAKEN", "USERNAME_TAKEN", "PASSWORD_INVALID", "INVALID_RESET_TOKEN"] as const;
  return code && (known as readonly string[]).includes(code) ? t(code as (typeof known)[number]) : fallback;
}

export const EMAIL_RX = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

/** 0–4 strength score for the password meter (length and character variety). */
export function passwordScore(pw: string) {
  if (!pw) return 0;
  let s = 0;
  if (pw.length >= 6) s++;
  if (pw.length >= 10) s++;
  if (/[a-z]/.test(pw) && /[A-Z]/.test(pw)) s++;
  if (/\d/.test(pw) && /[^A-Za-z0-9]/.test(pw)) s++;
  else if (/\d/.test(pw) || /[^A-Za-z0-9]/.test(pw)) s += pw.length >= 8 ? 1 : 0;
  return Math.min(4, s);
}
