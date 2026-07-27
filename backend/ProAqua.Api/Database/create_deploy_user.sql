-- ============================================================
-- MySQL: пользователь deploy для Workbench / ручного администрирования
-- Выполнить по SSH под root:
--   sudo mysql -u root -p < create_deploy_user.sql
--   или: sudo mysql -u root -p  и вставить команды ниже
-- ============================================================
-- Это НЕ пользователь Linux и НЕ учётка приложения ProAqua.
-- Пароль потом смените: ALTER USER 'deploy'@'%' IDENTIFIED BY 'новый';
-- ============================================================

CREATE USER IF NOT EXISTS 'deploy'@'%' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';
CREATE USER IF NOT EXISTS 'deploy'@'localhost' IDENTIFIED BY 'DeployWb_ChangeMe_2026!';

-- Полные права: любые БД, таблицы, ALTER/CREATE/DROP/INSERT/UPDATE/...
GRANT ALL PRIVILEGES ON *.* TO 'deploy'@'%' WITH GRANT OPTION;
GRANT ALL PRIVILEGES ON *.* TO 'deploy'@'localhost' WITH GRANT OPTION;

FLUSH PRIVILEGES;

-- Проверка:
-- SELECT user, host FROM mysql.user WHERE user = 'deploy';
-- SHOW GRANTS FOR 'deploy'@'%';
