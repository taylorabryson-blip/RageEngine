# Rage Engine 0.55 - faction weapons, Crystal Hunt valuables, rare finds, + Rakta conch / Sentari pipe noisemakers.
# blender -b -P re_items55.py -- <outdir> <texdir55> <texdir54>
# Weapons are built along +Y from the grip (Unity +Z), blade width / edge along +Z (Unity +Y), thickness along X.
# The runtime fits each weapon to its vanilla base weapon's colliders, so proportions matter, exact length doesn't.
# Small items (valuables, rare finds, noisemakers) are built at real size, centred on their origin.
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

def look(o, n):
    for p in o.data.polygons: p.use_smooth = n

SCENE = []                                                                     # (objects, euler, location) for the group renders

def finish(prefix, parts, origin=Vector((0, 0, 0))):
    """parts: [(obj, partname, smooth, material)] -> one OBJ per part; keeps the objects for the sheet render"""
    tris = 0
    for o, n, sm, m in parts:
        apply_all(o); o.data.transform(o.matrix_basis); o.matrix_basis = Matrix.Identity(4)
        look(o, sm)
        if m is not None and not o.data.materials: o.data.materials.append(m)
        tris += write_obj(o, prefix + n, origin=origin)
    print(prefix.upper(), 'tris', tris)
    return [p[0] for p in parts]

M = {}
def mat(key):
    if key in M: return M[key]
    f = {
        'eu': lambda: matimg('eu', T('blade_eu.png'), 0.15, metal=1.0), 'old': lambda: matimg('old', T('steel_old.png'), 0.45, metal=0.9),
        'hamon': lambda: matimg('hamon', T('steel.png'), 0.15, metal=1.0), 'black': lambda: matimg('black', T('blackblade.png'), 0.25, metal=0.9),
        'brass': lambda: matimg('brass', T4('bronze.png'), 0.28, metal=1.0), 'gold': lambda: matimg('gold', T('gold.png'), 0.22, metal=1.0),
        'iron': lambda: matimg('iron', T4('iron.png'), 0.5, metal=0.8), 'gun': lambda: matimg('gun', T('gunmetal.png'), 0.35, metal=0.9),
        'leather': lambda: matimg('leather', T4('leather.png'), 0.7), 'wrap': lambda: matimg('wrap', T4('leather_wrap.png'), 0.7),
        'blue': lambda: matimg('blue', T('leather_blue.png'), 0.6), 'ray': lambda: matimg('ray', T('rayskin.png'), 0.5),
        'ash': lambda: matimg('ash', T('ash.png'), 0.6), 'walnut': lambda: matimg('walnut', T('walnut.png'), 0.45), 'lacquer': lambda: matimg('lac', T('lacquer.png'), 0.12),
        'ito': lambda: matimg('ito', T('ito.png'), 0.6), 'kt': lambda: matimg('kt', T('bronze_kt.png'), 0.3, metal=1.0), 'jade': lambda: matimg('jade', T('jade.png'), 0.1),
        'enamel': lambda: matcol('enamel', (0.08, 0.2, 0.62), 0.12), 'turq': lambda: matcol('turq', (0.1, 0.62, 0.6), 0.15),
        'violet': lambda: matcol('violet', (0.55, 0.2, 0.95), 0.05, 0, 4.0), 'crimson': lambda: matcol('crimson', (0.85, 0.06, 0.08), 0.05, 0, 3.0),
        'amber': lambda: matcol('amber', (1.0, 0.55, 0.1), 0.05, 0, 3.0), 'seed': lambda: matcol('seed', (0.35, 1.0, 0.45), 0.1, 0, 4.0),
        'pearl': lambda: matcol('pearl', (0.93, 0.92, 0.88), 0.08, 0, 0.6), 'shell': lambda: matimg('shell', T('shell.png'), 0.35),
        'coin': lambda: matimg('coinc', T('coin_contraband.png'), 0.25, metal=1.0), 'crest': lambda: matimg('crest', T('signet_crest.png'), 0.15),
        'card': lambda: matimg('card', T('compass_card.png'), 0.6), 'rope': lambda: matimg('rope', T4('rope.png'), 0.85),
        'red': lambda: matcol('redcord', (0.55, 0.06, 0.06), 0.7), 'dark': lambda: matcol('dark', (0.06, 0.05, 0.07), 0.5),
        'glass': lambda: matcol('glass', (0.75, 0.8, 0.85), 0.02), 'silver': lambda: matimg('silver', T('silver.png'), 0.22, metal=1.0),
        'eyeglass': lambda: matcol('eyeglass', (0.16, 0.08, 0.24), 0.03), 'wood': lambda: matimg('wood54', T4('wood.png'), 0.6),
    }[key]
    M[key] = f(); return M[key]

# ============================================================================================ building blocks
def blade(name, y0, L, wfn, curve=0.0, single=False, th=(0.6, 0.25), ns=48, tipclip=0.0):
    """a blade along +Y from y0; wfn(t) = width; single-edged: spine at z_sp, edge at z_sp + w (edge = +Z);
       double-edged: centred, edges at +-w/2. curve bends the tip toward -Z. UV u = along, v = across (edge(s) at v=1 / v=0,1)."""
    bm = bmesh.new(); rings = []; meta = []
    for i in range(ns + 1):
        t = i / ns; y = y0 + L * t; w = max(wfn(t), 0.0); h = th[0] + (th[1] - th[0]) * t
        zc = -curve * (L * t) ** 2
        if single:
            zs = zc + tipclip * max(0.0, t - 0.82) / 0.18 * wfn(0.82) * 0.55       # clipped point: the spine drops to meet the edge
            w2 = max(0.0, (zc + w) - zs)
            sec = [(0, zs + w2), (h * 0.42, zs + 0.55 * w2), (h * 0.5, zs + 0.12 * w2), (h * 0.38, zs), (-h * 0.38, zs), (-h * 0.5, zs + 0.12 * w2), (-h * 0.42, zs + 0.55 * w2)]
            meta.append((zs, w2))
        else:
            sec = [(0, zc + w / 2), (h * 0.5, zc + w * 0.2), (h * 0.5, zc - w * 0.2), (0, zc - w / 2), (-h * 0.5, zc - w * 0.2), (-h * 0.5, zc + w * 0.2)]
            meta.append((zc - w / 2, w))
        rings.append([bm.verts.new((x, y, z)) for x, z in sec])
    m = len(rings[0])
    for i in range(ns):
        for k in range(m):
            l = (k + 1) % m; bm.faces.new((rings[i][k], rings[i][l], rings[i + 1][l], rings[i + 1][k]))
    bm.faces.new(list(reversed(rings[0])))
    tip = bm.verts.new((0, y0 + L + 0.2, (meta[-1][0] + meta[-1][1] * (0.5 if not single else 0.9))))
    for k in range(m): bm.faces.new((rings[-1][k], rings[-1][(k + 1) % m], tip))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4); bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm(name, bm)
    me = o.data; me.uv_layers.new(); uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            t = min(1.0, max(0.0, (co.y - y0) / L)); i = min(ns, int(round(t * ns))); z0, w = meta[i]
            uv[li].uv = (t, min(1.0, max(0.0, (co.z - z0) / max(w, 0.2))))
    return o

