from pathlib import Path

p = Path(r"E:\Project\Cursor\ProAqua\backend\ProAqua.Api\Database\seed_services_promotions.sql")
text = p.read_text(encoding="utf-8")

header = r"""-- ProAqua: сначала добавляем колонки для картинок (безопасно для повторного запуска)
SET @db := DATABASE();

SET @sql := (
  SELECT IF(COUNT(*)=0,
    'ALTER TABLE Services ADD COLUMN ImageData longblob NULL',
    'SELECT 1')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Services' AND COLUMN_NAME='ImageData');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (
  SELECT IF(COUNT(*)=0,
    'ALTER TABLE Services ADD COLUMN ImageContentType varchar(100) NULL',
    'SELECT 1')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Services' AND COLUMN_NAME='ImageContentType');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (
  SELECT IF(COUNT(*)=0,
    'ALTER TABLE Promotions ADD COLUMN ImageData longblob NULL',
    'SELECT 1')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Promotions' AND COLUMN_NAME='ImageData');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (
  SELECT IF(COUNT(*)=0,
    'ALTER TABLE Promotions ADD COLUMN ImageContentType varchar(100) NULL',
    'SELECT 1')
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Promotions' AND COLUMN_NAME='ImageContentType');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

"""

idx = text.find("INSERT INTO Services")
body = text[idx:] if idx >= 0 else text
p.write_text(header + body, encoding="utf-8")
print(f"updated {p.name}, starts with ALTER, size={p.stat().st_size}")
