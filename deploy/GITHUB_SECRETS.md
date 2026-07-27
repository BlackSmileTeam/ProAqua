# GitHub Secrets для ProAqua
# Сервер: 139.100.225.234
# Значения ниже — стартовые; потом смените пароли и JWT.

## SSH / деплой

| Secret | Значение (пример) |
|--------|-------------------|
| `PROD_HOST` | `139.100.225.234` |
| `PROD_USER` | `deploy` |
| `PROD_SSH_PRIVATE_KEY` | содержимое `C:\Users\vasek\.ssh\id_ed25519_ProAqua` (приватный!) |
| `PROD_SSH_PORT` | `22` |
| `PROD_DEPLOY_PATH` | `/opt/ProAqua` |
| `PROD_FRONTEND_PORT` | `55512` |
| `PROD_BACKEND_PORT` | `55511` |

## MySQL

Workbench (ручное администрирование, любые таблицы/БД):

| Поле | Значение |
|------|----------|
| Host | `139.100.225.234` |
| Port | `3306` |
| User | `deploy` |
| Password | `DeployWb_ChangeMe_2026!` |
| Рекомендуется | SSH-туннель от Linux-user `deploy`, MySQL host `127.0.0.1` |

Приложение (только схема `ProAqua`):

| Secret | Значение |
|--------|----------|
| `DB_CONNECTION_STRING` | `Server=139.100.225.234;Port=3306;Database=proaqua;User=proaqua;Password=ProAquaApp_ChangeMe_2026!;CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True` |
| `MYSQL_ROOT_PASSWORD` | `RootMysql_ChangeMe_2026!` |
| `MYSQL_APP_PASSWORD` | `ProAquaApp_ChangeMe_2026!` |

Локальный `appsettings` / VS уже смотрит на:

`Server=139.100.225.234;...;User=proaqua;Password=ProAquaApp_ChangeMe_2026!`

## JWT / прочее

| Secret | Значение |
|--------|----------|
| `JWT_KEY` | `ProAquaJwt_ChangeMe_AtLeast_32_Chars_Prod!` |
| `PUSH_PROVIDER` | `Dev` |
| `AMOCRM_ENABLED` | `false` |
| `AMOCRM_BASE_URL` | (когда включите) |
| `AMOCRM_ACCESS_TOKEN` | |
| `AMOCRM_PIPELINE_ID` | |
| `AMOCRM_STATUS_ID` | |

## SQL на сервере (по SSH под root)

```bash
# 1) пользователь Workbench deploy
sudo mysql -u root -p < /path/to/create_deploy_user.sql

# 2) БД ProAqua + таблицы + seed + пользовательль приложения ProAqua
sudo mysql -u root -p < /path/to/schema_proaqua.sql
```

Файлы в репозитории:

- `backend/ProAqua.Api/Database/create_deploy_user.sql`
- `backend/ProAqua.Api/Database/schema_proaqua.sql`

## Важно для удалённого MySQL

1. `bind-address` в MySQL должен позволять внешние/локальные подключения (или только SSH-туннель).
2. Firewall: либо **не** открывать 3306 в мир, либо только ваш домашний IP.
3. После смены паролей обновите secrets и `appsettings*.json`.
