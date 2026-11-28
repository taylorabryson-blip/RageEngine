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
    render_long([wood, metal, brass, steel, fl], os.path.join(OUT, 'musket_preview.png'), 185,
                (((math.radians(4), 0, math.radians(-90)), (-40, 0, 16), 1.0), ((math.radians(-20), math.radians(-12), math.radians(-110)), (-25, 30, -26), 0.8)))
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
    render_long([wood, brass, metal, fl], os.path.join(OUT, 'blunderbuss_preview.png'), 85,
                (((math.radians(4), 0, math.radians(-90)), (-4, 0, 8), 1.0), ((math.radians(-30), math.radians(10), math.radians(120)), (12, 25, -16), 0.7)))
    print('BLUNDER DONE')

# ============================================================================================ RAKTA TANEGASHIMA (matchlock)
if want('tane'):
    reset()
    BZ = 1.9
    stock = loft('stock', [(-38, -3.4, 1.55, 2.5, 3.3, 3.4), (-31, -2.7, 1.5, 2.2, 2.7, 3.0), (-22, -1.2, 1.38, 1.85, 2.0, 2.6), (-12, -0.2, 1.38, 1.75, 1.9, 2.6),
                            (-6, 0.3, 1.62, 1.75, 2.05, 3.0), (10, 0.35, 1.62, 1.75, 2.0, 3.0), (16, 0.55, 1.42, 1.35, 1.5, 2.8), (60, 0.6, 1.36, 1.25, 1.42, 2.8), (88, 0.65, 1.3, 1.2, 1.35, 2.8)], 18)
    barrel = lathe_y('barrel', [(0.0, -4), (1.62, -4), (1.62, 12), (1.48, 50), (1.38, 98), (1.46, 101), (1.75, 103.5), (1.8, 106), (1.62, 108), (1.62, 110), (0.62, 110), (0.62, 104), (0.0, 104)], 8, 0, BZ + 0.15, 22.5)
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
    inlays = []
    for sx in (-1, 1):                                                              # brass inlays: a crest on the butt, a fish-scale plate mid-stock
        crest = [(-33 + 2.6 * math.cos(2 * math.pi * k / 16), -2.6 + 1.6 * math.sin(2 * math.pi * k / 16)) for k in range(16)]
        inlays.append(side_plate('crest', crest, sx * 1.58, 0.12))
        inlays.append(side_plate('scale', [(34, 0.2), (36, 1.0), (44, 1.0), (46, 0.2), (44, -0.6), (36, -0.6)], sx * 1.42, 0.12))
    # the match cord: coiled round the wrist, running up to the serpentine's jaw
    coil = []
    for i in range(0, 22 * 16 + 1):
        a = 2 * math.pi * i / 16; y = -26 + i * 0.5 / 16
        coil.append((math.cos(a) * 1.6, y, -1.0 + math.sin(a) * 2.2))
    cord = tube('coil', coil, 0.25, 6)
    lead = tube('lead', [(1.7, -15.5, -1.0), (2.4, -8, 0.4), (2.6, -1, 2.0), (2.45, 1.2, 3.0), (2.3, 2.2, 3.0)], 0.2, 6)
    ember = sphere('ember', 0.42, (2.3, 2.45, 3.0), 12, 8)
    wood = join('Lacquer', [stock]); box_uv(wood, 0.06)
    iron = join('Iron', [barrel, fs, rs]); box_uv(iron, 0.12)
    brass = join('Brass', [plate, pan, panc, serp, pivot, trig, cap] + bands + inlays); box_uv(brass, 0.15)
    rope = join('Cord', [cord, lead]); box_uv(rope, 0.4)
    em = join('Ember', [ember]); box_uv(em)
    mats([(wood, LAC()), (iron, GUN()), (brass, BRS()), (rope, matcol('cord', (0.16, 0.10, 0.055), 0.9)), (em, matcol('ember', (1.0, 0.35, 0.05), 0.5, 0, 12.0))])
    export([(wood, 'Lacquer', True), (iron, 'Iron', False), (brass, 'Brass', True), (rope, 'Cord', True), (em, 'Ember', True)], 'RE_Tane_')
    print('TANE ember (unity m)', 0.023, 0.030, 0.023)
    render_long([wood, iron, brass, rope, em], os.path.join(OUT, 'tanegashima_preview.png'), 165,
                (((math.radians(4), 0, math.radians(-90)), (-34, 0, 14), 1.0), ((math.radians(-20), math.radians(-12), math.radians(-110)), (-22, 30, -24), 0.8)))
    print('TANE DONE')

