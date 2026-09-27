"""Text floor of the mock-up: the smallest text actually drawn, in 1080p pixels, after every scaling.

python3 floor.py [maquette.html]   (default: maquette-compos-visees.html)

For each scenario (list at 8 suggested, a composition's detail, the lineups panel, the tavern markers, the Skip
combat button) it walks every visible element of the plugin's panels that holds text of its own, takes
getComputedStyle().fontSize times the product of every CSS transform between the element and the stage (which
catches the old shrink-to-fit; the stage's own zoom is left out), and prints the minimum.
Exit status 1 when a scenario goes under 12 px (Ali, 2026-09-27: no text under 12 px at 1080p).
"""
import os
import sys
from playwright.sync_api import sync_playwright

FLOOR = 12
SOURCE = sys.argv[1] if len(sys.argv) > 1 else "maquette-compos-visees.html"

MEASURE = """(selectors) => {
  const stage = document.getElementById('stage');
  // Product of every CSS transform between the element and the stage (the stage's own zoom excluded): exact,
  // unlike a ratio of rounded layout widths.
  const scaleOf = el => {
    let s = 1;
    for (let a = el; a && a !== stage; a = a.parentElement) {
      const t = getComputedStyle(a).transform;
      if (t && t !== 'none') { const m = new DOMMatrixReadOnly(t); s *= Math.hypot(m.a, m.b); }
    }
    return s;
  };
  let min = null, where = null, count = 0;
  for (const root of document.querySelectorAll(selectors)) {
    for (const el of [root, ...root.querySelectorAll('*')]) {
      const own = [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim().length > 0);
      if (!own || el.getClientRects().length === 0) continue;
      const style = getComputedStyle(el);
      if (style.visibility === 'hidden' || style.display === 'none') continue;
      const px = parseFloat(style.fontSize) * scaleOf(el);
      count++;
      if (min === null || px < min) { min = px; where = (el.className || el.tagName) + ': "' + el.textContent.trim().slice(0, 40) + '"'; }
    }
  }
  return { min, where, count };
}"""


def measure(page, name, selectors):
    r = page.evaluate(MEASURE, selectors)
    ok = r["min"] is not None and r["min"] >= FLOOR - 1e-6
    print(f"{name:<32} {r['count']:>3} texts  min {r['min']:.2f} px  {'OK ' if ok else 'LOW'}  smallest: {r['where']}")
    return ok


def click_plus(page, times):
    for _ in range(times):
        page.locator("#panel button[title='One suggestion more']").click()
        page.wait_for_timeout(40)


with sync_playwright() as p:
    html = "<!doctype html><html><head><meta charset=utf-8></head><body>" + open(SOURCE, encoding="utf-8").read() + "</body></html>"
    open("floor-preview.html", "w", encoding="utf-8").write(html)
    browser = p.chromium.launch()
    page = browser.new_page(viewport={"width": 1960, "height": 1150})
    errors = []
    page.on("pageerror", lambda e: errors.append(str(e)))
    page.goto("file://" + os.path.abspath("floor-preview.html"))
    page.wait_for_timeout(500)
    page.click("#zoom-real")
    page.wait_for_timeout(100)
    print(f"source: {SOURCE}")
    results = []
    click_plus(page, 5)
    shrink = page.evaluate("document.getElementById('panel')?.dataset.shrink ?? 'none'")
    print(f"8 suggested: panel shrink factor = {shrink}")
    results.append(measure(page, "list, 8 suggested", "#panel"))
    results.append(measure(page, "tavern markers + buttons", ".mk-label, #overlay > .round"))
    if page.locator("#panel .row .cname").count() > 0:
        page.locator("#panel .row .cname").first.click()
    else:
        page.locator("#panel .ov").first.click()
    page.wait_for_timeout(100)
    results.append(measure(page, "detail of a composition", "#panel"))
    page.locator("#overlay button[title='How top boards field it']").nth(1).click()
    page.wait_for_timeout(100)
    results.append(measure(page, "lineups panel", "#lineups"))
    page.click("#ph-combat")
    page.wait_for_timeout(100)
    results.append(measure(page, "Skip combat button", "#skip"))
    print("page errors:", errors)
    browser.close()
    print("ALL >= 12 px" if all(results) else "SOME TEXT UNDER 12 px")
    sys.exit(0 if all(results) and not errors else 1)
