# Rage Engine asset pack - runs headless: blender -b -P re_assets.py -- <outdir>
# Real modelled gore pieces + the NPC Spawner device, exported as Unity-ready OBJ (Y up, metres, Unity winding),
# plus a Cycles preview render of everything.
import bpy, bmesh, math, os, random, sys
from mathutils import Vector, noise, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.path.expanduser('~'), 'Documents', 'RageEngine', 'assets')
os.makedirs(OUT, exist_ok=True)

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)
for m in list(bpy.data.metaballs): bpy.data.metaballs.remove(m)

def link(o): bpy.context.collection.objects.link(o); return o

def mat(name, col, rough=0.4, emit=None):
    m = bpy.data.materials.new(name)
    try: m.use_nodes = True
    except Exception: pass
    b = m.node_tree.nodes.get('Principled BSDF') if m.node_tree else None
    if b:
        b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = rough
        if emit is not None:
            for key in ('Emission Color', 'Emission'):
                if key in b.inputs: b.inputs[key].default_value = (*emit, 1)
            if 'Emission Strength' in b.inputs: b.inputs['Emission Strength'].default_value = 6.0
    return m

def from_meta(name, elems, res):
    mb = bpy.data.metaballs.new(name); mb.resolution = res; mb.render_resolution = res; mb.threshold = 0.6
    ob = link(bpy.data.objects.new(name + '_mb', mb))
    for p, r, size, neg in elems:
        e = mb.elements.new(); e.co = p; e.radius = r; e.use_negative = neg
        if size: e.type = 'ELLIPSOID'; e.size_x, e.size_y, e.size_z = size
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob, do_unlink=True)
    keep_main(me)
    return link(bpy.data.objects.new(name, me))

def B(p, r, size=None, neg=False): return (Vector(p), r, size, neg)

def tube(pts, radii, size=None):
    out = []
    for i in range(len(pts) - 1):
        a, b = Vector(pts[i]), Vector(pts[i + 1]); ra, rb = radii[i], radii[i + 1]
        n = max(2, int((b - a).length / (min(ra, rb) * 0.35)) + 1)
        for k in range(n): t = k / n; out.append(B(a.lerp(b, t), ra + (rb - ra) * t, size))
    out.append(B(pts[-1], radii[-1], size)); return out

def keep_main(me):
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seen = set(); comps = []
    for v in bm.verts:
        if v.index in seen: continue
        st = [v]; comp = []; seen.add(v.index)
        while st:
            x = st.pop(); comp.append(x)
            for e in x.link_edges:
                o = e.other_vert(x)
                if o.index not in seen: seen.add(o.index); st.append(o)
        comps.append(comp)
    comps.sort(key=len, reverse=True)
    for c in comps[1:]: bmesh.ops.delete(bm, geom=c, context='VERTS')
    bm.to_mesh(me); bm.free()

def displace(o, amp, freq, seed, ridged=False):
    random.seed(seed); off = Vector((random.random() * 40, random.random() * 40, random.random() * 40))
    me = o.data
    for v in me.vertices:
        p = v.co * freq + off
        n = (1.0 - abs(noise.noise(p))) ** 2 - 0.45 if ridged else noise.fractal(p, 0.6, 2.0, 4)
        v.co += v.normal * n * amp

