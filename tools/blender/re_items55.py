# Rage Engine 0.55 faction arsenal - blender -b -P re_items55.py -- <outdir> <texdir55> <texdir54>
# Two items per faction + two noisemakers, all built along +Y (Unity +Z) from the grip, top = +Z (Unity +Y).
#  Sentari: long musket (flintlock, socket bayonet), blunderbuss (brass bell barrel), bosun's pipe (noisemaker)
#  Rakta:   tanegashima (matchlock, octagonal barrel, black lacquer), naginata (curved blade on a lacquered pole), conch horn (noisemaker)
#  Outlaws: cannonball, grenado (iron shell, wooden fuse plug, fuse)       Wildfolk: blowgun + dart, bolas (stone weights, cords)
#  Eraden:  crossbow (oak tiller, steel prod, stirrup, crank, bolt), caltrops (bag + caltrop)
#  The Eye: flash crystal (crystal in a bronze cage). (The cursed skull uses the game's own skull.)
# One OBJ per material (Y up, metres, Unity winding, UVs), + Cycles previews. The runtime fits each long model to the
# base weapon's colliders along its handle axis, so only the proportions here matter, not the exact length.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import re_common
from re_common import *

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = argv[0] if argv else '/tmp/out'
TEX = argv[1] if len(argv) > 1 else OUT
TEX54 = argv[2] if len(argv) > 2 else TEX
os.makedirs(OUT, exist_ok=True); re_common.OUT = OUT
def T(f): return os.path.join(TEX, f)
def T4(f): return os.path.join(TEX54, f)
ONLY = os.environ.get('RE55_ONLY', '')
def want(n): return not ONLY or n in ONLY.split(',')

def export(parts, prefix, origin=Vector((0, 0, 0))):
    tris = 0
    for o, n, smooth in parts: look(o, smooth); tris += write_obj(o, prefix + n, origin=origin)
    print(prefix.upper(), 'tris', tris)
    return tris

def look(o, n):
    for p in o.data.polygons: p.use_smooth = n

def mats(pairs):
    for o, m in pairs: o.data.materials.append(m)

WAL = lambda: matimg('walnut', T('walnut.png'), 0.45)
GUN = lambda: matimg('gunmetal', T('gunmetal.png'), 0.35, metal=0.9)
BRS = lambda: matimg('brass', T4('bronze.png'), 0.28, metal=1.0)
IRN = lambda: matimg('iron', T4('iron.png'), 0.5, metal=0.8)
STL = lambda: matimg('steel', T('steel.png'), 0.18, metal=1.0)
LAC = lambda: matimg('lacquer', T('lacquer.png'), 0.12)
ROPE = lambda: matimg('rope', T4('rope.png'), 0.85)

