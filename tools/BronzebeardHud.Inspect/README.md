# Inspection du cache de stats

Lit les fichiers que le plugin a mis en cache (`%LocalAppData%\BronzebeardHud\stats`), **sans jamais télécharger ni rien
écrire**, et imprime ce que la consolidation en fait (chantier b, `docs/plans/2026-10-08-chantier-b-socle-plan.md`).

```bash
dotnet run --project tools/BronzebeardHud.Inspect -c Release -- /mnt/c/Users/<user>/AppData/Local/BronzebeardHud/stats [--bracket 25]
```

1. la vue que le plugin consolide pour la tranche (la même ligne que `stats view` dans le journal d'HDT) ;
2. chaque tranche de Firestone contre « tous les joueurs », traitées comme deux sources : combien de héros la règle de
   recouvrement dit « contested », l'écart en places et en écarts types. Les deux ne sont pas indépendantes (« tous »
   contient la tranche) : c'est une borne basse ;
3. les effectifs, et ce que les seuils (10 parties par héros, 200 parties par carte et par tour) laissent dehors.

La sortie porte des chiffres réels : elle reste **hors du dépôt**, qui est public. Avant de croire une mesure, la passer
sur un témoin (un dossier où la tranche est une copie de « tous les joueurs » : 0 contested, écarts nuls).
