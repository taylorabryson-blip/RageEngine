# Rage Engine 0.54 items - blender -b -P re_items54.py -- <outdir> <texdir>
#  Noisemaker: a beat-up twin-bell wind-up alarm clock (red enamel case, brass bells / bezel / handle / feet / keys,
#              paper dial, hands, a separate hammer that swings between the bells when it rings).
#  NPC Scanner: a rugged handheld field scanner (safety-orange shell, rubber corner bumpers and grips, sensor window
#              on top, recessed LCD, SCAN and PAGE keys).
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

# ============================================================================================ NOISEMAKER (alarm clock)
reset()
R, DEP = 3.8, 3.2                      # case radius, depth (cm); front face is -y
FY = -DEP / 2
case = cyl('case', R, DEP, (0, 0, 0), 40, 'Y', 0.45)
back = cyl('back', R - 0.45, 0.5, (0, DEP / 2 + 0.2, 0), 40, 'Y', 0.15)
body = join('Body', [case, back]); apply_all(body); box_uv(body, 0.15)

bezel = ring('bezel', R + 0.12, R - 0.42, 0.55, (0, FY - 0.12, 0), 40)
bells, posts = [], []
for sx in (-1, 1):
    tilt = Matrix.Rotation(math.radians(-sx * 28), 4, 'Y')
    centre = Vector((sx * 2.55, 0.2, 3.55))
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=10, radius=1.75)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.05], context='VERTS')        # a dome, open underneath
    bmesh.ops.transform(bm, matrix=tilt, verts=bm.verts); bmesh.ops.translate(bm, vec=centre, verts=bm.verts)
    b = obj_from_bm('bell', bm); m = b.modifiers.new('solid', 'SOLIDIFY'); m.thickness = 0.12; bells.append(b)
    knob = sphere('nut', 0.28, centre + tilt @ Vector((0, 0, 1.78)), 10, 6); bells.append(knob)
    posts.append(tube('post', [(sx * 1.6, 0.2, 2.9), tuple(centre + tilt @ Vector((0, 0, 0.6)))], 0.16, 8))
handle = tube('handle', [(-1.35, 0.4, 3.55)] + [(math.cos(math.radians(a)) * -1.35, 0.4, 3.55 + math.sin(math.radians(a)) * 1.9) for a in range(15, 180, 15)] + [(1.35, 0.4, 3.55)], 0.2, 8)
feet = []
for sx in (-1, 1):
    feet.append(tube('leg', [(sx * 2.2, 0.0, -3.0), (sx * 2.7, 0.0, -4.1)], 0.2, 8))
    feet.append(sphere('foot', 0.42, (sx * 2.75, 0.0, -4.2), 12, 6, (1, 1, 0.7)))
keys = []
for sx in (-1, 1):
    keys.append(tube('stem', [(sx * 1.4, DEP / 2 + 0.3, -0.5), (sx * 1.4, DEP / 2 + 1.0, -0.5)], 0.14, 8))
    keys.append(box('wing', (1.5, 0.18, 0.9), (sx * 1.4, DEP / 2 + 1.05, -0.5), 0.08, 2))
cap = cyl('cap', 0.28, 0.25, (0, FY - 0.36, 0), 16, 'Y', 0.05)
brass = join('Brass', [bezel] + bells + posts + [handle] + feet + keys + [cap]); apply_all(brass); box_uv(brass, 0.25)

face = flat_disc('Face', R - 0.42, FY - 0.04, 40)
planar_uv(face, 0, 2, (-(R - 0.42), -(R - 0.42), R - 0.42, R - 0.42))

def hand(name, length, width, angle, y, tip=True):
    bm = bmesh.new(); h = width / 2
    pts = [(-h, -0.5), (h, -0.5), (h * 0.8, length - (width if tip else 0)), (0, length), (-h * 0.8, length - (width if tip else 0))]
    vs = [bm.verts.new((x, y, z)) for x, z in pts]
    f = bm.faces.new(vs); f.normal_flip()
    ext = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, vec=Vector((0, 0.05, 0)), verts=[v for v in ext['geom'] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, y, 0), matrix=Matrix.Rotation(math.radians(-angle), 3, 'Y'))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(name, bm)
