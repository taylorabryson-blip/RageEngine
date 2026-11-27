# Rage Engine NPC Spawner remote - blender -b -P re_spawner.py -- <outdir>
# Rugged handheld remote (reference: worn radio/remote with LCD menu). Beveled housing, recessed LCD, SPAWN key,
# up/down keys, side grip ridges, side button, volume knob, antenna, lanyard loop.
# Exports one OBJ per material (Y up, metres, Unity winding, real UVs) + a Cycles preview from 3 angles.
import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
OUT = argv[0] if argv else '/tmp/out'
TEX = argv[1] if len(argv) > 1 else OUT
os.makedirs(OUT, exist_ok=True)
for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)

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

def cyl(name, r, h, loc, verts=24, axis='Z', bevel=0.0):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=verts, radius1=r, radius2=r, depth=h)
    rot = {'Z': Matrix(), 'X': Matrix.Rotation(math.radians(90), 4, 'Y'), 'Y': Matrix.Rotation(math.radians(90), 4, 'X')}[axis]
    bmesh.ops.transform(bm, matrix=rot, verts=bm.verts); bmesh.ops.translate(bm, vec=Vector(loc), verts=bm.verts)
    o = obj_from_bm(name, bm)
    if bevel > 0: m = o.modifiers.new('bev', 'BEVEL'); m.width = bevel; m.segments = 2; m.limit_method = 'ANGLE'
    return o

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
    """box-project UVs (world cm -> texture tiles) for the worn plastic texture"""
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
    """UVs 0..1 across rect=(u0,v0,u1,v1) in the given world axes (for screen / label faces)"""
    me = o.data
    if not me.uv_layers: me.uv_layers.new()
    uv = me.uv_layers.active.data
    for li, l in enumerate(me.loops):
        co = me.vertices[l.vertex_index].co
        u = (co[axis_u] - rect[0]) / (rect[2] - rect[0]); v = (co[axis_v] - rect[1]) / (rect[3] - rect[1])
        uv[li].uv = (u, v)

def write_obj(o, name, scale=0.01):
    """per-loop UVs, Blender Z-up -> Unity Y-up (axis swap = handedness flip, winding kept), origin = grip centre"""
    me = o.data; me.calc_loop_triangles()
    uvl = me.uv_layers.active.data if me.uv_layers else None
    L = ['# ' + name + ' - Rage Engine NPC Spawner (Blender)']
    for v in me.vertices: p = (o.matrix_world @ v.co) * scale; L.append('v %.5f %.5f %.5f' % (p.x, p.z, p.y))
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

# --------------------------------------------------------------------------------------------- dimensions (cm)
W, H, D = 6.2, 12.6, 2.7          # width (x), height (z), depth (y);  front face is -y
FY = -D / 2                        # front surface y
# housing: main shell + thicker lower bumper band + top shoulders
shell = box('shell', (W, D, H), (0, 0, 0), 0.55, 4)
band = box('band', (W + 0.35, D + 0.3, 3.0), (0, 0, -H / 2 + 1.7), 0.5, 4)
shoulder = box('shoulder', (W + 0.2, D + 0.2, 1.2), (0, 0, H / 2 - 1.0), 0.45, 4)
# screen bezel: raised frame, then a recess cut into it
SCR_W, SCR_H, SCR_Z = 4.5, 4.3, 2.6
bezel = box('bezel', (SCR_W + 0.9, 0.35, SCR_H + 0.9), (0, FY - 0.1, SCR_Z), 0.3, 3)
apply_all(bezel)
boolean(bezel, box('cut', (SCR_W, 1.0, SCR_H), (0, FY - 0.2, SCR_Z), 0.15, 2))
# grip ridges down both sides of the lower half
ridges = []
for side in (-1, 1):
    for i in range(7):
        ridges.append(box('r', (0.35, D * 0.75, 0.32), (side * (W / 2 + 0.08), 0, -1.2 - i * 0.62), 0.08, 2))
