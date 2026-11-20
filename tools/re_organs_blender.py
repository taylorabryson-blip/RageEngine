# Rage Engine organs - run in Blender: exec(open(r"<this file>").read())
# Builds 8 organs from metaballs, adds surface detail, writes Unity-ready OBJ files (Y up, metres) next to this
# script in an "organs" folder, and renders a preview picture of them all.
import bpy, math, os, random
from mathutils import Vector, noise

HERE = os.path.join(os.path.expanduser('~'), 'Documents', 'RageEngine')
OUT = os.path.join(HERE, 'organs'); os.makedirs(OUT, exist_ok=True)
S = 0.1   # model in decimetres, export in metres

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
for m in list(bpy.data.metaballs): bpy.data.metaballs.remove(m)

def ball(p, r, size=None, neg=False):
    return (Vector(p), r, size, neg)

def tube(pts, radii, size=None):
    """dense balls along a polyline so they fuse into one smooth tube"""
    out = []
    for i in range(len(pts) - 1):
        a, b = Vector(pts[i]), Vector(pts[i + 1]); ra, rb = radii[i], radii[i + 1]
        n = max(2, int((b - a).length / (min(ra, rb) * 0.35)) + 1)
        for k in range(n):
            t = k / n
            out.append(ball(a.lerp(b, t), ra + (rb - ra) * t, size))
    out.append(ball(pts[-1], radii[-1], size))
    return out

def keep_main(me):
    import bmesh
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    seen = set(); comps = []
    for v in bm.verts:
        if v.index in seen: continue
        stack = [v]; comp = []; seen.add(v.index)
        while stack:
            x = stack.pop(); comp.append(x)
            for e in x.link_edges:
                o = e.other_vert(x)
                if o.index not in seen: seen.add(o.index); stack.append(o)
        comps.append(comp)
    comps.sort(key=len, reverse=True)
    for comp in comps[1:]: bmesh.ops.delete(bm, geom=comp, context='VERTS')
    bm.to_mesh(me); bm.free()
    return len(comps)

