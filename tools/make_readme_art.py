"""Builds the animated SVG artwork used by README.md (docs/readme/*.svg).

GitHub shows SVG images with their CSS / SMIL animations (no scripts), so everything here is
plain SVG + CSS. The logo is embedded, so each file works on its own.
Usage: python tools/make_readme_art.py
"""
import base64
import random
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / 'docs' / 'readme'
LOGO = base64.b64encode((ROOT / 'docs' / 'icon.png').read_bytes()).decode()

GOLD, GOLD_LIGHT, GOLD_DEEP = '#d4af6a', '#f6e3b4', '#a87c35'
NAVY, INK = '#0b0d12', '#e8ebf1'
SANS = "'Segoe UI','Inter','Helvetica Neue',Arial,sans-serif"
MONO = "'Cascadia Code','Consolas','SFMono-Regular',Menlo,monospace"


def hero() -> str:
    W, H = 1280, 440
    rnd = random.Random(7)
    particles = ''.join(
        f'<circle class="p" cx="{rnd.randint(20, W - 20)}" cy="{rnd.randint(300, 430)}" r="{rnd.choice([1, 1.4, 1.8, 2.4])}" '
        f'style="animation-delay:{rnd.uniform(0, 6):.2f}s;animation-duration:{rnd.uniform(5, 9):.2f}s"/>'
        for _ in range(26))
    rays = ''.join(f'<line x1="640" y1="300" x2="{x}" y2="{H}"/>' for x in range(-760, 2041, 160))
    floor = ''.join(f'<line class="h" x1="0" y1="300" x2="{W}" y2="300" style="animation-delay:{d * 0.6:.1f}s"/>' for d in range(5))
    chips = ''
    x = 404
    for i, label in enumerate(['AED + FILS', 'PDC SERIES', 'ANALYTICS', 'OFFLINE', 'FREE']):
        w = len(label) * 8.6 + 24
        chips += (f'<g class="chip" style="animation-delay:{i * 0.35:.2f}s"><rect x="{x}" y="252" width="{w:.0f}" height="30" rx="15" '
                  f'fill="rgba(212,175,106,.08)" stroke="{GOLD}" stroke-opacity=".55"/>'
                  f'<text x="{x + w / 2:.0f}" y="272" text-anchor="middle" class="mono" font-size="13" fill="{GOLD}" letter-spacing="1">{label}</text></g>')
        x += w + 10
    corners = ''.join(
        f'<path d="{d}" fill="none" stroke="{GOLD}" stroke-width="2" stroke-opacity=".7"/>' for d in (
            'M24 64 V24 H64', f'M{W - 64} 24 H{W - 24} V64', f'M24 {H - 64} V{H - 24} H64', f'M{W - 64} {H - 24} H{W - 24} V{H - 64}'))

    # holographic cheque that draws itself and gets "printed"
    cx, cy, cw, ch = 952, 96, 296, 146
    cheque = f'''
  <g transform="translate({cx} {cy})" filter="url(#holo)">
    <rect class="draw" x="0" y="0" width="{cw}" height="{ch}" rx="8" fill="rgba(212,175,106,.04)" stroke="{GOLD}" stroke-width="1.5"/>
    <path class="draw d2" d="M18 64 H230 M18 90 H200 M18 114 H200 M196 132 H278" stroke="{GOLD}" stroke-opacity=".6" fill="none"/>
    <rect class="draw d2" x="208" y="78" width="74" height="24" rx="3" fill="none" stroke="{GOLD}" stroke-opacity=".8"/>
    <path class="draw d3" d="M6 34 L44 6 M6 46 L58 6" stroke="{GOLD}" stroke-opacity=".7" fill="none"/>
    <text class="ink i1 mono" x="282" y="26" text-anchor="end" font-size="12" fill="{INK}" letter-spacing="2">16-11-2026</text>
    <text class="ink i2 mono" x="22" y="59" font-size="10.5" fill="{INK}">**AL NOOR PROPERTIES LLC**</text>
    <text class="ink i3 mono" x="22" y="86" font-size="9.2" fill="{INK}">**Twelve Thousand Four Hundred</text>
    <text class="ink i3 mono" x="22" y="110" font-size="9.2" fill="{INK}">Fifty And Fils Seventy Five Only**</text>
    <text class="ink i4 mono" x="245" y="95" text-anchor="middle" font-size="10.5" font-weight="700" fill="{GOLD_LIGHT}">12,450.75</text>
    <text class="mono" x="0" y="{ch + 22}" font-size="11" fill="{GOLD}" fill-opacity=".65" letter-spacing="2">LIVE PREVIEW ▸ RAK-001</text>
  </g>'''

    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" role="img" aria-labelledby="t d">
