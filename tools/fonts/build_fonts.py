"""Builds assets/fonts from the pinned upstream files of tools/fonts/sources.json.

Run by hand when a font version changes; the output is committed. It needs Python 3.11+ and the fontTools
version of requirements.txt, installed in a throwaway virtual environment outside the repository:

    python -m venv %TEMP%\\clicalo-fonts-venv
    %TEMP%\\clicalo-fonts-venv\\Scripts\\python -m pip install -r tools\\fonts\\requirements.txt
    %TEMP%\\clicalo-fonts-venv\\Scripts\\python tools\\fonts\\build_fonts.py

What it writes (WPF does not render variable fonts reliably, so every font is static):

- Atkinson Hyperlegible Regular and Bold and JetBrains Mono Medium: the official static files, unchanged.
- Material Symbols Rounded: two static instances of the variable font (FILL 0 and FILL 1, wght 400, GRAD 0,
  opsz 24), cut to the icons that the product data and the prototype name, without ligatures: the app draws an
  icon by its code point (assets/fonts/codepoints.json), never by typing its name.
- The licenses of the three families and codepoints.json.

The output is deterministic: the same sources give the same bytes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
import tempfile
import urllib.request
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

REPO = Path(__file__).resolve().parents[2]
OUTPUT = REPO / "assets" / "fonts"
SOURCES = Path(__file__).resolve().with_name("sources.json")

# Files whose words name the icons the product can show: every JSON string of the catalogs and the starter content,
# the seed of the design package and every identifier of the binding prototype (over-inclusive on purpose: a word
# that happens to be an icon name costs a few hundred bytes, a missing icon is a blank tile).
ICON_SOURCES = (
    "data/catalogs/*.json",
    "data/content/**/*.json",
    "docs/design/handoff/data/seed-and-catalogs.json",
    "docs/design/handoff/prototype/Prototipo v4.dc.html",
)

# Axis values of the static instances (docs/07 «Tipografía»: fill 0, 1 in active states; weight 400).
MATERIAL_AXES = {"wght": 400, "GRAD": 0, "opsz": 24}
MATERIAL_INSTANCES = (
    (0, "Material Symbols Rounded", "MaterialSymbolsRounded"),
    (1, "Material Symbols Rounded Filled", "MaterialSymbolsRoundedFilled"),
)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument(
        "--cache",
        type=Path,
        default=Path(tempfile.gettempdir()) / "clicalo-font-sources",
        help="Folder for the downloaded sources (outside the repository).",
    )
    args = parser.parse_args()

    sources = json.loads(SOURCES.read_text(encoding="utf-8"))["sources"]
    files = {source["id"]: fetch(source, args.cache) for source in sources}

    OUTPUT.mkdir(parents=True, exist_ok=True)
    for source in sources:
        if source["output"]:
            shutil.copyfile(files[source["id"]], OUTPUT / source["output"])

    codepoints = read_codepoints(files["materialSymbolsRoundedCodepoints"])
    icons = collect_icons(set(codepoints))
    material = next(s for s in sources if s["id"] == "materialSymbolsRounded")
    for fill, family, postscript in MATERIAL_INSTANCES:
        build_material(
            files["materialSymbolsRounded"],
            sorted({int(codepoints[name], 16) for name in icons}),
            fill,
            family,
            postscript,
            material["commit"],
        )

    write_codepoints(icons, codepoints, material["commit"])
    print(f"build_fonts: {len(icons)} icons; assets/fonts written.")
    return 0


def fetch(source: dict, cache: Path) -> Path:
    """Downloads one source into the cache (once) and checks its SHA-256."""
    cache.mkdir(parents=True, exist_ok=True)
    path = cache / f"{source['id']}-{source['sha256'][:16]}"
    if not path.exists():
        partial = path.with_suffix(".part")
        with urllib.request.urlopen(source["url"]) as response, partial.open("wb") as file:
            shutil.copyfileobj(response, file)
        partial.replace(path)

    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest != source["sha256"]:
        path.unlink()
        raise SystemExit(
            f"{source['id']}: SHA-256 {digest} does not match sources.json ({source['sha256']})."
        )

    return path


def read_codepoints(path: Path) -> dict[str, str]:
    codepoints: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            name, value = line.split()
            codepoints[name] = value.lower()
    return codepoints


def collect_icons(known: set[str]) -> list[str]:
    words: set[str] = set()
    for pattern in ICON_SOURCES:
        matches = sorted(REPO.glob(pattern))
        if not matches:
            raise SystemExit(f"No file matches {pattern}.")
        for path in matches:
            text = path.read_text(encoding="utf-8")
            if path.suffix == ".json":
                words.update(strings(json.loads(text)))
            else:
                words.update(re.findall(r"[a-z][a-z0-9_]*", text))
    return sorted(word for word in words if word in known)


def strings(node) -> list[str]:
    if isinstance(node, str):
        return [node]
    if isinstance(node, dict):
        return [s for value in node.values() for s in strings(value)]
    if isinstance(node, list):
        return [s for value in node for s in strings(value)]
    return []


def build_material(
    variable: Path,
    unicodes: list[int],
    fill: int,
    family: str,
    postscript: str,
    commit: str,
) -> None:
    font = TTFont(variable, recalcTimestamp=False)

    options = subset.Options()
    options.layout_features = []  # No ligatures: icons are drawn by code point.
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    options.notdef_outline = True
    options.glyph_names = False
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=unicodes)
    subsetter.subset(font)

    instancer.instantiateVariableFont(font, {**MATERIAL_AXES, "FILL": fill}, inplace=True)
    for table in ("STAT", "fvar", "avar", "HVAR", "MVAR", "gvar"):
        if table in font:
            del font[table]

    names = font["name"]
    # Typographic family names and the axis and instance names of the removed fvar and STAT tables.
    names.names = [n for n in names.names if n.nameID not in (16, 17, 25) and n.nameID < 256]
    names.setName(family, 1, 3, 1, 0x409)
    names.setName("Regular", 2, 3, 1, 0x409)
    names.setName(f"{family} Regular", 4, 3, 1, 0x409)
    names.setName(f"{postscript}-Regular", 6, 3, 1, 0x409)
    names.setName(
        f"Static instance of Material Symbols Rounded (google/material-design-icons {commit}) made for "
        f"Clicalo: FILL {fill}, wght 400, GRAD 0, opsz 24, cut to {len(unicodes)} icons, without ligatures.",
        10,
        3,
        1,
        0x409,
    )
    version = names.getName(5, 3, 1, 0x409).toUnicode().removeprefix("Version ")
    names.setName(f"{version};GOOG;{postscript}-Regular;Clicalo", 3, 3, 1, 0x409)
    font["OS/2"].usWeightClass = 400
    font["head"].macStyle = 0
    font.save(OUTPUT / f"{postscript}-Regular.ttf")


def write_codepoints(icons: list[str], codepoints: dict[str, str], commit: str) -> None:
    document = {
        "$comment": (
            "Generated by tools/fonts/build_fonts.py: do not edit. Code point (hexadecimal) of every icon included "
            "in MaterialSymbolsRounded-Regular.ttf and MaterialSymbolsRoundedFilled-Regular.ttf, by Material "
            "Symbols name. The app draws an icon by its code point; the fonts have no ligatures."
        ),
        "source": f"google/material-design-icons {commit}",
        "icons": {name: codepoints[name] for name in icons},
    }
    text = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    (OUTPUT / "codepoints.json").write_text(text, encoding="utf-8", newline="\n")


if __name__ == "__main__":
    sys.exit(main())