# ============================================================================================ shared flintlock (right side = +X)
def flintlock(x0, ycen, zc, s=1.0):
    """lockplate, cock with flint, frizzen + pan, on the +X side; returns (iron parts, brass parts)"""
    P = lambda y, z: (ycen + y * s, zc + z * s)
    plate = side_plate('lockplate', [P(-7.5, 0.2), P(-6.5, 1.3), P(-3, 1.9), P(5, 1.9), P(8.5, 1.2), P(9.5, 0.2), P(8, -0.9), P(2, -1.4), P(-4.5, -1.2), P(-7, -0.6)], x0 + 0.35 * s, 0.35 * s)
    cock = sweep('cock', [(x0 + 0.6 * s, ycen - 3.8 * s, zc + 0.4 * s), (x0 + 0.65 * s, ycen - 4.6 * s, zc + 2.0 * s), (x0 + 0.7 * s, ycen - 3.6 * s, zc + 3.4 * s),
                          (x0 + 0.7 * s, ycen - 1.8 * s, zc + 3.7 * s), (x0 + 0.7 * s, ycen - 0.6 * s, zc + 3.3 * s)], rect(0.5 * s, 0.75 * s, 0.1 * s))
    jaw = box('jaw', (0.55 * s, 1.1 * s, 0.45 * s), (x0 + 0.7 * s, ycen - 0.4 * s, zc + 3.55 * s), 0.08 * s)
    flint = box('flint', (0.6 * s, 0.9 * s, 0.35 * s), (x0 + 0.7 * s, ycen + 0.5 * s, zc + 3.2 * s), 0.05 * s)
    frizzen = sweep('frizzen', [(x0 + 0.6 * s, ycen + 1.6 * s, zc + 1.7 * s), (x0 + 0.6 * s, ycen + 1.9 * s, zc + 3.3 * s), (x0 + 0.6 * s, ycen + 1.5 * s, zc + 4.6 * s)], rect(1.2 * s, 0.35 * s, 0.08 * s), up=Vector((0, 1, 0)))
    pan = box('pan', (1.2 * s, 1.8 * s, 0.4 * s), (x0 + 0.35 * s, ycen + 1.0 * s, zc + 1.6 * s), 0.1 * s)
    spring = sweep('spring', [(x0 + 0.55 * s, ycen + 2.0 * s, zc + 0.8 * s), (x0 + 0.6 * s, ycen + 5.0 * s, zc + 0.3 * s), (x0 + 0.55 * s, ycen + 7.5 * s, zc + 0.6 * s)], rect(0.35 * s, 0.5 * s))
    screw = cyl('screw', 0.42 * s, 0.25 * s, (x0 + 0.75 * s, ycen - 3.8 * s, zc + 0.4 * s), 12, 'X')
    return [plate, cock, jaw, frizzen, pan, spring, screw], [], flint

def trigger_guard(y0, y1, zb, drop, s=1.0):
    pts = [(0, y0, zb)] + [(0, y0 + (y1 - y0) * t, zb - drop * math.sin(math.pi * t) ** 0.8 - 0.2 * t) for t in [i / 10 for i in range(1, 10)]] + [(0, y1, zb)]
    g = sweep('guard', pts, rect(0.75 * s, 0.3 * s, 0.08 * s), up=Vector((1, 0, 0)))
    tang1 = box('tang1', (0.7 * s, 4.0 * s, 0.25 * s), (0, y0 - 2.0 * s, zb + 0.05), 0.08 * s)
    tang2 = box('tang2', (0.7 * s, 3.0 * s, 0.25 * s), (0, y1 + 1.5 * s, zb + 0.05), 0.08 * s)
    return [g, tang1, tang2]

def trigger(y, zb, s=1.0):
    return sweep('trig', [(0, y, zb + 0.3), (0, y - 0.5 * s, zb - 0.9 * s), (0, y - 1.4 * s, zb - 1.7 * s)], rect(0.32 * s, 0.6 * s, 0.06 * s), up=Vector((0, 1, 0)))

