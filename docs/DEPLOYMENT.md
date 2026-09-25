# نشر Axpense على VPS (Docker + Caddy)

الملفات اللي اتضافت:

- `src/Axpense.Api/Dockerfile` — بيعمل build للـ API (multi-stage, .NET 10).
- `src/Axpense.Web/Dockerfile` + `src/Axpense.Web/Caddyfile` — بيعمل build للفرونت إند (Vite) وبيحطه جوه
  Caddy، وCaddy نفسه بيعمل reverse proxy لـ `/api/*` على الـ API container، وبيجيب شهادة HTTPS
  تلقائي (Let's Encrypt) للدومين.
- `docker-compose.prod.yml` — الـ stack الكامل: `db` (Postgres) + `api` + `web` (Caddy).
- `.env.example` — نسخة منه لازم تتعمل `.env` وتتملى بالقيم الحقيقية قبل التشغيل.

هيكل الشبكة: `db` و `api` مش متعرضين للإنترنت خالص (مفيش ports منهم منشورة على الجهاز)، غير `web`
(Caddy) اللي فاتح على 80/443 وهو اللي بيتكلم مع `api` و `api` بيتكلم مع `db` جوه شبكة Docker
الداخلية بس.

## 1) DNS

اعمل A record للدومين (أو الساب دومين اللي هتستخدمه) يشاور على الـ IP بتاع الـ Contabo VPS.
لازم ال DNS يكون propagated (تقدر تتأكد بـ `nslookup yourdomain.com`) قبل ما تشغّل الـ stack،
عشان Caddy يقدر يجيب شهادة HTTPS بنجاح من أول مرة.

## 2) تجهيز السيرفر (مرة واحدة بس)

على الـ VPS (Ubuntu غالبًا):

```bash
# تثبيت Docker + Docker Compose plugin
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER   # بعدها اعمل logout/login أو newgrp docker

# فتح البورتات في الفايروول لو مفعّل
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 443/udp
sudo ufw enable   # لو لسه مش مفعّل
```

## 3) رفع الكود على السيرفر

أسهل طريقة: push للريبو على GitHub/GitLab وبعدين `git clone` على السيرفر. أو `scp`/`rsync`
مباشرة للمجلد لو الريبو لسه محلي بس.

```bash
git clone <repo-url> axpense
cd axpense
cp .env.example .env
nano .env   # املا DOMAIN, POSTGRES_PASSWORD, JWT_KEY (استخدم: openssl rand -base64 48 للـ JWT_KEY)
```

## 4) التشغيل

```bash
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml logs -f web   # تابع لوج Caddy لحد ما الشهادة تتعمل
```

لو كل حاجة تمام، هتلاقي `https://yourdomain.com` شغال، والفرونت إند بيكلم `/api/*` عادي على نفس
الدومين (مفيش CORS ولا absolute URL محتاج يتظبط).

## 5) تحديث بعد أي تعديل في الكود

```bash
git pull
docker compose -f docker-compose.prod.yml up -d --build
```

## ملاحظات مهمة

- الداتا بيز بتتعمل schema تلقائي أول تشغيل (`EnsureCreatedAsync` + seed)، فمفيش خطوة migration
  يدوية مطلوبة دلوقتي.
- بيانات الداتا بيز وشهادة الـ HTTPS محفوظين في Docker volumes (`axpense_pg`, `caddy_data`,
  `caddy_config`) فمش بيتمسحوا لو عملت `docker compose down` (لكن `down -v` هيمسحهم، فخليك
  واخد بالك).
- الـ `JWT_KEY` والـ `POSTGRES_PASSWORD` اللي في `.env` سرية — متسيبهاش في الريبو نفسه، ملف `.env`
  متضاف في `.gitignore`... تأكد إنه فعلاً متضاف قبل ما تعمل push.