<title id="t">RAK Special: RAK Bank cheque printing software for Windows</title>
<desc id="d">Animated banner: the RAK Special logo, the name, and a cheque being printed with the amount in words in AED and fils.</desc>
<defs>
  <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#05070b"/><stop offset="1" stop-color="#0e131d"/></linearGradient>
  <radialGradient id="glow"><stop offset="0" stop-color="{GOLD}" stop-opacity=".5"/><stop offset="1" stop-color="{GOLD}" stop-opacity="0"/></radialGradient>
  <radialGradient id="vignette" cx=".5" cy=".45" r=".75"><stop offset=".6" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity=".55"/></radialGradient>
  <linearGradient id="shine" x1="0" y1="0" x2="1" y2="0" spreadMethod="reflect">
    <stop offset="0" stop-color="{GOLD_DEEP}"/><stop offset=".45" stop-color="{GOLD}"/><stop offset=".5" stop-color="{GOLD_LIGHT}"/><stop offset=".55" stop-color="{GOLD}"/><stop offset="1" stop-color="{GOLD_DEEP}"/>
    <animateTransform attributeName="gradientTransform" type="translate" values="-1 0;1 0" dur="4.5s" repeatCount="indefinite"/>
  </linearGradient>
  <linearGradient id="fade" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0b0f17"/><stop offset="1" stop-color="#0b0f17" stop-opacity="0"/></linearGradient>
  <linearGradient id="scan" x1="0" x2="1"><stop offset="0" stop-color="{GOLD}" stop-opacity="0"/><stop offset=".5" stop-color="{GOLD}" stop-opacity=".9"/><stop offset="1" stop-color="{GOLD}" stop-opacity="0"/></linearGradient>
  <pattern id="dots" width="26" height="26" patternUnits="userSpaceOnUse"><circle cx="2" cy="2" r="1" fill="{GOLD}" fill-opacity=".13"/></pattern>
  <clipPath id="floorclip"><rect x="0" y="300" width="{W}" height="{H - 300}"/></clipPath>
  <clipPath id="typing"><rect x="400" y="196" width="0" height="40">
    <animate attributeName="width" values="0;0;532;532;0" keyTimes="0;.08;.5;.92;1" dur="8s" repeatCount="indefinite"/></rect></clipPath>
  <filter id="holo" x="-10%" y="-10%" width="120%" height="130%"><feGaussianBlur stdDeviation="1.6" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge></filter>
  <filter id="soft"><feGaussianBlur stdDeviation="10"/></filter>
</defs>
<style>
  .sans{{font-family:{SANS}}} .mono{{font-family:{MONO}}}
  .spin{{transform-origin:220px 196px;animation:spin 20s linear infinite}}
  .spin2{{transform-origin:220px 196px;animation:spin 11s linear infinite reverse}}
  @keyframes spin{{to{{transform:rotate(360deg)}}}}
  .pulse{{transform-origin:220px 196px;animation:pulse 3.4s ease-in-out infinite}}
  @keyframes pulse{{0%,100%{{opacity:.45;transform:scale(.9)}}50%{{opacity:1;transform:scale(1.08)}}}}
  .float{{animation:float 6s ease-in-out infinite}}
  @keyframes float{{0%,100%{{transform:translateY(0)}}50%{{transform:translateY(-6px)}}}}
  .h{{stroke:{GOLD};stroke-width:1;opacity:0;animation:floor 3s cubic-bezier(.55,0,1,.45) infinite}}
  @keyframes floor{{0%{{transform:translateY(0);opacity:0}}20%{{opacity:.45}}100%{{transform:translateY(140px);opacity:.15}}}}
  .p{{fill:{GOLD};opacity:0;animation:rise 7s linear infinite}}
  @keyframes rise{{0%{{transform:translateY(0);opacity:0}}15%{{opacity:.9}}100%{{transform:translateY(-190px);opacity:0}}}}
  .scan{{animation:scan 6s ease-in-out infinite}}
  @keyframes scan{{0%{{transform:translateY(0);opacity:0}}8%{{opacity:.7}}92%{{opacity:.7}}100%{{transform:translateY({H}px);opacity:0}}}}
  .draw{{stroke-dasharray:900;stroke-dashoffset:900;animation:draw 8s ease-in-out infinite}}
  .d2{{animation-delay:.5s}} .d3{{animation-delay:.9s}}
  @keyframes draw{{0%{{stroke-dashoffset:900;opacity:1}}35%{{stroke-dashoffset:0}}88%{{stroke-dashoffset:0;opacity:1}}100%{{stroke-dashoffset:0;opacity:0}}}}
  .ink{{opacity:0;animation:ink 8s ease-out infinite}}
  .i1{{animation-delay:1.6s}} .i2{{animation-delay:2.1s}} .i3{{animation-delay:2.6s}} .i4{{animation-delay:3.1s}}
  @keyframes ink{{0%{{opacity:0;transform:translateX(-6px)}}6%{{opacity:1;transform:none}}55%{{opacity:1}}62%,100%{{opacity:0}}}}
  .chip{{opacity:0;animation:chip 8s ease-out infinite}}
  @keyframes chip{{0%{{opacity:0;transform:translateY(8px)}}8%,90%{{opacity:1;transform:none}}100%{{opacity:0}}}}
  .blink{{animation:blink 1s steps(1) infinite}} @keyframes blink{{50%{{opacity:0}}}}
