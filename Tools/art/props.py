#!/usr/bin/env python3
"""Downloads single free models (poly.pizza; CC0 or CC BY only) into art-src/free/props, with their author and licence
in manifest.json; Tools/art/models.py converts them.   python3 -I Tools/art/props.py [credits]"""
import json, os, re, sys, time, urllib.request
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "art-src", "free", "props")
UA = {"User-Agent": "Mozilla/5.0 asset-pipeline"}
IDS = {"horse_a": "5ocnVSh_ZF-", "horse_b": "qvTrSG9pZF", "horse_c": "f0_m8hH9BI9", "horse_d": "2csviMCmO46", "horse_e": "F8HAAcLeBL", "horse_f": "2lIMvzwQBV3", "horse_g": "b48xU1GsYOT", "horse_h": "c3lXOu8gooq", "wagon_a": "7PGwjkWbUrS", "wagon_b": "136bU5GesHs", "wagon_c": "9J0ma6qneqK", "wagon_d": "8HnKzYQeRYD", "west_cart": "7Yhe7Ari-5j", "carriage": "6qE9DrRTExX", "cart": "l7bDe7ak6j", "cowboy": "S8hq7LEXTT", "saloon": "82JBTSsWag4", "water_tower": "9JYqZyTiVkU", "water_tower_b": "tMK8bhapAK", "water_tower_c": "WGSUkExDJ4", "dynamite": "HvLXdxqAkB", "gatling": "T8ofhRSenf", "big_cannon": "J15vlPVvKK", "windmill": "ctIRaIM3zXu", "minecart": "fjvk6xVJ3u3", "town_sign": "VSZubp0ru3"}
os.makedirs(OUT, exist_ok=True)
mp = os.path.join(OUT, "manifest.json")
man = json.load(open(mp)) if os.path.exists(mp) else {}
if len(sys.argv) > 1 and sys.argv[1] == "credits":
    by = {}
    for n, m in man.items(): by.setdefault((m["author"], m["licence"]), []).append(m["title"])
    for (a, l), ts in sorted(by.items()): print("- **%s** (%s, poly.pizza): %s." % (a, l, ", ".join(sorted(set(ts)))))
    sys.exit()
for name, pid in IDS.items():
    if name in man and any(os.path.exists(os.path.join(OUT, name + e)) for e in (".glb",)): continue
    page = urllib.request.urlopen(urllib.request.Request("https://poly.pizza/m/" + pid, headers=UA), timeout=60).read().decode("utf-8", "ignore")
    glb = re.search(r"https://static\.poly\.pizza/[^\"]+\.glb", page)
    m = re.search(r'"model":(\{.*?"Animated":(?:true|false)\})', page).group(1)
    g = lambda k: (re.search(r'"%s":"?([^",}]*)' % k, m) or [None, None])[1]
    lic = g("Licence") or ""
    if not glb or not (lic.startswith("CC0") or lic.startswith("CC-BY") or lic.startswith("CC BY")) or any(x in lic for x in ("NC", "SA", "ND")):
        print("SKIPPED", name, lic, bool(glb)); continue
    open(os.path.join(OUT, name + ".glb"), "wb").write(urllib.request.urlopen(urllib.request.Request(glb.group(0), headers=UA), timeout=120).read())
    man[name] = {"id": pid, "title": g("Title"), "author": (re.search(r'"Username":"([^"]*)"', m) or [None, ""])[1], "licence": lic}
    json.dump(man, open(mp, "w"), indent=1)
    print("prop", name, man[name]["title"], man[name]["author"], lic); time.sleep(0.4)
