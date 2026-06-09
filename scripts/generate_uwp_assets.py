"""Generate UWP/Store logo assets from a transparent PNG source."""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image


def prepare_source(image: Image.Image) -> Image.Image:
    return image.convert("RGBA")


def crop_to_content(image: Image.Image, padding: int = 4) -> Image.Image:
    bbox = image.getbbox()
    if not bbox:
        return image
    left, top, right, bottom = bbox
    left = max(0, left - padding)
    top = max(0, top - padding)
    right = min(image.width, right + padding)
    bottom = min(image.height, bottom + padding)
    return image.crop((left, top, right, bottom))


def fit_transparent(
    image: Image.Image,
    width: int,
    height: int,
    padding_ratio: float = 0.05,
) -> Image.Image:
    canvas = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    inner_w = max(1, int(width * (1.0 - padding_ratio * 2)))
    inner_h = max(1, int(height * (1.0 - padding_ratio * 2)))
    fitted = image.copy()
    fitted.thumbnail((inner_w, inner_h), Image.Resampling.LANCZOS)
    x = (width - fitted.width) // 2
    y = (height - fitted.height) // 2
    canvas.paste(fitted, (x, y), fitted)
    return canvas


def save_png(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="PNG", optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    logo = crop_to_content(prepare_source(Image.open(args.source)))

    assets = {
        "StoreLogo.png": fit_transparent(logo, 50, 50),
        "Square44x44Logo.png": fit_transparent(logo, 44, 44),
        "Square150x150Logo.png": fit_transparent(logo, 150, 150),
        "Wide310x150Logo.png": fit_transparent(logo, 310, 150),
        "SplashScreen.png": fit_transparent(logo, 620, 300, padding_ratio=0.08),
    }

    for name, img in assets.items():
        save_png(img, args.output_dir / name)

    save_png(logo, args.output_dir / "GeminiLogo.png")
    print(f"Generated {len(assets) + 1} files in {args.output_dir}")


if __name__ == "__main__":
    main()
