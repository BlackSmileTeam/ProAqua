# MySQL: Workbench user + схема ProAqua

## 1. По SSH (под root) — пользователь Workbench `deploy`

Скопируйте и выполните:

```sql
CREATE USER IF NOT EXISTS 'deploy'@'%' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';
CREATE USER IF NOT EXISTS 'deploy'@'localhost' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';

GRANT ALL PRIVILEGES ON *.* TO 'deploy'@'%' WITH GRANT OPTION;
GRANT ALL PRIVILEGES ON *.* TO 'deploy'@'localhost' WITH GRANT OPTION;

FLUSH PRIVILEGES;
```

Или файл: `create_deploy_user.sql`.

## 2. Схема приложения

Файл: `schema_proaqua.sql` — создаёт БД `ProAqua`, пользователя приложения `ProAqua`, все таблицы и seed.

```bash
sudo mysql -u root -p < schema_proaqua.sql
```

## 3. Workbench с ПК

**Безопаснее — SSH-туннель:**

- Method: Standard TCP/IP over SSH  
- SSH: `deploy@139.100.225.234`  
- MySQL: `127.0.0.1:3306`  
- User: `deploy` / `DeployWb_ChangeMe_2026!`

**Прямое подключение** (если открыт 3306): Host `139.100.225.234`, User `deploy`.

## 4. Приложение

Логин БД приложения: `ProAqua` / `ProAquaApp_ChangeMe_2026!`  
База: `ProAqua` на `139.100.225.234` (в Development) или `host.docker.internal` (контейнер на сервере).
