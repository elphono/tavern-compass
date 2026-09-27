Maquette cliquable du panneau des compos (publiée : https://claude.ai/artifact/R5bjiDfNWRMzf7tkz9QWDE). Reconstruire, dans ce dossier :
`curl -o cards.json https://api.hearthstonejson.com/v1/latest/enUS/cards.json && python3 build.py` (télécharge les images dans `cache/`, écrit `maquette-compos-visees.html`) ;
puis `python3 floor.py` (aucun texte sous 12 px, Playwright + chromium) et `python3 shots.py` (captures dans `shots/`). `data.py` : compositions synthétiques.
