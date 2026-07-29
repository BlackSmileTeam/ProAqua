"""Build SQL seed with images stored as LONGBLOB (FROM_BASE64)."""
from __future__ import annotations

import base64
from pathlib import Path

SEED = Path(r"E:\Project\Cursor\ProAqua\backend\ProAqua.Api\Database\seed-images")
OUT = Path(r"E:\Project\Cursor\ProAqua\backend\ProAqua.Api\Database\seed_services_promotions.sql")


def b64(name: str) -> str:
    data = (SEED / name).read_bytes()
    return base64.b64encode(data).decode("ascii")


def main() -> None:
    services = [
        ("c1111111-1111-1111-1111-111111111111", "Экспресс-мойка",
         "Кузов, диски, сушка. Быстро вернём блеск перед городом.", "wash", 40, "800.00", 1, "service_wash.jpg"),
        ("c2222222-2222-2222-2222-222222222222", "Комплексная мойка",
         "Снаружи и внутри: пылесос, пластик, стёкла, коврики.", "wash", 90, "1800.00", 2, "service_interior.jpg"),
        ("c4444444-4444-4444-4444-444444444444", "Глубокая очистка",
         "Пена, химия, удаление загрязнений с кузова и дисков.", "wash", 120, "3500.00", 3, "service_deep.jpg"),
        ("c5555555-5555-5555-5555-555555555555", "Детейлинг",
         "Полировка, восстановление блеска и защита ЛКП.", "detailing", 180, "8000.00", 4, "service_detailing.jpg"),
        ("c6666666-6666-6666-6666-666666666666", "Керамика",
         "Защитное керамическое покрытие. Эффект «до / после» видно сразу.", "detailing", 240, "12000.00", 5, "service_ceramic.jpg"),
    ]
    promos = [
        ("e1111111-1111-1111-1111-111111111111", "Комплекс со скидкой 15%",
         "При записи на комплексную мойку в будни — скидка 15%.", 30, "promo_complex.jpg"),
        ("e2222222-2222-2222-2222-222222222222", "Керамика — бонусные баллы x2",
         "За керамическое покрытие начисляем двойные баллы лояльности.", 45, "promo_ceramic.jpg"),
    ]

    lines = [
        "-- ProAqua: тестовые услуги и акции с картинками в БД (LONGBLOB)",
        "-- Запуск: mysql -u USER -p DB < seed_services_promotions.sql",
        "",
        "ALTER TABLE Services ADD COLUMN IF NOT EXISTS ImageData longblob NULL;",
        "ALTER TABLE Services ADD COLUMN IF NOT EXISTS ImageContentType varchar(100) NULL;",
        "ALTER TABLE Promotions ADD COLUMN IF NOT EXISTS ImageData longblob NULL;",
        "ALTER TABLE Promotions ADD COLUMN IF NOT EXISTS ImageContentType varchar(100) NULL;",
        "",
    ]

    # MySQL before 8.0.12 may not support ADD COLUMN IF NOT EXISTS — provide safe notes
    lines = [
        "-- ProAqua: тестовые услуги и акции с картинками в БД (LONGBLOB / FROM_BASE64)",
        "-- Запуск: mysql -u USER -p proaqua < seed_services_promotions.sql",
        "-- Перед запуском убедитесь, что колонки ImageData / ImageContentType существуют",
        "-- (API создаёт их автоматически при старте).",
        "",
    ]

    for sid, title, desc, cat, mins, price, sort, img in services:
        payload = b64(img)
        lines.append(
            "INSERT INTO Services (Id, Title, Description, Category, DurationMinutes, PriceFrom, ImageUrl, BeforeAfterImageUrl, IsActive, SortOrder, ImageData, ImageContentType)\n"
            f"VALUES ('{sid}', '{title}', '{desc}', '{cat}', {mins}, {price}, NULL, NULL, 1, {sort}, FROM_BASE64('{payload}'), 'image/jpeg')\n"
            "ON DUPLICATE KEY UPDATE\n"
            f"  Title=VALUES(Title), Description=VALUES(Description), Category=VALUES(Category),\n"
            f"  DurationMinutes=VALUES(DurationMinutes), PriceFrom=VALUES(PriceFrom), SortOrder=VALUES(SortOrder),\n"
            f"  ImageData=VALUES(ImageData), ImageContentType=VALUES(ImageContentType), IsActive=1;\n"
        )

    for pid, title, desc, days, img in promos:
        payload = b64(img)
        lines.append(
            "INSERT INTO Promotions (Id, Title, Description, StartsAt, EndsAt, IsActive, ImageUrl, ImageData, ImageContentType, CreatedAt)\n"
            f"VALUES ('{pid}', '{title}', '{desc}', UTC_TIMESTAMP(6), DATE_ADD(UTC_TIMESTAMP(6), INTERVAL {days} DAY), 1, NULL, FROM_BASE64('{payload}'), 'image/jpeg', UTC_TIMESTAMP(6))\n"
            "ON DUPLICATE KEY UPDATE\n"
            f"  Title=VALUES(Title), Description=VALUES(Description), EndsAt=VALUES(EndsAt),\n"
            f"  ImageData=VALUES(ImageData), ImageContentType=VALUES(ImageContentType), IsActive=1;\n"
        )

    OUT.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {OUT} ({OUT.stat().st_size // 1024} KB)")


if __name__ == "__main__":
    main()
