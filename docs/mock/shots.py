"""Headless captures of the mock-up, one per behaviour, at real size (1920 x 1080 stage): python3 shots.py"""
import os
from playwright.sync_api import sync_playwright

html = "<!doctype html><html><head><meta charset=utf-8><meta name=viewport content='width=device-width'></head><body>" + open("maquette-compos-visees.html", encoding="utf-8").read() + "</body></html>"
open("preview.html", "w", encoding="utf-8").write(html)
os.makedirs("shots", exist_ok=True)
with sync_playwright() as p:
    b = p.chromium.launch()
    pg = b.new_page(viewport={"width": 1960, "height": 1150})
    errs = []
    pg.on("pageerror", lambda e: errs.append(str(e)))
    pg.goto("file://" + os.path.abspath("preview.html"))
    pg.wait_for_timeout(600)
    pg.click("#zoom-real")
    pg.wait_for_timeout(100)
    vp = pg.locator("#viewport")

    def shot(name):
        path = os.path.join("shots", name)
        vp.screenshot(path=path)
        kinds = pg.evaluate("[...document.querySelectorAll('.mk-frame')].map(f => f.dataset.kind).join(',')")
        count = pg.evaluate("document.querySelector('#panel .count .n')?.textContent ?? ''")
        print(f"{path}  count=[{count}]  frames=[{kinds}]")

    def plus(times):
        for _ in range(times):
            pg.locator("#panel button[title='One suggestion more']").click()
            pg.wait_for_timeout(40)

    shot("list-3-tavern.png")                                   # 3 suggested, nothing ticked: highlights follow the suggestions
    plus(5)
    pg.click("#own-all")                                        # every composition reachable: 8 suggestions
    pg.wait_for_timeout(100)
    shot("list-8.png")                                          # 8 wanted: as many lines as fit, "n of 8 shown"
    pg.click("#own-preset")
    pg.wait_for_timeout(100)
    pg.locator("#panel .row .cname").first.click()
    pg.wait_for_timeout(100)
    shot("detail.png")                                          # the detail in place of the list
    pg.locator("#panel .dhead button").click()
    pg.locator("#panel .row .aim").nth(1).click()               # tick the second composition
    pg.wait_for_timeout(100)
    shot("tavern-ticked.png")                                   # highlights follow the ticked composition, in its colour
    pg.locator("#overlay button[title='How top boards field it']").nth(1).click()
    pg.wait_for_timeout(100)
    shot("lineups.png")                                         # lineups in the right-hand column
    pg.click("#ph-combat")
    pg.wait_for_timeout(100)
    shot("combat.png")                                          # combat: no combats panel any more, Skip combat button
    print("log:\n" + pg.locator("#log").inner_text())
    print("errors", errs)
    b.close()
