"""ChronoLock blockout kit. Blender 4.x/5.x; one Blender unit = one meter.
UI: open in Scripting and Run Script (creates assets only).
CLI: blender --background --python this.py -- --output-dir /absolute/new/folder
"""
import argparse
import math
import sys
import uuid
from pathlib import Path
import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument("--output-dir", help="Explicit export destination; optional")
args, _ = parser.parse_known_args(argv)
if bpy.context.mode != "OBJECT":
    raise RuntimeError("Switch to Object Mode before running this script.")
tag = "ChronoLock_" + uuid.uuid4().hex[:8]
collection = bpy.data.collections.new(tag)
bpy.context.scene.collection.children.link(collection)
selected_before = list(bpy.context.selected_objects)
active_before = bpy.context.view_layer.objects.active
roots = []

def material(name, color, metal=0.0):
    mat = bpy.data.materials.new(tag + "_" + name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metal
    shader.inputs["Roughness"].default_value = 0.42
    return mat

steel = material("Steel", (0.13, 0.19, 0.24), 0.65)
white = material("Panel", (0.62, 0.69, 0.72), 0.25)
cyan = material("TimeCyan", (0.02, 0.7, 0.85))
amber = material("Warning", (0.95, 0.42, 0.04))

def root(name, x, y):
    obj = bpy.data.objects.new(name, None)
    collection.objects.link(obj)
    obj.location = (x, y, 0)
    obj["meters_per_unit"] = 1.0
    roots.append(obj)
    return obj

def box(parent, name, pos, size, mat=steel, angle=0):
    verts = [(x * size[0]/2, y * size[1]/2, z * size[2]/2)
             for x, y, z in [(-1,-1,-1), (1,-1,-1), (1,1,-1), (-1,1,-1),
                             (-1,-1,1), (1,-1,1), (1,1,1), (-1,1,1)]]
    faces = [(0,3,2,1), (4,5,6,7), (0,1,5,4), (1,2,6,5), (2,3,7,6), (3,0,4,7)]
    mesh = bpy.data.meshes.new(tag + "_" + name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = parent
    obj.location = pos
    obj.rotation_euler[1] = angle
    mesh.materials.append(mat)
    bevel = obj.modifiers.new("SmallEdgeBevel", "BEVEL")
    bevel.width, bevel.segments = 0.025, 2
    return obj

r = root("CL_Floor_4x4", 0, 0)
box(r, "Floor", (0,0,-0.1), (4,4,0.2))
for x in (-1, 1):
    for y in (-1, 1):
        box(r, "FloorInset", (x,y,0.015), (1.92,1.92,0.03), white)
r = root("CL_Wall_4x3", 6, 0)
box(r, "WallBacking", (0,0,1.5), (4,0.2,3))
for x in (-1, 1):
    box(r, "WallPanel", (x,-0.13,1.5), (1.9,0.08,2.65), white)
box(r, "TimeStrip", (0,-0.185,2.65), (3.5,0.03,0.07), cyan)
r = root("CL_DoorFrame_3x3", 12, 0)
for x in (-1.25, 1.25):
    box(r, "Jamb", (x,0,1.5), (0.5,0.5,3), white)
box(r, "Lintel", (0,0,2.8), (2,0.5,0.4), white)
box(r, "DoorStatus", (0,-0.27,2.8), (1.4,0.04,0.1), cyan)
r = root("CL_DoorLeaf_2x2p6", 18, 0)
box(r, "SlidingLeaf", (0,0,1.3), (1.98,0.18,2.58), steel)
box(r, "DoorStripe", (0,-0.11,1.3), (0.08,0.03,2.3), amber)
r = root("CL_Console", 0, 6)
box(r, "ConsoleBase", (0,0,0.55), (1.1,0.7,1.1))
box(r, "ScreenHousing", (0,0,1.2), (1.25,0.3,0.65), white)
box(r, "Screen", (0,-0.165,1.2), (1.05,0.04,0.45), cyan)
r = root("CL_Rotor_Pivot", 6, 6)
box(r, "Hub", (0,0,0), (0.45,0.3,0.45), amber)
for i in range(4):
    a = i * math.pi/2
    box(r, "RotorBlade", (0.72*math.cos(a),0,0.72*math.sin(a)),
        (1.3,0.16,0.28), white, -a)
r = root("CL_Bridge_2x6", 12, 6)
box(r, "BridgeDeck", (0,0,-0.1), (2,6,0.2))
for x in (-0.96, 0.96):
    box(r, "Rail", (x,0,1.05), (0.08,6,0.1), amber)
    for y in (-2.8, 0, 2.8):
        box(r, "Post", (x,y,0.5), (0.08,0.08,1))

if args.output_dir:
    out = Path(args.output_dir).expanduser().resolve() / tag
    out.mkdir(parents=True, exist_ok=False)
    try:
        for r in roots:
            for obj in bpy.context.selected_objects:
                obj.select_set(False)
            for obj in [r] + list(r.children):
                obj.select_set(True)
            old = r.location.copy()
            try:
                r.location = (0,0,0)
                bpy.context.view_layer.update()
                bpy.ops.export_scene.fbx(filepath=str(out / (r.name + ".fbx")),
                    use_selection=True, object_types={"MESH", "EMPTY"},
                    global_scale=1.0 / bpy.context.scene.unit_settings.scale_length,
                    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                    axis_forward="-Z", axis_up="Y", bake_anim=False)
            finally:
                r.location = old
        bpy.ops.wm.save_as_mainfile(filepath=str(out / (tag + ".blend")), copy=True)
        print("ChronoLock exported:", out)
    finally:
        for obj in bpy.context.selected_objects:
            obj.select_set(False)
        for obj in selected_before:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = active_before
print("Created", collection.name, "with", len(roots), "module roots")
