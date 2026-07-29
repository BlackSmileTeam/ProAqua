# MySQL: Workbench user + схема ProAqua

## Прямое подключение без SSH (рекомендуется вам сейчас)

См. подробности: [`MYSQL_DIRECT_WORKBENCH.md`](MYSQL_DIRECT_WORKBENCH.md)

Кратко — на сервере под **root**:

```bash
sudo mysql -u root -p < backend/ProAqua.Api/Database/fix_deploy_remote_access.sql
```

Workbench: **Standard TCP/IP** → Host `139.100.225.234` → User `deploy` → Password `DeployWb_ChangeMe_2026!`

---

## 1. По SSH (под root) — пользователь Workbench `deploy`

Скопируйте и выполните файл `fix_deploy_remote_access.sql` (он сбрасывает пароль принудительно).

Старый `CREATE USER IF NOT EXISTS` **не меняет пароль**, если пользователь уже был — из‑за этого часто бывает Access denied.

## 2. Схема приложения

Файл: `schema_proaqua.sql` — создаёт БД `proaqua`, пользователя приложения `proaqua`, все таблицы и seed.

```bash
sudo mysql -u root -p < schema_proaqua.sql
```

## 3. Workbench с ПК

**Прямое (без SSH):** Host `139.100.225.234:3306`, User `deploy`, пароль из скрипта.

**Через SSH-туннель (если 3306 закрыт):** Method TCP/IP over SSH → SSH `deploy@139.100.225.234` → MySQL `127.0.0.1:3306` → User MySQL `deploy`.

## 4. Приложение

Логин БД приложения: `proaqua` / `ProAquaApp_ChangeMe_2026!`  
База: `proaqua` на `139.100.225.234`.
