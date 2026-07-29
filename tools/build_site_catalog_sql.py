"""Build one-time SQL: wipe old services, insert ProAqua website catalog with variants + images."""
from __future__ import annotations

import base64
import shutil
import uuid
from pathlib import Path

from PIL import Image

ROOT = Path(r"E:\Project\Cursor\ProAqua")
CURSOR_ASSETS = Path(r"C:\Users\vasek\.cursor\projects\e-Project-Cursor-ProAqua\assets")
SEED = ROOT / "backend" / "ProAqua.Api" / "Database" / "seed-images"
OUT_SQL = ROOT / "backend" / "ProAqua.Api" / "Database" / "replace_services_from_site.sql"
MOBILE = ROOT / "mobile" / "ProAqua.App" / "Resources" / "Images"

SEED.mkdir(parents=True, exist_ok=True)

IMG_MAP = {
    "wash_sedan": "img_wash_sedan.png",
    "wash_crossover": "img_wash_crossover.png",
    "wash_suv": "img_wash_suv.png",
    "wash_suvxl": "img_wash_suvxl.png",
    "interior": "img_interior_sedan.png",
    "ceramic": "img_ceramic_sedan.png",
    "ppf": "img_ppf_suv.png",
    "polish": "img_polish_sedan.png",
    "tint": "img_tint_sedan.png",
    "noise": "img_noise_sedan.png",
    "tuning": "img_tuning_sedan.png",
    "education": "img_education.png",
}


def compress(key: str) -> Path:
    src_name = IMG_MAP[key]
    src = CURSOR_ASSETS / src_name
    if not src.exists():
        raise FileNotFoundError(src)
    dest = SEED / f"{key}.jpg"
    im = Image.open(src).convert("RGB")
    im.thumbnail((960, 640))
    im.save(dest, format="JPEG", quality=70, optimize=True)
    # mobile fallbacks for a few keys
    if key in ("wash_sedan", "interior", "ceramic", "polish"):
        mobile_name = {
            "wash_sedan": "service_wash.png",
            "interior": "service_interior.png",
            "ceramic": "service_ceramic.png",
            "polish": "service_detailing.png",
        }[key]
        im.save(MOBILE / mobile_name, format="PNG", optimize=True)
    return dest


def b64(path: Path) -> str:
    return base64.b64encode(path.read_bytes()).decode("ascii")


def gid(name: str) -> str:
    return str(uuid.uuid5(uuid.NAMESPACE_URL, f"proaqua-service:{name}"))


# Prepare images
IMAGE_B64 = {k: b64(compress(k)) for k in IMG_MAP}