def grip(name, y0, y1, r, waist=0.9, segs=16, sq=2.2):
    n = 8; st = []
    for i in range(n + 1):
        t = i / n; y = y0 + (y1 - y0) * t
        rr = r * (1.0 - (1.0 - waist) * (2 * abs(t - 0.5)) ** 2)
        st.append((y, 0, rr * 0.9, rr, rr, sq))
    return loft(name, st, segs)

def pommel_wheel(name, y, R, w):
    return cyl(name, R, w, (0, y, 0), 28, 'X', min(0.4, w * 0.25))

def wedge_z(name, z0, z1, ylo, yhi, thick, nz=10, x0=0.0):
    """a tapered plate extending from z0 to z1 (an axe bit, a flange): for each z, span ylo(z)..yhi(z), thickness thick(z)"""
    bm = bmesh.new(); rings = []
    for i in range(nz + 1):
        t = i / nz; z = z0 + (z1 - z0) * t; a, b, h = ylo(t), yhi(t), thick(t)
        rings.append([bm.verts.new(v) for v in ((x0 - h / 2, a, z), (x0 + h / 2, a, z), (x0 + h / 2, b, z), (x0 - h / 2, b, z))])
    for i in range(nz):
        for k in range(4):
            l = (k + 1) % 4; bm.faces.new((rings[i][k], rings[i][l], rings[i + 1][l], rings[i + 1][k]))
    bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(name, bm)

def rot_y(o, deg):
    apply_all(o); o.data.transform(Matrix.Rotation(math.radians(deg), 4, 'Y')); return o