</style>

<rect width="{W}" height="{H}" fill="url(#bg)"/>
<rect width="{W}" height="{H}" fill="url(#dots)"/>

<g clip-path="url(#floorclip)">
  <g stroke="{GOLD}" stroke-opacity=".22">{rays}</g>
  {floor}
</g>
<rect x="0" y="296" width="{W}" height="60" fill="url(#fade)"/>
<g>{particles}</g>

<circle class="pulse" cx="220" cy="196" r="130" fill="url(#glow)"/>
<g class="float">
  <circle class="spin" cx="220" cy="196" r="100" fill="none" stroke="{GOLD}" stroke-width="2" stroke-dasharray="3 9"/>
  <circle class="spin2" cx="220" cy="196" r="114" fill="none" stroke="{GOLD}" stroke-opacity=".45" stroke-width="1.5" stroke-dasharray="70 18 8 18"/>
  <path class="spin" d="M220 74 v12 M220 306 v12 M98 196 h12 M330 196 h12" stroke="{GOLD}" stroke-width="2"/>
  <image href="data:image/png;base64,{LOGO}" x="150" y="126" width="140" height="140"/>
</g>

<text x="400" y="170" class="sans" font-size="66" font-weight="800" letter-spacing="7" fill="url(#shine)">RAK SPECIAL</text>
<g clip-path="url(#typing)">
  <text x="402" y="224" class="sans" font-size="25" fill="{INK}">RAK Bank cheque printing for Windows · UAE</text>
</g>
<rect class="blink" x="400" y="203" width="3" height="27" fill="{GOLD}">
  <animate attributeName="x" values="400;400;934;934;400" keyTimes="0;.08;.5;.92;1" dur="8s" repeatCount="indefinite"/>
</rect>
{chips}
{cheque}

<rect class="scan" x="0" y="0" width="{W}" height="2" fill="url(#scan)"/>
<rect width="{W}" height="{H}" fill="url(#vignette)"/>
{corners}
<text x="48" y="{H - 40}" class="mono" font-size="12" fill="{GOLD}" fill-opacity=".7" letter-spacing="2">SYS ▸ READY   ·   v2.1   ·   WINDOWS 10 / 11   ·   x64</text>
<g transform="translate({W - 48} {H - 40})">
  <text x="0" y="0" text-anchor="end" class="mono" font-size="12" fill="{GOLD}" fill-opacity=".7" letter-spacing="2">AMOUNT-IN-WORDS ENGINE ▸ ONLINE</text>
  <circle class="blink" cx="-300" cy="-4" r="4" fill="#4ade80"/>
