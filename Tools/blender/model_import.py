"""Imports a free (CC0 / CC BY) GLB model for Crosswind: aircraft, vehicles, buildings, people.

  blender -b -P Tools/blender/model_import.py -- <in.glb> <out.fbx> [--height H | --size S] [--turn DEG] [--nose] [--rigged]

--nose: an aircraft or a vehicle: its long side is turned to run fore and aft, with the end that stands lowest (the
nose: the tail fin is the highest point) forward.

Colours are baked into the mesh as vertex colours (material base colours, or the kit's palette texture sampled at
each corner's UV), so the game draws them with its own Crosswind/VertexLit shader like everything else. The model is
scaled to a height (characters) or to its longest side, stood on the ground, centred, and turned so it faces +Z in
Unity. Rigged characters keep their armature and every animation, renamed to plain names (Idle, Walk, Run, Sword...).
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, out = a[0], a[1]


def arg(name, default=None, cast=float):
    return cast(a[a.index(name) + 1]) if name in a else default


height, size, turn = arg("--height"), arg("--size"), arg("--turn", 0.0)
rigged = "--rigged" in a

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
# Quaternius characters carry a helper sphere and a prop in their hands: drop them.
for o in list(bpy.context.scene.objects):
    if o.type == "MESH" and (o.name.lower().startswith("icosphere") or o.name.lower().startswith("weapon")):
        bpy.data.objects.remove(o)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
arm = next((o for o in bpy.context.scene.objects if o.type == "ARMATURE"), None)


# --pose action:frame  freezes a rigged character in one frame of one of its clips (a walking stride, sitting), so
# crowds can be drawn as plain meshes and animated by swapping a few of them.
pose = arg("--pose", None, str)
if pose and arm is not None:
    name, frame = pose.split(":")
    act = bpy.data.actions.get(name)
    if act is not None:
        arm.animation_data_create()
        arm.animation_data.action = act
        bpy.context.scene.frame_set(int(frame))
        bpy.context.view_layer.update()
        for o in meshes:
            bpy.context.view_layer.objects.active = o
            for mod in list(o.modifiers):
                if mod.type == "ARMATURE":
                    bpy.ops.object.modifier_apply(modifier=mod.name)


def to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


# Byte colour attributes take linear values (Blender stores them as sRGB bytes itself), so flat material colours,
# which glTF gives in linear, go in as they are, and texture pixels (sRGB) are linearised first.


def base(mat):
    """(image, colour) of a material's base colour: the texture if one is wired in, else the flat colour."""
    if mat is None or not mat.use_nodes:
        return None, (0.8, 0.8, 0.8, 1)
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        return None, (0.8, 0.8, 0.8, 1)
    inp = bsdf.inputs["Base Color"]
    if inp.is_linked:
        node = inp.links[0].from_node
        while node is not None and node.type != "TEX_IMAGE" and node.inputs and any(i.is_linked for i in node.inputs):
            node = next(i.links[0].from_node for i in node.inputs if i.is_linked)
        if node is not None and node.type == "TEX_IMAGE" and node.image is not None:
            return node.image, None
    c = inp.default_value
    return None, (c[0], c[1], c[2], 1)


cache = {}


def sample(img, uv):
    if img.name not in cache:
        w, h = img.size
        cache[img.name] = (w, h, list(img.pixels[:]))
    w, h, px = cache[img.name]
    if not (uv[0] == uv[0] and uv[1] == uv[1]):  # NaN UVs on a few Kenney faces
        uv = (0.0, 0.0)
    x = min(w - 1, max(0, int((uv[0] % 1.0) * w)))
    y = min(h - 1, max(0, int((uv[1] % 1.0) * h)))
    i = (y * w + x) * 4
    return (to_linear(px[i]), to_linear(px[i + 1]), to_linear(px[i + 2]), 1)


def tame(c):
    """Kenney's palette has pastel whites that bloom in the tropical sun: keep every channel under 0.62 (linear)."""
    m = max(c[0], c[1], c[2])
    k = 0.9 / m if m > 0.9 else 1.0
    return (c[0] * k, c[1] * k, c[2] * k, 1)


for o in meshes:
    me = o.data
    # Read the UVs before adding the colour layer: adding a layer reallocates the mesh's layers and leaves an
    # earlier UV handle pointing at nothing.
    uvs = [tuple(d.uv) for d in me.uv_layers.active.data] if me.uv_layers.active else None
    picks = []
    for poly in me.polygons:
        mat = o.material_slots[poly.material_index].material if o.material_slots else None
        img, flat = base(mat)
        for li in poly.loop_indices:
            picks.append((li, tame(sample(img, uvs[li]) if (img is not None and uvs is not None) else flat)))
    col = me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    for li, c in picks:
        col.data[li].color = c
    me.color_attributes.active_color = col

# Size and place: feet (or base) at 0, centred, facing the right way.
bpy.context.view_layer.update()
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
dims = mx - mn
k = 1.0
if height:
    k = height / dims.z
elif size:
    k = size / max(dims.x, dims.y, dims.z)
if "--nose" in a:
    # An aircraft mirrors itself across its fuselage, so its points average out to the middle along the span but not
    # along its length: the lopsided axis is the fuselage. The highest point (the fin) marks the tail.
    allv = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    mean = sum(allv, Vector()) / len(allv)
    off_x = abs(mean.x - (mn.x + mx.x) / 2) / max(dims.x, 1e-6)
    off_y = abs(mean.y - (mn.y + mx.y) / 2) / max(dims.y, 1e-6)
    long_x = off_x > off_y if abs(off_x - off_y) > 0.004 else dims.x >= dims.y
    top = max(allv, key=lambda p: p.z)
    mid = (mn.x + mx.x) / 2 if long_x else (mn.y + mx.y) / 2
    tail_positive = (top.x if long_x else top.y) > mid
    nose = Vector((-1 if tail_positive else 1, 0, 0)) if long_x else Vector((0, -1 if tail_positive else 1, 0))
    turn += math.degrees(math.atan2(-1, 0) - math.atan2(nose.y, nose.x))
    if size:
        k = size / (dims.x if long_x else dims.y)      # sized by its length, not its wingspan
roots = [o for o in bpy.context.scene.objects if o.parent is None]
pivot = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
m = Matrix.Rotation(math.radians(180 + turn), 4, "Z") @ Matrix.Scale(k, 4) @ Matrix.Translation(-pivot)
for r in roots:
    r.matrix_world = m @ r.matrix_world

if rigged and arm is not None:
    for act in bpy.data.actions:
        parts = [p for p in act.name.split("|") if p and not p.endswith("Armature")]
        act.name = parts[0] if parts else act.name
    # Keep the armature's transform (applying scale to it would leave the animations' bone moves at the old size).
    for o in list(bpy.context.scene.objects):
        if o.type not in ("MESH", "ARMATURE"):
            bpy.data.objects.remove(o)
else:
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o.type != "MESH":
            bpy.data.objects.remove(o)

for o in bpy.context.scene.objects:
    if o.type == "MESH":
        o.data.materials.clear()

os.makedirs(os.path.dirname(out), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False, bake_space_transform=not rigged, axis_forward="-Z", axis_up="Y",
                         bake_anim=rigged, bake_anim_use_all_actions=rigged, bake_anim_use_nla_strips=False,
                         bake_anim_simplify_factor=0.5, mesh_smooth_type="FACE", colors_type="SRGB", object_types={"ARMATURE", "MESH"})
tris = sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type == "MESH")
print("[free] exported", out, "faces", tris, "size", tuple(round(v * k, 2) for v in dims),
      "anims", [act.name for act in bpy.data.actions] if rigged else [])