# ============================================================================================ RAKTA NAGINATA (blade edge = +Z)
if want('naginata'):
    reset()
    pole = loft('pole', [(-100, 0, 1.45, 1.75, 1.75, 2.3), (-40, 0, 1.5, 1.8, 1.8, 2.3), (20, 0, 1.55, 1.85, 1.85, 2.3), (76, 0, 1.6, 1.9, 1.9, 2.3)], 16)
    wrapA = loft('wrapA', [(45, 0, 1.68, 1.98, 1.98, 2.3), (72, 0, 1.68, 1.98, 1.98, 2.3)], 16, cap=False)
    wrapB = loft('wrapB', [(-14, 0, 1.6, 1.9, 1.9, 2.3), (8, 0, 1.6, 1.9, 1.9, 2.3)], 16, cap=False)
    for w in (wrapA, wrapB):                                                       # ito texture: diamonds along the grip
        me = w.data; uv = me.uv_layers.active.data
        for p in me.polygons:
            for li in p.loop_indices:
                co = me.vertices[me.loops[li].vertex_index].co
                uv[li].uv = ((co.y - (45 if w is wrapA else -14)) / 27.0 * 2.0, (math.atan2(co.z, co.x) / (2 * math.pi) + 0.5))
    ishi = lathe_y('ishizuki', [(0.0, -106.5), (0.7, -106), (1.5, -104.5), (1.68, -101), (1.68, -99.5), (0.0, -99.5)], 16)
    seme = loft('semegane', [(72, 0, 1.78, 2.05, 2.05, 2.4), (76.5, 0, 1.78, 2.05, 2.05, 2.4)], 16)
    tsuba = loft('tsuba', [(76.6, 0.2, 3.3, 3.6, 3.2, 2.2), (77.6, 0.2, 3.3, 3.6, 3.2, 2.2)], 28)
    habaki = loft('habaki', [(77.7, 0.6, 0.75, 2.4, 0.5, 3.0), (80.8, 0.6, 0.62, 2.3, 0.45, 3.0)], 12)
    # the blade: spine (mune) at z_sp, edge (ha) at z_sp + w; curves toward the spine near the tip; planar UV v = 0 spine -> 1 edge
    Y0, L = 80.5, 56.0
    def zsp(y): return -0.0018 * (y - Y0) ** 2
    def width(y):
        t = (y - Y0) / L
        w = 3.1 + 0.9 * t
        if t > 0.82: w *= math.sqrt(max(0.0, 1 - ((t - 0.82) / 0.18) ** 2))
        return max(w, 0.0)
    bm = bmesh.new(); rings = []; NS = 40
    for i in range(NS + 1):
        y = Y0 + L * i / NS; w = width(y); th = 0.42 * (1 - 0.45 * i / NS); z0 = zsp(y)
        sec = [(0.0, z0), (th, z0 + 0.15), (th * 0.9, z0 + 0.45 * w), (0.0, z0 + w), (-th * 0.9, z0 + 0.45 * w), (-th, z0 + 0.15)]
        rings.append([bm.verts.new((x, y, z)) for x, z in sec])
    for i in range(NS):
        for k in range(6):
            l = (k + 1) % 6; bm.faces.new((rings[i][k], rings[i][l], rings[i + 1][l], rings[i + 1][k]))
    bm.faces.new(list(reversed(rings[0])))
    tipv = bm.verts.new((0, Y0 + L + 0.3, zsp(Y0 + L) + 0.1))
    for k in range(6): bm.faces.new((rings[-1][k], rings[-1][(k + 1) % 6], tipv))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    blade = obj_from_bm('Blade', bm)
    me = blade.data; me.uv_layers.new(); uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co; w = max(width(co.y), 0.3)
            uv[li].uv = ((co.y - Y0) / L, min(1.0, max(0.0, (co.z - zsp(co.y)) / w)))
    pole_o = join('Pole', [pole]); box_uv(pole_o, 0.05)
    wrap_o = join('Wrap', [wrapA, wrapB])
    iron = join('Iron', [ishi, tsuba]); box_uv(iron, 0.15)
    brass = join('Brass', [seme, habaki]); box_uv(brass, 0.2)
    mats([(pole_o, LAC()), (wrap_o, matimg('ito', T('ito.png'), 0.6)), (iron, IRN()), (brass, BRS()), (blade, STL())])
    export([(pole_o, 'Pole', True), (wrap_o, 'Wrap', True), (iron, 'Iron', True), (brass, 'Brass', True), (blade, 'Blade', False)], 'RE_Naginata_')
    render_long([pole_o, wrap_o, iron, brass, blade], os.path.join(OUT, 'naginata_preview.png'), 260,
                (((0, 0, math.radians(-90)), (-20, 0, 40), 1.0), ((0, math.radians(90), math.radians(-90)), (-160, 60, -40), 1.9)))
    print('NAGINATA DONE')

