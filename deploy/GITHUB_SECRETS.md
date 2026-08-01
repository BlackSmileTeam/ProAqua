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

Workflow пишет `DB_CONNECTION_STRING` **буквально** в `.env.production` как `APP_CONNECTION_STRING` → `ConnectionStrings__DefaultConnection` у backend. **Server не переписывается** на `mysql` — если в секрете `Server=139.100.225.234`, API ходит туда же. `Database=` / `User=` / `Password=` тоже берутся как в секрете (например `aquapro` vs `proaqua` — не форсируется).

Секрет должен быть достижим **из контейнера** `proaqua-backend` (например `Server=139.100.225.234;Port=3306` или `3307`). Если MySQL на хосте слушает только `127.0.0.1`, контейнер до публичного IP не достучится — нужен bind на `0.0.0.0` (и firewall). Не подставляйте `host.docker.internal`: используйте IP/порт из секрета как есть.

Дополнительно workflow **парсит** из строки `User`/`Uid`/`User ID`, `Password`/`Pwd` и `Database`/`Initial Catalog` (python3 на сервере) только для опционального compose-сервиса `mysql` (`MYSQL_*`). Пароль может содержать `$`, backticks, `!`, кавычки и прочие shell-символы (но не `;` — это разделитель полей ADO.NET).

Compose-сервис `mysql` в профиле `local-mysql` и **не** стартует при обычном деплое; backend не `depends_on` mysql.

## Что НЕ нужно в GitHub Secrets

Workflow подставляет defaults (секреты не требуются):

| Переменная | Default |
|------------|---------|
| Deploy path | `$HOME/ProAqua` (например `/home/deploy/ProAqua`) |
| `FRONTEND_PORT` | `55512` |
| `BACKEND_PORT` | `55511` |
| `MYSQL_PORT` (host) | `3307` |
| `MYSQL_ROOT_PASSWORD` | `RootMysql_ChangeMe_2026!` (фиксированный, как в `.env.production.example`) |
| `MYSQL_APP_USER` / `MYSQL_APP_PASSWORD` / `MYSQL_DATABASE` | из `DB_CONNECTION_STRING` (только для optional `local-mysql`) |
| `JWT_ISSUER` / `JWT_AUDIENCE` | `ProAquaApi` / `ProAquaClient` |
| `PUSH_PROVIDER` | `Dev` |
| AmoCRM | выключен (`AMOCRM_ENABLED=false`) |

Опционально (если когда-нибудь добавите): `PROD_DEPLOY_PATH`, `PROD_FRONTEND_PORT`, `PROD_BACKEND_PORT` — пустые значения игнорируются.

## Важно про пароли MySQL и volume

1. **API** использует пароль и БД из `DB_CONNECTION_STRING` как есть — они должны совпадать с реальным MySQL на `Server=` из секрета.
2. **`MYSQL_APP_PASSWORD`** (для optional compose mysql) тоже берётся из `Password=` / `Pwd=` в секрете.
3. **`MYSQL_ROOT_PASSWORD`** — стабильный default `RootMysql_ChangeMe_2026!`. MySQL применяет root-пароль **только при первой инициализации** volume. Если volume уже создан с другим root-паролем, смена default в workflow его не поменяет.
4. Не генерируйте случайные пароли на каждый деплой — это ломает существующий `mysql_data` volume (если пользуетесь `local-mysql`).

## Workbench (ручное администрирование)

| Поле | Значение |
|------|----------|
| Host | `139.100.225.234` (или SSH-туннель → `127.0.0.1`) |
| Port | `3306` на хосте MySQL **или** `3307` если смотрите в optional Docker mysql (`--profile local-mysql`) |
| User / Password / Database | как в `DB_CONNECTION_STRING` |

## SQL на сервере (по SSH)

Файлы в репозитории:

- `backend/ProAqua.Api/Database/create_deploy_user.sql`
- `backend/ProAqua.Api/Database/schema_proaqua.sql`
