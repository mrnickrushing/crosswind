# blender -b --factory-startup -P Tools/blender/icon_render.py -- jobs.json
# Renders each job (name, files, out, optional yaw / pitch) as an icon: the model (or several, side by side) on a clear
# background under studio light with a soft shadow on the floor, seen from the front right and a little above.
import bpy, json, math, os, sys
from mathutils import Vector, Matrix

jobs = json.load(open(sys.argv[sys.argv.index("--") + 1]))
SIZE = 512


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 48
    sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 4
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = SIZE
    sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
    world = bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
    bg = world.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.82, 0.88, 1.0, 1); bg.inputs[1].default_value = 0.55
    return sc


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def load(path):
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.context.scene.objects if o not in before]
    # One parent for the lot, so it can be scaled and stood where it belongs.
    holder = bpy.data.objects.new("holder", None); bpy.context.scene.collection.objects.link(holder)
    for o in new:
        if o.parent is None:
            o.parent = holder
    bpy.context.view_layer.update()
    meshes = [o for o in new if o.type == "MESH"]
    for o in new:
        if o.type in ("CAMERA", "LIGHT"):
            o.hide_render = True
    return holder, meshes


for job in jobs:
    sc = reset()
    yaw, pitch = math.radians(job.get("yaw", 0)), math.radians(job.get("pitch", 22))
    all_meshes, x = [], 0.0
    for path in job["files"]:
        holder, meshes = load(path)
        if not meshes:
            print("[icon] EMPTY", job["name"], path); continue
        lo, hi = bounds(meshes)
        size = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
        # A group stands side by side, each one the same height; a single model is its largest side long.
        s = (1.0 / max(1e-6, hi.z - lo.z)) if len(job["files"]) > 1 else 1.0 / max(1e-6, size)
        holder.rotation_euler = (0, 0, yaw)
        holder.scale = (s, s, s)
        bpy.context.view_layer.update()
        lo, hi = bounds(meshes)
        holder.location = Vector((x - (lo.x + hi.x) / 2, -(lo.y + hi.y) / 2, -lo.z))
        x += (hi.x - lo.x) * 0.5 + 0.34
        all_meshes += meshes
    if not all_meshes:
        continue
    bpy.context.view_layer.update()
    lo, hi = bounds(all_meshes)
    centre = (lo + hi) / 2
    # The floor only catches the shadow.
    bpy.ops.mesh.primitive_plane_add(size=40, location=(centre.x, centre.y, lo.z - 0.0005))
    floor = bpy.context.object; floor.is_shadow_catcher = True
    # Camera: from the front (glTF's front is Blender's -Y), 35 degrees round to the right, a little above.
    az = math.radians(-35)
    direction = Vector((math.sin(az) * math.cos(pitch) * -1, -math.cos(az) * math.cos(pitch), math.sin(pitch)))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.clip_end = 200
    cam.location = centre + direction * 30
    cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
    bpy.context.view_layer.update()
    inv = cam.matrix_world.inverted()
    pts = [inv @ (o.matrix_world @ Vector(c)) for o in all_meshes for c in o.bound_box]
    if job.get("top"):
        # Only the top of it (a head and shoulders, the cab of a tower): fit the picture to that part of its box.
        z0 = hi.z - (hi.z - lo.z) * job["top"]
        pts = [inv @ Vector((x_, y_, z_)) for x_ in (lo.x, hi.x) for y_ in (lo.y, hi.y) for z_ in (z0, hi.z)]
    w = max(p.x for p in pts) - min(p.x for p in pts); h = max(p.y for p in pts) - min(p.y for p in pts)
    mid = Vector(((max(p.x for p in pts) + min(p.x for p in pts)) / 2, (max(p.y for p in pts) + min(p.y for p in pts)) / 2, 0))
    cam.location = cam.location + cam.matrix_world.to_3x3() @ mid
    cam.data.ortho_scale = max(w, h) * 1.3
    # Lights: a warm key from the upper left of the picture, a cool fill from the right, a rim from behind.
    def sun(name, rot, strength, colour, angle):
        l = bpy.data.lights.new(name, "SUN"); l.energy = strength; l.color = colour; l.angle = math.radians(angle)
        o = bpy.data.objects.new(name, l); sc.collection.objects.link(o); o.rotation_euler = [math.radians(v) for v in rot]
    sun("key", (30, 0, az * 180 / math.pi * -1 - 55), 2.3, (1.0, 0.97, 0.92), 14)
    sun("fill", (62, 0, az * 180 / math.pi * -1 + 70), 0.7, (0.8, 0.88, 1.0), 40)
    sun("rim", (115, 0, az * 180 / math.pi * -1 + 170), 1.2, (1, 1, 1), 20)
    sc.render.filepath = job["out"]
    bpy.ops.render.render(write_still=True)
    print("[icon]", job["name"], "faces", sum(len(o.data.polygons) for o in all_meshes))