# ============================================================================================ ERADEN CROSSBOW (prod across X at the front)
if want('xbow'):
    reset()
    tiller = loft('tiller', [(-38, -1.4, 1.75, 1.9, 2.8, 3.2), (-24, -0.9, 1.7, 1.8, 2.5, 3.2), (-10, 0, 1.65, 1.6, 2.1, 3.6), (30, 0, 1.6, 1.55, 2.0, 3.6),
                              (38, 0, 2.3, 1.8, 2.3, 3.2), (44, 0, 2.3, 1.8, 2.3, 3.2), (45.5, 0, 1.8, 1.4, 1.8, 3.0)], 16)
    groove = box('groove', (0.5, 40, 0.3), (0, 18, 1.55), 0.05)                     # the bolt channel's bone inlay
    nut = cyl('nut', 0.95, 2.4, (0, -2.0, 1.6), 16, 'X', 0.1)
    lever = sweep('lever', [(0, -4.5, -1.7), (0, -12, -3.2), (0, -21, -3.4), (0, -24, -2.9)], rect(0.7, 0.55, 0.12), up=Vector((1, 0, 0)))
    def prod_pts(k=1.0):
        return [(x, 40.5 - 4.0 * (abs(x) / 30.0) ** 1.6 * k, 0.3) for x in [-30 + i * 2.5 for i in range(25)]]
    prod = sweep('prod', prod_pts(), lambda t: rect(0.9 * (1 - 0.35 * abs(2 * t - 1)), 2.2 * (1 - 0.3 * abs(2 * t - 1)), 0.15), up=Vector((0, 0, 1)))
    nocks = [sphere('nock', 0.55, (sx * 30.2, 40.5 - 4.0, 0.3), 10, 6) for sx in (-1, 1)]
    bind = [loft('bind%d' % i, [(y, 0, 2.45, 1.95, 2.45, 3.0), (y + 1.2, 0, 2.45, 1.95, 2.45, 3.0)], 16) for i, y in enumerate((38.5, 42.5))]
    stirrup = tube('stirrup', [(-3.5, 45, 0.3), (-4.2, 50, 0.0), (-3.4, 55, -0.4), (0, 57, -0.6), (3.4, 55, -0.4), (4.2, 50, 0.0), (3.5, 45, 0.3)], 0.42, 8)
    housing = cyl('housing', 2.4, 1.2, (2.2, -22, -0.4), 20, 'X', 0.15)
    rivets = [cyl('rv', 0.32, 0.3, (sx * 1.85, y, 0.2), 8, 'X') for sx in (-1, 1) for y in (-30, -14, 8, 26)]
    # crank: arm + wooden knob, pivot at the housing centre (rotates about X)
    CP = Vector((3.0, -22, -0.4))
    crank = join('Crank', [sweep('arm', [(3.0, -22, -0.4), (3.4, -22, -3.8), (3.4, -22, -6.2)], rect(0.45, 0.9, 0.12), up=Vector((0, 1, 0))),
                           cyl('knob', 0.65, 2.6, (4.7, -22, -6.2), 14, 'X', 0.12), cyl('hub', 0.9, 0.6, (3.1, -22, -0.4), 14, 'X', 0.1)])
    # strings: drawn back to the nut (loaded) / straight across (rest)
    tipL, tipR = Vector((-30.2, 36.5, 0.3)), Vector((30.2, 36.5, 0.3))
    sset = join('StringSet', [tube('s1', [tipL, (-0.8, -1.3, 1.6)], 0.18, 6), tube('s2', [tipR, (0.8, -1.3, 1.6)], 0.18, 6)])
    srest = join('StringRest', [tube('s3', [tipL, tipR], 0.18, 6)])
    # the bolt in the groove (also the flying bolt's model)
    def bolt(y0):
        shaft = lathe_y('shaft', [(0.0, y0), (0.42, y0 + 0.3), (0.46, y0 + 6), (0.42, y0 + 32), (0.0, y0 + 32.2)], 10, 0, 2.15)
        head = lathe_y('head', [(0.0, y0 + 31.5), (0.62, y0 + 32), (0.66, y0 + 34.5), (0.0, y0 + 38.5)], 4, 0, 2.15, 45)
        vanes = [box('vane', (0.08, 5.5, 1.1), (0, y0 + 3.4, 2.15), 0.02) for _ in range(2)]
        vanes[0].rotation_euler = (0, math.radians(30), 0); vanes[1].rotation_euler = (0, math.radians(-30), 0)
        for v in vanes:
            bpy.context.view_layer.objects.active = v; v.select_set(True); bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
            for vv in v.data.vertices:                                              # vanes rotate about the shaft axis
                pass
        return shaft, head, vanes
    shaft, head, vanes = bolt(-1.0)
    oak = join('Oak', [tiller]); box_uv(oak, 0.06)
    iron = join('Iron', [prod, lever, stirrup, housing, nut] + nocks + rivets); box_uv(iron, 0.12)
    brass = join('Bind', bind + [groove]); box_uv(brass, 0.2)
    box_uv(crank, 0.15); box_uv(sset, 0.4); box_uv(srest, 0.4)
    bshaft = join('BoltShaft', [shaft]); box_uv(bshaft, 0.1)
    bhead = join('BoltHead', [head]); box_uv(bhead, 0.3)
    bvane = join('BoltVanes', vanes); box_uv(bvane, 0.3)
    mats([(oak, matimg('oak', T('oak.png'), 0.55)), (iron, IRN()), (brass, matimg('bind', T4('leather_wrap.png'), 0.7)), (crank, IRN()), (sset, ROPE()), (srest, ROPE()),
          (bshaft, matimg('oak2', T('oak.png'), 0.55)), (bhead, IRN()), (bvane, matimg('fl', T('feather.png'), 0.8))])
    export([(oak, 'Oak', True), (iron, 'Iron', True), (brass, 'Bind', True), (sset, 'StringSet', True), (srest, 'StringRest', True),
            (bshaft, 'BoltShaft', True), (bhead, 'BoltHead', False), (bvane, 'BoltVanes', False)], 'RE_Xbow_')
    export([(crank, 'Crank', True)], 'RE_Xbow_', origin=CP)
    print('XBOW crank pivot (unity m)', CP.x * 0.01, CP.z * 0.01, CP.y * 0.01)
    srest.hide_render = True
    render_long([oak, iron, brass, crank, sset, bshaft, bhead, bvane], os.path.join(OUT, 'crossbow_preview.png'), 100,
                (((0, math.radians(-62), math.radians(-90)), (-4, 0, 4), 1.0),))
    print('XBOW DONE')