# Parents: (key, title, description, category, sort, image_key, min_price)
PARENTS = [
    ("wash_body", "Комплексная мойка кузова",
     "Двухфазная мойка, очистка колёсных арок, сушка, обработка кузова. Цены зависят от класса автомобиля.",
     "wash", 10, "wash_sedan", 1500),
    ("wash_salon", "Комплексная мойка с уборкой в салоне",
     "Комплексная мойка кузова и влажная уборка салона. Несколько пакетов с разной комплектацией.",
     "wash", 20, "wash_crossover", 3000),
    ("wash_extra", "Дополнительные услуги мойки",
     "Мойка двигателя, чистка дисков, обработка пластика, нано-защита и другие опции.",
     "wash", 30, "wash_suv", 600),
    ("coatings", "Защитные покрытия для кузова",
     "Многослойное нанесение керамики и других защитных составов для ЛКП.",
     "exterior", 40, "ceramic", 2000),
    ("ppf_parts", "Антигравийная плёнка PPF",
     "Невидимая защита кузова от сколов, царапин и реагентов — по элементам.",
     "exterior", 50, "ppf", 4000),
    ("polish", "Полировка и отчистка кузова",
     "Профессиональная коррекция ЛКП с устранением царапин и загрязнений.",
     "exterior", 60, "polish", 2000),
    ("chem", "Химчистка салона",
     "Глубокая чистка поверхностей салона: ковролин, потолок, сиденья, запахи.",
     "interior", 70, "interior", 2000),
    ("reupholstery", "Перешив и реставрация салона",
     "Восстановление и защита кожаных поверхностей, перешив руля.",
     "interior", 80, "interior", 8500),
    ("interior_protect", "Защита интерьера",
     "Нанесение защитных составов и плёнок на поверхности салона.",
     "interior", 90, "interior", 5500),
    ("tint", "Тонировка стёкол",
     "Профессиональная тонировка атермальными и стандартными плёнками.",
     "other", 100, "tint", 2000),
    ("noise", "Шумоизоляция",
     "Комплексная ШВИ для максимального комфорта.",
     "other", 110, "noise", 15000),
    ("tuning", "Тюнинг и дооснащение",
     "Установка дополнительного оборудования: подсветка, обвес, камеры и др.",
     "other", 120, "tuning", 3000),
    ("edu_basic", "Базовый курс Детейлинга",
     "Основы детейлинга для начинающих: мойка, химчистка, полировка, защиты. 15 дней.",
     "education", 130, "education", 60000),
    ("edu_ppf", "Курс Основы оклейки плёнками",
     "Обучение оклейке защитной плёнкой PPF и винилом. 25 дней.",
     "education", 140, "education", 80000),
    ("edu_full", "Углубленный курс полного спектра",
     "Полный спектр услуг детейлинга и бизнес-модуль. 2 месяца.",
     "education", 150, "education", 150000),
    ("ppf_basic", "PPF Basic",
     "Базовая защита передней части: бампер, капот, фары, стойки.",
     "ppf", 160, "ppf", 47000),
    ("ppf_premium", "PPF Premium",
     "Расширенная защита: Basic + крылья, зона под ручками, зеркала и др.",
     "ppf", 170, "ppf", 83000),
    ("ppf_ultimate", "PPF Ultimate",
     "Максимальная защита: полная оклейка кузова, разбор, броня лобового.",
     "ppf", 180, "wash_suvxl", 250000),
]


def variant(parent_key, title, desc, minutes, s, c, u, x, sort, image_key=None):
    return {
        "parent": parent_key,
        "title": title,
        "desc": desc,
        "minutes": minutes,
        "s": s, "c": c, "u": u, "x": x,
        "sort": sort,
        "image": image_key,
    }


