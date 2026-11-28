# Rage Engine 0.54 items - blender -b -P re_items54.py -- <outdir> <texdir>
# Medieval, one noisemaker per faction + the Seer's Mirror:
#  Eraden hand bell (bronze bell, turned wooden handle, iron clapper that swings)
#  Outlaw coin pouch (cinched leather purse, drawstring, gold coins spilling out)
#  Wildfolk bone rattle (painted hide drum on a bone handle, beads and teeth on cords)
#  Eye resonance fork (bronze two-pronged ritual fork, eye boss with a glowing gem, wooden grip)
#  Seer's Mirror (arched bronze hand mirror, dark glass that shows what Rage Engine knows about an NPC)
# One OBJ per material (Y up, metres, Unity winding, real UVs), origin where the hand holds it, + Cycles previews.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = argv[0] if argv else '/tmp/out'
TEX = argv[1] if len(argv) > 1 else OUT
os.makedirs(OUT, exist_ok=True)

def reset():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for m in list(bpy.data.meshes): bpy.data.meshes.remove(m)

def obj_from_bm(name, bm):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o); return o

def box(name, size, loc, bevel=0.0, segs=3):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + Vector(loc)
    o = obj_from_bm(name, bm)
    if bevel > 0:
        m = o.modifiers.new('bev', 'BEVEL'); m.width = bevel; m.segments = segs; m.limit_method = 'ANGLE'
    return o

def cyl(name, r, h, loc, verts=24, axis='Z', bevel=0.0, r2=None):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=verts, radius1=r, radius2=r if r2 is None else r2, depth=h)
    rot = {'Z': Matrix(), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'), 'Y': Matrix.Rotation(math.radians(90), 4, 'X')}[axis]
    bmesh.ops.transform(bm, matrix=rot, verts=bm.verts); bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    o = obj_from_bm(name, bm)
    if bevel > 0: m = o.modifiers.new('bev', 'BEVEL'); m.width = bevel; m.segments = 2; m.limit_method = 'ANGLE'
    return o

def sphere(name, r, loc, seg=16, rings=8, scale=(1, 1, 1)):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    for v in bm.verts: v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2])) + Vector(loc)
    return obj_from_bm(name, bm)

def ring(name, r_out, r_in, depth, loc, seg=40, axis='Y'):
    """flat washer (bezel) - outer wall, inner wall, front and back faces; bevelled"""
    bm = bmesh.new(); outer = []; inner = []
    for k in range(2):
        z = -depth / 2 + k * depth
        outer.append([bm.verts.new((math.cos(2 * math.pi * i / seg) * r_out, math.sin(2 * math.pi * i / seg) * r_out, z)) for i in range(seg)])
        inner.append([bm.verts.new((math.cos(2 * math.pi * i / seg) * r_in, math.sin(2 * math.pi * i / seg) * r_in, z)) for i in range(seg)])
    for i in range(seg):
        j = (i + 1) % seg
        bm.faces.new((outer[0][i], outer[0][j], outer[1][j], outer[1][i]))
        bm.faces.new((inner[0][j], inner[0][i], inner[1][i], inner[1][j]))
        bm.faces.new((outer[1][i], outer[1][j], inner[1][j], inner[1][i]))
        bm.faces.new((outer[0][j], outer[0][i], inner[0][i], inner[0][j]))
    rot = Matrix.Rotation(math.radians(90), 4, 'X') if axis == 'Y' else Matrix()
    bmesh.ops.transform(bm, matrix=rot, verts=bm.verts); bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm(name, bm); m = o.modifiers.new('bev', 'BEVEL'); m.width = 0.08; m.segments = 2; m.limit_method = 'ANGLE'
    return o