# ============================================================================================ WILDFOLK BLOWGUN (+ dart)
if want('blowgun'):
    reset()
    prof = [(0.0, -55), (1.25, -55)]
    for n in range(7):                                                              # cane nodes every ~20 cm
        y = -55 + n * 20
        prof += [(1.25, y + 0.5), (1.38, y + 1.2), (1.25, y + 2.0)]
    prof += [(1.15, 80), (0.5, 80), (0.5, 74), (0.0, 74)]
    cane = lathe_y('cane', prof, 16)
    me = cane.data; me.uv_layers.new(); uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = ((co.y + 55) * 0.02, (math.atan2(co.z, co.x) / (2 * math.pi) + 0.5) * 0.5)
    wraps = [lathe_y('w%d' % i, [(1.3, y), (1.48, y + 0.3), (1.48, y + 4.7), (1.3, y + 5.0)], 16) for i, y in enumerate((-46, 18, 66))]
    mouth = lathe_y('mouth', [(0.55, -61), (1.0, -61), (1.55, -59.5), (1.5, -57), (1.32, -55.2), (0.6, -55.2), (0.55, -59)], 16)
    # feather tassel hanging from the rear wrap
    feathers = []
    for i, (x, ang) in enumerate(((-0.6, -12), (0.3, 4), (1.0, 16))):
        pts = [(-0.9, 0), (-0.75, -4), (-0.4, -8), (0.0, -10.5), (0.45, -8), (0.8, -4), (0.9, 0)]
        f = outline_face('feather', pts, 0.0)
        f.rotation_euler = (0, math.radians(ang), math.radians(90)); f.location = (x, -43.5, -2.8)
        bpy.context.view_layer.objects.active = f; f.select_set(True); bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)
        feathers.append(f)
    tassel = tube('tie', [(0, -43.5, -1.4), (0.1, -43.5, -2.9)], 0.15, 6)
    caneo = join('Cane', [cane])
    lea = join('Wraps', wraps + [tassel]); box_uv(lea, 0.3)
    bone = join('Mouth', [mouth]); box_uv(bone, 0.3)
    fea = join('Feathers', feathers); planar_uv(fea, 1, 2, (-46, -14, -40, -2))
    mats([(caneo, matimg('cane', T('cane.png'), 0.5)), (lea, matimg('lw', T4('leather.png'), 0.75)), (bone, matimg('bone', T4('bone.png'), 0.55)), (fea, matimg('fea', T('feather.png'), 0.8))])
    export([(caneo, 'Cane', True), (lea, 'Wraps', True), (bone, 'Mouth', True), (fea, 'Feathers', False)], 'RE_Blowgun_')
    render_long([caneo, lea, bone, fea], os.path.join(OUT, 'blowgun_preview.png'), 150,
                (((math.radians(0), 0, math.radians(90)), (0, 0, 10), 1.0), ((math.radians(-20), math.radians(10), math.radians(70)), (25, 30, -25), 0.7)))
    # the dart: thorn shaft + fluff, tip at +Y
    reset()
    thorn = lathe_y('thorn', [(0.0, -4.5), (0.12, -4.2), (0.12, 3.5), (0.0, 5.0)], 6)
    fluff = sphere('fluff', 0.55, (0, -4.6, 0), 10, 6, (1.0, 1.6, 1.0))
    d1 = join('DartShaft', [thorn]); box_uv(d1, 0.5); d2 = join('DartFluff', [fluff]); box_uv(d2, 0.5)
    export([(d1, 'Shaft', True), (d2, 'Fluff', True)], 'RE_Dart_')
    print('BLOWGUN DONE')