# side button (right) + volume knob (top left) + antenna collar
sidebtn = box('sidebtn', (0.35, 1.0, 2.0), (W / 2 + 0.15, 0, 1.2), 0.12, 2)
body = join('Body', [shell, band, shoulder, bezel, sidebtn] + ridges)
apply_all(body); box_uv(body)

# keys: SPAWN (wide), up/down rocker plate + two keys, arrows, knob, antenna, lanyard -> "Dark" rubber/plastic
spawn_key = box('spawn', (4.0, 0.5, 1.45), (0, FY - 0.2, -0.95), 0.28, 3)
plate = box('plate', (2.5, 0.25, 2.8), (0, FY - 0.08, -3.55), 0.3, 3)
k_up = box('kup', (1.9, 0.45, 1.1), (0, FY - 0.25, -2.95), 0.22, 3)
k_dn = box('kdn', (1.9, 0.45, 1.1), (0, FY - 0.25, -4.15), 0.22, 3)
knob = cyl('knob', 0.62, 0.7, (-W / 2 + 1.1, 0, H / 2 + 0.25), 28, 'Z', 0.08)
for v in knob.data.vertices:                                       # knurled knob
    a = math.atan2(v.co.y, v.co.x + W / 2 - 1.1)
    if abs(v.co.z - (H / 2 + 0.25)) < 0.3: pass
collar = cyl('collar', 0.55, 0.6, (W / 2 - 1.1, 0, H / 2 + 0.2), 24, 'Z', 0.1)
ant = cyl('ant', 0.45, 3.0, (W / 2 - 1.1, 0, H / 2 + 2.0), 20, 'Z')
for v in ant.data.vertices:                                        # taper
    t = (v.co.z - (H / 2 + 0.5)) / 3.0
    s = 1.0 - 0.3 * max(0.0, min(1.0, t))
    v.co.x = (v.co.x - (W / 2 - 1.1)) * s + (W / 2 - 1.1); v.co.y *= s
tip = cyl('tip', 0.36, 0.45, (W / 2 - 1.1, 0, H / 2 + 3.6), 20, 'Z', 0.12)
# lanyard: a loop tube from the bottom-right corner
bpy.ops.curve.primitive_bezier_circle_add(radius=1.0)
cv = bpy.context.object; cv.data.bevel_depth = 0.14; cv.data.bevel_resolution = 3; cv.data.resolution_u = 16
cv.scale = (1.1, 2.0, 1.0); cv.rotation_euler = (math.radians(90), math.radians(-12), 0); cv.location = (W / 2 - 0.75, 0, -H / 2 - 2.05)
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
bpy.ops.object.convert(target='MESH'); lan = bpy.context.object
eyelet = box('eyelet', (0.9, 1.0, 0.6), (W / 2 - 0.6, 0, -H / 2 - 0.25), 0.15, 2)
dark = join('Dark', [spawn_key, plate, k_up, k_dn, knob, collar, ant, tip, lan, eyelet])
apply_all(dark); box_uv(dark, 0.2)

# arrows (light grey, raised triangles on the keys)
def tri(name, up, z):
    bm = bmesh.new(); s = 0.38 if up else -0.38
    vs = [bm.verts.new((-0.38, FY - 0.49, z - s * 0.5)), bm.verts.new((0.38, FY - 0.49, z - s * 0.5)), bm.verts.new((0, FY - 0.49, z + s * 0.5))]
    f = bm.faces.new(vs if up else list(reversed(vs)))
    ext = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, vec=Vector((0, 0.06, 0)), verts=[v for v in ext['geom'] if isinstance(v, bmesh.types.BMVert)])
    return obj_from_bm(name, bm)
arrows = join('Arrows', [tri('a1', True, -2.95), tri('a2', False, -4.15)])
box_uv(arrows)