def tube(name, pts, r, seg=8):
    """a round tube along a polyline"""
    bm = bmesh.new(); rings_ = []
    for i, p in enumerate(pts):
        p = Vector(p); t = (Vector(pts[min(i + 1, len(pts) - 1)]) - Vector(pts[max(i - 1, 0)])).normalized()
        a = t.cross(Vector((0, 1, 0))) if abs(t.y) < 0.9 else t.cross(Vector((1, 0, 0))); a.normalize(); b = t.cross(a)
        rings_.append([bm.verts.new(p + (a * math.cos(2 * math.pi * k / seg) + b * math.sin(2 * math.pi * k / seg)) * r) for k in range(seg)])
    for i in range(len(rings_) - 1):
        for k in range(seg):
            l = (k + 1) % seg
            bm.faces.new((rings_[i][k], rings_[i][l], rings_[i + 1][l], rings_[i + 1][k]))
    bm.faces.new(list(reversed(rings_[0]))); bm.faces.new(rings_[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(name, bm)

def boolean(target, cutter, op='DIFFERENCE'):
    m = target.modifiers.new('bool', 'BOOLEAN'); m.object = cutter; m.operation = op; m.solver = 'EXACT'
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(cutter, do_unlink=True)

def apply_all(o):
    bpy.context.view_layer.objects.active = o
    for m in list(o.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)

def join(name, objs):
    for o in objs: apply_all(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]; bpy.ops.object.join()
    objs[0].name = name; return objs[0]

def box_uv(o, scale=0.12):
    me = o.data
    if not me.uv_layers: me.uv_layers.new()
    uv = me.uv_layers.active.data
    for p in me.polygons:
        n = p.normal; ax = max(range(3), key=lambda i: abs(n[i]))
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            a, b = [(co.y, co.z), (co.x, co.z), (co.x, co.y)][ax]
            uv[li].uv = (a * scale, b * scale)

def planar_uv(o, axis_u, axis_v, rect):
    me = o.data
    if not me.uv_layers: me.uv_layers.new()
    uv = me.uv_layers.active.data
    for li, l in enumerate(me.loops):
        co = me.vertices[l.vertex_index].co
        uv[li].uv = ((co[axis_u] - rect[0]) / (rect[2] - rect[0]), (co[axis_v] - rect[1]) / (rect[3] - rect[1]))

def write_obj(o, name, scale=0.01, origin=Vector((0, 0, 0))):
    """per-loop UVs, Blender Z-up -> Unity Y-up (axis swap = handedness flip, winding kept)"""
    me = o.data; me.calc_loop_triangles()
    uvl = me.uv_layers.active.data if me.uv_layers else None
    L = ['# ' + name + ' - Rage Engine 0.54 (Blender)']
    for v in me.vertices: p = (o.matrix_world @ v.co - origin) * scale; L.append('v %.5f %.5f %.5f' % (p.x, p.z, p.y))
    vt = []
    for t in me.loop_triangles:
        for li in t.loops: vt.append(uvl[li].uv if uvl else (0, 0))
    for u in vt: L.append('vt %.4f %.4f' % (u[0], u[1]))
    k = 0
    for t in me.loop_triangles:
        a, b, c = (i + 1 for i in t.vertices)
        L.append('f %d/%d %d/%d %d/%d' % (a, k + 1, b, k + 2, c, k + 3)); k += 3
    open(os.path.join(OUT, name + '.obj'), 'w').write('\n'.join(L) + '\n')
    print('wrote', name, len(me.vertices), 'verts', len(me.loop_triangles), 'tris')
    return len(me.loop_triangles)

def flat_disc(name, r, y, seg=40, loc=(0, 0)):
    bm = bmesh.new(); c = bm.verts.new((loc[0], y, loc[1]))
    vs = [bm.verts.new((loc[0] + math.cos(2 * math.pi * i / seg) * r, y, loc[1] + math.sin(2 * math.pi * i / seg) * r)) for i in range(seg)]
    for i in range(seg): bm.faces.new((c, vs[(i + 1) % seg], vs[i]))
    return obj_from_bm(name, bm)

def matimg(name, img, rough=0.6, emit=0.0, metal=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes['Principled BSDF']
    b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metal
    if img and os.path.exists(img):
        tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = bpy.data.images.load(img)
        nt.links.new(tx.outputs['Color'], b.inputs['Base Color'])
        if emit > 0:
            for key in ('Emission Color', 'Emission'):
                if key in b.inputs: nt.links.new(tx.outputs['Color'], b.inputs[key]); break
            b.inputs['Emission Strength'].default_value = emit
    return m

def matcol(name, col, rough, metal=0.0, emit=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metal
    if emit > 0:
        for key in ('Emission Color', 'Emission'):
            if key in b.inputs: b.inputs[key].default_value = (*col, 1); break
        b.inputs['Emission Strength'].default_value = emit
    return m

def render(grp, path, cam_dist=40, poses=None):
    copies = []
    for (rx, ry, rz), loc, sc in poses:
        for o in grp:
            c = o.copy(); c.data = o.data; bpy.context.collection.objects.link(c)
            c.rotation_euler = (rx, ry, rz); c.location = loc; c.scale = (sc, sc, sc); copies.append(c)
    for o in grp: o.hide_render = True
    bpy.ops.object.camera_add(location=(0.5, -cam_dist, 0.0), rotation=(math.radians(90), 0, 0))
    cam = bpy.context.object; bpy.context.scene.camera = cam; cam.data.lens = 50
    for loc, e in (((-14, -18, 16), 9000), ((18, -10, 4), 4000), ((0, 14, 10), 6000)):
        bpy.ops.object.light_add(type='AREA', location=loc); L = bpy.context.object; L.data.energy = e; L.data.size = 10
        L.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    w = bpy.data.worlds.new('w'); bpy.context.scene.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.012, 0.012, 0.014, 1)
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 64
    try: sc.cycles.use_denoising = True
    except Exception: pass
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = 1536, 1024
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)

# ============================================================================================ shared shapes
def lathe(name, prof, segs=32, z_axis=True):
    """revolve a profile [(r, z), ...] around Z; r == 0 at an end closes it"""
    bm = bmesh.new(); rings = []
    for r, z in prof:
        if r <= 1e-4: rings.append([bm.verts.new((0, 0, z))])
        else: rings.append([bm.verts.new((math.cos(2 * math.pi * k / segs) * r, math.sin(2 * math.pi * k / segs) * r, z)) for k in range(segs)])
    for a, b in zip(rings, rings[1:]):
        if len(a) == 1 and len(b) == 1: continue
        if len(a) == 1:
            for k in range(segs): bm.faces.new((a[0], b[k], b[(k + 1) % segs]))
        elif len(b) == 1:
            for k in range(segs): bm.faces.new((a[k], b[0], a[(k + 1) % segs]))
        else:
            for k in range(segs): bm.faces.new((a[k], b[k], b[(k + 1) % segs], a[(k + 1) % segs]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(name, bm)

def outline_face(name, pts, y, thick=0.0):
    """a flat face from an outline in the XZ plane at depth y (front = -y); optional extrude to +y"""
    bm = bmesh.new(); vs = [bm.verts.new((x, y, z)) for x, z in pts]
    f = bm.faces.new(vs)
    if f.normal.y > 0: f.normal_flip()
    if thick > 0:
        ext = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, vec=Vector((0, thick, 0)), verts=[v for v in ext['geom'] if isinstance(v, bmesh.types.BMVert)])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    return obj_from_bm(name, bm)

def look(o, n):
    for p in o.data.polygons: p.use_smooth = n

def poses(a, b, c): return (a, b, c)
STD = (((0, 0, math.radians(-18)), (-7.5, 0, 0.5), 1.0), ((math.radians(-10), 0, math.radians(150)), (8.5, 8, 4.5), 0.62), ((0, 0, math.radians(-80)), (9.0, 8, -6.0), 0.62))
def T(f): return os.path.join(TEX, f)

# ============================================================================================ ERADEN HAND BELL (handle up, bell down)
reset()
bell = lathe('bell', [(3.3, -11.0), (3.35, -10.75), (3.05, -10.2), (2.55, -8.6), (2.25, -7.2), (2.05, -6.2), (1.6, -5.4), (1.0, -5.05), (0.0, -4.95),
                      (0.0, -5.25), (0.85, -5.3), (1.45, -5.65), (1.85, -6.3), (2.05, -7.3), (2.35, -8.7), (2.8, -10.2), (3.05, -10.9), (3.3, -11.0)], 36)
collar = lathe('collar', [(0.0, -5.2), (1.15, -5.2), (1.2, -4.6), (0.9, -4.3), (0.95, -3.9), (0.0, -3.9)], 24)
cap = lathe('cap', [(0.0, 5.1), (0.85, 5.1), (1.05, 5.6), (0.9, 6.2), (0.0, 6.4)], 24)
bodyB = join('BellBody', [bell, collar, cap]); box_uv(bodyB, 0.2)
handle = lathe('handle', [(0.0, -3.95), (0.75, -3.95), (0.95, -3.2), (0.8, -2.0), (0.95, -0.5), (1.05, 1.5), (1.0, 3.2), (0.8, 4.3), (0.9, 5.1), (0.0, 5.15)], 24)
box_uv(handle, 0.2)
PIVB = Vector((0, 0, -5.45))
clapper = join('Clapper', [tube('rod', [(0, 0, -5.45), (0, 0, -9.6)], 0.14, 8), sphere('ball', 0.55, (0, 0, -9.9), 14, 8)]); box_uv(clapper, 0.25)
tris = 0
for o, n in ((bodyB, 'RE_Bell_Body'), (handle, 'RE_Bell_Handle')): look(o, True); tris += write_obj(o, n)
look(clapper, True); tris += write_obj(clapper, 'RE_Bell_Clapper', origin=PIVB)
print('BELL tris', tris, 'clapper pivot (unity m)', PIVB.x * 0.01, PIVB.z * 0.01, PIVB.y * 0.01)
bodyB.data.materials.append(matimg('bronze', T('bronze.png'), 0.3, metal=1.0)); handle.data.materials.append(matimg('wood', T('wood.png'), 0.55))
clapper.data.materials.append(matimg('iron', T('iron.png'), 0.5, metal=0.8))
render([bodyB, handle, clapper], os.path.join(OUT, 'bell_preview.png'), 52, (((0, 0, math.radians(-18)), (-7.5, 0, 1.5), 1.0), ((math.radians(-30), 0, math.radians(150)), (8.5, 8, 5.5), 0.6), ((math.radians(90), 0, 0), (9.0, 8, -7.0), 0.6)))
print('BELL DONE')

# ============================================================================================ OUTLAW COIN POUCH
reset()
bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=16, radius=1.0)
for v in bm.verts:
    x, y, z = v.co; rho = math.hypot(x, y); a = math.atan2(y, x)
    zz = z if z >= -0.2 else -0.2 + (z + 0.2) * 0.55                             # sits flat-ish
    if rho < 1e-5: v.co = Vector((0, 0, 3.0 if z > 0 else zz * 3.4)); continue  # top pole sinks in: a cinched mouth
    lump = 0.25 * math.sin(a * 3 + z * 4) * math.cos(z * 3.1) + 0.15 * math.sin(a * 5 - 1.3)
    if z <= 0.45: R = (3.3 + lump) * rho * (1.0 + 0.04 * math.sin(a * 11) * max(0.0, z + 0.2))   # creases running up to the neck
    else:
        t = (z - 0.45) / 0.55; R0 = 3.3 * math.sqrt(1 - 0.45 ** 2)
        R = R0 * (1 - t / 0.8) + 1.05 * (t / 0.8) if t < 0.8 else 1.05 + (t - 0.8) * 3.2 + 0.16 * math.sin(a * 9)
        R *= 1.0 + 0.07 * math.sin(a * 11) * min(1.0, t * 1.6)                     # the gathered leather folds into pleats
    v.co = Vector((math.cos(a) * R, math.sin(a) * R, zz * 3.4))
sack = obj_from_bm('Sack', bm); box_uv(sack, 0.18)
rope = join('Rope', [tube('tie', [(math.cos(2 * math.pi * k / 20) * 1.15, math.sin(2 * math.pi * k / 20) * 1.15, 2.95) for k in range(21)], 0.2, 8),
                     tube('end1', [(1.1, -0.3, 2.9), (1.8, -0.6, 2.0), (2.1, -0.7, 0.8)], 0.17, 8), sphere('k1', 0.32, (2.1, -0.7, 0.7), 10, 6),
                     tube('end2', [(1.0, 0.4, 2.9), (1.5, 0.9, 1.9), (1.6, 1.2, 0.9)], 0.17, 8), sphere('k2', 0.32, (1.6, 1.2, 0.8), 10, 6)])
box_uv(rope, 0.4)
coins = []
for (x, y, z), (rx, ry) in (((0.3, -0.3, 3.35), (25, 10)), ((-0.5, 0.4, 3.25), (-35, 20)), ((2.95, -1.7, -1.3), (88, 0))):
    c = cyl('coin', 1.0, 0.17, (0, 0, 0), 20, 'Z', 0.03); apply_all(c)
    planar_uv(c, 0, 1, (-1, -1, 1, 1))
    c.rotation_euler = (math.radians(rx), math.radians(ry), 0); c.location = (x, y, z)
    bpy.context.view_layer.objects.active = c; c.select_set(True); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    coins.append(c)
coinsO = join('Coins', coins)
tris = 0
for o, n in ((sack, 'RE_Pouch_Leather'), (rope, 'RE_Pouch_Rope'), (coinsO, 'RE_Pouch_Coins')): look(o, n != 'RE_Pouch_Coins'); tris += write_obj(o, n)
print('POUCH tris', tris)
sack.data.materials.append(matimg('leather', T('leather.png'), 0.7)); rope.data.materials.append(matimg('rope', T('rope.png'), 0.8))
coinsO.data.materials.append(matimg('coin', T('coin.png'), 0.3, metal=1.0))
render([sack, rope, coinsO], os.path.join(OUT, 'pouch_preview.png'), 44, STD)
print('POUCH DONE')

# ============================================================================================ WILDFOLK BONE RATTLE (head up, bone handle down)
reset()
HZ = 4.2
head = sphere('head', 1.0, (0, 0, 0), 28, 16, (3.8, 2.0, 4.4)); 
for v in head.data.vertices: v.co.z += HZ
planar_uv(head, 0, 2, (-3.8, HZ - 4.4, 3.8, HZ + 4.4))
lace = tube('lace', [(math.sin(2 * math.pi * k / 28) * 3.85, 0, HZ + math.cos(2 * math.pi * k / 28) * 4.45) for k in range(29)], 0.22, 8)
box_uv(lace, 0.4)
bone = lathe('bone', [(0.0, -11.6), (1.0, -11.4), (1.25, -10.8), (0.95, -10.1), (0.62, -9.2), (0.55, -6.0), (0.6, -2.4), (0.85, -1.2), (1.05, -0.4), (0.7, 0.2), (0.0, 0.3)], 18)
for v in bone.data.vertices:                                                     # knuckled ends, a little irregular
    v.co.x *= 1.0 + 0.12 * math.sin(v.co.z * 1.7); v.co.y *= 1.0 + 0.1 * math.cos(v.co.z * 2.3)
teeth, beads = [], []
for i, (x, y) in enumerate(((-1.6, -0.4), (0.2, -0.6), (1.7, -0.3))):
    top = Vector((x, y, HZ - 4.25 + abs(x) * 0.25)); cord = []
    for k in range(5): cord.append((x + 0.15 * math.sin(k), y, top.z - k * 1.1))
    lace = join('lace', [lace, tube('cord', cord, 0.07, 6)])
    for k in range(3):
        beads.append(sphere('bead', 0.34, (x + 0.15 * math.sin(k + 1), y, top.z - 1.0 - k * 1.0), 10, 6))
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.32, radius2=0.0, depth=1.3)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(180 + (i - 1) * 12), 3, 'X'))
    bmesh.ops.translate(bm, vec=Vector((x, y, top.z - 4.6)), verts=bm.verts); teeth.append(obj_from_bm('tooth', bm))