# ============================================================================================ OUTLAW CANNONBALL + GRENADO
if want('balls'):
    reset()
    ball = sphere('ball', 6.0, (0, 0, 0), 32, 16)
    for v in ball.data.vertices:                                                    # sand-cast: faintly lumpy
        v.co *= 1.0 + 0.012 * math.sin(v.co.x * 2.1) * math.cos(v.co.y * 1.7)
    seam = torus('seam', 6.02, 0.12, (0, 0, 0), 48, 5, 'Z')
    sprue = cyl('sprue', 0.7, 0.35, (0, 0, 6.0), 10, 'Z', 0.08)
    cb = join('Ball', [ball, seam, sprue]); box_uv(cb, 0.08)
    mats([(cb, matimg('ci', T('castiron.png'), 0.65, metal=0.6))])
    export([(cb, 'Iron', True)], 'RE_Cannonball_')
    reset()
    shell = sphere('shell', 5.4, (0, 0, 0), 28, 14)
    seam = torus('seam', 5.42, 0.11, (0, 0, 0), 44, 5, 'Z')
    collar = lathe('collar', [(0.0, 4.9), (1.45, 4.9), (1.5, 5.8), (1.3, 6.1), (0.0, 6.1)], 16)
    plug = lathe('plug', [(0.0, 5.9), (1.05, 5.9), (1.0, 7.3), (0.75, 7.6), (0.0, 7.6)], 14)
    fuse = tube('fuse', [(0, 0, 7.4), (0.2, 0, 8.6), (0.9, 0.2, 9.6), (1.9, 0.3, 10.1), (2.8, 0.2, 10.0)], 0.2, 6)
    FT = Vector((2.8, 0.2, 10.0))
    gi = join('Iron', [shell, seam, collar]); box_uv(gi, 0.08)
    gw = join('Plug', [plug]); box_uv(gw, 0.2)
    gf = join('Fuse', [fuse]); box_uv(gf, 0.4)
    mats([(gi, matimg('ci2', T('castiron.png'), 0.65, metal=0.6)), (gw, matimg('wd', T4('wood.png'), 0.6)), (gf, ROPE())])
    export([(gi, 'Iron', True), (gw, 'Plug', True), (gf, 'Fuse', True)], 'RE_Grenado_')
    print('GRENADO fuse tip (unity m)', FT.x * 0.01, FT.z * 0.01, FT.y * 0.01)
    reset()
    a = sphere('a', 6.0, (0, 0, 0), 32, 16); a2 = torus('s', 6.02, 0.12, (0, 0, 0), 48, 5, 'Z'); a3 = cyl('sp', 0.7, 0.35, (0, 0, 6.0), 10, 'Z', 0.08)
    b = sphere('b', 5.4, (16, 0, 0), 28, 14); b2 = torus('s2', 5.42, 0.11, (16, 0, 0), 44, 5, 'Z')
    b3 = lathe('c', [(0.0, 4.9), (1.45, 4.9), (1.5, 5.8), (1.3, 6.1), (0.0, 6.1)], 16); b3.location = (16, 0, 0)
    b4 = lathe('p', [(0.0, 5.9), (1.05, 5.9), (1.0, 7.3), (0.75, 7.6), (0.0, 7.6)], 14); b4.location = (16, 0, 0)
    b5 = tube('f', [(16, 0, 7.4), (16.2, 0, 8.6), (16.9, 0.2, 9.6), (17.9, 0.3, 10.1), (18.8, 0.2, 10.0)], 0.2, 6)
    i1 = join('I', [a, a2, a3, b, b2]); box_uv(i1, 0.08); i2 = join('W', [b4]); box_uv(i2, 0.2); i3 = join('C', [b3]); box_uv(i3, 0.08); i4 = join('F', [b5]); box_uv(i4, 0.4)
    for o in (i1, i2, i3, i4): look(o, True)
    mats([(i1, matimg('ci3', T('castiron.png'), 0.65, metal=0.6)), (i2, matimg('wd2', T4('wood.png'), 0.6)), (i3, matimg('ci4', T('castiron.png'), 0.65, metal=0.6)), (i4, ROPE())])
    render_long([i1, i2, i3, i4], os.path.join(OUT, 'balls_preview.png'), 34, (((0, 0, math.radians(-20)), (-8, 0, 0), 1.0),))
    print('BALLS DONE')

