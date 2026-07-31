# GitHub Secrets для ProAqua

Минимальный набор — всё, что реально нужно для CI-деплоя. Остальное workflow задаёт сам.

## Обязательные секреты

### Organization secrets (SSH)

| Secret | Назначение | Пример |
|--------|------------|--------|
| `PROD_HOST` | IP / hostname сервера | `139.100.225.234` |
| `PROD_USER` | SSH-пользователь | `deploy` |
| `PROD_SSH_PORT` | SSH-порт | `22` |
| `PROD_SSH_PRIVATE_KEY` | приватный ключ (PEM) | содержимое `id_ed25519_ProAqua` |

Репозиторий должен иметь доступ к этим org secrets.

### Repository secrets (приложение)

| Secret | Назначение | Пример |
|--------|------------|--------|
| `DB_CONNECTION_STRING` | MySQL connection string приложения | см. ниже |
| `JWT_KEY` | ключ JWT (≥32 символов) | `ProAquaJwt_ChangeMe_AtLeast_32_Chars_Prod!` |

Пример `DB_CONNECTION_STRING`:

```text
Server=139.100.225.234;Port=3306;Database=proaqua;User=proaqua;Password=ProAquaApp_ChangeMe_2026!;CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True
```

Workflow **парсит** из строки `User`/`Uid`/`User ID`, `Password`/`Pwd` и `Database`/`Initial Catalog` (python3 на сервере) и пишет их в `.env.production` для MySQL-контейнера. Пароль может содержать `$`, backticks, `!`, кавычки и прочие shell-символы (но не `;` — это разделитель полей ADO.NET).  
Для API внутри Docker connection string **переписывается** на сервис compose:

```text
Server=mysql;Port=3306;Database=<из секрета>;User=<из секрета>;Password=<из секрета>;CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True
```

Host/Port в секрете могут указывать на внешний адрес (удобно для локальной разработки / Workbench) — на сервере API всё равно ходит в контейнер `mysql:3306`.

## Что НЕ нужно в GitHub Secrets

Workflow подставляет defaults (секреты не требуются):

| Переменная | Default |
|------------|---------|
| Deploy path | `$HOME/ProAqua` (например `/home/deploy/ProAqua`) |
| `FRONTEND_PORT` | `55512` |
| `BACKEND_PORT` | `55511` |
| `MYSQL_PORT` (host) | `3307` |
| `MYSQL_ROOT_PASSWORD` | `RootMysql_ChangeMe_2026!` (фиксированный, как в `.env.production.example`) |
| `MYSQL_APP_USER` / `MYSQL_APP_PASSWORD` / `MYSQL_DATABASE` | из `DB_CONNECTION_STRING` |
| `JWT_ISSUER` / `JWT_AUDIENCE` | `ProAquaApi` / `ProAquaClient` |
| `PUSH_PROVIDER` | `Dev` |
| AmoCRM | выключен (`AMOCRM_ENABLED=false`) |

Опционально (если когда-нибудь добавите): `PROD_DEPLOY_PATH`, `PROD_FRONTEND_PORT`, `PROD_BACKEND_PORT` — пустые значения игнорируются.

## Важно про пароли MySQL и volume

1. **`MYSQL_APP_PASSWORD`** всегда берётся из `Password=` или `Pwd=` в `DB_CONNECTION_STRING`. Меняете пароль приложения — обновите секрет; он должен совпадать с уже созданным MySQL-пользователем в volume.
2. **`MYSQL_ROOT_PASSWORD`** — стабильный default `RootMysql_ChangeMe_2026!`. MySQL применяет root-пароль **только при первой инициализации** volume. Если volume уже создан с другим root-паролем, смена default в workflow его не поменяет; для root используйте прежний пароль или пересоздайте volume (данные пропадут).
3. Не генерируйте случайные пароли на каждый деплой — это ломает существующий `mysql_data` volume.

## Workbench (ручное администрирование)

| Поле | Значение |
|------|----------|
| Host | `139.100.225.234` (или SSH-туннель → `127.0.0.1`) |
| Port | `3306` на хосте MySQL **или** `3307` если смотрите в Docker-порт из compose |
| User / Password | как в `DB_CONNECTION_STRING` (`proaqua` / …) |

## SQL на сервере (по SSH)

Файлы в репозитории:

- `backend/ProAqua.Api/Database/create_deploy_user.sql`
- `backend/ProAqua.Api/Database/schema_proaqua.sql`