boneO = join('Bone', [bone] + teeth); box_uv(boneO, 0.2)
beadO = join('Beads', beads); box_uv(beadO)
box_uv(lace, 0.4)
GR = Vector((0, 0, -5.5))
tris = 0
for o, n in ((head, 'RE_Rattle_Hide'), (boneO, 'RE_Rattle_Bone'), (lace, 'RE_Rattle_Lace'), (beadO, 'RE_Rattle_Beads')): look(o, True); tris += write_obj(o, n, origin=GR)
print('RATTLE tris', tris)
head.data.materials.append(matimg('hide', T('hide.png'), 0.75)); boneO.data.materials.append(matimg('bone', T('bone.png'), 0.6))
lace.data.materials.append(matimg('lace', T('leather.png'), 0.8)); beadO.data.materials.append(matcol('bead', (0.62, 0.12, 0.08), 0.35))
render([head, boneO, lace, beadO], os.path.join(OUT, 'rattle_preview.png'), 58, (((0, 0, math.radians(-12)), (-7.5, 0, 2.5), 1.0), ((math.radians(-10), 0, math.radians(130)), (9.5, 8, 6.5), 0.55), ((0, 0, math.radians(-80)), (10.0, 8, -7.5), 0.55)))
print('RATTLE DONE')

# ============================================================================================ EYE RESONANCE FORK (prongs up, grip down)
reset()
parts = []
for sx in (-1, 1):
    pr = box('prong', (0.62, 0.62, 9.0), (sx * 1.25, 0, 6.6), 0.12, 2); apply_all(pr)
    for v in pr.data.vertices:                                                   # taper to the tips, bowed slightly outward
        t = (v.co.z - 2.1) / 9.0; v.co.x = sx * 1.25 + (v.co.x - sx * 1.25) * (1 - 0.35 * t) + sx * 0.15 * math.sin(t * math.pi)
        v.co.y *= (1 - 0.35 * t)
    parts.append(pr)