# ============================================================================================ WILDFOLK BOLAS (weights + knot + unit cord; the cords are simulated in game)
if want('bolas'):
    reset()
    st = sphere('stone', 3.1, (0, 0, 0), 20, 12, (1.0, 1.05, 0.9))
    for v in st.data.vertices: v.co *= 1.0 + 0.05 * math.sin(v.co.x * 1.3 + v.co.z * 0.7)
    straps = [torus('st1', 3.05, 0.32, (0, 0, 0), 28, 6, 'Y'), torus('st2', 3.05, 0.32, (0, 0, 0), 28, 6, 'X')]
    for s_ in straps:
        for v in s_.data.vertices: v.co.z *= 0.92
    eye = torus('eye', 0.8, 0.22, (0, 0, 3.4), 14, 6, 'Y')
    w_st = join('Stone', [st]); box_uv(w_st, 0.15)
    w_le = join('Straps', straps + [eye]); box_uv(w_le, 0.3)
    knot = join('Knot', [sphere('k', 1.2, (0, 0, 0), 14, 8, (1.0, 1.0, 0.8)), torus('k2', 1.1, 0.35, (0, 0, 0), 14, 6, 'Z')]); box_uv(knot, 0.3)
    cord = lathe_y('cord', [(0.0, 0), (0.24, 0.1), (0.24, 99.9), (0.0, 100)], 6)
    me = cord.data; me.uv_layers.new(); uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co; uv[li].uv = (co.y * 0.08, (math.atan2(co.z, co.x) / (2 * math.pi) + 0.5))
    mats([(w_st, matimg('stone', T('stone.png'), 0.8)), (w_le, matimg('lea', T4('leather.png'), 0.75)), (knot, matimg('lea2', T4('leather.png'), 0.75)), (cord, matimg('cord', T4('rope.png'), 0.85))])
    export([(w_st, 'Stone', True), (w_le, 'Straps', True)], 'RE_Bolas_')
    export([(knot, 'Knot', True)], 'RE_Bolas_'); export([(cord, 'Cord', True)], 'RE_Bolas_')
    # preview: three weights splayed out on cords
    objs = [knot]
    for i in range(3):
        a = math.radians(90 + i * 120); p = Vector((math.cos(a) * 26, 0, math.sin(a) * 26 - 6))
        for src in (w_st, w_le):
            c = src.copy(); c.data = src.data; bpy.context.collection.objects.link(c); c.location = p; objs.append(c)
        cc = cord.copy(); cc.data = cord.data; bpy.context.collection.objects.link(cc)
        d = p - Vector((0, 0, 0)); cc.rotation_euler = Vector((0, 1, 0)).rotation_difference(d.normalized()).to_euler(); cc.scale = (1, d.length / 100.0 - 0.03, 1); objs.append(cc)
    for o in objs: look(o, True)
    cord.hide_render = True; w_st.hide_render = True; w_le.hide_render = True
    render_long(objs, os.path.join(OUT, 'bolas_preview.png'), 70, (((0, 0, 0), (0, 0, 2), 1.0),))
    print('BOLAS DONE')

# ============================================================================================ ERADEN CALTROPS (bag + one caltrop)
if want('caltrops'):
    reset()
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=14, radius=1.0)
    for v in bm.verts:
        x, y, z = v.co; rho = math.hypot(x, y); a = math.atan2(y, x)
        zz = z if z >= -0.25 else -0.25 + (z + 0.25) * 0.5
        if rho < 1e-5: v.co = Vector((0, 0, 2.6 if z > 0 else zz * 2.6)); continue
        lump = 0.3 * math.sin(a * 4 + z * 5) * math.cos(z * 2.4)                      # the spikes push the canvas out in knobs
        if z <= 0.4: R = (2.6 + lump) * rho
        else:
            t = (z - 0.4) / 0.6; R0 = 2.6 * math.sqrt(1 - 0.4 ** 2)
            R = R0 * (1 - t / 0.8) + 0.8 * (t / 0.8) if t < 0.8 else 0.8 + (t - 0.8) * 2.6 + 0.12 * math.sin(a * 8)
            R *= 1.0 + 0.07 * math.sin(a * 10) * min(1.0, t * 1.6)
        v.co = Vector((math.cos(a) * R, math.sin(a) * R, zz * 2.6))
    sack = obj_from_bm('Bag', bm); box_uv(sack, 0.2)
    tie = join('Tie', [tube('tie', [(math.cos(2 * math.pi * k / 18) * 0.9, math.sin(2 * math.pi * k / 18) * 0.9, 2.35) for k in range(19)], 0.16, 6),
                       tube('e1', [(0.85, -0.2, 2.3), (1.5, -0.5, 1.4), (1.7, -0.5, 0.5)], 0.13, 6), sphere('k1', 0.25, (1.7, -0.5, 0.4), 8, 6)])
    box_uv(tie, 0.4)
    mats([(sack, matimg('canvas', T4('leather.png'), 0.8)), (tie, ROPE())])
    export([(sack, 'Bag', True), (tie, 'Tie', True)], 'RE_Caltrop_')
    reset()
    spikes = []
    for d in (Vector((1, 1, 1)), Vector((1, -1, -1)), Vector((-1, 1, -1)), Vector((-1, -1, 1))):
        d.normalize(); bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=7, radius1=0.42, radius2=0.0, depth=2.8)
        bmesh.ops.translate(bm, vec=Vector((0, 0, 1.4)), verts=bm.verts)
        bmesh.ops.transform(bm, matrix=Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4(), verts=bm.verts)
        spikes.append(obj_from_bm('spike', bm))
    hub = sphere('hub', 0.55, (0, 0, 0), 10, 6)
    cal = join('Caltrop', spikes + [hub]); box_uv(cal, 0.4)
    mats([(cal, IRN())])
    export([(cal, 'Iron', False)], 'RE_Caltrop_')
    print('CALTROPS DONE')

