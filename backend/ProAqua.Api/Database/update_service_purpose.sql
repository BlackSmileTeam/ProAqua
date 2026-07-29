-- Заполнить пояснение "для чего" у услуг
-- Можно запускать повторно

SET @db := DATABASE();
SET @sql := (
  SELECT IF(
    COUNT(*) = 0,
    'ALTER TABLE Services ADD COLUMN `Purpose` varchar(400) NULL',
    'SELECT 1'
  )
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Services' AND COLUMN_NAME = 'Purpose'
);
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

UPDATE Services
SET Purpose = CASE
  WHEN Title LIKE '%Комплексная мойка кузова%' THEN 'Для регулярной поддерживающей мойки и блеска кузова.'
  WHEN Title LIKE '%с уборкой в салоне%' THEN 'Для комплексного ухода: чистый кузов + свежий салон.'
  WHEN Title LIKE '%Дополнительные услуги мойки%' THEN 'Для точечных задач: диски, арки, моторный отсек, защита.'
  WHEN Title LIKE '%Защитные покрытия для кузова%' THEN 'Для длительной защиты ЛКП от грязи, реагентов и УФ.'
  WHEN Title LIKE '%Антигравийная плёнка%' OR Title LIKE '%PPF%' THEN 'Для защиты от сколов, царапин и пескоструя.'
  WHEN Title LIKE '%Полировка%' THEN 'Для восстановления глубины цвета и удаления мелких дефектов.'
  WHEN Title LIKE '%Химчистка салона%' THEN 'Для глубокой очистки тканей, кожи и пластика салона.'
  WHEN Title LIKE '%Перешив и реставрация салона%' THEN 'Для восстановления внешнего вида и состояния интерьера.'
  WHEN Title LIKE '%Защита интерьера%' THEN 'Для долговременной защиты материалов салона от износа.'
  WHEN Title LIKE '%Тонировка стёкол%' THEN 'Для приватности, защиты от солнца и снижения нагрева салона.'
  WHEN Title LIKE '%Тюнинг и дооснащение%' THEN 'Для улучшения функционала и индивидуализации автомобиля.'
  WHEN Title LIKE '%Шумоизоляция%' THEN 'Для снижения шума и повышения комфорта в поездках.'
  WHEN Title LIKE '%курс%' OR Title LIKE '%Курс%' THEN 'Для обучения профессиональному детейлингу и оклейке.'
  ELSE Purpose
END
WHERE ParentId IS NULL;