VARIANTS = [
    # wash body
    variant("wash_body", "Трехфазная мойка кузова", "Бесконтактная+контактная+воск+ковры+чернение резины", 40, 1500, 1600, 1700, 1800, 1, "wash_sedan"),
    variant("wash_body", "Трехфазная мойка + обезжиривание", "", 50, 2200, 2300, 2500, 2800, 2, "wash_crossover"),
    variant("wash_body", "Трехфазная мойка + обезжиривание + быстрая керамика", "", 60, 3200, 3400, 3800, 4300, 3, "wash_suv"),
    variant("wash_body", "Бережная мойка (керамика)", "Нанесение полимера", 60, 3200, 3400, 3800, 4300, 4, "ceramic"),
    variant("wash_body", "Бережная мойка (плёнка)", "Воск на основе кремния", 60, 3200, 3400, 3800, 4300, 5, "ppf"),
    variant("wash_body", "Химчистка кузова и дисков", "Удаление битума, металлических вкраплений, воск", 120, 10000, 11000, 12000, 14000, 6, "wash_suvxl"),
    # wash salon
    variant("wash_salon", "Мойка Люкс", "Трехфазная мойка + уборка салона (без багажника)", 70, 3000, 3200, 3400, 3800, 1, "wash_sedan"),
    variant("wash_salon", "Мойка Все Включено", "+ обезжиривание + уборка с багажником", 90, 3600, 3900, 4000, 4500, 2, "wash_crossover"),
    variant("wash_salon", "Мойка Ультра", "+ быстрая керамика + уборка с багажником", 100, 5000, 5300, 5600, 6000, 3, "wash_suv"),
    variant("wash_salon", "Мойка Максимум", "+ отчистка кожи + кондиционер кожи", 120, 7000, 7500, 8000, 8500, 4, "interior"),
    variant("wash_salon", "Мойка Премиум с мойкой подвески и днища", "+ снятие колёс, мойка днища, подвески, замена смазки", 180, 10000, 11000, 12000, 13000, 5, "wash_suv"),
    variant("wash_salon", "PRO Мойка", "+ снятие локеров, мойка двигателя, радиаторов", 240, 20000, 22000, 24000, 28000, 6, "wash_suvxl"),
    # extras
    variant("wash_extra", "Удаление битумных пятен (1 элемент)", "", 20, 600, 600, 600, 600, 1, "polish"),
    variant("wash_extra", "Пылесос салона", "", 20, 600, 600, 700, 700, 2, "interior"),
    variant("wash_extra", "Комплексная уборка салона", "", 40, 1300, 1400, 1500, 1700, 3, "interior"),
    variant("wash_extra", "Кондиционер кожи", "", 40, 2000, 2000, 2500, 2500, 4, "interior"),
    variant("wash_extra", "Химчистка дисков", "", 40, 2000, 2000, 2000, 2000, 5, "wash_suv"),
    variant("wash_extra", "Антидождь/Антилёд (передняя полусфера)", "", 40, 2500, 2500, 2500, 2500, 6, "tint"),
    variant("wash_extra", "Химчистка кузова", "", 90, 5000, 6000, 6000, 7000, 7, "wash_crossover"),
    variant("wash_extra", "Восстановление неокрашенного пластика", "", 40, 1100, 1100, 1100, 1100, 8, "polish"),
    # coatings
    variant("coatings", "Керамика Krytex 9H (3 слоя)", "", 240, 10000, 11000, 12000, 14000, 1, "ceramic"),
    variant("coatings", "Жидкое стекло", "", 120, 8000, 8000, 9000, 9000, 2, "ceramic"),
    variant("coatings", "Антидождь (Полусфера)", "", 40, 2500, 2500, 2500, 2500, 3, "tint"),
    variant("coatings", "Быстрая керамика", "", 40, 2000, 2000, 2500, 2500, 4, "ceramic"),
    variant("coatings", "Тефлоновый воск", "", 60, 5000, 5000, 6000, 6000, 5, "ceramic"),
    variant("coatings", "Полимер кузова", "", 40, 2000, 2000, 2500, 2500, 6, "ceramic"),
    # ppf parts
    variant("ppf_parts", "Броня Капота", "", 180, 17000, 18000, 18000, 19000, 1, "ppf"),
    variant("ppf_parts", "Броня Бампера", "", 180, 18000, 19000, 20000, 22000, 2, "ppf"),
    variant("ppf_parts", "Броня Фар (2 шт.)", "", 60, 4000, 4000, 4000, 4000, 3, "ppf"),
    variant("ppf_parts", "Броня Зеркала (2 шт.)", "", 45, 4000, 4000, 5000, 5000, 4, "ppf"),
    variant("ppf_parts", "Броня Дверь (1 шт.)", "", 90, 10000, 10000, 11000, 12000, 5, "ppf"),
    # polish
    variant("polish", "Лайт Полировка", "", 150, 12000, 13000, 14000, 16000, 1, "polish"),
    variant("polish", "Восстановительная Полировка", "", 240, 18000, 19000, 20000, 22000, 2, "polish"),
    variant("polish", "Полировка фар (2 шт.)", "", 45, 2000, 2000, 2000, 3000, 3, "polish"),
    variant("polish", "Химчистка кузова", "", 90, 5000, 6000, 6000, 7000, 4, "wash_crossover"),
    variant("polish", "Отчистка кузова от битума", "", 60, 3000, 3500, 3500, 4000, 5, "wash_suv"),
    # chem
    variant("chem", "Химчистка Ковролина", "", 90, 5500, 6000, 6000, 7000, 1, "interior"),
    variant("chem", "Химчистка потолка", "", 90, 5500, 6000, 6500, 7000, 2, "interior"),
    variant("chem", "Химчистка Салона Целиком", "", 180, 14000, 16000, 18000, 20000, 3, "interior"),
    variant("chem", "Химчистка одного сиденья", "", 40, 2000, 2000, 2000, 2000, 4, "interior"),
    variant("chem", "Удаление Запахов в салоне", "", 40, 2000, 2000, 2000, 2000, 5, "interior"),
    # reupholstery
    variant("reupholstery", "Перешив руля Эко кожа", "", 180, 8500, 8500, 8500, 8500, 1, "interior"),
    variant("reupholstery", "Перешив Руль натуральная кожа", "", 240, 13500, 13500, 13500, 13500, 2, "interior"),
    variant("reupholstery", "Керамическая защита кожи (Весь салон)", "", 120, 14000, 15000, 15000, 16000, 3, "interior"),
    # interior protect
    variant("interior_protect", "Керамическое покрытие кожи (весь салон)", "", 150, 15000, 17000, 19000, 22000, 1, "interior"),
    variant("interior_protect", "Защита экрана салона полиуретановой плёнкой", "", 60, 5500, 5500, 6000, 6000, 2, "interior"),
    variant("interior_protect", "Защита глянцевых элементов салона плёнкой", "", 180, 20000, 20000, 20000, 20000, 3, "interior"),
    # tint
    variant("tint", "Заднее Стекло", "", 60, 3000, 3000, 3500, 4000, 1, "tint"),
    variant("tint", "Боковые стекла (1 шт.)", "", 40, 2000, 2000, 2500, 2500, 2, "tint"),
    variant("tint", "Лобовое стекло", "", 60, 3000, 3000, 3500, 4000, 3, "tint"),
    variant("tint", "Атермалка Лобовое/Заднее", "", 90, 5000, 5500, 6000, 6500, 4, "tint"),
    variant("tint", "Тонировка Фар (2 шт.)", "", 60, 6000, 6000, 6000, 6000, 5, "tint"),
    # noise
    variant("noise", "Двери (4 шт.)", "", 300, 22000, 22000, 22000, 22000, 1, "noise"),
    variant("noise", "Арки снаружи", "", 240, 25000, 25000, 25000, 25000, 2, "noise"),
    variant("noise", "Пол", "", 360, 35000, 35000, 37000, 37000, 3, "noise"),
    variant("noise", "Крыша", "", 240, 18000, 18000, 19000, 20000, 4, "noise"),
    variant("noise", "Багажник", "", 180, 15000, 15000, 17000, 17000, 5, "noise"),
    # tuning
    variant("tuning", "Контурная подсветка салона (ambient light)", "", 240, 25000, 25000, 25000, 25000, 1, "tuning"),
    variant("tuning", "Установка обвеса", "", 180, 12000, 13000, 14000, 15000, 2, "tuning"),
    variant("tuning", "Установка видеорегистратора", "", 60, 4000, 4000, 4000, 4000, 3, "tuning"),
    variant("tuning", "Установка задней камеры", "", 90, 4000, 4000, 4000, 4000, 4, "tuning"),
    variant("tuning", "Установка спойлера", "", 60, 3000, 3000, 3000, 3000, 5, "tuning"),
    # education single variant = itself
    variant("edu_basic", "Базовый курс детейлинга", "Мойка, химчистка, полировка, защитные покрытия. 15 дней.", 0, 60000, 60000, 60000, 60000, 1, "education"),
    variant("edu_ppf", "Курс по оклейке плёнками", "PPF, винил, оклейка салона. 25 дней.", 0, 80000, 80000, 80000, 80000, 1, "education"),
    variant("edu_full", "Углублённый курс полного спектра детейлинга", "Полный спектр, бизнес-модуль. 2 месяца.", 0, 150000, 150000, 150000, 150000, 1, "education"),
    # ppf packages
    variant("ppf_basic", "Пакет Basic", "Бампер, капот, фары, стойки, полоса над лобовым", 480, 47000, 47000, 47000, 47000, 1, "ppf"),
    variant("ppf_premium", "Пакет Premium", "Basic + крылья, зона под ручками, пороги, зеркала и др.", 720, 83000, 83000, 83000, 83000, 1, "ppf"),
    variant("ppf_ultimate", "Пакет Ultimate", "Полная оклейка кузова, разбор, броня лобового, защита салона", 1440, 250000, 250000, 250000, 250000, 1, "wash_suvxl"),
]


