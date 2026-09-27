# Synthetic compositions (no Firestone/HSReplay data). Card ids are real BG ids.
COMPS = [
  dict(id="undead-dr", name="Undead Deathrattle", place=3.6, tribes=["Undead"], core=["BG25_354","BG25_008"], addon=["BG25_010","BG25_014"],
       board=["BG25_008","BG25_008","BG25_014","BG25_354","BG25_010","BG25_007","BG25_008"], hero=dict(est=3.2, games=23)),
  dict(id="naga-spell", name="Naga Spellcraft", place=3.9, tribes=["Naga"], core=["BG23_012","BG26_171"], addon=["BG27_514","BG_LOE_077"],
       board=["BG23_008","BG23_012","BG26_502","BG23_009","BG26_171","BG23_007","BG27_514"]),
  dict(id="murloc-scam", name="Murloc Scam", place=3.5, tribes=["Murloc"], core=["BG26_354","BG22_403"], addon=["BG27_514","BG_LOE_077"],
       board=["BG27_513","BG26_354","BG22_403","BG32_860","BG27_556","BG22_202","BG_UNG_073"]),
  dict(id="beast-buff", name="Beast Handbuff", place=4.0, tribes=["Beast"], core=["BG26_805","BG26_802"], addon=["BG31_175","BGS_071"],
       board=["BGS_021","BG26_805","BG26_802","BG25_806","BG26_801","BGS_018","BGS_078"]),
  dict(id="mech-mag", name="Mech Magnetic", place=4.1, tribes=["Mech"], core=["BG26_147","BG26_149"], addon=["BG31_175","BGS_071"],
       board=["BG26_152","BG26_147","BG26_149","BG31_175","BGS_071","BG_GVG_113","BG25_807"]),
  dict(id="elem-tavern", name="Elemental Tavern", place=4.3, tribes=["Elemental"], core=["BG26_535","BG26_505"], addon=["BGS_115","BGS_126"],
       board=["BGS_115","BG26_535","BG26_505","BGS_126","BGS_105","BGS_124","BG26_162"]),
  dict(id="pirate-gold", name="Pirate Gold", place=4.4, tribes=["Pirate"], core=["BG26_766","BG29_866"], addon=["BGS_061","BGS_066"],
       board=["BGS_061","BG26_018","BG29_866","BG26_766","BG26_817","BGS_066","BG20_104"]),
  dict(id="dragon-brann", name="Dragon Brann", place=4.6, tribes=["Dragon"], core=["BG_LOE_077","BG21_015"],
       addon=["BG24_500","BG24_003","BG21_014","BGS_036","BGS_041"], board=None),
]