hands = join('Hands', [hand('hour', 1.9, 0.32, 300, FY - 0.14), hand('minute', 2.8, 0.22, 52, FY - 0.2)]); box_uv(hands)
alarm = hand('Alarm', 1.35, 0.14, 205, FY - 0.26); box_uv(alarm)

# hammer: separate so it can swing between the bells; exported around its pivot
PIV = Vector((0, 0.35, 3.25))
hammer = join('Hammer', [tube('rod', [(0, 0.35, 3.25), (0, 0.35, 4.75)], 0.09, 8), sphere('ball', 0.3, (0, 0.35, 4.85), 12, 6)]); box_uv(hammer, 0.25)

grip = Vector((0, 0, 0))
tris = 0
for o, n in ((body, 'RE_Noise_Body'), (brass, 'RE_Noise_Brass'), (face, 'RE_Noise_Face'), (hands, 'RE_Noise_Hands'), (alarm, 'RE_Noise_Alarm')):
    for p in o.data.polygons: p.use_smooth = n not in ('RE_Noise_Face', 'RE_Noise_Hands', 'RE_Noise_Alarm')
    tris += write_obj(o, n)
for p in hammer.data.polygons: p.use_smooth = True
tris += write_obj(hammer, 'RE_Noise_Hammer', origin=PIV)
print('NOISEMAKER tris', tris, 'hammer pivot (unity m)', PIV.x * 0.01, PIV.z * 0.01, PIV.y * 0.01)

body.data.materials.append(matimg('enamel', os.path.join(TEX, 'noise_body.png'), 0.35))
brass.data.materials.append(matimg('brass', os.path.join(TEX, 'noise_brass.png'), 0.32, metal=1.0))
face.data.materials.append(matimg('dial', os.path.join(TEX, 'noise_face.png'), 0.7))
hands.data.materials.append(matcol('black', (0.03, 0.03, 0.03), 0.4, 0.6))
alarm.data.materials.append(matcol('red', (0.6, 0.06, 0.05), 0.4))
hammer.data.materials.append(matimg('brass2', os.path.join(TEX, 'noise_brass.png'), 0.32, metal=1.0))
render([body, brass, face, hands, alarm, hammer], os.path.join(OUT, 'noisemaker_preview.png'), 44,
       (((0, 0, math.radians(-18)), (-7.5, 0, 0.5), 1.0),
        ((math.radians(-8), 0, math.radians(150)), (8.5, 8, 4.5), 0.62),
        ((0, 0, math.radians(-80)), (9.0, 8, -6.0), 0.62)))
print('NOISEMAKER DONE')

# ============================================================================================ NPC SCANNER
reset()
W, H, D = 7.4, 15.5, 3.0
FY = -D / 2
shell = box('shell', (W, D, H), (0, 0, 0), 0.6, 4)
head = box('head', (W + 0.5, D + 0.5, 2.6), (0, 0, H / 2 - 0.9), 0.55, 4)
SCR_W, SCR_H, SCR_Z = 6.0, 4.3, 3.0
bezel_cut = box('cut', (SCR_W, 2.0, SCR_H), (0, FY - 0.6, SCR_Z), 0.15, 2)
shell = join('ShellJ', [shell, head]); apply_all(shell)
boolean(shell, bezel_cut)
pad = box('pad', (5.4, 0.3, 3.2), (0, FY + 0.05, -2.3), 0.3, 3); apply_all(pad)
boolean(shell, pad)                                     # key well
box_uv(shell, 0.12)