def finish(o, tris=1200, smooth=0.3):
    me = o.data
    if len(me.polygons) > tris:
        d = o.modifiers.new('dec', 'DECIMATE'); d.ratio = tris / len(me.polygons)
    if smooth > 0: s = o.modifiers.new('sm', 'SMOOTH'); s.factor = smooth; s.iterations = 2
    bpy.context.view_layer.update()
    me2 = bpy.data.meshes.new_from_object(o.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    o.modifiers.clear(); o.data = me2
    for p in me2.polygons: p.use_smooth = True
    return o

def write_obj(o, name, scale=1.0, center=True):
    me = o.data; me.calc_loop_triangles()
    vs = [o.matrix_world @ v.co for v in me.vertices]
    c = sum(vs, Vector()) / len(vs) if center else Vector()
    zs = [v.z for v in vs]; z0, z1 = min(zs), max(zs)
    L = ['# ' + name + ' - Rage Engine (Blender)']
    for v in vs: p = (v - c) * scale; L.append('v %.5f %.5f %.5f' % (p.x, p.z, p.y))
    for v in vs:
        d = v - c; L.append('vt %.4f %.4f' % (math.atan2(d.y, d.x) / (2 * math.pi) + 0.5, (v.z - z0) / max(1e-6, z1 - z0)))
    for t in me.loop_triangles:
        a, b, cc = (i + 1 for i in t.vertices); L.append('f %d/%d %d/%d %d/%d' % (a, a, b, b, cc, cc))
    open(os.path.join(OUT, name + '.obj'), 'w').write('\n'.join(L) + '\n')
    print('wrote', name, len(vs), 'verts', len(me.loop_triangles), 'tris')

made = []   # (object, material) for the preview
M_TOOTH = mat('tooth', (0.86, 0.82, 0.7), 0.35); M_BONE = mat('bone', (0.82, 0.74, 0.6), 0.55)
M_BRAIN = mat('brain', (0.78, 0.5, 0.5), 0.3); M_EYE = mat('eye', (0.92, 0.9, 0.86), 0.15)
M_DEV = mat('dev', (0.06, 0.06, 0.065), 0.45); M_DEVM = mat('devm', (0.35, 0.35, 0.37), 0.25); M_GLOW = mat('glow', (1.0, 0.3, 0.06), 0.2, (1.0, 0.32, 0.05))

def keep(o, name, m, scale=0.01):
    o.data.materials.append(m); write_obj(o, name, scale); made.append(o)

# ---------------------------------------------------------------- teeth (modelled in mm-ish units, exported x0.01 -> ~2 cm)
random.seed(7)
# incisor: flat spade crown, one long root
o = from_meta('RE_Tooth_1', [B((0, 0, 0.35), 0.42, (1.0, 0.45, 0.9))] + tube([(0, 0, 0.2), (0, 0, -0.5), (0, 0.02, -1.15)], [0.3, 0.24, 0.08]), 0.04)
displace(o, 0.015, 3.0, 1); keep(finish(o, 700), 'RE_Tooth_1', M_TOOTH)
# canine: pointed crown, longest root
o = from_meta('RE_Tooth_2', tube([(0, 0, 0.75), (0, 0, 0.35), (0, 0, -0.3), (0, 0.03, -1.3)], [0.1, 0.36, 0.3, 0.07]), 0.04)
displace(o, 0.015, 3.0, 2); keep(finish(o, 700), 'RE_Tooth_2', M_TOOTH)
# premolar: two cusps, one root
o = from_meta('RE_Tooth_3', [B((-0.14, 0, 0.42), 0.3), B((0.14, 0, 0.42), 0.3), B((0, 0, 0.25), 0.4, (1.0, 0.85, 0.7))] + tube([(0, 0, 0.1), (0, 0, -0.95)], [0.3, 0.1]), 0.04)
displace(o, 0.015, 3.5, 3); keep(finish(o, 700), 'RE_Tooth_3', M_TOOTH)
# molar: four cusps, two splayed roots
cusps = [B((sx * 0.2, sy * 0.18, 0.5), 0.27) for sx in (-1, 1) for sy in (-1, 1)]
o = from_meta('RE_Tooth_4', cusps + [B((0, 0, 0.3), 0.48, (1.0, 0.9, 0.6))] + tube([(-0.2, 0, 0.1), (-0.3, 0, -0.85)], [0.22, 0.07]) + tube([(0.2, 0, 0.1), (0.32, 0, -0.85)], [0.22, 0.07]), 0.04)
displace(o, 0.012, 4.0, 4); keep(finish(o, 900), 'RE_Tooth_4', M_TOOTH)

# ---------------------------------------------------------------- skull fragments: a curved shell broken along clean fracture planes
for i in range(1, 7):
    random.seed(100 + i)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=96, ring_count=64, radius=1.0)
    sph = bpy.context.object; me = sph.data
    center = Vector((random.uniform(-1, 1), random.uniform(-1, 1), random.uniform(0.3, 1))).normalized()
    bm = bmesh.new(); bm.from_mesh(me)
    # keep a cap around 'center', then crack it with 4-6 random planes through the cap
    t1 = center.orthogonal().normalized(); t2 = center.cross(t1)
    k = random.randint(4, 6); size = random.uniform(0.3, 0.45)
    for j in range(k):
        a = j * 2 * math.pi / k + random.uniform(-0.35, 0.35)
        d = (t1 * math.cos(a) + t2 * math.sin(a)).normalized()
        n = (d + center * random.uniform(-0.25, 0.25)).normalized()
        co = center * math.cos(size) + d * math.sin(size) * random.uniform(0.75, 1.15)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=n, clear_outer=True)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=center * 0.55, plane_no=-center, clear_outer=True)
    bm.to_mesh(me); bm.free(); keep_main(me)
    sol = sph.modifiers.new('sol', 'SOLIDIFY'); sol.thickness = 0.065; sol.offset = -1
    bpy.context.view_layer.update()
    me2 = bpy.data.meshes.new_from_object(sph.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(sph, do_unlink=True)
    o = link(bpy.data.objects.new('RE_Skull_Frag_%d' % i, me2))
    displace(o, 0.008, 7.0, 200 + i)
    o.data.materials.append(M_BONE); finish(o, 900, 0.0); write_obj(o, 'RE_Skull_Frag_%d' % i, 0.09); made.append(o)

# ---------------------------------------------------------------- brain chunks: folded gyri
for i in range(1, 4):
    random.seed(300 + i)
    el = [B((random.uniform(-0.3, 0.3), random.uniform(-0.25, 0.25), random.uniform(-0.2, 0.2)), random.uniform(0.35, 0.5)) for _ in range(5)]
    o = from_meta('RE_Brain_%d' % i, el, 0.018)
    displace(o, 0.13, 7.5, 310 + i, ridged=True)
    displace(o, 0.04, 15.0, 315 + i, ridged=True)
    displace(o, 0.012, 22.0, 320 + i)
    o.data.materials.append(M_BRAIN); finish(o, 1600, 0.2); write_obj(o, 'RE_Brain_%d' % i, 0.06); made.append(o)

# ---------------------------------------------------------------- vertebra: body, arch, spinous + transverse processes, facets
o = from_meta('RE_Bone_Vertebra', tube([(0, 0.3, -0.2), (0, 0.3, 0.2)], [0.4, 0.4])
              + tube([(-0.25, -0.05, 0), (-0.18, -0.28, 0), (0, -0.36, 0), (0.18, -0.28, 0), (0.25, -0.05, 0)], [0.1, 0.1, 0.1, 0.1, 0.1])
              + tube([(0, -0.36, 0), (0, -0.75, -0.12)], [0.11, 0.07])
              + tube([(-0.22, -0.12, 0), (-0.62, -0.18, 0.04)], [0.1, 0.07]) + tube([(0.22, -0.12, 0), (0.62, -0.18, 0.04)], [0.1, 0.07])
              + [B((-0.16, -0.22, 0.16), 0.08), B((0.16, -0.22, 0.16), 0.08)], 0.03)
displace(o, 0.012, 5.0, 400); keep(finish(o, 1100), 'RE_Bone_Vertebra', M_BONE, 0.06)

# ---------------------------------------------------------------- jaw fragment: a broken half-mandible with sockets
o = from_meta('RE_Jaw_Frag', tube([(0.9, 0, 0), (0.35, 0.05, 0), (-0.1, 0.25, 0), (-0.35, 0.55, 0.15), (-0.4, 0.6, 0.55)], [0.16, 0.18, 0.2, 0.17, 0.13], (1.0, 0.7, 1.4))
              + [B((0.6, 0, 0.18), 0.07, None, True), B((0.4, 0.02, 0.18), 0.07, None, True), B((0.2, 0.06, 0.18), 0.07, None, True)], 0.035)
displace(o, 0.015, 5.0, 500); keep(finish(o, 1100), 'RE_Jaw_Frag', M_BONE, 0.06)

# ---------------------------------------------------------------- eyeball with optic nerve stub
o = from_meta('RE_Eyeball', [B((0, 0, 0), 0.62)] + tube([(0, -0.5, 0), (0.05, -0.9, -0.05), (0.15, -1.3, -0.12)], [0.12, 0.1, 0.08]) + [B((0, 0.55, 0), 0.2, (1, 0.3, 1))], 0.03)
keep(finish(o, 1000, 0.1), 'RE_Eyeball', M_EYE, 0.02)

# ---------------------------------------------------------------- NPC Spawner: a knurled beacon you hold, glowing core
def lathe(name, profile, segs=40):
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        ring = [bm.verts.new((r * math.cos(a * 2 * math.pi / segs), r * math.sin(a * 2 * math.pi / segs), z)) for a in range(segs)]
        rings.append(ring)
    for k in range(len(rings) - 1):
        for a in range(segs):
            b2 = (a + 1) % segs
            bm.faces.new((rings[k][a], rings[k][b2], rings[k + 1][b2], rings[k + 1][a]))
    bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return link(bpy.data.objects.new(name, me))

body = lathe('RE_Spawner_Body', [(0.0, 0.0), (1.55, 0.0), (1.7, 0.15), (1.7, 0.4), (1.5, 0.55), (1.5, 3.4), (1.7, 3.55), (1.75, 3.9), (1.4, 4.05), (1.2, 4.1)], 48)
# knurling: ridges on the grip
for v in body.data.vertices:
    if 0.6 < v.co.z < 3.35:
        a = math.atan2(v.co.y, v.co.x); k = 1.0 + 0.04 * (1 if math.sin(a * 24) > 0 else -1)
        v.co.x *= k; v.co.y *= k
body.data.materials.append(M_DEV); finish(body, 4000, 0); write_obj(body, 'RE_Spawner_Body', 0.025, False); made.append(body)
cage = lathe('RE_Spawner_Cage', [(1.25, 4.1), (1.3, 4.3), (1.3, 5.6), (1.0, 5.9), (0.6, 6.05), (0.0, 6.1)], 32)
for v in cage.data.vertices:                                 # slots in the cage: pinch every other sector inward
    if 4.3 < v.co.z < 5.6:
        a = math.atan2(v.co.y, v.co.x)
        if math.sin(a * 6) > 0.3: v.co.x *= 0.82; v.co.y *= 0.82
cage.data.materials.append(M_DEVM); finish(cage, 3000, 0); write_obj(cage, 'RE_Spawner_Cage', 0.025, False); made.append(cage)
core = from_meta('RE_Spawner_Core', [B((0, 0, 4.95), 0.95, (1, 1, 1.25))], 0.08)
core.data.materials.append(M_GLOW); finish(core, 600, 0.1); write_obj(core, 'RE_Spawner_Core', 0.025, False); made.append(core)

# ---------------------------------------------------------------- preview render (Cycles CPU)
cols = 6
for i, o in enumerate(made):
    if o.name.startswith('RE_Spawner'): continue
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]; ctr = sum(bb, Vector()) / 8
    ext = max((max(v[j] for v in bb) - min(v[j] for v in bb)) for j in range(3))
    s = 1.3 / max(ext, 1e-4)
    o.scale = (s, s, s); bpy.context.view_layer.update()
    bb = [o.matrix_world @ Vector(c) for c in o.bound_box]; ctr = sum(bb, Vector()) / 8
    o.location += Vector(((i % cols) * 1.8 - (cols - 1) * 0.9, 0, -(i // cols) * 1.8 + 1.6)) - ctr
for o in made:
    if o.name.startswith('RE_Spawner'): o.location = (4.6, 0, -2.75); o.scale = (0.22, 0.22, 0.22)
bpy.ops.object.camera_add(location=(0.6, -14, -0.2), rotation=(math.radians(90), 0, 0))
cam = bpy.context.object; bpy.context.scene.camera = cam; cam.data.lens = 42
for loc, e in (((-6, -8, 6), 1400), ((7, -6, 2), 700), ((0, 6, 4), 900)):
    bpy.ops.object.light_add(type='AREA', location=loc); L = bpy.context.object; L.data.energy = e; L.data.size = 5
    L.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
w = bpy.data.worlds.new('w'); bpy.context.scene.world = w
try:
    w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.015, 0.015, 0.02, 1)
except Exception: pass
sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 48
try: sc.cycles.use_denoising = True
except Exception: pass
try: sc.view_settings.view_transform = 'Standard'
except Exception: pass
sc.render.resolution_x, sc.render.resolution_y = 1600, 900
sc.render.filepath = os.path.join(OUT, 'assets_preview.png')
bpy.ops.render.render(write_still=True)
print('RAGE ASSETS DONE ->', OUT)
