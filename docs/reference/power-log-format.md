# Power.log Format Reference

Extracted from real Battlegrounds games and python-hslog source.

## Line Structure

Every line:
```
D 08:47:21.5643288 GameState.DebugPrintPower() - CREATE_GAME
^ ^               ^                              ^
|  timestamp       function call                   payload
level (D=debug)
```

**Important:** Each packet is logged TWICE — once by `GameState.DebugPrintPower()` (immediate) and once by `PowerTaskList.DebugPrintPower()` (deferred). Parse `GameState` lines only.

**Indentation:** 4-space groups indicate nesting depth. 0 = top-level, 4 = one level deep, etc.

## Entity Reference Format

Entities appear in multiple forms:

| Form | Example |
|------|---------|
| GameEntity | `Entity=GameEntity` |
| Bracket ref | `Entity=[entityName=Ragnaros the Firelord id=77 zone=HAND zonePos=1 cardId=TB_BaconShop_HERO_11 player=3]` |
| Numeric ID | `Entity=7` or `Target=0` |
| Player name | `Entity=Rival#5678` or `Entity=The Innkeeper` |

Bracket format: `[entityName=<name> id=<int> zone=<zone> zonePos=<int> cardId=<cardId> player=<int>]`

## Packet Types

### CREATE_GAME
```
CREATE_GAME
    GameEntity EntityID=7
        tag=CARDTYPE value=GAME
        tag=ZONE value=PLAY
    Player EntityID=8 PlayerID=3 GameAccountId=[hi=144115198130930503 lo=100002]
        tag=CONTROLLER value=3
        tag=CARDTYPE value=PLAYER
```

### FULL_ENTITY - Creating
```
FULL_ENTITY - Creating ID=29 CardID=TB_BaconShop_HERO_PH
    tag=CONTROLLER value=3
    tag=CARDTYPE value=HERO
    tag=HEALTH value=40
    tag=ZONE value=PLAY
```

### FULL_ENTITY - Updating (PowerTaskList only)
```
FULL_ENTITY - Updating [entityName=BaconPHhero id=29 zone=PLAY zonePos=0 cardId=TB_BaconShop_HERO_PH player=3] CardID=TB_BaconShop_HERO_PH
```

### SHOW_ENTITY
```
SHOW_ENTITY - Updating Entity=229 CardID=TB_BaconShopBadsongE
    tag=CONTROLLER value=3
    tag=CARDTYPE value=ENCHANTMENT
```

### HIDE_ENTITY
```
HIDE_ENTITY - Entity=[entityName=Costs 0 id=229 zone=PLAY zonePos=0 cardId=TB_BaconShopBadsongE player=3] tag=ZONE value=REMOVEDFROMGAME
```

### TAG_CHANGE
```
TAG_CHANGE Entity=GameEntity tag=STEP value=BEGIN_MULLIGAN
TAG_CHANGE Entity=[entityName=Ragnaros the Firelord id=77 zone=HAND zonePos=1 cardId=TB_BaconShop_HERO_11 player=3] tag=LAST_AFFECTED_BY value=8
TAG_CHANGE Entity=79 tag=TAG_SCRIPT_DATA_NUM_1 value=5 DEF CHANGE
```

### BLOCK_START / BLOCK_END
```
BLOCK_START BlockType=TRIGGER Entity=7 EffectCardId= EffectIndex=1 Target=0 SubOption=-1 TriggerKeyword=0
BLOCK_START BlockType=ATTACK Entity=[entityName=Murloc Tidehunter id=167 zone=PLAY zonePos=2 cardId=EX1_506 player=11] EffectCardId= EffectIndex=1 Target=0 SubOption=-1
BLOCK_START BlockType=PLAY Entity=[entityName=Drag To Buy id=165 zone=PLAY ...] EffectCardId= EffectIndex=0 Target=[entityName=Righteous Protector id=166 ...] SubOption=-1
BLOCK_END
```

BlockTypes seen in BG: `TRIGGER`, `PLAY`, `POWER`, `ATTACK`, `DEATHS`, `MOVE_MINION`

### META_DATA
```
META_DATA - Meta=CONTROLLER_AND_ZONE_CHANGE Data=0 InfoCount=5
    Info[0] = [entityName=Righteous Protector id=166 zone=PLAY zonePos=1 cardId=ICC_038 player=11]
    Info[1] = 11
```

## Key Tags for BG Phase Detection

| Tag Change | Meaning |
|---|---|
| `GameEntity tag=STEP value=BEGIN_MULLIGAN` | Hero selection phase |
| `GameEntity tag=STEP value=MAIN_READY` | Shopping phase start |
| `GameEntity tag=STEP value=MAIN_START_TRIGGERS` | Combat phase start |
| `tag=PLAYER_TECH_LEVEL value=N` | Tavern tier changed to N |
| `tag=ZONE value=PLAY` (from HAND) | Minion played to board |
| `tag=ZONE value=GRAVEYARD` | Entity died |

## Source References

- [python-hslog tokens.py](https://github.com/HearthSim/python-hslog/blob/main/hslog/tokens.py)
- [python-hslog parser.py](https://github.com/HearthSim/python-hslog/blob/main/hslog/parser.py)
- [HS Game State Protocol](https://hearthsim.info/docs/gamestate-protocol/)
- [Test data: 36393_battlegrounds.power.log](https://github.com/HearthSim/hsreplay-test-data/blob/master/hslog-tests/36393_battlegrounds.power.log)
