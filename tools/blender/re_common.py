# Rage Engine - shared Blender helpers (from re_items54.py) for item scripts. Set re_common.OUT before writing.
import bpy, bmesh, math, os
from mathutils import Vector, Matrix
OUT = '/tmp/out'

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


# ------------------------------------------------------------------ 0.55 additions
def lathe(name, prof, segs=32):
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

def lathe_y(name, prof, segs=24, cx=0.0, cz=0.0, rot=0.0):
    """revolve [(r, y), ...] around a line parallel to Y through (cx, cz)"""
    o = lathe(name, prof, segs); apply_all(o)
    m = Matrix.Translation((cx, 0, cz)) @ Matrix.Rotation(math.radians(-90), 4, 'X') @ Matrix.Rotation(math.radians(rot), 4, 'Z')
    o.data.transform(m); return o

def loft(name, stations, segs=16, cap=True):
    """a stock/shaft: stations [(y, cz, hw, ht, hb, sq)] - superellipse sections across X/Z, centred at x=0.
       UV: u along the length (grain), v around."""
    bm = bmesh.new(); rings = []
    y0, y1 = stations[0][0], stations[-1][0]
    for (y, cz, hw, ht, hb, sq) in stations:
        ring = []
        for k in range(segs):
            a = 2 * math.pi * k / segs; dx, dz = math.cos(a), math.sin(a)
            px = hw * math.copysign(abs(dx) ** (2 / sq), dx); pz = cz + (ht if dz > 0 else hb) * math.copysign(abs(dz) ** (2 / sq), dz)
            ring.append(bm.verts.new((px, y, pz)))
        rings.append(ring)
    faces = []
    for i in range(len(rings) - 1):
        for k in range(segs):
            l = (k + 1) % segs
            faces.append(bm.faces.new((rings[i][k], rings[i][l], rings[i + 1][l], rings[i + 1][k])))
    if cap:
        for ring, y in ((rings[0], y0), (rings[-1], y1)):
            c = bm.verts.new((0, y, sum(v.co.z for v in ring) / len(ring)))
            for k in range(segs): bm.faces.new((ring[k], ring[(k + 1) % segs], c))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm(name, bm)
    me = o.data; me.uv_layers.new(); uv = me.uv_layers.active.data
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv[li].uv = ((co.y - y0) * 0.035, (math.atan2(co.z, co.x) / (2 * math.pi) + 0.5) * 0.6)
    return o

def sweep(name, pts, section, close_ends=True, up=Vector((0, 0, 1))):
    """sweep a 2D section [(a, b), ...] (a = across, b = along 'up') along a polyline; section may be a function of t"""
    bm = bmesh.new(); rings = []
    n = len(pts)
    for i, p in enumerate(pts):
        p = Vector(p); t = (Vector(pts[min(i + 1, n - 1)]) - Vector(pts[max(i - 1, 0)])).normalized()
        b = (up - t * up.dot(t)); b = b.normalized() if b.length > 1e-4 else Vector((1, 0, 0)); a = t.cross(b)
        sec = section(i / (n - 1)) if callable(section) else section
        rings.append([bm.verts.new(p + a * sa + b * sb) for sa, sb in sec])
    m = len(rings[0])
    for i in range(n - 1):
        for k in range(m):
            l = (k + 1) % m
            bm.faces.new((rings[i][k], rings[i][l], rings[i + 1][l], rings[i + 1][k]))
    if close_ends: bm.faces.new(list(reversed(rings[0]))); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(name, bm)

def rect(w, h, r=0.0):
    """rectangle section (w across, h along up), corners chamfered by r"""
    if r <= 0: return [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
    return [(-w / 2 + r, -h / 2), (w / 2 - r, -h / 2), (w / 2, -h / 2 + r), (w / 2, h / 2 - r), (w / 2 - r, h / 2), (-w / 2 + r, h / 2), (-w / 2, h / 2 - r), (-w / 2, -h / 2 + r)]

def torus(name, R, r, loc=(0, 0, 0), seg=24, rseg=8, axis='Y'):
    pts = [(math.cos(2 * math.pi * k / seg) * R, 0, math.sin(2 * math.pi * k / seg) * R) for k in range(seg + 1)]
    o = tube(name, pts, r, rseg); apply_all(o)
    m = Matrix.Translation(loc) @ ({'Y': Matrix(), 'Z': Matrix.Rotation(math.radians(90), 4, 'X'), 'X': Matrix.Rotation(math.radians(90), 4, 'Z')}[axis])
    o.data.transform(m); return o

def side_plate(name, outline_yz, x, thick):
    """a flat plate on the side of a stock: outline in (y, z), front face at x, extruded toward the centre"""
    bm = bmesh.new(); vs = [bm.verts.new((x, yy, zz)) for yy, zz in outline_yz]
    f = bm.faces.new(vs)
    if (f.normal.x > 0) != (x > 0): f.normal_flip()
    ext = bmesh.ops.extrude_face_region(bm, geom=[f]); bmesh.ops.translate(bm, vec=Vector((-math.copysign(thick, x), 0, 0)), verts=[v for v in ext['geom'] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return obj_from_bm(name, bm)

def render_long(grp, path, length, poses, samples=48):
    """previews for long weapons lying along X (models are built along +Y)"""
    copies = []
    for (rx, ry, rz), loc, sc in poses:
        for o in grp:
            c = o.copy(); c.data = o.data; bpy.context.collection.objects.link(c)
            c.rotation_euler = (rx, ry, rz); c.location = loc; c.scale = (sc, sc, sc); copies.append(c)
    for o in grp: o.hide_render = True
    D = length * 1.45
    bpy.ops.object.camera_add(location=(0, -D, 0), rotation=(math.radians(90), 0, 0))
    cam = bpy.context.object; bpy.context.scene.camera = cam; cam.data.lens = 50
    k = length / 40.0
    for loc, e in (((-14 * k, -18 * k, 16 * k), 9000 * k * k), ((18 * k, -10 * k, 4 * k), 4000 * k * k), ((0, 14 * k, 10 * k), 6000 * k * k)):
        bpy.ops.object.light_add(type='AREA', location=loc); L = bpy.context.object; L.data.energy = e; L.data.size = 10 * k
        L.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    w = bpy.data.worlds.new('w'); bpy.context.scene.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.012, 0.012, 0.014, 1)
    sc = bpy.context.scene; sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = samples
    try: sc.cycles.use_denoising = True
    except Exception: pass
    sc.view_settings.view_transform = 'Standard'
    sc.render.resolution_x, sc.render.resolution_y = 1536, 1024
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