yoke = tube('yoke', [(-1.25, 0, 2.4)] + [(-1.25 * math.cos(math.radians(a)), 0, 2.4 - 1.25 * math.sin(math.radians(a))) for a in range(15, 180, 15)] + [(1.25, 0, 2.4)], 0.36, 10)
parts.append(yoke)
# the eye: a vesica-shaped boss on the yoke
eye = []
for k in range(25):
    a = 2 * math.pi * k / 24
    eye.append((math.cos(a) * 1.7, -0.45, 1.35 + math.sin(a) * 0.8 * abs(math.cos(a)) ** 0.0 * (0.55 + 0.45 * math.cos(a) ** 2)))
parts.append(tube('eyerim', eye, 0.16, 8))
parts.append(cyl('stem', 0.5, 2.2, (0, 0, 0.1), 16, 'Z', 0.08))
parts.append(lathe('pommel', [(0.0, -9.2), (0.9, -9.1), (1.15, -8.5), (0.85, -7.9), (0.95, -7.5), (0.0, -7.4)], 20))
parts.append(lathe('guard', [(0.0, -1.2), (1.15, -1.2), (1.3, -0.8), (0.95, -0.3), (0.0, -0.3)], 20))
bronzeF = join('Bronze', parts); box_uv(bronzeF, 0.25)
grip = lathe('grip', [(0.0, -7.6), (0.82, -7.6), (0.95, -6.0), (1.0, -4.3), (0.92, -2.6), (0.82, -1.1), (0.0, -1.1)], 20); box_uv(grip, 0.25)
gem = sphere('Gem', 0.62, (0, -0.55, 1.35), 16, 8, (1.0, 0.45, 0.62)); box_uv(gem)
GF = Vector((0, 0, -4.3))
tris = 0
for o, n in ((bronzeF, 'RE_Fork_Bronze'), (grip, 'RE_Fork_Grip'), (gem, 'RE_Fork_Gem')): look(o, True); tris += write_obj(o, n, origin=GF)
print('FORK tris', tris)
bronzeF.data.materials.append(matimg('bronzef', T('bronze.png'), 0.3, metal=1.0)); grip.data.materials.append(matimg('wrap', T('leather_wrap.png'), 0.75))
gem.data.materials.append(matcol('gem', (0.35, 0.15, 0.85), 0.1, 0, 3.0))
render([bronzeF, grip, gem], os.path.join(OUT, 'fork_preview.png'), 60, (((0, 0, math.radians(-15)), (-7.5, 0, 0.5), 1.0), ((math.radians(-10), 0, math.radians(140)), (9.5, 8, 6.0), 0.55), ((0, 0, math.radians(-80)), (10.0, 8, -7.5), 0.55)))
print('FORK DONE')