bumpers = []
for sx in (-1, 1):
    for sz in (-1, 1):
        z = H / 2 - 0.9 if sz > 0 else -H / 2 + 0.9
        bumpers.append(box('bump', (1.6, D + 0.7, 1.9), (sx * (W / 2 - 0.35), 0, z), 0.5, 3))
    for i in range(6):
        bumpers.append(box('grip', (0.35, D * 0.8, 0.55), (sx * (W / 2 + 0.05), 0, -1.5 - i * 0.85), 0.12, 2))
frame = box('frame', (SCR_W + 0.7, 0.3, SCR_H + 0.7), (0, FY - 0.02, SCR_Z), 0.2, 2)
apply_all(frame); boolean(frame, box('fcut', (SCR_W - 0.1, 1.0, SCR_H - 0.1), (0, FY - 0.1, SCR_Z)))
eyelet = tube('eyelet', [(-0.7, 0, -H / 2 - 0.1), (-0.7, 0, -H / 2 - 0.9), (0.7, 0, -H / 2 - 0.9), (0.7, 0, -H / 2 - 0.1)], 0.22, 8)
rubber = join('Rubber', bumpers + [frame, eyelet]); apply_all(rubber); box_uv(rubber, 0.2)

kscan = box('kscan', (3.6, 0.5, 1.7), (0, FY - 0.1, -1.6), 0.3, 3)
kpage = box('kpage', (2.2, 0.5, 1.1), (0, FY - 0.1, -3.55), 0.25, 3)
apply_all(kscan); apply_all(kpage)
planar_uv(kscan, 0, 2, (-1.8, -1.6 - 0.85, 1.8, -1.6 + 0.85))
for l in kscan.data.uv_layers.active.data: l.uv = (l.uv[0] * 0.5, l.uv[1])
planar_uv(kpage, 0, 2, (-1.1, -3.55 - 0.55, 1.1, -3.55 + 0.55))
for l in kpage.data.uv_layers.active.data: l.uv = (0.5 + l.uv[0] * 0.5, l.uv[1])
keys = join('Keys', [kscan, kpage])

lens = box('Lens', (4.4, 1.6, 0.25), (0, 0, H / 2 + 0.42), 0.12, 2); apply_all(lens); box_uv(lens)
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
for v in bm.verts: v.co = Vector((v.co.x * SCR_W, FY - 0.0, v.co.y * SCR_H + SCR_Z))
for f in bm.faces: f.normal_flip()
screen = obj_from_bm('Screen', bm)
planar_uv(screen, 0, 2, (-SCR_W / 2, SCR_Z - SCR_H / 2, SCR_W / 2, SCR_Z + SCR_H / 2))

grip = Vector((0, 0, -2.5))
tris = 0
for o, n in ((shell, 'RE_Scan_Shell'), (rubber, 'RE_Scan_Rubber'), (keys, 'RE_Scan_Keys'), (lens, 'RE_Scan_Lens'), (screen, 'RE_Scan_Screen')):
    for p in o.data.polygons: p.use_smooth = n in ('RE_Scan_Shell', 'RE_Scan_Rubber')
    tris += write_obj(o, n, origin=grip)
print('SCANNER tris', tris)

shell.data.materials.append(matimg('abs', os.path.join(TEX, 'scanner_shell.png'), 0.55))
rubber.data.materials.append(matimg('rub', os.path.join(TEX, 'scanner_rubber.png'), 0.85))
keys.data.materials.append(matimg('keys', os.path.join(TEX, 'scanner_keys.png'), 0.7))
lens.data.materials.append(matcol('lens', (0.9, 0.08, 0.05), 0.1, 0, 4.0))
screen.data.materials.append(matimg('lcd', os.path.join(TEX, 'scanner_demo.png'), 0.3, 0.9))
render([shell, rubber, keys, lens, screen], os.path.join(OUT, 'scanner_preview.png'), 56,
       (((0, math.radians(4), math.radians(-14)), (-8.0, 0, -0.5), 1.0),
        ((math.radians(-48), math.radians(8), math.radians(32)), (9.5, 8, 5.5), 0.55),
        ((0, 0, math.radians(-76)), (10.0, 8, -7.5), 0.55)))
print('SCANNER DONE')