def arc_pts(cy, cz, R, a0, a1, n=24, x=0.0):
    return [(x, cy + R * math.sin(math.radians(a0 + (a1 - a0) * i / n)), cz + R * math.cos(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]

def gem(name, R, h, loc, segs=8, axis='X'):
    o = lathe(name, [(0.0, -h), (R * 0.75, -h * 0.35), (R, 0.0), (R * 0.75, h * 0.35), (0.0, h)], segs); apply_all(o)
    if axis == 'X': o.data.transform(Matrix.Rotation(math.radians(90), 4, 'Y'))
    elif axis == 'Y': o.data.transform(Matrix.Rotation(math.radians(-90), 4, 'X'))
    o.data.transform(Matrix.Translation(loc)); return o

# ============================================================================================ 1. BLACKWAKE SABER (Outlaws cutlass)
if want('weapons'):
    reset()
    def w_cut(t): return (3.5 + 0.9 * t) * (1.0 if t < 0.9 else max(0.0, 1 - (t - 0.9) / 0.1) ** 0.7)
    bl = blade('Blade', 7.5, 70, w_cut, curve=0.0009, single=True, th=(0.62, 0.3), tipclip=1.0); bl.location = (0, 0, -1.4); apply_all(bl)
    shell = loft('shell', [(6.0, 1.0, 3.6, 4.6, 4.2, 2.2), (6.9, 1.0, 3.6, 4.6, 4.2, 2.2)], 24)
    bow = tube('bow', [(0, 6.4, 4.8), (0, 3.0, 6.4), (0, -2.0, 6.3), (0, -6.5, 5.0), (0, -8.6, 2.2), (0, -9.0, 0.6)], 0.42, 8)
    quil = tube('quil', [(0, 6.4, -2.6), (0, 7.4, -4.2), (0, 8.8, -4.6), (0, 9.8, -3.8)], 0.38, 8)
    grp = grip('grip', -7.5, 6.0, 1.45, 0.88)
    pom = lathe_y('pommel', [(0.0, -10.2), (1.0, -10.0), (1.45, -9.2), (1.3, -7.6), (0.0, -7.6)], 18)
    ferr = lathe_y('ferrule', [(1.35, 5.2), (1.55, 5.4), (1.55, 6.2), (1.35, 6.2)], 18)
    brass = join('Brass', [shell, bow, quil, pom, ferr]); box_uv(brass, 0.2)
    box_uv(grp, 0.2)
    p1 = finish('RE_Blackwake_', [(bl, 'Blade', False, mat('old')), (brass, 'Brass', True, mat('brass')), (grp, 'Grip', True, mat('wrap'))])
    SCENE.append(('saber', p1))

# ============================================================================================ 2. BRIARHOOK AXE (Wildfolk)
if want('weapons'):
    def zs(t): return 0.0
    haft = loft('haft', [(-36, 0.0, 1.35, 1.6, 1.6, 2.3), (-20, 0.1, 1.3, 1.55, 1.55, 2.3), (0, 0.2, 1.25, 1.5, 1.5, 2.3), (20, 0.25, 1.2, 1.45, 1.45, 2.3), (37, 0.2, 1.3, 1.5, 1.5, 2.3), (39, 0.2, 1.1, 1.2, 1.2, 2.3)], 14)
    wrap = loft('wrap', [(-34, 0.0, 1.5, 1.75, 1.75, 2.3), (-17, 0.1, 1.45, 1.7, 1.7, 2.3)], 14, cap=False)
    knob = lathe_y('knob', [(0.0, -38.8), (1.2, -38.6), (1.75, -37.4), (1.5, -36.0), (0.0, -36.0)], 14)
    thorns = []
    import random as _r; R_ = _r.Random(7)
    for i in range(16):
        y = -14 + i * 2.6 + R_.uniform(-0.6, 0.6); a = R_.uniform(0, 2 * math.pi)
        d = Vector((math.cos(a), 0.25, math.sin(a))).normalized(); base = Vector((math.cos(a) * 1.25, y, 0.2 + math.sin(a) * 1.45))
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.38, radius2=0.0, depth=1.6)
        bmesh.ops.translate(bm, vec=Vector((0, 0, 0.8)), verts=bm.verts)
        bmesh.ops.transform(bm, matrix=Vector((0, 0, 1)).rotation_difference(d).to_matrix().to_4x4(), verts=bm.verts)
        bmesh.ops.translate(bm, vec=base, verts=bm.verts); thorns.append(obj_from_bm('thorn', bm))
    sock = lathe_y('socket', [(1.75, 25.5), (2.2, 26.0), (2.25, 37.5), (1.8, 38.2), (1.5, 38.2), (1.5, 25.5)], 16, 0, 0.2)
    bit = wedge_z('bit', 1.6, 13.5, lambda t: 27.0 - 7.5 * t ** 1.6, lambda t: 37.2 + 4.0 * t ** 1.3, lambda t: 2.3 * (1 - t) ** 1.4 + 0.12, 14)
    hook = sweep('hook', [(0, 33.5, -1.6), (0, 32.0, -5.0), (0, 29.2, -8.0), (0, 26.6, -9.4)], lambda t: [(math.cos(a) * 0.9 * (1 - t) + 0.02, math.sin(a) * 0.55 * (1 - t) + 0.02) for a in [2 * math.pi * k / 8 for k in range(8)]])
    wood = join('Wood', [haft, knob] + thorns); box_uv(wood, 0.08)
    head = join('Head', [sock, bit, hook]); box_uv(head, 0.12)
    box_uv(wrap, 0.2)
    p2 = finish('RE_Briarhook_', [(wood, 'Wood', True, mat('ash')), (head, 'Head', False, mat('iron')), (wrap, 'Wrap', True, mat('leather'))])
    SCENE.append(('axe', p2))

# ============================================================================================ 3. KING'S OATH (Eraden longsword)
if want('weapons'):
    def w_ls(t): return (5.0 - 1.6 * t) * (1.0 if t < 0.88 else max(0.0, 1 - (t - 0.88) / 0.12) ** 0.85)
    bl = blade('Blade', 9.6, 86, w_ls, th=(0.75, 0.32))
    cross = sweep('cross', [(0, 9.2 + 0.9 * (abs(z) / 11.0) ** 2, z) for z in [-11 + 22 * i / 16 for i in range(17)]], lambda t: rect(1.1 + 0.5 * abs(2 * t - 1) ** 3, 1.0 + 0.5 * abs(2 * t - 1) ** 3, 0.18), up=Vector((1, 0, 0)))
    block = box('block', (1.9, 1.6, 3.2), (0, 9.1, 0), 0.25)
    plq = [box('plq', (0.2, 1.0, 1.9), (sx * 0.98, 9.1, 0), 0.08) for sx in (-1, 1)]
    grp = grip('grip', -12.0, 8.4, 1.5, 0.86)
    rings_ = [lathe_y('ring', [(1.45, y), (1.65, y + 0.1), (1.65, y + 0.6), (1.45, y + 0.7)], 16) for y in (-12.2, 7.7)]
    wheel = pommel_wheel('wheel', -15.0, 3.0, 2.3)
    neck = lathe_y('neck', [(1.2, -13.8), (1.45, -12.4)], 16)
    ena = [cyl('ena', 1.9, 0.22, (sx * 1.2, -15.0, 0), 24, 'X') for sx in (-1, 1)]
    for e in ena: planar_uv(e, 1, 2, (-16.9, -1.9, -13.1, 1.9))
    gold = join('Gold', [cross, block, wheel, neck] + rings_); box_uv(gold, 0.15)
    enam = join('Enamel', plq); box_uv(enam)
    crest = join('Crest', ena)
    box_uv(grp, 0.2)
    p3 = finish('RE_KingsOath_', [(bl, 'Blade', False, mat('eu')), (gold, 'Gold', True, mat('gold')), (grp, 'Grip', True, mat('blue')),
                                  (enam, 'Enamel', False, mat('enamel')), (crest, 'Crest', False, mat('crest'))])
    SCENE.append(('longsword', p3))

# ============================================================================================ 4. IRON CHANCELLOR MACE (Eraden)
if want('weapons'):
    shaft = lathe_y('shaft', [(0.0, -22.5), (1.6, -22.5), (1.9, -21.5), (1.5, -20.5), (1.35, -6), (1.25, 10), (1.2, 22), (1.6, 23), (0.0, 23)], 16)
    grp = loft('grip', [(-20.2, 0, 1.55, 1.55, 1.55, 2.0), (-6.2, 0, 1.6, 1.6, 1.6, 2.0)], 16, cap=False)
    band = lathe_y('band', [(1.5, 21.2), (2.1, 21.4), (2.1, 23.0), (1.5, 23.2)], 20)
    core = lathe_y('core', [(0.0, 22.5), (1.9, 22.8), (2.0, 37.5), (1.2, 38.5), (0.0, 38.5)], 14)
    flanges = []
    for k in range(7):
        f = wedge_z('fl', 1.6, 5.0, lambda t: 24.5 + 2.4 * t, lambda t: 37.5 - 2.0 * t + 2.6 * math.sin(math.pi * min(1, t * 1.15)) ** 2, lambda t: 0.9 - 0.45 * t, 8)
        rot_y(f, 360.0 * k / 7); flanges.append(f)
    fin = lathe_y('finial', [(0.0, 38.0), (1.4, 38.2), (0.9, 39.6), (1.3, 40.4), (0.0, 44.2)], 12)
    steel = join('Steel', [shaft, core] + flanges); box_uv(steel, 0.12)
    gold = join('Gold', [band, fin]); box_uv(gold, 0.15); box_uv(grp, 0.2)
    p4 = finish('RE_Chancellor_', [(steel, 'Steel', False, mat('gun')), (gold, 'Gold', True, mat('gold')), (grp, 'Grip', True, mat('blue'))])
    SCENE.append(('mace', p4))

# ============================================================================================ 5. OBSERVER'S EDGE (The Eye) - an open eye between guard and blade, a violet crystal floating in it
if want('weapons'):
    def w_ob(t): return (4.4 - 1.0 * t) * (1.0 if t < 0.9 else max(0.0, 1 - (t - 0.9) / 0.1) ** 0.9)
    bl = blade('Blade', 20.0, 76, w_ob, th=(0.7, 0.3))
    guard = sweep('guard', [(0, 8.0 + 2.6 * (abs(z) / 9.0) ** 2.2, z) for z in [-9 + 18 * i / 18 for i in range(19)]], lambda t: rect(1.0, 0.9 + 0.4 * (1 - abs(2 * t - 1)), 0.12), up=Vector((1, 0, 0)))
    lid1 = tube('lid1', [(0, 8.6, 0.0)] + [(0, 8.6 + 11.4 * s, 3.6 * math.sin(math.pi * s)) for s in [i / 14 for i in range(1, 15)]], 0.42, 8)
    lid2 = tube('lid2', [(0, 8.6, 0.0)] + [(0, 8.6 + 11.4 * s, -3.6 * math.sin(math.pi * s)) for s in [i / 14 for i in range(1, 15)]], 0.42, 8)
    root = box('root', (0.9, 1.4, 3.0), (0, 19.6, 0), 0.2)
    grp = grip('grip', -10.0, 7.6, 1.45, 0.86)
    pom = lathe_y('pommel', [(0.0, -15.5), (1.1, -13.0), (1.9, -11.5), (1.5, -10.0), (0.0, -10.0)], 4, 0, 0, 45)
    crys = gem('crystal', 1.35, 2.3, (0, 14.3, 0), 6, 'Y')
    metal = join('Guard', [guard, lid1, lid2, root, pom]); box_uv(metal, 0.15); box_uv(grp, 0.2); box_uv(crys)
    p5 = finish('RE_Observer_', [(bl, 'Blade', False, mat('black')), (metal, 'Guard', True, mat('gun')), (grp, 'Grip', True, mat('wrap'))])
    finish('RE_Observer_', [(crys, 'Crystal', False, mat('violet'))], origin=Vector((0, 14.3, 0)))
    SCENE.append(('eye sword', p5 + [crys]))
    print('OBSERVER crystal centre (unity m)', 0.0, 0.0, 0.143)

# ============================================================================================ 6. REDSTONE GLAIVE (Rakta)
if want('weapons'):
    pole = loft('pole', [(-100, 0, 1.45, 1.75, 1.75, 2.3), (-40, 0, 1.5, 1.8, 1.8, 2.3), (20, 0, 1.55, 1.85, 1.85, 2.3), (74, 0, 1.6, 1.9, 1.9, 2.3)], 16)
    wrapA = loft('wrapA', [(44, 0, 1.68, 1.98, 1.98, 2.3), (70, 0, 1.68, 1.98, 1.98, 2.3)], 16, cap=False)
    wrapB = loft('wrapB', [(-14, 0, 1.6, 1.9, 1.9, 2.3), (8, 0, 1.6, 1.9, 1.9, 2.3)], 16, cap=False)
    for w, y0w in ((wrapA, 44), (wrapB, -14)):
        me = w.data; uv = me.uv_layers.active.data
        for p in me.polygons:
            for li in p.loop_indices:
                co = me.vertices[me.loops[li].vertex_index].co; uv[li].uv = ((co.y - y0w) / 26.0 * 2.0, (math.atan2(co.z, co.x) / (2 * math.pi) + 0.5))
    ishi = lathe_y('ishi', [(0.0, -106.5), (0.7, -106), (1.5, -104.5), (1.68, -101), (1.68, -99.5), (0.0, -99.5)], 16)
    collar = loft('collar', [(70.5, 0.4, 2.0, 2.6, 2.3, 2.6), (74, 0.6, 2.4, 3.6, 2.5, 2.6), (78.5, 0.6, 2.4, 3.6, 2.5, 2.6), (81, 0.6, 1.3, 2.8, 0.9, 2.8)], 18)
    rims = [torus('rim', 1.35, 0.22, (sx * 2.35, 76.2, 1.0), 20, 6, 'X') for sx in (-1, 1)]
    gems = [gem('gem', 1.25, 0.75, (sx * 2.25, 76.2, 1.0), 10, 'X') for sx in (-1, 1)]
    def w_gl(t): return (4.2 + 1.6 * math.sin(math.pi * min(1.0, t * 1.1)) ** 0.8) * (1.0 if t < 0.8 else math.sqrt(max(0.0, 1 - ((t - 0.8) / 0.2) ** 2)))
    bl = blade('Blade', 80.5, 52, w_gl, curve=0.0016, single=True, th=(0.75, 0.35))
    spike = sweep('spike', [(0, 82.5, -0.4), (0, 84.5, -2.4), (0, 87.5, -3.6)], lambda t: [(math.cos(a) * 0.55 * (1 - t) + 0.03, math.sin(a) * 0.5 * (1 - t) + 0.03) for a in [2 * math.pi * k / 6 for k in range(6)]])
    pole_o = join('Pole', [pole]); box_uv(pole_o, 0.05)
    wrap_o = join('Wrap', [wrapA, wrapB])
    iron = join('Iron', [ishi, collar, spike]); box_uv(iron, 0.15)
    gold = join('Gold', rims); box_uv(gold, 0.3)
    gm = join('Gems', gems); box_uv(gm)
    p6 = finish('RE_Glaive_', [(pole_o, 'Pole', True, mat('lacquer')), (wrap_o, 'Wrap', True, mat('ito')), (iron, 'Iron', True, mat('gun')),
                               (gold, 'Gold', True, mat('gold')), (gm, 'Gem', False, mat('crimson')), (bl, 'Blade', False, mat('hamon'))])
    SCENE.append(('glaive', p6))

# ============================================================================================ 7. ASHEN TALON (Rakta short sword)
if want('weapons'):
    def w_at(t): return (3.1 + 0.3 * t) * (1.0 if t < 0.86 else max(0.0, 1 - (t - 0.86) / 0.14))
    bl = blade('Blade', 7.8, 48, w_at, curve=0.0016, single=True, th=(0.7, 0.35), tipclip=0.35); bl.location = (0, 0, -1.2); apply_all(bl)
    tsuba = loft('tsuba', [(6.3, 0.0, 3.4, 3.6, 3.6, 7.0), (7.1, 0.0, 3.4, 3.6, 3.6, 7.0)], 20)
    habaki = loft('habaki', [(7.1, -0.1, 0.55, 2.2, 1.4, 3.0), (9.6, -0.1, 0.5, 2.1, 1.35, 3.0)], 12)
    grp = loft('grip', [(-9.0, 0, 1.25, 1.5, 1.5, 2.4), (-1.5, 0, 1.2, 1.45, 1.45, 2.4), (6.2, 0, 1.3, 1.55, 1.55, 2.4)], 16)
    me = grp.data; uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co; uv[li].uv = ((co.y + 9.0) / 15.2 * 1.6, (math.atan2(co.z, co.x) / (2 * math.pi) + 0.5))
    kashira = lathe_y('kashira', [(0.0, -11.6), (0.9, -11.6), (1.55, -10.6), (1.6, -9.0), (0.0, -9.0)], 6, 0, 0, 30)
    fuchi = loft('fuchi', [(5.6, 0, 1.4, 1.65, 1.65, 3.0), (6.3, 0, 1.4, 1.65, 1.65, 3.0)], 16)
    iron = join('Iron', [tsuba]); box_uv(iron, 0.2)
    gold = join('Gold', [habaki, kashira, fuchi]); box_uv(gold, 0.2)
    p7 = finish('RE_AshenTalon_', [(bl, 'Blade', False, mat('hamon')), (iron, 'Iron', True, mat('gun')), (gold, 'Gold', True, mat('gold')), (grp, 'Grip', True, mat('ito'))])
    SCENE.append(('talon', p7))

# ============================================================================================ 8. TIDEBREAKER TRIDENT (Sentari)
if want('weapons'):
    pole = loft('pole', [(-96, 0, 1.4, 1.4, 1.4, 2.0), (0, 0, 1.5, 1.5, 1.5, 2.0), (72, 0, 1.5, 1.5, 1.5, 2.0)], 14)
    wrapT = loft('wrapT', [(-12, 0, 1.62, 1.62, 1.62, 2.0), (12, 0, 1.62, 1.62, 1.62, 2.0)], 14, cap=False)
    bands = [lathe_y('b', [(1.45, y), (1.8, y + 0.2), (1.8, y + 1.4), (1.45, y + 1.6)], 16) for y in (-60, -30, 30, 50)]
    butt = lathe_y('butt', [(0.0, -99.5), (1.2, -99.2), (1.65, -97.5), (1.55, -95.5), (0.0, -95.5)], 16)
    sock = lathe_y('socket', [(1.5, 70.5), (1.85, 71.0), (1.7, 74.0), (2.0, 75.5), (1.8, 78.0), (2.3, 80.0), (2.3, 81.5), (0.0, 81.5)], 18)
    bar = sweep('bar', [(0, 81.6 + 2.4 * (abs(z) / 8.6) ** 1.8, z) for z in [-8.6 + 17.2 * i / 16 for i in range(17)]], rect(0.9, 1.6, 0.2), up=Vector((1, 0, 0)))
    def prong(z0, zt, y0, y1):
        pts = [(0, y0 + (y1 - y0) * i / 12, z0 + (zt - z0) * (i / 12) ** 1.3) for i in range(13)]
        return sweep('pr', pts, lambda t: [(math.cos(a) * (0.62 * (1 - t) + 0.04), math.sin(a) * (0.8 * (1 - t) + 0.05)) for a in [2 * math.pi * k / 4 + math.pi / 4 for k in range(4)]])
    prongs = [prong(0.0, 0.0, 81.5, 118.0), prong(-8.6, -9.6, 84.8, 108.0), prong(8.6, 9.6, 84.8, 108.0)]
    barbs = []
    for (zc, yt) in ((0.0, 118.0), (-9.6, 108.0), (9.6, 108.0)):
        for s in ((-1, 1) if zc == 0 else ((1,) if zc < 0 else (-1,))):
            barbs.append(sweep('barb', [(0, yt - 7.0, zc), (0, yt - 9.5, zc + s * 1.8), (0, yt - 10.4, zc + s * 2.6)], lambda t: [(math.cos(a) * 0.32 * (1 - t) + 0.03, math.sin(a) * 0.42 * (1 - t) + 0.03) for a in [2 * math.pi * k / 4 for k in range(4)]]))
    wood = join('Pole', [pole]); box_uv(wood, 0.05)
    brass = join('Brass', bands + [butt, sock, bar]); box_uv(brass, 0.15)
    steel = join('Prongs', prongs + barbs); box_uv(steel, 0.15); box_uv(wrapT, 0.2)
    p8 = finish('RE_Tidebreaker_', [(wood, 'Pole', True, mat('walnut')), (brass, 'Brass', True, mat('brass')), (steel, 'Prongs', False, mat('eu')), (wrapT, 'Wrap', True, mat('blue'))])
    SCENE.append(('trident', p8))

# ============================================================================================ 9. AZURE CORSAIR SABER (Sentari)
if want('weapons'):
    def w_az(t): return (2.9 + 0.15 * t) * (1.0 if t < 0.86 else max(0.0, 1 - (t - 0.86) / 0.14) ** 0.8)
    bl = blade('Blade', 7.4, 74, w_az, curve=0.0011, single=True, th=(0.6, 0.25)); bl.location = (0, 0, -1.0); apply_all(bl)
    bow = tube('bow', [(0, 6.8, 2.6), (0, 6.6, 4.6), (0, 3.0, 5.6), (0, -2.5, 5.4), (0, -6.6, 4.0), (0, -8.2, 1.9)], 0.36, 8)
    langet = [loft('lg', [(6.0, 0.6, 0.55, 2.9, 2.0, 2.2), (7.0, 0.6, 0.55, 2.9, 2.0, 2.2)], 16) for _ in range(1)]
    plaques = [loft('pq', [(6.05, 0.6, 0.25, 1.9, 1.2, 2.0), (6.95, 0.6, 0.25, 1.9, 1.2, 2.0)], 16)]
    for q, sx in zip([plaques[0].copy()] if False else [], (1,)): pass
    pq1 = plaques[0]; pq1.location = (0.38, 0, 0); apply_all(pq1)
    pq2 = loft('pq2', [(6.05, 0.6, 0.25, 1.9, 1.2, 2.0), (6.95, 0.6, 0.25, 1.9, 1.2, 2.0)], 16); pq2.location = (-0.38, 0, 0); apply_all(pq2)
    quil = tube('quil', [(0, 6.6, -1.4), (0, 7.3, -2.6), (0, 8.6, -3.0)], 0.3, 8)
    grp = grip('grip', -8.0, 5.8, 1.35, 0.88)
    back = sweep('back', [(0, -8.3, 0.6), (0, -4, 1.45), (0, 0, 1.5), (0, 4, 1.45), (0, 5.9, 1.4)], rect(1.0, 0.25, 0.08), up=Vector((0, 0, 1)))
    pom = lathe_y('pom', [(0.0, -10.4), (0.9, -10.2), (1.4, -9.4), (1.25, -8.0), (0.0, -8.0)], 16)
    brass = join('Brass', [bow, langet[0], quil, back, pom]); box_uv(brass, 0.2)
    ena = join('Enamel', [pq1, pq2]); box_uv(ena); box_uv(grp, 0.15)
    p9 = finish('RE_Azure_', [(bl, 'Blade', False, mat('eu')), (brass, 'Brass', True, mat('brass')), (ena, 'Enamel', False, mat('enamel')), (grp, 'Grip', True, mat('ray'))])
    SCENE.append(('sabre', p9))

# ============================================================================================ 10. DUNE KING'S KHOPESH (Khar-Tib)
if want('weapons'):
    grp = grip('grip', -9.5, 3.5, 1.45, 0.9)
    pom = lathe_y('pom', [(0.0, -12.0), (1.2, -11.8), (1.9, -11.0), (1.6, -9.6), (0.0, -9.6)], 18)
    collar = lathe_y('collar', [(1.4, 3.2), (1.9, 3.5), (1.9, 5.6), (1.5, 6.0), (0.0, 6.0)], 18)
    def w_nk(t): return 2.6 + 0.4 * t
    neck = blade('neck', 5.8, 21.5, w_nk, th=(1.0, 0.85), ns=10)
    for v in neck.data.vertices:
        if v.co.y > 27.0: v.co.y = 27.0                                              # flat end, the crescent takes over
    # the crescent: a fat curved blade sweeping forward and hooking back, sharp on the outer curve (+Z side)
    cpts = [(0, 27.0 + 16.0 * math.sin(math.radians(170 * i / 25)), -7.0 * (1 - math.cos(math.radians(170 * i / 25)))) for i in range(26)]
    def cres(t):
        w = 3.0 + 2.2 * math.sin(math.pi * min(1.0, t * 1.05)) ** 0.7
        w *= (1.0 if t < 0.85 else max(0.05, 1 - (t - 0.85) / 0.15))
        h = 0.85 - 0.5 * t
        return [(-w * 0.62, 0.0), (-w * 0.2, h * 0.5), (w * 0.38, h * 0.45), (w * 0.38, -h * 0.45), (-w * 0.2, -h * 0.5)]
    cr = sweep('crescent', cpts, cres, up=Vector((1, 0, 0)))
    sun = cyl('sun', 2.0, 1.4, (0, 13.0, 0.0), 24, 'X', 0.2)
    rays = []
    for k in range(12):
        a = 2 * math.pi * k / 12
        for sx in (-1, 1):
            bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=0.45, radius2=0.0, depth=1.5)
            bmesh.ops.transform(bm, matrix=Matrix.Rotation(math.radians(90), 4, 'X'), verts=bm.verts)
            bmesh.ops.transform(bm, matrix=Matrix.Rotation(-a, 4, 'X'), verts=bm.verts)
            bmesh.ops.translate(bm, vec=Vector((sx * 0.62, 13.0 + math.cos(a) * 2.6, math.sin(a) * 2.6)), verts=bm.verts)
            rays.append(obj_from_bm('ray', bm))
    turq = [cyl('tq', 1.1, 0.25, (sx * 0.72, 13.0, 0), 20, 'X') for sx in (-1, 1)]
    bronze = join('Bronze', [neck, cr, collar, pom]); box_uv(bronze, 0.12)
    gold = join('Gold', [sun] + rays); box_uv(gold, 0.3)
    tq = join('Turq', turq); box_uv(tq); box_uv(grp, 0.2)
    p10 = finish('RE_Khopesh_', [(bronze, 'Bronze', True, mat('kt')), (gold, 'Gold', True, mat('gold')), (tq, 'Turquoise', False, mat('turq')), (grp, 'Grip', True, mat('wrap'))])
    SCENE.append(('khopesh', p10))