def organ(name, balls, res=0.035, bump=0.03, freq=3.0, col=(0.5, 0.05, 0.05)):
    mb = bpy.data.metaballs.new(name); mb.resolution = res; mb.render_resolution = res; mb.threshold = 0.6
    ob = bpy.data.objects.new(name + '_mb', mb); bpy.context.collection.objects.link(ob)
    for p, r, size, neg in balls:
        e = mb.elements.new(); e.co = p; e.radius = r; e.use_negative = neg
        if size: e.type = 'ELLIPSOID'; e.size_x, e.size_y, e.size_z = size
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(ob, do_unlink=True)
    islands = keep_main(me)
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    if islands > 1: print(name, 'dropped', islands - 1, 'loose bits')
    random.seed(hash(name) & 0xffff)
    off = Vector((random.random() * 50, random.random() * 50, random.random() * 50))
    for v in me.vertices:
        n = noise.fractal(v.co * freq + off, 0.6, 2.0, 4)
        v.co += v.normal * n * bump
    # decimate to a Quest-friendly size
    o.modifiers.new('dec', 'DECIMATE').ratio = min(1.0, 1400.0 / max(1, len(me.polygons)))
    sm = o.modifiers.new('sm', 'SMOOTH'); sm.factor = 0.4; sm.iterations = 2
    bpy.context.view_layer.update()
    me2 = bpy.data.meshes.new_from_object(o.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    o.modifiers.clear(); o.data = me2
    for p in me2.polygons: p.use_smooth = True
    mat = bpy.data.materials.new(name + '_mat')
    try: mat.use_nodes = True
    except Exception: pass
    b = mat.node_tree.nodes.get('Principled BSDF')
    if b: b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = 0.25
    me2.materials.append(mat)
    write_obj(o, name)
    return o

def write_obj(o, name):
    me = o.data
    me.calc_loop_triangles()
    vs = [v.co.copy() for v in me.vertices]
    c = sum(vs, Vector()) / len(vs)
    ys = [v.z for v in vs]; y0, y1 = min(ys), max(ys)
    lines = ['# ' + name + ' - Rage Engine (Blender)']
    for v in vs:
        p = (v - c) * S
        lines.append('v %.5f %.5f %.5f' % (p.x, p.z, p.y))           # Blender Z-up -> Unity Y-up (swap = handedness flip)
    for v in vs:
        d = v - c
        lines.append('vt %.4f %.4f' % (math.atan2(d.y, d.x) / (2 * math.pi) + 0.5, (v.z - y0) / max(1e-6, y1 - y0)))
    for t in me.loop_triangles:
        a, b2, cc = (i + 1 for i in t.vertices)
        lines.append('f %d/%d %d/%d %d/%d' % (a, a, b2, b2, cc, cc))
    open(os.path.join(OUT, name + '.obj'), 'w').write('\n'.join(lines) + '\n')
    print(name, len(vs), 'verts', len(me.loop_triangles), 'tris')

made = []
made.append(organ('RE_Organ_Heart',
    [ball((0, 0, 0), 0.58, (0.95, 0.85, 1.05)), ball((-0.3, 0.1, 0.42), 0.34), ball((0.25, -0.12, 0.45), 0.31), ball((0.3, 0.2, 0.15), 0.3)]
    + tube([(0, 0, 0), (0.1, 0.02, -0.35), (0.2, 0, -0.62)], [0.48, 0.34, 0.16])
    + tube([(0, 0, 0.35), (0.02, 0, 0.8), (0.18, 0, 1.02), (0.42, 0, 0.95)], [0.2, 0.18, 0.17, 0.15])
    + tube([(-0.12, 0.12, 0.45), (-0.2, 0.2, 0.92)], [0.17, 0.14]),
    bump=0.02, col=(0.32, 0.02, 0.03)))
made.append(organ('RE_Organ_Lung', [
    ball((0, 0, 0), 0.66, (1.0, 0.58, 0.6)), ball((0, 0.02, 0.5), 0.58, (0.85, 0.52, 0.85)),
    ball((0, 0.04, 0.95), 0.44), ball((0, 0.05, 1.3), 0.28), ball((0, 0.05, 1.5), 0.17),
    ball((0.6, 0, 0.35), 0.32, None, True),
], bump=0.04, freq=4.5, col=(0.55, 0.16, 0.2)))
made.append(organ('RE_Organ_Liver', [
    ball((0, 0, 0), 0.82, (1.3, 0.75, 0.55)), ball((-0.62, 0, 0.05), 0.72, (0.9, 0.8, 0.75)),
    ball((0.95, 0, 0.1), 0.48, (1.1, 0.55, 0.38)), ball((1.35, 0, 0.12), 0.3, (1.0, 0.5, 0.32)),
    ball((0.2, 0.32, -0.38), 0.36, None, True),
], bump=0.018, col=(0.22, 0.03, 0.025)))
made.append(organ('RE_Organ_Kidney',
    tube([(0, 0, -0.4), (-0.08, 0, 0), (0, 0, 0.4)], [0.3, 0.32, 0.3], (0.9, 0.7, 1.0))
    + [ball((0.32, 0, 0), 0.2, None, True)]
    + tube([(0.2, 0, 0), (0.42, 0, -0.18), (0.48, 0, -0.5)], [0.11, 0.09, 0.08]),
    bump=0.01, col=(0.3, 0.05, 0.045)))
made.append(organ('RE_Organ_Stomach',
    tube([(-0.68, 0, 1.0), (-0.5, 0, 0.65), (-0.2, 0, 0.5), (0.15, 0, 0.25), (0.3, 0, -0.1), (0.18, 0, -0.42), (-0.08, 0, -0.55), (-0.38, 0, -0.5)],
         [0.13, 0.3, 0.45, 0.48, 0.4, 0.3, 0.22, 0.16]),
    bump=0.028, freq=5.0, col=(0.6, 0.24, 0.22)))
made.append(organ('RE_Organ_Spleen', tube([(0, 0, -0.3), (0.1, 0, 0), (0, 0, 0.3)], [0.27, 0.31, 0.25], (1, 0.45, 1)),
    bump=0.015, col=(0.25, 0.03, 0.08)))
made.append(organ('RE_Organ_Bladder', [ball((0, 0, 0), 0.44)] + tube([(0, 0, -0.2), (0, 0, -0.45), (0, 0, -0.7)], [0.32, 0.18, 0.09]),
    bump=0.02, col=(0.62, 0.38, 0.3)))
made.append(organ('RE_Organ_Intestine',
    tube([(0.34 * math.cos(t * 0.35), 0.34 * math.sin(t * 0.35), t * 0.028 - 0.5) for t in range(36)], [0.13] * 36),
    bump=0.02, freq=6.0, col=(0.62, 0.27, 0.27)))

# ---- preview render
for i, o in enumerate(made):
    o.location = ((i % 4) * 1.75 - 2.6, 0, -(i // 4) * 2.1 + 1.0)
bpy.ops.object.camera_add(location=(0, -7.5, 0.3), rotation=(math.radians(90), 0, 0))
cam = bpy.context.object; bpy.context.scene.camera = cam; cam.data.lens = 38
for loc, e in (((-3, -4, 3), 260), ((3, -3, 1), 140), ((0, 3, 2), 160)):
    bpy.ops.object.light_add(type='AREA', location=loc); L = bpy.context.object; L.data.energy = e; L.data.size = 3
    L.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
w = bpy.context.scene.world or bpy.data.worlds.new('w'); bpy.context.scene.world = w; w.use_nodes = True
bg = w.node_tree.nodes.get('Background')
if bg: bg.inputs[0].default_value = (0.02, 0.02, 0.025, 1)
sc = bpy.context.scene
sc.render.resolution_x, sc.render.resolution_y = 1600, 900
try: sc.view_settings.view_transform = 'Standard'
except Exception: pass
try: sc.render.engine = 'BLENDER_EEVEE_NEXT'
except Exception: sc.render.engine = 'BLENDER_EEVEE'
sc.render.filepath = os.path.join(OUT, 'organs_preview.png')
bpy.ops.render.render(write_still=True)
print('RAGE ORGANS DONE ->', OUT)