# ============================================================================================ THE EYE FLASH CRYSTAL (crystal in a bronze cage, grip at the bottom)
if want('crystal'):
    reset()
    cr = lathe('crystal', [(0.0, -0.5), (1.5, 1.8), (1.65, 8.0), (1.2, 10.0), (0.0, 12.0)], 6)
    for v in cr.data.vertices: v.co.x *= 1.0 + 0.08 * math.sin(v.co.z * 0.9); v.co.y *= 0.92
    planar_uv(cr, 0, 2, (-1.7, -0.5, 1.7, 12))
    base = lathe('base', [(0.0, -4.0), (1.2, -4.0), (1.5, -3.2), (1.1, -2.2), (1.3, -1.2), (2.0, -0.2), (2.0, 0.6), (0.0, 0.6)], 20)
    ribs = []
    for i in range(3):
        a = 2 * math.pi * i / 3; c, s = math.cos(a), math.sin(a)
        pts = [(c * 1.95, s * 1.95, 0.4), (c * 2.15, s * 2.15, 3.5), (c * 2.15, s * 2.15, 7.5), (c * 1.6, s * 1.6, 10.2), (c * 0.5, s * 0.5, 12.2), (0, 0, 12.7)]
        ribs.append(tube('rib', pts, 0.2, 6))
    top = sphere('top', 0.45, (0, 0, 12.9), 10, 6); loop = torus('loop', 0.65, 0.14, (0, 0, 13.8), 14, 5, 'Y')
    eye = torus('eyering', 1.95, 0.16, (0, 0, -0.1), 28, 6, 'Z')
    cage = join('Cage', [base] + ribs + [top, loop, eye]); box_uv(cage, 0.2)
    crys = join('Crystal', [cr])
    mats([(cage, BRS()), (crys, matimg('cry', T('crystal.png'), 0.05, 1.6))])
    GC = Vector((0, 0, -2.0))
    export([(cage, 'Cage', True), (crys, 'Glass', False)], 'RE_Flash_', origin=GC)
    render_long([cage, crys], os.path.join(OUT, 'crystal_preview.png'), 30, (((0, 0, math.radians(-15)), (0, 0, -4), 1.0),))
    print('CRYSTAL DONE')