</g>
</svg>
'''


def button(primary: bool) -> str:
    W, H = (540, 100) if primary else (380, 100)
    if primary:
        title, sub = 'Download for Windows', 'RAKSpecial-Setup.exe  ·  Free  ·  Windows 10 / 11'
        fill, text, subc, stroke = 'url(#g)', NAVY, 'rgba(11,13,18,.72)', 'none'
        icon_bg, icon_fg = NAVY, GOLD
    else:
        title, sub = 'Portable ZIP', 'No install  ·  runs from a USB stick'
        fill, text, subc, stroke = '#0e131d', GOLD, 'rgba(232,235,241,.7)', GOLD
        icon_bg, icon_fg = 'rgba(212,175,106,.14)', GOLD
    icon = ('<path d="M0 -13 V7 M-8 -1 L0 7 L8 -1 M-11 13 H11" fill="none" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>'
            if primary else
            '<path d="M-9 -13 H5 L10 -8 V13 H-9 Z M-2 -13 V1 M-2 -9 h3 M-2 -5 h-3 M-2 -1 h3" fill="none" stroke-width="2.2" stroke-linejoin="round"/>')
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" role="img" aria-label="{title}: {sub}">
<title>{title}: {sub}</title>
<defs>
  <linearGradient id="g" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#f1d9a3"/><stop offset="1" stop-color="#c4994e"/></linearGradient>
  <linearGradient id="sh" x1="0" x2="1"><stop offset="0" stop-color="#fff" stop-opacity="0"/><stop offset=".5" stop-color="#fff" stop-opacity="{'.6' if primary else '.18'}"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>
  <clipPath id="c"><rect x="12" y="12" width="{W - 24}" height="{H - 24}" rx="{(H - 24) / 2}"/></clipPath>
  <filter id="b" x="-20%" y="-60%" width="140%" height="220%"><feGaussianBlur stdDeviation="8"/></filter>
</defs>
<style>
  .glow{{animation:glow 2.6s ease-in-out infinite}} @keyframes glow{{0%,100%{{opacity:.25}}50%{{opacity:.8}}}}
  .sh{{animation:sh 3.4s ease-in-out infinite}} @keyframes sh{{0%{{transform:translateX(-180px)}}55%,100%{{transform:translateX({W + 80}px)}}}}
  .bob{{animation:bob 1.8s ease-in-out infinite}} @keyframes bob{{0%,100%{{transform:translate(60px,50px)}}50%{{transform:translate(60px,54px)}}}}
</style>
<rect class="glow" x="18" y="20" width="{W - 36}" height="{H - 40}" rx="{(H - 40) / 2}" fill="{GOLD}" filter="url(#b)"/>
<rect x="12" y="12" width="{W - 24}" height="{H - 24}" rx="{(H - 24) / 2}" fill="{fill}" stroke="{stroke}" stroke-width="1.5"/>
<g clip-path="url(#c)"><g class="sh"><rect x="0" y="-10" width="110" height="{H + 20}" fill="url(#sh)" transform="skewX(-22)"/></g></g>
<circle cx="60" cy="50" r="25" fill="{icon_bg}"/>
<g class="bob" stroke="{icon_fg}">{icon}</g>
<text x="98" y="48" font-family="{SANS}" font-size="25" font-weight="800" fill="{text}">{title}</text>
<text x="99" y="72" font-family="{SANS}" font-size="14" fill="{subc}">{sub}</text>
</svg>
'''


def how_it_works() -> str:
    W, H = 1280, 270
    steps = [
        ('01', 'TYPE', 'Payee and amount. The words in AED', 'and fils appear instantly.',
         '<path d="M-14 14 L-10 2 L8 -16 L16 -8 L-2 10 Z M-10 2 L-2 10 M4 -12 L12 -4" fill="none" stroke-width="2.4" stroke-linejoin="round"/>'),
        ('02', 'PREVIEW', 'See it on the cheque, live,', 'before anything is printed.',
         '<path d="M-20 0 Q0 -17 20 0 Q0 17 -20 0 Z" fill="none" stroke-width="2.4"/><circle r="6" fill="none" stroke-width="2.4"/>'),
        ('03', 'PRINT', 'Any A4 printer. Saved to your', 'register and analytics.',
         '<path d="M-10 -8 V-18 H10 V-8 M-10 8 H-20 V-8 H20 V8 H10 M-10 2 H10 V18 H-10 Z" fill="none" stroke-width="2.4" stroke-linejoin="round"/>'),
    ]
    xs = [213, 640, 1067]
    nodes = ''
    for i, ((num, name, l1, l2, icon), x) in enumerate(zip(steps, xs)):
        nodes += f'''
  <g transform="translate({x} 100)">
    <circle class="halo" r="62" fill="url(#halo)" style="animation-delay:{i * 1.5:.1f}s"/>
    <circle r="46" fill="#0e131d" stroke="{GOLD}" stroke-width="2"/>
    <circle class="ring" r="54" fill="none" stroke="{GOLD}" stroke-opacity=".5" stroke-dasharray="4 8" style="animation-delay:{i * -2}s"/>
    <g stroke="{GOLD}">{icon}</g>
  </g>
  <text x="{x}" y="190" text-anchor="middle" font-family="{MONO}" font-size="15" fill="{GOLD}" letter-spacing="3">{num} ▸ {name}</text>
  <text x="{x}" y="218" text-anchor="middle" font-family="{SANS}" font-size="16" fill="{INK}">{l1}</text>
  <text x="{x}" y="240" text-anchor="middle" font-family="{SANS}" font-size="16" fill="{INK}">{l2}</text>'''
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" role="img" aria-label="How it works: type, preview, print">
<title>How RAK Special works: 1. type the payee and amount, 2. preview it on the cheque, 3. print on any printer</title>
<defs>
  <linearGradient id="bg" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#070a0f"/><stop offset="1" stop-color="#111827"/></linearGradient>
  <radialGradient id="halo"><stop offset=".55" stop-color="{GOLD}" stop-opacity=".5"/><stop offset="1" stop-color="{GOLD}" stop-opacity="0"/></radialGradient>
  <pattern id="grid" width="32" height="32" patternUnits="userSpaceOnUse"><path d="M32 0 H0 V32" fill="none" stroke="{GOLD}" stroke-opacity=".06"/></pattern>
  <filter id="b"><feGaussianBlur stdDeviation="4"/></filter>