# ============================================================================================ SEER'S MIRROR (arched glass up, handle down)
reset()
MW, MS = 8.0, 6.0                       # glass width, height of the straight sides; arch radius = MW/2 on top
def arch(w, s, inset=0.0, steps=24):
    hw = w / 2 - inset; pts = [(-hw, inset), (hw, inset), (hw, s)]
    for k in range(1, steps):
        a = math.pi * k / steps; pts.append((hw * math.cos(a), s + hw * math.sin(a)))
    pts.append((-hw, s)); return pts
glass = outline_face('Glass', arch(MW, MS), -0.62)
MH = MS + MW / 2
planar_uv(glass, 0, 2, (-MW / 2, 0, MW / 2, MH))
ring = [(x, -0.4, z) for x, z in arch(MW + 0.5, MS, 0.0, 28)]; ring.append(ring[0])
frame = [tube('frame', ring, 0.42, 10)]
inner = [(x, -0.55, z) for x, z in arch(MW, MS, 0.12, 28)]; inner.append(inner[0])
frame.append(tube('bead', inner, 0.14, 6))
frame.append(outline_face('back', arch(MW + 0.6, MS, -0.05), -0.45, 0.55))
frame.append(sphere('fin', 0.55, (0, 0, MH + 0.75), 12, 8)); frame.append(cyl('finc', 0.25, 0.8, (0, 0, MH + 1.4), 10, 'Z', 0.05))
for sx in (-1, 1): frame.append(sphere('orb', 0.42, (sx * (MW / 2 + 0.25), -0.2, MS), 10, 6))
frame.append(lathe('collar', [(0.0, -1.6), (0.95, -1.6), (1.2, -1.0), (0.9, -0.4), (1.3, 0.1), (0.0, 0.25)], 20))
frame.append(lathe('pommel', [(0.0, -11.2), (0.9, -11.0), (1.15, -10.4), (0.85, -9.8), (0.0, -9.7)], 20))
bronzeM = join('Frame', frame); box_uv(bronzeM, 0.25)
wrap = lathe('wrap', [(0.0, -9.9), (0.8, -9.9), (0.9, -8.0), (0.92, -5.8), (0.88, -3.6), (0.82, -1.5), (0.0, -1.5)], 20); box_uv(wrap, 0.25)
GM = Vector((0, 0, -5.8))
tris = 0
for o, n in ((bronzeM, 'RE_Mirror_Frame'), (wrap, 'RE_Mirror_Grip'), (glass, 'RE_Mirror_Glass')): look(o, n != 'RE_Mirror_Glass'); tris += write_obj(o, n, origin=GM)
print('MIRROR tris', tris, 'glass rect (unity m, relative to grip)', -MW / 200, (0 - GM.z) / 100, MW / 200, (MH - GM.z) / 100)
bronzeM.data.materials.append(matimg('bronzem', T('bronze.png'), 0.3, metal=1.0)); wrap.data.materials.append(matimg('wrapm', T('leather_wrap.png'), 0.75))
glass.data.materials.append(matimg('glass', T('seer_demo.png'), 0.2, 1.2))
render([bronzeM, wrap, glass], os.path.join(OUT, 'mirror_preview.png'), 62, (((0, 0, math.radians(-14)), (-7.5, 0, 0.0), 1.0), ((math.radians(-10), 0, math.radians(150)), (9.5, 8, 6.0), 0.55), ((0, 0, math.radians(-78)), (10.0, 8, -7.5), 0.55)))
print('MIRROR DONE')
