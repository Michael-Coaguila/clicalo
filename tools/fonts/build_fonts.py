"""Builds assets/fonts and the generated part of data/catalogs/icons.json from the pinned files of tools/fonts/sources.json.

Run by hand when a font version changes; the output is committed. It needs Python 3.11+ and the fontTools
version of requirements.txt, installed in a throwaway virtual environment outside the repository:

    python -m venv %TEMP%\\clicalo-fonts-venv
    %TEMP%\\clicalo-fonts-venv\\Scripts\\python -m pip install -r tools\\fonts\\requirements.txt
    %TEMP%\\clicalo-fonts-venv\\Scripts\\python tools\\fonts\\build_fonts.py

What it writes (WPF does not render variable fonts reliably, so every font is static):

- Atkinson Hyperlegible Regular and Bold and JetBrains Mono Medium: the official static files, unchanged.
- Material Symbols Rounded: two static instances of the variable font (FILL 0 and FILL 1, wght 400, GRAD 0,
  opsz 24) with every icon of the font, without ligatures: the app draws an icon by its code point
  (assets/fonts/codepoints.json), never by typing its name.
- The licenses of the three families and codepoints.json, with every name of the font (aliases included).
- data/catalogs/icons.json (D14): the curated entries (the ones with «tags», edited by hand) keep their place and
  their words and get their category and the official English keywords; after them come every other icon of the
  Material Symbols metadata, most popular first, with its category and keywords. defaults, featured,
  profileFeatured and catalogVersion are kept as they are.

The output is deterministic: the same sources and the same curated entries give the same bytes.
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
from collections import Counter
from pathlib import Path

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

REPO = Path(__file__).resolve().parents[2]
OUTPUT = REPO / "assets" / "fonts"
CATALOG = REPO / "data" / "catalogs" / "icons.json"
SOURCES = Path(__file__).resolve().with_name("sources.json")

# Axis values of the static instances (docs/07 «Tipografía»: fill 0, 1 in active states; weight 400).
MATERIAL_AXES = {"wght": 400, "GRAD": 0, "opsz": 24}
MATERIAL_INSTANCES = (
    (0, "Material Symbols Rounded", "MaterialSymbolsRounded"),
    (1, "Material Symbols Rounded Filled", "MaterialSymbolsRoundedFilled"),
)
MATERIAL_FAMILY = "Material Symbols Rounded"

# The categories of the Material Symbols metadata, in the order of the picker: id in icons.json, upstream name,
# Spanish label, English label.
CATEGORIES = (
    ("uiActions", "UI actions", "Interfaz", "Interface"),
    ("actions", "Actions", "Acciones", "Actions"),
    ("text", "Text", "Texto", "Text"),
    ("communicate", "Communicate", "Comunicación", "Communication"),
    ("audioVideo", "Audio&Video", "Audio y vídeo", "Audio and video"),
    ("images", "Images", "Imágenes", "Images"),
    ("hardware", "Hardware", "Dispositivos", "Devices"),
    ("android", "Android", "Sistema", "System"),
    ("business", "Business", "Trabajo y dinero", "Business"),
    ("social", "Social", "Personas", "People"),
    ("activities", "Activities", "Actividades", "Activities"),
    ("maps", "Maps", "Mapas", "Maps"),
    ("travel", "Travel", "Viajes", "Travel"),
    ("transit", "Transit", "Transporte", "Transport"),
    ("home", "Home", "Casa conectada", "Smart home"),
    ("household", "Household", "Hogar", "Household"),
    ("privacy", "Privacy", "Privacidad", "Privacy"),
)

# Category of a retired Material Symbols name that the product data still uses (brightness_high, for example): the
# metadata only lists it under the older Material Icons, whose categories map onto the new ones like this.
LEGACY_CATEGORIES = {
    "action": "actions",
    "alert": "uiActions",
    "av": "audioVideo",
    "communication": "communicate",
    "content": "text",
    "device": "hardware",
    "editor": "text",
    "file": "actions",
    "hardware": "hardware",
    "home": "home",
    "image": "images",
    "maps": "maps",
    "navigation": "uiActions",
    "notification": "uiActions",
    "places": "travel",
    "search": "actions",
    "social": "social",
    "toggle": "uiActions",
}

# Metadata tags that are build flags, not words.
IGNORED_TAGS = {"disable_ios"}

# A keyword shared by more than this share of the icons («symbol», «data», «line») does not tell them apart: it is
# left out, which keeps icons.json near 1 MB and the search results short.
COMMON_KEYWORD_SHARE = 0.03


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
    material = next(s for s in sources if s["id"] == "materialSymbolsRounded")
    unicodes = sorted(set(codepoints.values()))
    for fill, family, postscript in MATERIAL_INSTANCES:
        build_material(files["materialSymbolsRounded"], unicodes, fill, family, postscript, material["commit"])

    write_codepoints(codepoints, material["commit"])
    metadata = next(s for s in sources if s["id"] == "materialSymbolsMetadata")
    count = write_catalog(files["materialSymbolsMetadata"], codepoints, material["commit"], metadata["sha256"])
    print(f"build_fonts: {len(unicodes)} glyphs, {len(codepoints)} names, {count} icons in the catalog.")
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


def read_codepoints(path: Path) -> dict[str, int]:
    codepoints: dict[str, int] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.strip():
            name, value = line.split()
            codepoints[name] = int(value, 16)
    return codepoints


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

    missing = set(unicodes) - set(font.getBestCmap())
    if missing:
        raise SystemExit(f"{postscript}: no glyph for {len(missing)} code points of the codepoints file.")

    names = font["name"]
    # Typographic family names and the axis and instance names of the removed fvar and STAT tables.
    names.names = [n for n in names.names if n.nameID not in (16, 17, 25) and n.nameID < 256]
    names.setName(family, 1, 3, 1, 0x409)
    names.setName("Regular", 2, 3, 1, 0x409)
    names.setName(f"{family} Regular", 4, 3, 1, 0x409)
    names.setName(f"{postscript}-Regular", 6, 3, 1, 0x409)
    names.setName(
        f"Static instance of Material Symbols Rounded (google/material-design-icons {commit}) made for "
        f"Clicalo: FILL {fill}, wght 400, GRAD 0, opsz 24, every icon ({len(unicodes)} code points), without "
        f"ligatures.",
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


def write_codepoints(codepoints: dict[str, int], commit: str) -> None:
    document = {
        "$comment": (
            "Generated by tools/fonts/build_fonts.py: do not edit. Code point (hexadecimal) of every icon name of "
            "MaterialSymbolsRounded-Regular.ttf and MaterialSymbolsRoundedFilled-Regular.ttf, aliases included. The "
            "app draws an icon by its code point; the fonts have no ligatures."
        ),
        "source": f"google/material-design-icons {commit}",
        "icons": {name: f"{codepoints[name]:x}" for name in sorted(codepoints)},
    }
    text = json.dumps(document, ensure_ascii=False, indent=2) + "\n"
    (OUTPUT / "codepoints.json").write_text(text, encoding="utf-8", newline="\n")


def write_catalog(metadata_path: Path, codepoints: dict[str, int], commit: str, metadata_sha: str) -> int:
    text = metadata_path.read_text(encoding="utf-8")
    metadata = json.loads(text[text.index("{") :])  # The response starts with the anti-XSSI prefix )]}'.
    symbols: dict[str, dict] = {}
    legacy: dict[str, dict] = {}
    for icon in metadata["icons"]:
        target = legacy if MATERIAL_FAMILY in icon["unsupported_families"] else symbols
        target.setdefault(icon["name"], icon)

    upstream = {name: identifier for identifier, name, _, _ in CATEGORIES}

    def category(name: str) -> str:
        if name in symbols:
            return upstream[symbols[name]["categories"][0]]
        if name in legacy:
            return LEGACY_CATEGORIES[legacy[name]["categories"][0]]
        raise SystemExit(f"icons.json: {name} is not in the Material Symbols metadata.")

    def keywords(name: str, curated: list[str]) -> list[str]:
        icon = symbols.get(name) or legacy[name]
        skip = set(name.split("_")) | set(curated)
        words = {
            word
            for tag in icon["tags"]
            if tag.lower() not in IGNORED_TAGS
            for word in re.findall(r"[a-z0-9]+", tag.lower())
            if len(word) > 1 and word not in skip
        }
        return sorted(words)

    current = json.loads(CATALOG.read_text(encoding="utf-8"))
    entries: list[dict] = []
    taken: set[int] = set()
    for icon in current["icons"]:
        if "tags" not in icon:
            continue
        name = icon["id"]
        if name not in codepoints:
            raise SystemExit(f"icons.json: the font has no icon {name}.")
        if codepoints[name] in taken:
            raise SystemExit(f"icons.json: {name} draws the same glyph as another curated icon.")
        taken.add(codepoints[name])
        entry = {"id": name, "category": category(name), "tags": icon["tags"]}
        entry["keywords"] = keywords(name, icon["tags"]["en"])
        entries.append(entry)

    for icon in sorted(symbols.values(), key=lambda i: (-i["popularity"], i["name"])):
        name = icon["name"]
        if name not in codepoints or codepoints[name] in taken:
            continue  # A second name of a glyph already listed.
        taken.add(codepoints[name])
        entries.append({"id": name, "category": category(name), "keywords": keywords(name, [])})

    usage = Counter(word for entry in entries for word in entry["keywords"])
    common = {word for word, count in usage.items() if count > COMMON_KEYWORD_SHARE * len(entries)}
    for entry in entries:
        entry["keywords"] = [word for word in entry["keywords"] if word not in common]

    comment = (
        "Edited by hand: catalogVersion, defaults, featured, profileFeatured and the icons with «tags» (their order "
        "and their words). Written by tools/fonts/build_fonts.py (D14): «categories», every «category» and "
        "«keywords», and the icons without «tags», most popular first. Sources: google/material-design-icons "
        f"{commit} and the Material Symbols metadata with SHA-256 {metadata_sha}."
    )
    categories = [
        inline({"id": identifier, "label": {"es": es, "en": en}}) for identifier, _, es, en in CATEGORIES
    ]
    lines = [
        "{",
        f'  "$schema": {inline(current["$schema"])},',
        f'  "$comment": {inline(comment)},',
        f'  "catalogVersion": {current["catalogVersion"]},',
        f'  "defaults": {inline(current["defaults"])},',
        block("featured", [inline(i) for i in current["featured"]]) + ",",
        block("profileFeatured", [inline(i) for i in current["profileFeatured"]]) + ",",
        block("categories", categories) + ",",
        block("icons", [inline(e) for e in entries]),
        "}",
    ]
    CATALOG.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    return len(entries)


def block(name: str, items: list[str]) -> str:
    body = ",\n".join(f"    {item}" for item in items)
    return f'  "{name}": [\n{body}\n  ]'


def inline(value) -> str:
    if isinstance(value, dict):
        return "{ " + ", ".join(f"{inline(k)}: {inline(v)}" for k, v in value.items()) + " }"
    if isinstance(value, list):
        return "[" + ", ".join(inline(v) for v in value) + "]"
    return json.dumps(value, ensure_ascii=False)


if __name__ == "__main__":
    sys.exit(main())