# screen: a plane inside the recess, UV 0..1 -> LCD texture
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
for v in bm.verts: v.co = Vector((v.co.x * SCR_W * 0.97, FY - 0.08, v.co.y * SCR_H * 0.97 + SCR_Z))
for f in bm.faces: f.normal_flip()
screen = obj_from_bm('Screen', bm)
planar_uv(screen, 0, 2, (-SCR_W * 0.485, SCR_Z - SCR_H * 0.485, SCR_W * 0.485, SCR_Z + SCR_H * 0.485))
# SPAWN label: plane on the key face, UV 0..1 -> label texture
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5)
for v in bm.verts: v.co = Vector((v.co.x * 3.2, FY - 0.46, v.co.y * 0.95 - 0.95))
for f in bm.faces: f.normal_flip()
label = obj_from_bm('Label', bm)
planar_uv(label, 0, 2, (-1.6, -1.425, 1.6, -0.475))

# move origin so the grip (lower-middle of the housing) is at 0 -> where the hand holds it
grip = Vector((0, 0, -2.0))
for o in (body, dark, arrows, screen, label): o.location -= grip
for o in (body, dark, arrows, screen, label): bpy.context.view_layer.objects.active = o; o.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o, n in ((body, 'RE_Spawner2_Body'), (dark, 'RE_Spawner2_Dark'), (arrows, 'RE_Spawner2_Arrows'), (screen, 'RE_Spawner2_Screen'), (label, 'RE_Spawner2_Label')):
    for p in o.data.polygons: p.use_smooth = n not in ('RE_Spawner2_Screen', 'RE_Spawner2_Label')
    write_obj(o, n)

# --------------------------------------------------------------------------------------------- preview materials + render
def matimg(name, img, rough=0.6, emit=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes['Principled BSDF']
    b.inputs['Roughness'].default_value = rough
    if img and os.path.exists(img):
        tx = nt.nodes.new('ShaderNodeTexImage'); tx.image = bpy.data.images.load(img)
        nt.links.new(tx.outputs['Color'], b.inputs['Base Color'])
        if emit > 0:
            for key in ('Emission Color', 'Emission'):
                if key in b.inputs: nt.links.new(tx.outputs['Color'], b.inputs[key]); break
            b.inputs['Emission Strength'].default_value = emit
    return m
def matcol(name, col, rough):
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*col, 1); b.inputs['Roughness'].default_value = rough; return m
body.data.materials.append(matimg('body', os.path.join(TEX, 'spawner_body.png'), 0.55))
dark.data.materials.append(matimg('dark', os.path.join(TEX, 'spawner_dark.png'), 0.7))
arrows.data.materials.append(matcol('arr', (0.75, 0.75, 0.75), 0.5))
screen.data.materials.append(matimg('lcd', os.path.join(TEX, 'lcd_0.png'), 0.35, 0.35))
label.data.materials.append(matimg('lab', os.path.join(TEX, 'spawn_label.png'), 0.6))
for o in (body, dark, arrows, screen, label): o.data.update()
grp = [body, dark, arrows, screen, label]
def place(rot, loc, scale):
    pass
# three copies like a product sheet: front 3/4, angled top, side
copies = []
for (rx, ry, rz), loc, sc in (((math.radians(0), math.radians(6), math.radians(-16)), (-7.0, 0, -0.5), 1.0),
                              ((math.radians(-50), math.radians(10), math.radians(30)), (8.0, 8, 5.0), 0.55),
                              ((math.radians(0), math.radians(0), math.radians(-78)), (9.0, 8, -6.5), 0.55)):
    for o in grp:
        c = o.copy(); c.data = o.data; bpy.context.collection.objects.link(c)
        c.rotation_euler = (rx, ry, rz); c.location = loc; c.scale = (sc, sc, sc); copies.append(c)
for o in grp: o.hide_render = True
bpy.ops.object.camera_add(location=(0.5, -52, 0.0), rotation=(math.radians(90), 0, 0))
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
sc.render.filepath = os.path.join(OUT, 'spawner_preview.png')
bpy.ops.render.render(write_still=True)
print('SPAWNER DONE')