# ============================================================================================ VALUABLES (real size, centred)
if want('valuables'):
    # Contraband Coin: face normal +Z
    coin = cyl('coin', 1.6, 0.26, (0, 0, 0), 40, 'Z', 0.05)
    planar_uv(coin, 0, 1, (-1.6, -1.6, 1.6, 1.6))
    rim = torus('rim', 1.55, 0.09, (0, 0, 0), 40, 5, 'Z')
    gold = join('Rim', [rim]); box_uv(gold, 0.5)
    v1 = finish('RE_Coin_', [(coin, 'Face', True, mat('coin')), (gold, 'Rim', True, mat('gold'))])
    # Royal Signet: band + bezel with the crest on top
    band = torus('band', 1.05, 0.2, (0, 0, 0), 36, 8, 'Y');
    for v in band.data.vertices:
        if v.co.z > 0.6: v.co.x *= 1.0 + 0.6 * (v.co.z - 0.6)                        # the shoulders thicken toward the bezel
    bez = cyl('bezel', 0.72, 0.34, (0, 0, 1.18), 28, 'Z', 0.06)
    top = cyl('top', 0.62, 0.04, (0, 0, 1.36), 28, 'Z'); planar_uv(top, 0, 1, (-0.62, -0.62, 0.62, 0.62))
    rg = join('Ring', [band, bez]); box_uv(rg, 0.5)
    v2 = finish('RE_Signet_', [(rg, 'Gold', True, mat('gold')), (top, 'Crest', False, mat('crest'))])
    # Heartroot Totem: a cage of twisted roots round a glowing seed, root feet and an antler crown
    roots = []
    for k in range(5):
        a0 = 2 * math.pi * k / 5; pts = []
        for i in range(25):
            t = i / 24; a = a0 + t * 2.2; r = 1.0 + 1.5 * math.sin(math.pi * t) ** 1.2 + (0.9 * (1 - t / 0.15) if t < 0.15 else 0)
            pts.append((math.cos(a) * r, math.sin(a) * r, -7.0 + t * 15.0))
        roots.append(sweep('root', pts, lambda t: [(math.cos(q) * (0.55 - 0.3 * t), math.sin(q) * (0.55 - 0.3 * t)) for q in [2 * math.pi * j / 7 for j in range(7)]]))
    crown = []
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        crown.append(sweep('ant', [(math.cos(a) * 0.6, math.sin(a) * 0.6, 7.6), (math.cos(a) * 1.6, math.sin(a) * 1.6, 9.0), (math.cos(a) * 2.2, math.sin(a) * 2.2, 10.6)], lambda t: [(math.cos(q) * 0.3 * (1 - 0.7 * t), math.sin(q) * 0.3 * (1 - 0.7 * t)) for q in [2 * math.pi * j / 6 for j in range(6)]]))
    base_ = lathe('base', [(0.0, -7.6), (2.6, -7.6), (2.9, -7.0), (2.2, -6.4), (0.0, -6.4)], 20)
    seed = sphere('seed', 1.25, (0, 0, 0.2), 20, 12, (1.0, 1.0, 1.25))
    wood = join('Wood', roots + crown + [base_]); box_uv(wood, 0.12)
    v3 = finish('RE_Heartroot_', [(wood, 'Wood', True, mat('walnut')), (seed, 'Seed', True, mat('seed'))])
    # Imperial Jade Relic: a jade bi disc on a gold cord with a red tassel
    bi = lathe('bi', [(1.2, -0.32), (4.3, -0.32), (4.5, 0.0), (4.3, 0.32), (1.2, 0.32), (1.05, 0.0)], 48)
    planar_uv(bi, 0, 1, (-4.5, -4.5, 4.5, 4.5))
    grooves = [torus('gr', r, 0.05, (0, 0, sz * 0.33), 48, 4, 'Z') for r in (2.0, 3.4) for sz in (-1, 1)]
    cord = tube('cord', [(0, 1.15, 0), (0, 0.2, 0.6), (0, -1.15, 0), (0, -2.0, -0.3), (0, -4.6, -0.2), (0, -6.0, 0)], 0.12, 6)
    knot = sphere('knot', 0.4, (0, -6.2, 0), 10, 6)
    tass = [tube('t', [(x * 0.12, -6.4, 0), (x * 0.18, -9.5, 0.1 * x)], 0.07, 4) for x in range(-3, 4)]
    v4 = finish('RE_Jade_', [(bi, 'Jade', True, mat('jade')), (join('Grooves', grooves), 'Grooves', True, mat('gold')),
                             (join('Cord', [cord, knot]), 'Cord', True, mat('gold')), (join('Tassel', tass), 'Tassel', True, mat('red'))])
    # Captain's Pearl: an opened scallop with a big pearl
    def scallop(name, flip):
        bm = bmesh.new(); rows = []
        for i in range(13):
            rho = 0.35 + 4.8 * i / 12; row = []
            for j in range(25):
                ph = math.radians(-62 + 124 * j / 24)
                z = 1.6 * (1 - (rho / 5.2) ** 2) ** 0.5 * 0.9 + 0.18 * max(0, math.cos(ph * 9)) * (rho / 5.2)
                row.append(bm.verts.new((rho * math.sin(ph), rho * math.cos(ph) - 2.4, z * flip)))
            rows.append(row)
        for i in range(12):
            for j in range(24): bm.faces.new((rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        o = obj_from_bm(name, bm); sol = o.modifiers.new('s', 'SOLIDIFY'); sol.thickness = 0.22; apply_all(o); return o
    low = scallop('low', -1); up_ = scallop('up', 1)
    apply_all(up_); up_.data.transform(Matrix.Translation((0, -2.4, 0)) @ Matrix.Rotation(math.radians(62), 4, 'X') @ Matrix.Translation((0, 2.4, 0)))
    up_.data.transform(Matrix.Translation((0, 0, 0.1)))
    for o in (low, up_): box_uv(o, 0.25)
    pearl = sphere('pearl', 1.75, (0, -0.6, 0.2), 28, 16)
    hinge = cyl('hinge', 0.35, 1.6, (0, -2.1, 0.0), 12, 'X', 0.05)
    v5 = finish('RE_Pearl_', [(join('Shell', [low, up_]), 'Shell', True, mat('shell')), (pearl, 'Pearl', True, mat('pearl')), (join('Hinge', [hinge]), 'Hinge', True, mat('brass'))])
    # Celestial Sunstone: an amber crystal in a bronze sunburst, standing on a small foot
    st = gem('stone', 1.6, 3.2, (0, 0, 0.6), 8, 'Z')
    ring = torus('ring', 2.3, 0.26, (0, 0, 0.6), 40, 8, 'Y')
    rays = []
    for k in range(16):
        a = 2 * math.pi * k / 16; L = 2.0 if k % 2 == 0 else 1.2
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=4, radius1=0.42, radius2=0.0, depth=L)
        bmesh.ops.translate(bm, vec=Vector((0, 0, L / 2)), verts=bm.verts)
        bmesh.ops.transform(bm, matrix=Matrix.Rotation(a, 4, 'Y'), verts=bm.verts)
        bmesh.ops.translate(bm, vec=Vector((math.sin(a) * 2.45, 0, 0.6 + math.cos(a) * 2.45)), verts=bm.verts)
        rays.append(obj_from_bm('ray', bm))
    prongs = [tube('pr', [(math.cos(2 * math.pi * k / 4) * 2.2, 0, 0.6 + math.sin(2 * math.pi * k / 4) * 2.2), (math.cos(2 * math.pi * k / 4) * 1.05, 0, 0.6 + math.sin(2 * math.pi * k / 4) * 1.05 * 1.6)], 0.14, 6) for k in range(4)]
    foot = lathe('foot', [(0.0, -4.6), (1.9, -4.6), (2.0, -4.2), (0.7, -3.6), (0.45, -2.0), (0.0, -1.6)], 20)
    sb = join('Bronze', [ring, foot] + rays + prongs); box_uv(sb, 0.2)
    v6 = finish('RE_Sunstone_', [(sb, 'Bronze', True, mat('kt')), (st, 'Stone', False, mat('amber'))])
    SCENE.append(('valuables', [v1, v2, v3, v4, v5, v6]))

# ============================================================================================ RARE FINDS
if want('rare'):
    # Hollow Eye Crystal: dark bronze grip with an eye ring, a hollow violet shell with a burning core; beam leaves the tip (+Z)
    gripc = lathe('grip', [(0.0, -11.0), (1.1, -11.0), (1.35, -10.2), (1.0, -9.4), (1.05, -3.0), (1.6, -1.6), (2.0, -0.6), (1.4, 0.0), (0.0, 0.0)], 8)
    eye = torus('eye', 2.35, 0.28, (0, 0, 1.0), 32, 8, 'Z')
    claws = [tube('claw', [(math.cos(a) * 1.7, math.sin(a) * 1.7, -0.6), (math.cos(a) * 2.5, math.sin(a) * 2.5, 2.4), (math.cos(a) * 1.9, math.sin(a) * 1.9, 5.4), (math.cos(a) * 1.2, math.sin(a) * 1.2, 7.0)], 0.2, 6)
             for a in [2 * math.pi * k / 3 + 0.5 for k in range(3)]]
    shellc = lathe('shell', [(0.0, -0.4), (1.7, 1.8), (1.85, 7.5), (1.2, 10.2), (0.0, 13.0)], 6)
    core = lathe('core', [(0.0, 1.6), (0.7, 3.2), (0.75, 7.0), (0.45, 9.5), (0.0, 11.0)], 6)
    br = join('Bronze', [gripc, eye] + claws); box_uv(br, 0.2)
    r1 = finish('RE_HollowEye_', [(br, 'Bronze', True, mat('gun')), (shellc, 'Shell', False, mat('eyeglass'))], origin=Vector((0, 0, -6.0)))
    finish('RE_HollowEye_', [(core, 'Core', False, mat('violet'))], origin=Vector((0, 0, -6.0)))
    print('HOLLOWEYE tip (unity m)', 0.0, 0.19, 0.0)
    # Captain's Compass: brass case, glass, card, needle, open lid, ring
    case = lathe('case', [(0.0, -0.75), (3.2, -0.75), (3.45, -0.4), (3.45, 0.35), (3.1, 0.5), (2.9, 0.1), (0.0, 0.1)], 40)
    card = cyl('card', 2.85, 0.04, (0, 0, 0.14), 40, 'Z'); planar_uv(card, 0, 1, (-2.85, -2.85, 2.85, 2.85))
    glass = cyl('glass', 2.95, 0.06, (0, 0, 0.42), 40, 'Z')
    lid = lathe('lid', [(0.0, 0.0), (3.3, 0.0), (3.45, 0.25), (3.3, 0.55), (0.0, 0.62)], 40)
    apply_all(lid); lid.data.transform(Matrix.Translation((0, 3.45, 0.45)) @ Matrix.Rotation(math.radians(-105), 4, 'X') @ Matrix.Translation((0, -3.3, 0)))
    bow = torus('bow', 0.75, 0.16, (0, -3.95, 0.0), 20, 6, 'X')
    knob = cyl('knob', 0.45, 0.6, (0, -3.55, 0.0), 12, 'Y')
    nN = box('n1', (0.32, 2.4, 0.08), (0, 1.2, 0.22), 0.0); nS = box('n2', (0.32, 2.4, 0.08), (0, -1.2, 0.22), 0.0)
    for nd in (nN, nS):
        for v in nd.data.vertices: v.co.x *= max(0.0, 1.0 - abs(v.co.y) / 2.45)
        box_uv(nd)
    hub = cyl('hub', 0.22, 0.2, (0, 0, 0.27), 12, 'Z')
    cs = join('Brass', [case, lid, bow, knob, hub]); box_uv(cs, 0.2)
    r2 = finish('RE_Compass_', [(cs, 'Brass', True, mat('brass')), (card, 'Card', False, mat('card')), (glass, 'Glass', True, mat('glass'))])
    finish('RE_Compass_', [(nN, 'NeedleN', False, mat('red')), (nS, 'NeedleS', False, mat('gun'))])
    SCENE.append(('rare', [r1 + [core], r2 + [nN, nS]]))

# ============================================================================================ NOISEMAKERS: Rakta conch horn (rebuilt), Sentari bosun's pipe
if want('conch'):
    bm = bmesh.new(); NS, NT = 160, 24; g = 0.13; S = 4.6 * 2 * math.pi; grid = []
    def C(s):
        R = 0.22 * math.exp(g * s); return Vector((0.92 * R * math.cos(s), 0.92 * R * math.sin(s), -1.25 * R)), R
    for i in range(NS + 1):
        s = S * i / NS; c, R = C(s); out = Vector((math.cos(s), math.sin(s), 0)); upv = Vector((0, 0, 1)); row = []
        lip = 1.0 + 0.25 * max(0.0, (i / NS - 0.9) / 0.1)
        for k in range(NT):
            t = 2 * math.pi * k / NT; knob = 1.0 + 0.06 * max(0.0, math.cos(t * 1.0)) * (0.5 + 0.5 * math.cos(s * 4.5))
            row.append(bm.verts.new(c + (out * math.cos(t) * 1.0 + upv * math.sin(t) * 1.35) * R * lip * knob))
        grid.append(row)
    for i in range(NS):
        for k in range(NT):
            l = (k + 1) % NT; bm.faces.new((grid[i][k], grid[i][l], grid[i + 1][l], grid[i + 1][k]))
    a0 = bm.verts.new(C(0)[0] + Vector((0, 0, 0.3))); [bm.faces.new((grid[0][(k + 1) % NT], grid[0][k], a0)) for k in range(NT)]
    cl, Rl = C(S); inner = bm.verts.new(cl - Vector((math.cos(S), math.sin(S), 0)) * 0.45 * Rl)
    for k in range(NT): bm.faces.new((grid[-1][k], grid[-1][(k + 1) % NT], inner))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    shell = obj_from_bm('Shell', bm)
    me = shell.data; me.uv_layers.new(); uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            vi = me.loops[li].vertex_index; uv[li].uv = (min(vi // NT, NS) / NS * 6.0, (vi % NT) / NT)
    # the whole shell scaled to ~30 cm, spire pointing up
    zs = [v.co.z for v in me.vertices]; zmin, zmax = min(zs), max(zs); k_ = 30.0 / (zmax - zmin)
    me.transform(Matrix.Scale(k_, 4)); me.transform(Matrix.Translation((0, 0, -(zmin * k_ + zmax * k_) / 2)))
    zs = [v.co.z for v in me.vertices]; ztop = max(zs)
    mouth = lathe('mouth', [(0.0, ztop + 4.2), (0.9, ztop + 4.2), (1.05, ztop + 3.6), (0.6, ztop + 2.6), (0.55, ztop + 0.8), (0.8, ztop - 0.6), (0.0, ztop - 0.6)], 18)
    def ring_at(z, grow=1.04):
        rr = max(math.hypot(v.co.x, v.co.y) for v in me.vertices if abs(v.co.z - z) < 1.0) * grow
        cx = sum(v.co.x for v in me.vertices if abs(v.co.z - z) < 1.0) / max(1, sum(1 for v in me.vertices if abs(v.co.z - z) < 1.0))
        return [(math.cos(2 * math.pi * k / 32) * rr * 0.92, math.sin(2 * math.pi * k / 32) * rr * 0.92, z) for k in range(33)]
    net = [tube('ring', ring_at(z), 0.2, 6) for z in (ztop - 6.0, ztop - 13.0)]
    p0 = ring_at(ztop - 6.0)[8]; p1 = ring_at(ztop - 13.0)[8]
    carry = tube('carry', [p0, (p0[0] * 1.6, p0[1] * 1.6, ztop - 2.0), (p0[0] * 1.2, p0[1] * 1.2, ztop + 2.5), (0.6, 0.0, ztop + 1.5)], 0.2, 6)
    tas = [tube('t', [(p1[0], p1[1] + 0.25 * j, p1[2]), (p1[0] * 1.08, p1[1] + 0.3 * j, p1[2] - 4.0), (p1[0] * 1.1, p1[1] + 0.35 * j, p1[2] - 8.0)], 0.13, 5) for j in range(-3, 4)]
    tknot = sphere('tk', 0.55, (p1[0], p1[1], p1[2] - 0.2), 10, 6)
    GZ = Vector((0, 0, 0))
    nc = finish('RE_Conch_', [(shell, 'Shell', True, mat('shell')), (join('Mouth', [mouth]), 'Mouth', True, mat('brass')),
                              (join('Net', net + [carry]), 'Net', True, mat('rope')), (join('Tassel', tas + [tknot]), 'Tassel', True, mat('red'))])
    for o in nc[1:]: box_uv(o, 0.3)
    SCENE.append(('conch', nc))

if want('pipe'):
    gun = sweep('gun', [(0, -4.6 + i * 0.6, 0.35 * math.sin(i / 14 * math.pi) - 0.1) for i in range(15)], lambda t: [(math.cos(a) * (0.42 - 0.12 * t), math.sin(a) * (0.42 - 0.12 * t)) for a in [2 * math.pi * k / 12 for k in range(12)]])
    buoy = sphere('buoy', 1.15, (0, 4.6, 0.25), 18, 10)
    hole = sphere('hole', 0.5, (0, 4.6, 1.25), 10, 6, (1, 1, 0.35))
    keel = side_plate('keel', [(-3.6, -0.3), (-1.0, -0.6), (2.5, -0.9), (4.6, -0.6), (4.8, 0.0), (-3.6, 0.0)], 0.12, 0.24)
    for v in keel.data.vertices: v.co = Vector((v.co.x, v.co.y, v.co.z - 0.45))
    shackle = torus('shackle', 0.42, 0.1, (0, 1.2, -1.4), 12, 5, 'X')
    chain = [torus('link', 0.34, 0.075, (0, 1.2, -2.0 - i * 0.55), 12, 4, 'X' if i % 2 else 'Y') for i in range(7)]
    pipe = join('Silver', [gun, buoy, keel, shackle] + chain); box_uv(pipe, 0.4)
    dark = join('Hole', [hole]); box_uv(dark)
    np_ = finish('RE_Pipe_', [(pipe, 'Silver', True, mat('silver')), (dark, 'Hole', True, mat('dark'))])
    SCENE.append(('pipe', np_))

# ============================================================================================ preview sheets
def sheet(path, items, extent, cam_rot=(90, 0, 0)):
    """items: [(objects, (rx, ry, rz) degrees, (x, y, z) location, scale)] -> one Cycles render"""
    for o in bpy.context.scene.objects: o.hide_render = True
    shown = []
    for objs, rot, loc, sc in items:
        for o in objs:
            c = o.copy(); c.data = o.data; bpy.context.collection.objects.link(c)
            c.rotation_euler = tuple(math.radians(a) for a in rot); c.location = loc; c.scale = (sc, sc, sc); c.hide_render = False; shown.append(c)
    D = extent * 1.45
    bpy.ops.object.camera_add(location=(0, -D, 0), rotation=(math.radians(90), 0, 0))
    cam = bpy.context.object; bpy.context.scene.camera = cam; cam.data.lens = 50
    k = extent / 40.0
    for loc, e in (((-14 * k, -18 * k, 16 * k), 9000 * k * k), ((18 * k, -10 * k, 4 * k), 4000 * k * k), ((0, 14 * k, 10 * k), 6000 * k * k)):
        bpy.ops.object.light_add(type='AREA', location=loc); L = bpy.context.object; L.data.energy = e; L.data.size = 10 * k
        L.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    w = bpy.data.worlds.new('w'); bpy.context.scene.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.012, 0.012, 0.014, 1)
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 64
    try: sc.cycles.use_denoising = True
    except Exception: pass
    sc.view_settings.view_transform = 'Standard'; sc.render.resolution_x, sc.render.resolution_y = 1536, 1024; sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for c in shown: bpy.data.objects.remove(c, do_unlink=True)
    for o in [o for o in bpy.context.scene.objects if o.type in ('CAMERA', 'LIGHT')]: bpy.data.objects.remove(o, do_unlink=True)

S = dict(SCENE)
if want('weapons'):
    # swords: tip to the right, edge up, stacked
    rows = [('longsword', 42), ('eye sword', 25), ('saber', 8), ('sabre', -8), ('talon', -25), ('khopesh', -40)]
    sheet(os.path.join(OUT, 'swords_preview.png'), [(S[n], (0, 0, -90), (-38 if n != 'khopesh' else -22, 0, z), 1.0) for n, z in rows if n in S], 140)
    rows = [('glaive', 34), ('trident', 12), ('axe', -14), ('mace', -36)]
    sheet(os.path.join(OUT, 'polearms_preview.png'), [(S['glaive'], (0, 0, -90), (-8, 0, 34), 0.78), (S['trident'], (0, 0, -90), (-10, 0, 10), 0.82),
                                                      (S['axe'], (0, 0, -90), (-40, 0, -22), 1.0), (S['mace'], (0, 0, -90), (40, 0, -22), 1.0)], 190)
if want('valuables'):
    v = S['valuables']
    sheet(os.path.join(OUT, 'valuables_preview.png'), [(v[0], (70, 0, 10), (-14, 0, 5), 2.2), (v[1], (20, 0, 20), (-4.5, 0, 6), 2.6), (v[2], (0, 0, 20), (6, 0, 2), 0.9),
                                                       (v[3], (85, 0, 0), (-13, 0, -7), 0.95), (v[4], (40, 0, 160), (-1, 0, -8), 1.1), (v[5], (90, 0, 0), (12, 0, -6), 1.0)], 30)
if want('rare'):
    r = S['rare']
    sheet(os.path.join(OUT, 'rare_preview.png'), [(r[0], (0, 0, 25), (-6, 0, 0), 1.4), (r[1], (60, 0, 15), (7, 0, 0), 1.6)], 24)
if want('conch') or want('pipe'):
    it = []
    if 'conch' in S: it.append((S['conch'], (0, 0, -40), (-8, 0, 0), 0.9))
    if 'pipe' in S: it.append((S['pipe'], (10, 0, 70), (12, 0, 2), 1.6))
    sheet(os.path.join(OUT, 'noisemakers_preview.png'), it, 36)
print('ITEMS55 DONE')