# ============================================================================================ RAKTA CONCH HORN (horagai: triton shell, brass mouthpiece at the apex, cord net)
if want('conch'):
    reset()
    bm = bmesh.new(); NS, NT = 150, 22; S = 6.4 * math.pi; g = 0.105; grid = []
    def C(s):                                                                      # conical spiral; apex up at +Z, body whorl down
        R = 0.32 * math.exp(g * s); return Vector((R * math.cos(s), R * math.sin(s), 13.0 - 0.95 * math.exp(g * s) * 1.9)), R
    for i in range(NS + 1):
        s = S * i / NS; c, R = C(s); r = 0.78 * R
        out = Vector((math.cos(s), math.sin(s), 0)); upv = Vector((0, 0, 1))
        varix = 1.0 + 0.09 * max(0.0, math.cos(s * 1.0 - 0.6)) ** 8                   # a ridge every half-turn (varices)
        row = []
        for k in range(NT):
            t = 2 * math.pi * k / NT; rib = 1.0 + 0.04 * math.sin(t * 9)
            row.append(bm.verts.new(c + (out * math.cos(t) * 1.0 + upv * math.sin(t) * 1.15) * r * rib * varix))
        grid.append(row)
    for i in range(NS):
        for k in range(NT):
            l = (k + 1) % NT; bm.faces.new((grid[i][k], grid[i][l], grid[i + 1][l], grid[i + 1][k]))
    cap0 = bm.verts.new(C(0)[0]); [bm.faces.new((grid[0][(k + 1) % NT], grid[0][k], cap0)) for k in range(NT)]
    cl, Rl = C(S); inner = bm.verts.new(cl - Vector((math.cos(S), math.sin(S), 0)) * 0.3 * Rl)   # the aperture: a dark, sunken mouth
    for k in range(NT): bm.faces.new((grid[-1][k], grid[-1][(k + 1) % NT], inner))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    shell = obj_from_bm('Shell', bm)
    me = shell.data; me.uv_layers.new(); uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            vi = me.loops[li].vertex_index; i_ = min(vi // NT, NS); k_ = vi % NT
            uv[li].uv = (i_ / NS * 5.0, k_ / NT)
    apex = C(0)[0]
    mouth = lathe('mouth', [(0.0, apex.z + 4.6), (0.95, apex.z + 4.6), (1.1, apex.z + 4.0), (0.62, apex.z + 3.0), (0.55, apex.z + 1.0), (0.75, apex.z - 0.3), (0.0, apex.z - 0.3)], 18)
    net = []
    for zz in (6.0, 2.5):                                                           # cord rings round the body + a carrying cord
        pts = []
        for k in range(29):
            a = 2 * math.pi * k / 28; best = None
            for i in range(NS + 1):
                c, R = C(S * i / NS)
                if abs(c.z - zz) < 1.2: best = max(best or 0, R * 1.82)
            rr = best or 4.0; pts.append((math.cos(a) * rr, math.sin(a) * rr, zz))
        net.append(tube('ring', pts, 0.18, 6))
    net.append(tube('carry', [(3.0, 0, 6.0), (6.5, -1.0, 10.0), (5.0, -1.2, 15.5), (1.4, -0.3, apex.z + 0.5)], 0.18, 6))
    tassel = [tube('t%d' % j, [(4.2, 0.4 * j, 2.3), (4.6 + 0.2 * j, 0.5 * j, -2.0), (4.5 + 0.3 * j, 0.6 * j, -6.0)], 0.12, 5) for j in range(-2, 3)]
    sh = join('Shell', [shell]); br = join('Mouth', [mouth]); box_uv(br, 0.2)
    nt = join('Net', net); box_uv(nt, 0.4); ts = join('Tassel', tassel); box_uv(ts, 0.4)
    GZ = Vector((0, 0, 4.0))
    mats([(sh, matimg('shell', T('shell.png'), 0.35)), (br, BRS()), (nt, ROPE()), (ts, matcol('tassel', (0.42, 0.12, 0.5), 0.8))])
    export([(sh, 'Shell', True), (br, 'Mouth', True), (nt, 'Net', True), (ts, 'Tassel', True)], 'RE_Conch_', origin=GZ)
    render_long([sh, br, nt, ts], os.path.join(OUT, 'conch_preview.png'), 42, (((0, 0, math.radians(-30)), (-3, 0, -6), 1.0),))
    print('CONCH DONE')

# ============================================================================================ SENTARI BOSUN'S PIPE (gun, buoy, keel, shackle, chain)
if want('pipe'):
    reset()
    gun = sweep('gun', [(0, -4.6 + i * 0.6, 0.35 * math.sin(i / 14 * math.pi) - 0.1) for i in range(15)], lambda t: [(math.cos(a) * (0.42 - 0.12 * t), math.sin(a) * (0.42 - 0.12 * t)) for a in [2 * math.pi * k / 12 for k in range(12)]])
    buoy = sphere('buoy', 1.15, (0, 4.6, 0.25), 18, 10)
    hole = sphere('hole', 0.5, (0, 4.6, 1.25), 10, 6, (1, 1, 0.35))
    keel = side_plate('keel', [(-3.6, -0.3), (-1.0, -0.6), (2.5, -0.9), (4.6, -0.6), (4.8, 0.0), (-3.6, 0.0)], 0.12, 0.24)
    for v in keel.data.vertices: v.co = Vector((v.co.x, v.co.y, v.co.z - 0.45))
    shackle = torus('shackle', 0.42, 0.1, (0, 1.2, -1.4), 12, 5, 'X')
    chain = []
    for i in range(7):
        chain.append(torus('link', 0.34, 0.075, (0, 1.2 - 0.0, -2.0 - i * 0.55), 12, 4, 'X' if i % 2 else 'Y'))
    pipe = join('Silver', [gun, buoy, keel, shackle] + chain); box_uv(pipe, 0.4)
    dark = join('Hole', [hole]); box_uv(dark)
    mats([(pipe, matimg('silver', T('silver.png'), 0.22, metal=1.0)), (dark, matcol('hole', (0.03, 0.03, 0.03), 0.6))])
    export([(pipe, 'Silver', True), (dark, 'Hole', True)], 'RE_Pipe_')
    render_long([pipe, dark], os.path.join(OUT, 'pipe_preview.png'), 14, (((math.radians(10), 0, math.radians(70)), (0, 0, 0), 1.0),))
    print('PIPE DONE')
print('ITEMS55 DONE')