def esc(s: str) -> str:
    return s.replace("\\", "\\\\").replace("'", "''")


def main():
    lines = [
        "-- ONE-TIME: replace Services catalog from official ProAqua price list",
        "-- Includes parent services + variants with prices for Sedan/Crossover/SUV/SUV XL",
        "-- WARNING: deletes existing bookings that reference services",
        "",
        "SET FOREIGN_KEY_CHECKS=0;",
        "DELETE FROM Bookings;",
        "DELETE FROM Services;",
        "SET FOREIGN_KEY_CHECKS=1;",
        "",
        "-- Ensure columns exist",
        "SET @db := DATABASE();",
    ]

    for col, definition in [
        ("ParentId", "char(36) NULL"),
        ("PriceSedan", "decimal(10,2) NULL"),
        ("PriceCrossover", "decimal(10,2) NULL"),
        ("PriceSuv", "decimal(10,2) NULL"),
        ("PriceSuvXl", "decimal(10,2) NULL"),
        ("ImageData", "longblob NULL"),
        ("ImageContentType", "varchar(100) NULL"),
    ]:
        lines += [
            f"SET @sql := (SELECT IF(COUNT(*)=0, 'ALTER TABLE Services ADD COLUMN {col} {definition}', 'SELECT 1')",
            f"  FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Services' AND COLUMN_NAME='{col}');",
            "PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;",
            "",
        ]

    parent_ids = {}
    for key, title, desc, cat, sort, img, price in PARENTS:
        pid = gid(f"parent:{key}")
        parent_ids[key] = pid
        payload = IMAGE_B64[img]
        lines.append(
            "INSERT INTO Services (Id, Title, Description, Category, DurationMinutes, PriceFrom, ImageUrl, BeforeAfterImageUrl, IsActive, SortOrder, ImageData, ImageContentType, ParentId, PriceSedan, PriceCrossover, PriceSuv, PriceSuvXl)\n"
            f"VALUES ('{pid}', '{esc(title)}', '{esc(desc)}', '{cat}', 0, {price:.2f}, NULL, NULL, 1, {sort}, FROM_BASE64('{payload}'), 'image/jpeg', NULL, NULL, NULL, NULL, NULL);\n"
        )

    for v in VARIANTS:
        vid = gid(f"variant:{v['parent']}:{v['title']}")
        pid = parent_ids[v["parent"]]
        img_key = v["image"] or "wash_sedan"
        payload = IMAGE_B64[img_key]
        price_from = min(v["s"], v["c"], v["u"], v["x"])
        lines.append(
            "INSERT INTO Services (Id, Title, Description, Category, DurationMinutes, PriceFrom, ImageUrl, BeforeAfterImageUrl, IsActive, SortOrder, ImageData, ImageContentType, ParentId, PriceSedan, PriceCrossover, PriceSuv, PriceSuvXl)\n"
            f"VALUES ('{vid}', '{esc(v['title'])}', '{esc(v['desc'])}', "
            f"(SELECT Category FROM Services p WHERE p.Id='{pid}' LIMIT 1), "
            f"{v['minutes']}, {price_from:.2f}, NULL, NULL, 1, {v['sort']}, FROM_BASE64('{payload}'), 'image/jpeg', '{pid}', "
            f"{v['s']:.2f}, {v['c']:.2f}, {v['u']:.2f}, {v['x']:.2f});\n"
        )

    # MySQL may not allow subquery in INSERT VALUES like that for Category - use explicit category from parent
    # Fix: rebuild variant inserts with explicit category
    # Actually rewrite the file properly

    OUT_SQL.write_text("\n".join(lines), encoding="utf-8")
    print("first pass written; rewriting variants with explicit categories...")

    # Rewrite cleanly
    lines = [
        "-- ONE-TIME: replace Services catalog from official ProAqua price list (xn--80aaf5asfh.com/price.html)",
        "-- Parents + variants; prices: Sedan / Crossover / SUV / SUV XL",
        "-- WARNING: deletes Bookings and existing Services",
        "",
        "SET FOREIGN_KEY_CHECKS=0;",
        "DELETE FROM Bookings;",
        "DELETE FROM Services;",
        "SET FOREIGN_KEY_CHECKS=1;",
        "",
        "SET @db := DATABASE();",
    ]
    for col, definition in [
        ("ParentId", "char(36) NULL"),
        ("PriceSedan", "decimal(10,2) NULL"),
        ("PriceCrossover", "decimal(10,2) NULL"),
        ("PriceSuv", "decimal(10,2) NULL"),
        ("PriceSuvXl", "decimal(10,2) NULL"),
        ("ImageData", "longblob NULL"),
        ("ImageContentType", "varchar(100) NULL"),
    ]:
        lines += [
            f"SET @sql := (SELECT IF(COUNT(*)=0, 'ALTER TABLE Services ADD COLUMN `{col}` {definition}', 'SELECT 1') FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='Services' AND COLUMN_NAME='{col}');",
            "PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;",
        ]
    lines.append("")

    parent_cat = {k: cat for k, _, _, cat, _, _, _ in PARENTS}
    parent_ids = {}
    for key, title, desc, cat, sort, img, price in PARENTS:
        pid = gid(f"parent:{key}")
        parent_ids[key] = pid
        payload = IMAGE_B64[img]
        lines.append(
            "INSERT INTO Services (Id, Title, Description, Category, DurationMinutes, PriceFrom, ImageUrl, BeforeAfterImageUrl, IsActive, SortOrder, ImageData, ImageContentType, ParentId, PriceSedan, PriceCrossover, PriceSuv, PriceSuvXl) VALUES\n"
            f"('{pid}', '{esc(title)}', '{esc(desc)}', '{cat}', 0, {price:.2f}, NULL, NULL, 1, {sort}, FROM_BASE64('{payload}'), 'image/jpeg', NULL, NULL, NULL, NULL, NULL);\n"
        )

    for v in VARIANTS:
        vid = gid(f"variant:{v['parent']}:{v['title']}")
        pid = parent_ids[v["parent"]]
        cat = parent_cat[v["parent"]]
        img_key = v["image"] or "wash_sedan"
        payload = IMAGE_B64[img_key]
        price_from = min(v["s"], v["c"], v["u"], v["x"])
        lines.append(
            "INSERT INTO Services (Id, Title, Description, Category, DurationMinutes, PriceFrom, ImageUrl, BeforeAfterImageUrl, IsActive, SortOrder, ImageData, ImageContentType, ParentId, PriceSedan, PriceCrossover, PriceSuv, PriceSuvXl) VALUES\n"
            f"('{vid}', '{esc(v['title'])}', '{esc(v['desc'])}', '{cat}', {v['minutes']}, {price_from:.2f}, NULL, NULL, 1, {v['sort']}, FROM_BASE64('{payload}'), 'image/jpeg', '{pid}', {v['s']:.2f}, {v['c']:.2f}, {v['u']:.2f}, {v['x']:.2f});\n"
        )

    OUT_SQL.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {OUT_SQL} ({OUT_SQL.stat().st_size // 1024} KB), parents={len(PARENTS)}, variants={len(VARIANTS)}")


if __name__ == "__main__":
    main()
