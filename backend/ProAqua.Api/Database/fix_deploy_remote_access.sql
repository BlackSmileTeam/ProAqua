-- ============================================================
-- Прямой доступ Workbench БЕЗ SSH (по паролю)
-- Выполнить на сервере по SSH под root:
--   sudo mysql -u root -p < fix_deploy_remote_access.sql
-- ============================================================
-- Ошибка Access denied для deploy@ВАШ_IP обычно значит:
-- 1) пользователя deploy@% нет, или
-- 2) пароль другой (IF NOT EXISTS не обновляет пароль!), или
-- 3) MySQL не слушает внешние подключения / закрыт firewall
-- ============================================================

-- Сброс учётки (игнорируем ошибки, если кого-то нет)
DROP USER IF EXISTS 'deploy'@'%';
DROP USER IF EXISTS 'deploy'@'localhost';
DROP USER IF EXISTS 'deploy'@'127.0.0.1';
DROP USER IF EXISTS 'deploy'@'92.100.101.204';

-- Пароль для Workbench (смените при желании)
CREATE USER 'deploy'@'%' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';
CREATE USER 'deploy'@'localhost' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';
CREATE USER 'deploy'@'127.0.0.1' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';
-- Явно ваш текущий домашний IP (на случай проблем с '%')
CREATE USER 'deploy'@'92.100.101.204' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';

GRANT ALL PRIVILEGES ON *.* TO 'deploy'@'%' WITH GRANT OPTION;
GRANT ALL PRIVILEGES ON *.* TO 'deploy'@'localhost' WITH GRANT OPTION;
GRANT ALL PRIVILEGES ON *.* TO 'deploy'@'127.0.0.1' WITH GRANT OPTION;
GRANT ALL PRIVILEGES ON *.* TO 'deploy'@'92.100.101.204' WITH GRANT OPTION;

-- На MySQL 8 иногда помогает явный плагин
ALTER USER 'deploy'@'%' IDENTIFIED WITH caching_sha2_password BY 'DeployWb_ChangeMe_2026!';
ALTER USER 'deploy'@'92.100.101.204' IDENTIFIED WITH caching_sha2_password BY 'DeployWb_ChangeMe_2026!';

FLUSH PRIVILEGES;

-- Проверка:
SELECT user, host, plugin FROM mysql.user WHERE user = 'deploy';
SHOW GRANTS FOR 'deploy'@'%';
