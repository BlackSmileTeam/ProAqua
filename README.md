# ПроАква (ProAqua)

Мобильное приложение (MAUI: Android + iOS), Web API (.NET 8) и веб-админка для **мойки и детейлинг-центра**: запись, статусы, лояльность, рефералы, синхронизация с AMOCRM.

Оплаты в приложении **нет** — только запись и сервис.

## Состав репозитория

| Папка | Назначение |
|-------|------------|
| `backend/ProAqua.Api` | Web API, PIN-вход, push, AMOCRM sync, MySQL |
| `admin` | Веб-админка (React): слоты/записи, услуги, клиенты, аналитика |
| `mobile/ProAqua.App` | Клиентское MAUI-приложение |
| `assets` | Сгенерированные визуалы |
| `docker-compose.production.yml` | MySQL + API + Admin в контейнерах |
| `.github/workflows/deploy-production.yml` | Деплой по SSH (как в bebochka) |

## AMOCRM: нужна ли отдельная админка?

**Да, отдельная админка нужна.** AMOCRM оставляем для продаж/лидов/задач менеджерам.

| Задача | Где правда |
|--------|------------|
| Слоты, боксы, длительность услуг | **ProAqua Admin + API** |
| Баллы, уровни, рефералы | **ProAqua** |
| Статус авто «в работе / готово» | **ProAqua** |
| Воронка, звонки, задачи | **AMOCRM** |
| Контакт/сделка по новой записи | **синхронизация API → AMOCRM** |

Схема: **мобилка ↔ ваш API ↔ MySQL**; API опционально создаёт сделку в AMOCRM.  
В админке AMOCRM **нельзя** удобно вести расписание мойки и лояльность — поэтому админка реализована отдельно (`admin/`).

## База данных: какую брать?

**MySQL 8** — рекомендуем (как в bebochka): проще/дешевле на VPS, знакомый стек, Pomelo EF Core уже подключён.

PostgreSQL тоже отличен, но для вашего текущего деплоя **MySQL = меньше сюрпризов**.

Локально / на сервере:

```bash
# пример строки подключения (compose)
server=mysql;port=3306;database=ProAqua;user=ProAqua;password=...;SslMode=None;AllowPublicKeyRetrieval=True
```

При старте API делает `EnsureCreated` + seed (услуги, боксы, админ `+79000000001`).

## Быстрый старт (локально)

### 1. MySQL

Поднимите MySQL 8, создайте БД/пользователя `ProAqua`, либо используйте docker-compose.

### 2. API

```bash
cd backend/ProAqua.Api
dotnet run --urls http://0.0.0.0:8080
```

Swagger: `http://localhost:8080/swagger`  
Вход: телефон + PIN (демо-админ `+79000000001` / `1234`).

### 3. Админка

```bash
cd admin
npm install
npm run dev
```

Откройте `http://localhost:5173` → вход `+79000000001` + Dev-код.

### 4. MAUI

Откройте `ProAqua.slnx` / `mobile/ProAqua.App` в Visual Studio.  
Для Android-эмулятора API: `http://10.0.2.2:8080` (уже в коде).  
Для устройства — укажите IP сервера в `Services/ProAquaApi.BaseUrl`.

## Деплой в контейнере через GitHub Actions

Паттерн взят из `E:\Project\Cursor\bebochka`:

1. Репозиторий на GitHub, ветка `main`.
2. На сервере: Docker + SSH-ключ для git fetch.
3. Secrets (см. `deploy/GITHUB_SECRETS.md`).
4. Push в `main` → workflow пишет `.env.production` и делает  
   `docker compose -f docker-compose.production.yml --env-file .env.production up -d --build`.

Порты по умолчанию: backend `55511`, admin `55512`, mysql host `3307`.  
Перед nginx/HTTPS проксируйте как в bebochka (`/api`, `/uploads` → backend).

Пример env: `.env.production.example`.

## Авторизация (без SMS)

Клиенты **не регистрируются сами**. Администратор заводит клиента в админке при визите (телефон, PIN, авто, реферал) и выдаёт PIN.

Вход в приложение и админку: `POST /api/auth/login` — телефон + PIN.

Демо: админ `+79000000001` / PIN `1234`.

## Push-уведомления

Также с сервера:

1. Приложение регистрирует device token → `POST /api/me/devices`
2. API шлёт при создании записи и смене статуса
3. Провайдеры:
   - **`Push:Provider=Dev`** — пишет в лог (0 ₽)
   - **`Push:Provider=FcmHttpV1`** — Firebase Cloud Messaging (бесплатный тариф достаточен)

Нужно будет:

- Firebase project (Android)
- для iOS: Apple Developer + APNs key, подключить к Firebase
- service account JSON на сервере

Стоимость инфраструктуры push ≈ **0 ₽** (FCM free tier).  
Платите только Apple Developer ($99/год) и Google Play (~$25 разово).

## Публикация в Google Play и App Store (РФ)

### Общее

- Юрлицо или ИП (для компаний удобнее)
- Политика конфиденциальности + согласие на ПДн (152-ФЗ) — **обязательная страница на сайте**
- Без оплаты в приложении проще модерация (нет In-App Purchase)

### Google Play (РФ)

1. Аккаунт Google Play Console (~25 USD разово)
2. Подписать AAB (keystore хранить надёжно)
3. Скриншоты, описание, категория, возрастной рейтинг
4. Data safety форма (какие данные: телефон, устройство)
5. Для РФ: аккаунт может быть на физлицо/ИП; актуальные ограничения Google проверяйте перед релизом (аккаунт + платежный профиль)

### App Store (РФ / Apple)

1. **Apple Developer Program** — 99 USD/год (нужна карта, принимаемая Apple)
2. Сертификаты, Provisioning, Archive в Xcode / VS
3. App Privacy, скриншоты iPhone
4. Аккаунт разработчика из РФ возможен, но иногда сложнее с оплатой членства — запасной вариант: через компанию/агента
5. Тест на **реальном iPhone** (TestFlight)

### Что ещё нужно для MAUI-релиза

- Уникальный ApplicationId / BundleId (`com.yourcompany.ProAqua`)
- Иконки/splash (есть заготовки в `assets`)
- Privacy Policy URL
- Production API URL (HTTPS)
- Включение push (FCM/APNs) перед стором, если обещаете уведомления в описании

## Что уже сделано в коде (MVP)

- Вход по телефону + PIN (регистрация клиентов только из админки)
- Каталог услуг с картинками
- Запись / история / повтор в 1 тап
- Лояльность (баллы + уровни) и реферальный код
- Админка: записи, статусы, регистрация клиентов, клиенты, аналитика
- Заготовка sync в AMOCRM
- Dev Push
- Docker Compose + GitHub Actions deploy

## Демо-учётки (seed)

| Роль | Телефон | PIN |
|------|---------|-----|
| Admin | `+79000000001` | `1234` |
| Master | `+79000000002` | `1234` |
