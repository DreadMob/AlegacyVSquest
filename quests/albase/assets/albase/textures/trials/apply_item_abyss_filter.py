"""
Скрипт для создания текстур предметов trials из ванильных текстур + фиолетовый фильтр бездны.
Копирует ванильные текстуры, применяет фильтр, сохраняет в папку trials/.

Использование: python apply_item_abyss_filter.py
"""

from PIL import Image, ImageEnhance
import os
import shutil

# Пути
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
VANILLA_BASE = r"C:\Users\shado\Desktop\vs 1.22.1\assets\survival\textures\entity\humanoid\seraphclothes"
OUTPUT_DIR = SCRIPT_DIR  # trials/ folder

# Параметры фиолетового фильтра бездны (те же что для боссов)
ABYSS_COLOR = (90, 20, 160)
ABYSS_INTENSITY = 0.45
DARKEN_FACTOR = 0.75
SATURATION_BOOST = 1.3

# Маппинг: выходное имя -> путь к ванильной текстуре (относительно VANILLA_BASE)
TEXTURE_MAP = {
    # trial-shadow-earring -> jester head
    "shadow-earring.png": "head/hood-jester.png",
    # trial-void-pendant -> nazar2
    "void-pendant.png": "neck/nazar2.png",
    # trial-abyss-pendant -> bronzeamulet
    "abyss-pendant.png": "neck/bronzeamulet.png",
    # trial-void-ring -> marketeer neck
    "void-ring.png": "neck/marketeer.png",
    # trial-rift-bracelet -> deep gloves
    "rift-bracelet.png": "hand/deep.png",
    # trial-void-cloak -> midnightponcho
    "void-cloak.png": "shoulder/midnightponcho.png",
    # trial-abyss-mask -> forgotten upperbodyover
    "abyss-mask.png": "upperbodyover/forgotten.png",
    # trial-deep-sigil -> simple-cross
    "deep-sigil.png": "neck/simple-cross.png",
    # trial-abyss-belt -> midsummer waist
    "abyss-belt.png": "waist/midsummer.png",
}


def apply_abyss_filter(img):
    """Применяет фиолетовый фильтр бездны к изображению PIL."""
    img = img.convert("RGBA")
    r, g, b, a = img.split()

    purple_overlay = Image.new("RGBA", img.size, (*ABYSS_COLOR, 255))
    darkened = ImageEnhance.Brightness(img).enhance(DARKEN_FACTOR)
    blended = Image.blend(darkened, purple_overlay, ABYSS_INTENSITY)
    blended = ImageEnhance.Color(blended).enhance(SATURATION_BOOST)

    r2, g2, b2, _ = blended.split()
    result = Image.merge("RGBA", (r2, g2, b2, a))
    return result


def main():
    print(f"Создание текстур предметов trials с фильтром бездны")
    print(f"Источник: {VANILLA_BASE}")
    print(f"Выход: {OUTPUT_DIR}")
    print(f"Параметры: цвет RGB{ABYSS_COLOR}, интенсивность {ABYSS_INTENSITY}")
    print()

    success = 0
    errors = 0

    for output_name, vanilla_path in sorted(TEXTURE_MAP.items()):
        src = os.path.join(VANILLA_BASE, vanilla_path)
        dst = os.path.join(OUTPUT_DIR, output_name)

        if not os.path.exists(src):
            print(f"  [SKIP] {vanilla_path} — файл не найден!")
            errors += 1
            continue

        img = Image.open(src)
        filtered = apply_abyss_filter(img)
        filtered.save(dst)
        print(f"  [OK] {vanilla_path} -> {output_name}")
        success += 1

    # Также применим фильтр к текстуре Эхо Пустоты (trial-tracker)
    tracker_texture = os.path.join(os.path.dirname(SCRIPT_DIR), "bosshunt.png")
    if os.path.exists(tracker_texture):
        img = Image.open(tracker_texture)
        filtered = apply_abyss_filter(img)
        tracker_out = os.path.join(OUTPUT_DIR, "trial-tracker.png")
        filtered.save(tracker_out)
        print(f"  [OK] bosshunt.png -> trial-tracker.png (Эхо Пустоты)")
        success += 1

    print(f"\nГотово! Создано {success} текстур, ошибок: {errors}")


if __name__ == "__main__":
    main()