# ============================================================================================ SENTARI LONG MUSKET
if want('musket'):
    reset()
    BZ = 1.95                                                                   # barrel axis height
    stock = loft('stock', [(-48, -4.5, 2.1, 4.0, 6.4, 3.2), (-40, -3.7, 2.2, 3.8, 5.6, 3.0), (-30, -2.6, 2.0, 3.2, 4.2, 2.8), (-22, -1.6, 1.7, 2.6, 2.9, 2.5),
                            (-14, -0.6, 1.45, 1.9, 2.1, 2.2), (-8, -0.2, 1.55, 1.9, 2.3, 2.4), (-3, 0.2, 1.95, 1.9, 2.6, 3.0), (12, 0.3, 1.95, 1.8, 2.4, 3.0),
                            (18, 0.4, 1.5, 1.45, 1.9, 2.6), (40, 0.5, 1.42, 1.3, 1.7, 2.4), (62, 0.6, 1.36, 1.2, 1.6, 2.4), (84, 0.7, 1.26, 1.1, 1.45, 2.4)], 18)
    barrel = lathe_y('barrel', [(0.0, -4), (1.15, -4), (1.17, 10), (1.02, 50), (0.95, 91.5), (1.06, 93.5), (1.06, 96), (0.6, 96), (0.6, 92), (0.0, 92)], 20, 0, BZ)
    tang = box('tang', (0.9, 7.0, 0.3), (0, -7.5, BZ + 0.6), 0.1)
    fsight = box('fs', (0.15, 0.9, 0.5), (0, 93.5, BZ + 1.25), 0.04)
    rsight = box('rs', (0.9, 0.6, 0.45), (0, 14, BZ + 1.3), 0.05)
    lockI, _, flint = flintlock(1.95, 3.0, 0.6)
    ramrod = lathe_y('ramrod', [(0.0, 10), (0.3, 10), (0.3, 93), (0.42, 93.4), (0.42, 95), (0.0, 95.2)], 10, 0, -0.85)
    bayonet = []
    bayonet.append(lathe_y('socket', [(1.22, 86.5), (1.42, 86.5), (1.42, 87.2), (1.32, 87.6), (1.32, 94.6), (1.45, 95.0), (1.45, 95.8), (1.22, 95.8)], 18, 0, BZ))
    bayonet.append(sweep('neck', [(0, 95.2, BZ - 1.1), (0, 96.5, BZ - 1.9), (0, 98.0, BZ - 2.4)], rect(0.7, 0.55, 0.1), up=Vector((0, 1, 0))))
    def tri(t):                                                                  # triangular bayonet section, shrinking to the point
        k = (1 - t) ** 0.8 * 1.0 + 0.02
        return [(0, 0.75 * k), (-0.95 * k, -0.35 * k), (0.95 * k, -0.35 * k)]
    bayonet.append(sweep('blade', [(0, 97.5 + i * 1.75, BZ - 2.5) for i in range(21)], tri))
    bands = [loft('band%d' % i, [(y, 1.05, 1.6, 2.05, 2.0, 3.0), (y + 1.1, 1.05, 1.6, 2.05, 2.0, 3.0)], 18) for i, y in enumerate((38, 64))]
    nose = loft('nose', [(83.6, 0.8, 1.38, 1.2, 1.55, 2.6), (86, 0.8, 1.3, 1.15, 1.45, 2.6)], 18)
    pipes = [lathe_y('pipe%d' % i, [(0.38, y), (0.5, y), (0.5, y + 2.2), (0.38, y + 2.2)], 10, 0, -0.85) for i, y in enumerate((28, 52, 76))]
    butt = loft('buttplate', [(-48.9, -4.5, 2.2, 4.1, 6.5, 3.2), (-48.0, -4.5, 2.2, 4.1, 6.5, 3.2)], 18)
    guard = trigger_guard(-16, -2.5, -2.55, 2.0)
    trig = trigger(-8.5, -2.4)
    side = side_plate('sideplate', [(-5, 0.3), (-3, 1.2), (6, 1.1), (9, 0.3), (6, -0.6), (-3, -0.7)], -1.95, 0.3)
    wood = join('Wood', [stock]); box_uv(wood, 0.06)
    for o in (wood,): pass
    metal = join('Barrel', [barrel, tang, fsight, rsight, ramrod] + lockI); box_uv(metal, 0.12)
    brass = join('Brass', bands + [nose, butt, side] + pipes + guard); box_uv(brass, 0.15)
    steel = join('Bayonet', bayonet + [trig]); box_uv(steel, 0.08)
    fl = join('Flint', [flint]); box_uv(fl, 0.3)
    mats([(wood, WAL()), (metal, GUN()), (brass, BRS()), (steel, STL()), (fl, matcol('flint', (0.28, 0.25, 0.22), 0.6))])
    export([(wood, 'Wood', True), (metal, 'Iron', True), (brass, 'Brass', True), (steel, 'Steel', True), (fl, 'Flint', False)], 'RE_Musket_')
    render_long([wood, metal, brass, steel, fl], os.path.join(OUT, 'musket_preview.png'), 150,
                (((math.radians(4), 0, math.radians(90)), (0, 0, 12), 1.0), ((math.radians(-25), math.radians(15), math.radians(70)), (25, 30, -30), 0.75)))
    print('MUSKET DONE')

