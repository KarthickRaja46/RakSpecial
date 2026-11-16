"""Builds the pictures that come from the logo (src/ChequeWriter/app.ico):
  docs/icon.png                     - logo shown in README.md
  installer/wizard-*.bmp            - tall picture on the left of the setup wizard
  installer/wizard-small-*.bmp      - small logo at the top right of the setup wizard
Run it again after changing app.ico. Usage: python tools/make_images.py   (needs Pillow, Windows fonts)"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
ICO = ROOT / 'src' / 'ChequeWriter' / 'app.ico'
INSTALLER = ROOT / 'installer'

NAVY = (11, 13, 18)      # logo background
GOLD = (212, 175, 106)   # logo star
WHITE = (255, 255, 255)

# Inno Setup picks the size closest to the screen scaling (100 %, 150 %, 200 %).
WIZARD_SIZES = {100: (164, 314), 150: (246, 471), 200: (328, 628)}
SMALL_SIZES = {100: 55, 150: 83, 200: 110}


def logo(size: int) -> Image.Image:
    ico = Image.open(ICO)
    ico.size = max(ico.info['sizes'])
    return ico.convert('RGBA').resize((size, size), Image.LANCZOS)


def font(px: int, bold: bool) -> ImageFont.FreeTypeFont:
    for name in (('segoeuib.ttf', 'arialbd.ttf') if bold else ('segoeui.ttf', 'arial.ttf')):
        try:
            return ImageFont.truetype(name, px)
        except OSError:
            pass
    return ImageFont.load_default(px)


def wizard(w: int, h: int) -> Image.Image:
    img = Image.new('RGB', (w, h), NAVY)
    s = w / 164
    size = int(110 * s)
    mark = logo(size)
    top = int(h * 0.30)
    img.paste(mark, ((w - size) // 2, top), mark)
    d = ImageDraw.Draw(img)
    y = top + size + int(18 * s)
    for text, f, color in (("RAK Special", font(int(20 * s), True), GOLD),
                           ("RAK Bank cheque printing", font(int(10 * s), False), WHITE)):
        tw = d.textlength(text, font=f)
        d.text(((w - tw) / 2, y), text, font=f, fill=color)
        y += int(f.size * 1.6)
    return img


def small(n: int) -> Image.Image:
    img = Image.new('RGB', (n, n), WHITE)  # wizard header is white
    mark = logo(n)
    img.paste(mark, (0, 0), mark)
    return img


def main() -> None:
    INSTALLER.mkdir(exist_ok=True)
    logo(256).save(ROOT / 'docs' / 'icon.png')
    for pct, (w, h) in WIZARD_SIZES.items():
        wizard(w, h).save(INSTALLER / f'wizard-{pct}.bmp')
    for pct, n in SMALL_SIZES.items():
        small(n).save(INSTALLER / f'wizard-small-{pct}.bmp')
    print('Saved docs/icon.png and installer/wizard*.bmp')


if __name__ == '__main__':
    main()