</defs>
<style>
  .halo{{opacity:0;animation:halo 4.5s ease-in-out infinite}}
  @keyframes halo{{0%{{opacity:0;transform:scale(.8)}}10%{{opacity:1;transform:scale(1)}}30%,100%{{opacity:0;transform:scale(1.15)}}}}
  .ring{{animation:spin 12s linear infinite}} @keyframes spin{{to{{transform:rotate(360deg)}}}}
  .flow{{stroke-dasharray:6 10;animation:flow 1.2s linear infinite}} @keyframes flow{{to{{stroke-dashoffset:-16}}}}
</style>
<rect x="1" y="1" width="{W - 2}" height="{H - 2}" rx="22" fill="url(#bg)" stroke="{GOLD}" stroke-opacity=".25"/>
<rect x="1" y="1" width="{W - 2}" height="{H - 2}" rx="22" fill="url(#grid)"/>
<path id="track" d="M259 100 H594 M686 100 H1021" stroke="{GOLD}" stroke-opacity=".55" stroke-width="2" class="flow" fill="none"/>
<g>
  <circle r="9" fill="{GOLD}" filter="url(#b)"><animateMotion dur="4.5s" repeatCount="indefinite" path="M213 100 H1067" keyTimes="0;1" calcMode="linear"/></circle>
  <circle r="4" fill="#fff"><animateMotion dur="4.5s" repeatCount="indefinite" path="M213 100 H1067"/></circle>
</g>
{nodes}
</svg>
'''


def divider() -> str:
    W, H = 1280, 28
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" role="presentation">
<defs>
  <linearGradient id="l" x1="0" x2="1"><stop offset="0" stop-color="{GOLD}" stop-opacity="0"/><stop offset=".5" stop-color="{GOLD}" stop-opacity=".55"/><stop offset="1" stop-color="{GOLD}" stop-opacity="0"/></linearGradient>
  <linearGradient id="s" x1="0" x2="1"><stop offset="0" stop-color="{GOLD_LIGHT}" stop-opacity="0"/><stop offset=".5" stop-color="{GOLD_LIGHT}"/><stop offset="1" stop-color="{GOLD_LIGHT}" stop-opacity="0"/></linearGradient>
  <filter id="b"><feGaussianBlur stdDeviation="2"/></filter>
</defs>
<style>.run{{animation:run 3.6s ease-in-out infinite}} @keyframes run{{0%{{transform:translateX(-240px)}}100%{{transform:translateX({W}px)}}}}
.spin{{transform-origin:640px 14px;animation:spin 6s linear infinite}} @keyframes spin{{to{{transform:rotate(360deg)}}}}</style>
<rect x="0" y="13" width="{W}" height="1.5" fill="url(#l)"/>
<g class="run"><rect x="0" y="11" width="240" height="6" fill="url(#s)" filter="url(#b)"/></g>
<rect class="spin" x="634" y="8" width="12" height="12" fill="#0e131d" stroke="{GOLD}" stroke-width="1.5" transform="rotate(45 640 14)"/>
</svg>
'''


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    files = {
        'hero.svg': hero(),
        'download.svg': button(True),
        'portable.svg': button(False),
        'how-it-works.svg': how_it_works(),
        'divider.svg': divider(),
    }
    for name, svg in files.items():
        (OUT / name).write_text(svg, encoding='utf-8')
    print('Saved', ', '.join(f'docs/readme/{n}' for n in files))


if __name__ == '__main__':
    main()