# ============================================================================================ SENTARI BLUNDERBUSS
if want('blunder'):
    reset()
    BZ = 2.1
    stock = loft('stock', [(-34, -3.9, 2.0, 3.4, 5.7, 3.2), (-27, -3.1, 2.0, 3.1, 4.7, 3.0), (-19, -1.6, 1.65, 2.4, 2.7, 2.5), (-12, -0.6, 1.45, 1.85, 2.05, 2.2),
                            (-6, 0.2, 1.95, 2.0, 2.5, 3.0), (8, 0.3, 1.95, 1.9, 2.4, 3.0), (13, 0.5, 1.55, 1.5, 1.8, 2.6), (28, 0.6, 1.45, 1.35, 1.6, 2.4), (31, 0.7, 1.2, 1.1, 1.3, 2.4)], 18)
    barrel = lathe_y('barrel', [(0.0, -4), (1.45, -4), (1.5, -3), (1.5, 4), (1.36, 5), (1.3, 14), (1.25, 22), (1.36, 28), (1.6, 32), (2.0, 35), (2.6, 37.5), (3.15, 39.2), (3.35, 39.8),
                                (3.35, 40.6), (2.95, 40.6), (2.4, 38.5), (1.6, 34), (1.0, 30), (0.0, 30)], 28, 0, BZ)
    rings_ = [lathe_y('r%d' % i, [(1.3, y), (1.48, y), (1.48, y + 0.8), (1.3, y + 0.8)], 24, 0, BZ) for i, y in enumerate((5, 20))]
    lockI, _, flint = flintlock(1.95, 1.5, 0.5, 0.95)
    butt = loft('buttplate', [(-34.9, -3.9, 2.1, 3.5, 5.8, 3.2), (-34.0, -3.9, 2.1, 3.5, 5.8, 3.2)], 18)
    guard = trigger_guard(-14.5, -2.0, -2.5, 2.1)
    trig = trigger(-7.5, -2.35)
    nose = loft('nose', [(29.5, 0.7, 1.5, 1.4, 1.65, 2.5), (31.2, 0.7, 1.3, 1.2, 1.4, 2.5)], 18)
    sight = box('sight', (0.2, 0.9, 0.45), (0, 36.5, BZ + 2.4), 0.05)
    wood = join('Wood', [stock]); box_uv(wood, 0.06)
    brass = join('Brass', [barrel, butt, nose, sight] + rings_ + guard); box_uv(brass, 0.12)
    metal = join('Iron', lockI + [trig]); box_uv(metal, 0.12)
    fl = join('Flint', [flint]); box_uv(fl, 0.3)
    mats([(wood, WAL()), (brass, BRS()), (metal, GUN()), (fl, matcol('flint', (0.28, 0.25, 0.22), 0.6))])
    export([(wood, 'Wood', True), (brass, 'Brass', True), (metal, 'Iron', True), (fl, 'Flint', False)], 'RE_Blunder_')
    render_long([wood, brass, metal, fl], os.path.join(OUT, 'blunderbuss_preview.png'), 80,
                (((math.radians(4), 0, math.radians(90)), (0, 0, 6), 1.0), ((math.radians(-30), math.radians(10), math.radians(-60)), (14, 25, -16), 0.7)))
    print('BLUNDER DONE')

