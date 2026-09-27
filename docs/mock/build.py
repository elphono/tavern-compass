import io, json, base64, os, sys, random, subprocess
from PIL import Image
from data import COMPS
DB = {c["id"]: c for c in json.load(open("cards.json"))}
TRIBES = {"undead-dr": ["UNDEAD"], "naga-spell": ["NAGA"], "murloc-scam": ["MURLOC"], "beast-buff": ["BEAST"],
          "mech-mag": ["MECHANICAL"], "elem-tavern": ["ELEMENTAL"], "pirate-gold": ["PIRATE"], "dragon-brann": ["DRAGON"]}
META = {"undead-dr": (2310, "S"), "naga-spell": (1874, "A"), "murloc-scam": (2655, "S"), "beast-buff": (1412, "A"),
        "mech-mag": (1190, "B"), "elem-tavern": (1733, "B"), "pirate-gold": (964, "B"), "dragon-brann": (802, "C")}
cache = "cache"; os.makedirs(cache, exist_ok=True)
rng = random.Random(7)
ids = []
for c in COMPS:
    c["tribes"] = TRIBES[c["id"]]
    c["games"], c["tier"] = META[c["id"]]
    for x in c["core"] + c["addon"] + (c["board"] or []):
        if x not in ids: ids.append(x)
# Synthetic final boards: the reference board, then four variants (one or two slots swapped, a pair moved).
for c in COMPS:
    base = c["board"] or (c["core"] + c["addon"])[:7]
    extras = [x for x in c["addon"] + c["core"] if x not in base] or c["addon"]
    boards = []
    for i in range(5):
        b = list(base)
        if i > 0:
            for _ in range(1 + i % 2):
                j = rng.randrange(7)
                if b[j] not in c["core"]:
                    b[j] = rng.choice(extras + [rng.choice(ids)])
            if i % 2 == 0:
                j = rng.randrange(6); b[j], b[j + 1] = b[j + 1], b[j]
        boards.append(dict(cards=b, mmr=9100 - i * 230 - rng.randrange(80), turn=10 + rng.randrange(5)))
    c["finalBoards"] = boards
def fetch(url, path):
    if not os.path.exists(path):
        code = subprocess.run(["curl", "-s", "-o", path, "-w", "%{http_code}", url], capture_output=True, text=True).stdout
        assert code == "200", (url, code)
    return Image.open(path)
def uri(img, fmt, **kw):
    b = io.BytesIO(); img.save(b, fmt, **kw)
    return f"data:image/{fmt.lower()};base64," + base64.b64encode(b.getvalue()).decode()
cards = {}
for cid in ids:
    art = fetch(f"https://art.hearthstonejson.com/v1/256x/{cid}.jpg", f"{cache}/{cid}.jpg").convert("RGB").resize((144, 144), Image.LANCZOS)
    full = fetch(f"https://art.hearthstonejson.com/v1/bgs/latest/enUS/256x/{cid}.png", f"{cache}/{cid}.png").convert("RGBA")
    d = DB[cid]
    cards[cid] = dict(name=d["name"], tier=d.get("techLevel"), races=d.get("races") or [],
                      art=uri(art, "JPEG", quality=86), full=uri(full, "WEBP", quality=82))
data = dict(comps=COMPS, cards=cards)
html = open("template.html", encoding="utf-8").read().replace("/*__DATA__*/null", json.dumps(data, ensure_ascii=False))
open("maquette-compos-visees.html", "w", encoding="utf-8").write(html)
print("size", len(html), file=sys.stderr)
