-- Заполняет расширенное описание услуги (HTML) для красивого показа в мобильном приложении
-- Можно выполнять повторно

SET @db := DATABASE();
SET @sql := (
  SELECT IF(
    COUNT(*) = 0,
    'ALTER TABLE Services ADD COLUMN `DetailsHtml` longtext NULL',
    'SELECT 1'
  )
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Services' AND COLUMN_NAME = 'DetailsHtml'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

UPDATE Services
SET
  Purpose = 'Для старта в профессии детейлера и системного освоения практических навыков.',
  DetailsHtml = '
<h3>📊 О курсе</h3>
<p><strong>Базовый курс детейлинга</strong> — это полный старт для новичков. За 15 дней вы изучите ключевые процессы и закрепите навыки на практике.</p>
<table>
  <tr><th>Параметр</th><th>Значение</th></tr>
  <tr><td>Длительность</td><td>15 дней</td></tr>
  <tr><td>Стоимость</td><td>60 000 ₽</td></tr>
  <tr><td>Формат</td><td>Группы до 5 человек</td></tr>
</table>
<h3>🎯 Вы научитесь</h3>
<ul>
  <li>Проводить профессиональную мойку и подготовку авто</li>
  <li>Выполнять полировку кузова</li>
  <li>Проводить химчистку салона</li>
  <li>Наносить защитные покрытия</li>
  <li>Подбирать материалы под задачу</li>
</ul>
<h3>Программа курса</h3>
<table>
  <tr><th>Модуль</th><th>Содержание</th><th>Длительность</th></tr>
  <tr><td>1</td><td>Введение в детейлинг</td><td>2 дня</td></tr>
  <tr><td>2</td><td>Профессиональная мойка</td><td>3 дня</td></tr>
  <tr><td>3</td><td>Химчистка салона</td><td>3 дня</td></tr>
  <tr><td>4</td><td>Полировка кузова</td><td>4 дня</td></tr>
  <tr><td>5</td><td>Защитные покрытия</td><td>2 дня</td></tr>
  <tr><td>6</td><td>Экзамен и сертификация</td><td>1 день</td></tr>
</table>'
WHERE Title LIKE '%Базовый курс детейлинга%';