# ============================================================================================ RAKTA TANEGASHIMA (matchlock)
if want('tane'):
    reset()
    BZ = 1.9
    stock = loft('stock', [(-38, -3.4, 1.55, 2.5, 3.3, 3.4), (-31, -2.7, 1.5, 2.2, 2.7, 3.0), (-22, -1.2, 1.38, 1.85, 2.0, 2.6), (-12, -0.2, 1.38, 1.75, 1.9, 2.6),
                            (-6, 0.3, 1.62, 1.75, 2.05, 3.0), (10, 0.35, 1.62, 1.75, 2.0, 3.0), (16, 0.55, 1.42, 1.35, 1.5, 2.8), (60, 0.6, 1.36, 1.25, 1.42, 2.8), (88, 0.65, 1.3, 1.2, 1.35, 2.8)], 18)
    barrel = lathe_y('barrel', [(0.0, -4), (1.42, -4), (1.42, 12), (1.3, 50), (1.22, 98), (1.3, 101), (1.55, 103.5), (1.6, 106), (1.45, 108), (1.45, 110), (0.62, 110), (0.62, 104), (0.0, 104)], 8, 0, BZ, 22.5)
    fs = box('fs', (0.2, 1.0, 0.55), (0, 106.5, BZ + 1.7), 0.05)
    rs = box('rs', (0.9, 0.7, 1.1), (0, 18, BZ + 1.75), 0.06)
    # brass: side plate, pan (hizara) + cover, serpentine (hibasami) arm, trigger, barrel bands, butt cap
    plate = side_plate('plate', [(-7, 0.0), (-6, 1.0), (9, 1.0), (10.5, 0.0), (9, -1.1), (-6, -1.1)], 1.7, 0.25)
    pan = box('pan', (1.1, 2.2, 0.35), (1.95, 2.0, BZ - 0.15), 0.1)
    panc = box('panc', (1.2, 2.4, 0.18), (2.0, 2.6, BZ + 0.35), 0.05)
    serp = sweep('serp', [(2.05, 7.0, 0.6), (2.15, 6.3, 2.0), (2.2, 4.8, 3.1), (2.2, 3.2, 3.4), (2.15, 2.4, 2.9)], rect(0.45, 0.65, 0.1))
    pivot = cyl('pivot', 0.5, 0.35, (2.0, 7.0, 0.6), 14, 'X')
    trig = sweep('trig', [(0, -4.0, -1.6), (0, -4.6, -2.6), (0, -5.6, -3.3)], rect(0.4, 0.7, 0.08), up=Vector((0, 1, 0)))
    bands = [loft('band%d' % i, [(y, 1.0, 1.55, 2.4, 1.6, 3.2), (y + 0.9, 1.0, 1.55, 2.4, 1.6, 3.2)], 18) for i, y in enumerate((30, 58, 84))]
    cap = loft('buttcap', [(-38.8, -3.4, 1.62, 2.55, 3.4, 3.4), (-37.9, -3.4, 1.62, 2.55, 3.4, 3.4)], 18)
    # the match cord: coiled round the wrist, running up to the serpentine's jaw
    coil = []
    for i in range(0, 4 * 24 + 1):
        a = 2 * math.pi * i / 24; y = -28 + i * 0.13
        coil.append((math.cos(a) * 1.75, y, -1.0 + math.sin(a) * 2.3))
    cord = tube('coil', coil, 0.2, 6)
    lead = tube('lead', [(1.7, -15.5, -1.0), (2.4, -8, 0.4), (2.6, -1, 2.0), (2.45, 1.2, 3.0), (2.3, 2.2, 3.0)], 0.2, 6)
    ember = sphere('ember', 0.26, (2.3, 2.3, 3.0), 10, 6)
    wood = join('Lacquer', [stock]); box_uv(wood, 0.06)
    iron = join('Iron', [barrel, fs, rs]); box_uv(iron, 0.12)
    brass = join('Brass', [plate, pan, panc, serp, pivot, trig, cap] + bands); box_uv(brass, 0.15)
    rope = join('Cord', [cord, lead]); box_uv(rope, 0.4)
    em = join('Ember', [ember]); box_uv(em)
    mats([(wood, LAC()), (iron, GUN()), (brass, BRS()), (rope, ROPE()), (em, matcol('ember', (1.0, 0.35, 0.05), 0.5, 0, 6.0))])
    export([(wood, 'Lacquer', True), (iron, 'Iron', False), (brass, 'Brass', True), (rope, 'Cord', True), (em, 'Ember', True)], 'RE_Tane_')
    print('TANE ember (unity m)', 0.023, 0.030, 0.023)
    render_long([wood, iron, brass, rope, em], os.path.join(OUT, 'tanegashima_preview.png'), 150,
                (((math.radians(4), 0, math.radians(90)), (-3, 0, 12), 1.0), ((math.radians(-25), math.radians(15), math.radians(70)), (25, 30, -30), 0.75)))
    print('TANE DONE')
