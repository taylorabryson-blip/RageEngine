# Rage Engine 0.55: item JSONs cloned from vanilla bases (shape, grips, damage and AI come from the base; the look is ours).
# python3 make_items55.py <vanilla Items dir> <mod Items dir>
import json, sys, os
SRC, DST = sys.argv[1], sys.argv[2]
def load(n): return json.load(open(os.path.join(SRC, n), encoding='utf-8-sig'))
def own(n): return json.load(open(os.path.join(DST, n), encoding='utf-8-sig'))
W = [  # id, base file, name, value, tier, mass (None = base), description
 ('RE_Blackwake_Outlaw', 'Item_Weapon_SwordOldieFalchion.json', 'Blackwake Saber', 640, 2, None, 'A weathered cutlass with a brass knuckle guard. Outlaw boarding crews carry them.'),
 ('RE_Briarhook_Wildfolk', 'Item_Weapon_AxeShortHatchet.json', 'Briarhook Axe', 520, 2, None, 'A thorned ash haft and a hooked, bearded bit. The Wildfolk make them from what the forest gives.'),
 ('RE_KingsOath_Eraden', 'Item_Weapon_SwordLongReinforced.json', "King's Oath", 1400, 3, None, 'A gilt royal longsword with blue enamel. Eraden officers swear their oath on it.'),
 ('RE_Chancellor_Eraden', 'Item_Weapon_MaceShortFlanged.json', 'Iron Chancellor Mace', 820, 3, None, 'A flanged steel mace whose head is shaped like a crown. Carried by Eraden enforcers.'),
 ('RE_Observer_Eye', 'Item_Weapon_SwordLongCommon.json', "Observer's Edge", 1500, 3, None, 'A black blade. In the open eye at its root a violet crystal floats, turning slowly.'),
 ('RE_Glaive_Rakta', 'Item_Weapon_SpearRaktaGuanDao.json', 'Redstone Glaive', 1300, 3, 3.6, 'A long Rakta glaive. Crimson crystals are set in its collar.'),
 ('RE_AshenTalon_Rakta', 'Item_Weapon_SwordShortCommon.json', 'Ashen Talon', 700, 2, None, 'A curved Rakta short sword with an angular pommel and an ash-grey wrap.'),
 ('RE_Tidebreaker_Sentari', 'Item_Weapon_SpearBoar.json', 'Tidebreaker Trident', 1150, 3, None, 'A barbed Sentari trident with brass fittings, made for boarding and for the sea.'),
 ('RE_Azure_Sentari', 'Item_Weapon_SwordShortLarge.json', 'Azure Corsair Saber', 980, 3, None, 'A slim Sentari sabre. Its brass stirrup hilt is set with blue enamel.'),
 ('RE_Khopesh_KharTib', 'Item_Weapon_SwordOldieFalchion.json', "Dune King's Khopesh", 900, 3, None, 'A hooked bronze sword of Khar-Tib, with a sun emblem and turquoise inlay.'),
]
V = [  # id, base, name, value, tier, description
 ('RE_Coin_Contraband', 'Item_Valuable_GoldCoin.json', 'Contraband Coin', 60, 3, 'A heavy gold coin struck with a skull over crossed keys. Outlaws pay their debts in these.'),
 ('RE_Signet_Royal', 'Item_Valuable_GoldCoin.json', 'Royal Signet', 95, 3, 'A gold signet ring with the Eraden crown in blue enamel.'),
 ('RE_Heartroot_Totem', 'Item_Valuable_StatuetteWildfolk.json', 'Heartroot Totem', 110, 3, 'Twisted roots around a seed that still glows green. A Wildfolk treasure.'),
 ('RE_Jade_Relic', 'Item_Valuable_Pendant.json', 'Imperial Jade Relic', 120, 4, 'A carved jade disc on a gold cord, from the Rakta imperial treasury.'),
 ('RE_Pearl_Captain', 'Item_Valuable_Relic.json', "Captain's Pearl", 140, 4, 'An oversized pearl that glows faintly, kept in an opened scallop. A Sentari captain\'s prize.'),
 ('RE_Sunstone_Celestial', 'Item_Valuable_StatuetteGuard.json', 'Celestial Sunstone', 130, 4, 'An amber crystal in a bronze sunburst. It holds the light of the Khar-Tib desert.'),
]
R = [  # rare finds: hand tools on the potion base (like the NPC Spawner)
 ('RE_HollowEye_Crystal', 'Hollow Eye Crystal', 2500, 'An empty crystal from deep in an Eye dungeon. Pull the trigger to fire a violet beam. It overheats if held on too long.'),
 ('RE_Compass_Captain', "Captain's Compass", 1800, 'A Sentari captain\'s compass. The needle does not point north - it points to the nearest treasure.'),
]
def common(d, i, name, value, tier, desc):
    d['id'] = i; d['localizationId'] = i; d['displayName'] = name; d['description'] = desc; d['author'] = 'Rage Engine'
    d['value'] = float(value); d['tier'] = tier; d['levelRequired'] = 0
    return d
n = 0
for i, base, name, value, tier, mass, desc in W:
    d = common(load(base), i, name, value, tier, desc)
    if mass: d['overrideMassAndDrag'] = True; d['mass'] = mass
    json.dump(d, open(os.path.join(DST, f'Item_{i}.json'), 'w'), indent=2); n += 1
for i, base, name, value, tier, desc in V:
    d = common(load(base), i, name, value, tier, desc)
    json.dump(d, open(os.path.join(DST, f'Item_{i}.json'), 'w'), indent=2); n += 1
for i, name, value, desc in R:
    d = common(own('Item_RE_NPCSpawner.json'), i, name, value, 4, desc)
    d['category'] = 'Utilities'
    json.dump(d, open(os.path.join(DST, f'Item_{i}.json'), 'w'), indent=2); n += 1
for i, name, value, desc in (('RE_Conch_Rakta', 'Rakta Conch Horn', 70, 'A war horn made from a great sea shell. Blow it (trigger) and NPCs nearby come to see what it was.'),
                             ('RE_Pipe_Sentari', "Sentari Bosun's Pipe", 55, "A ship's silver whistle. Pipe it (trigger) and NPCs nearby come to see what it was.")):
    d = common(own('Item_RE_Bell_Eraden.json'), i, name, value, 2, desc)
    json.dump(d, open(os.path.join(DST, f'Item_{i}.json'), 'w'), indent=2); n += 1
print('wrote', n, 'items')
