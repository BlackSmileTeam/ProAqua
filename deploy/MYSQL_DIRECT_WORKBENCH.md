# MySQL Workbench: прямое подключение (без SSH)

## Ошибка
`Access denied for user 'deploy'@'92.100.101.204' (using password: YES)`

MySQL до вас **доходит**, но отклоняет логин: неверный пароль или нет прав для вашего IP.

## 1. На сервере (через веб-консоль / SSH как root)

```bash
sudo mysql -u root -p < /path/to/fix_deploy_remote_access.sql
```

Или вручную в `mysql -u root -p` вставьте содержимое  
`backend/ProAqua.Api/Database/fix_deploy_remote_access.sql`.

Это **пересоздаёт** `deploy` с паролем `DeployWb_ChangeMe_2026!` для `%` и вашего IP.

Проверка на сервере:

```sql
SELECT user, host, plugin FROM mysql.user WHERE user = 'deploy';
```

Должны быть строки `deploy` / `%` и желательно `deploy` / `92.100.101.204`.

## 2. MySQL должен слушать не только localhost

```bash
sudo grep -R "bind-address" /etc/mysql/ /etc/my.cnf 2>/dev/null
```

Нужно `bind-address = 0.0.0.0` (или закомментировать).  
После правки:

```bash
sudo systemctl restart mysql
# или: sudo systemctl restart mariadb
```

## 3. Firewall — порт 3306

```bash
# ufw
sudo ufw allow from 92.100.101.204 to any port 3306 proto tcp
sudo ufw reload

# или временно для теста (хуже по безопасности):
# sudo ufw allow 3306/tcp
```

У Selectel/облака проверьте Security Group: inbound TCP **3306** с вашего IP.

## 4. Настройки в Workbench (без SSH)

| Поле | Значение |
|------|----------|
| Connection Method | **Standard TCP/IP** (не over SSH) |
| Hostname | `139.100.225.234` |
| Port | `3306` |
| Username | `deploy` |
| Password | `DeployWb_ChangeMe_2026!` (Store in Vault) |
| Default Schema | `proaqua` (опционально) |

Test Connection → OK.

## 5. Если снова Access denied

1. Убедитесь, что пароль **точно** тот (без пробелов).
2. На сервере:

```sql
ALTER USER 'deploy'@'%' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';
ALTER USER 'deploy'@'92.100.101.204' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';
FLUSH PRIVILEGES;
```

3. Если домашний IP сменился — создайте нового:

```sql
CREATE USER 'deploy'@'НОВЫЙ.IP' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';
GRANT ALL PRIVILEGES ON *.* TO 'deploy'@'НОВЫЙ.IP' WITH GRANT OPTION;
FLUSH PRIVILEGES;
```

Узнать IP: https://ifconfig.me

## Безопасность

Лучше ограничить 3306 **только вашим IP**, а не всем интернетом.  
Пользователь приложения `proaqua` — для API; `deploy` — только для Workbench.
