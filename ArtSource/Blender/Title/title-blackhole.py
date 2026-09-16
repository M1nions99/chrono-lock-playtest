"""Run through Blender MCP. Append an isolated title scene; preserve all old scenes."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
out=Path(r'C:\Users\bhb41\Documents\Codex\2026-09-14\sf\outputs\TitleBackground')
original=bpy.context.window.scene
with bpy.data.libraries.load(str(out/'Title_Observatory.blend'),link=False) as (src,dst):
    dst.scenes=['CHRONO_Title_Observatory']
scene=dst.scenes[0];scene.name='CHRONO_Title_BlackHole_Observatory'
bpy.context.window.scene=scene

def emission(name,color,strength):
    m=bpy.data.materials.new('BLACKHOLE_'+name);m.use_nodes=True
    nodes=m.node_tree.nodes;nodes.clear()
    shader=nodes.new('ShaderNodeEmission');shader.inputs[0].default_value=(*color,1);shader.inputs[1].default_value=strength
    output=nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(shader.outputs[0],output.inputs['Surface'])
    return m
void=emission('Event horizon',(0,0,0),0)
gold=emission('Lensed photon orbit',(1,.39,.075),4)
whitegold=emission('Inner photon filament',(1,.73,.34),5)
teal=emission('Alien geometric inscriptions',(.045,.54,.57),1.3)
for o in scene.objects:
    if o.name.startswith('Blue orbital world'):o.hide_render=True
    if o.type=='LIGHT' and o.name.startswith('Orbital sunlight'):o.data.energy=0
    if o.type=='LIGHT' and o.name.startswith('Cool window bounce'):
        o.data.color=(1,.44,.13);o.data.energy=1000
    if o.type=='LIGHT' and o.name.startswith('Broad ceramic key'):
        o.data.energy=1250;o.data.color=(.49,.73,.84)

center=Vector((7.1,20,5.15))
bpy.ops.mesh.primitive_uv_sphere_add(segments=128,ring_count=64,radius=2.61,location=center)
hole=bpy.context.object;hole.name='Absolute black event horizon';hole.data.materials.append(void)
for p in hole.data.polygons:p.use_smooth=True

def ring(name,r,t,m):
    bpy.ops.mesh.primitive_torus_add(major_segments=192,minor_segments=12,major_radius=r,minor_radius=t,location=center)
    o=bpy.context.object;o.name=name;o.rotation_euler.x=math.pi/2;o.data.materials.append(m)
    for p in o.data.polygons:p.use_smooth=True
    return o
ring('Gravitationally lensed inner orbit',2.65,.021,whitegold)
ring('Warm photon ring',2.72,.068,gold)
ring('Faint outer lens halo',2.86,.017,emission('Outer lens halo',(1,.23,.045),.6))

# A fine banded annulus provides an edge-on accretion disk with a real black occluder.
for band in range(18):
    inner=2.66+band*.185;outer=inner+.178
    vertices=[];faces=[];steps=192
    for i in range(steps):
        a=i*2*math.pi/steps
        for radius in (inner,outer):
            # A slight warp keeps the physical disk from looking like a flat UI ellipse.
            vertices.append((math.cos(a)*radius,math.sin(a)*radius,.08*math.sin(a*2)))
    for i in range(steps):
        a=i*2;b=((i+1)%steps)*2;faces.append((a,b,b+1,a+1))
    mesh=bpy.data.meshes.new('Accretion band mesh');mesh.from_pydata(vertices,[],faces);mesh.update()
    o=bpy.data.objects.new('Accretion plasma band %02d'%band,mesh);scene.collection.objects.link(o)
    o.location=center;o.rotation_euler=(.16,.07,-.18)
    m=bpy.data.materials.new('BLACKHOLE_PlasmaBand_%02d'%band);m.use_nodes=True
    n=m.node_tree.nodes;n.clear();l=m.node_tree.links
    output=n.new('ShaderNodeOutputMaterial');e=n.new('ShaderNodeEmission')
    e.inputs[0].default_value=(1,.32+.016*(18-band),.05+.007*(18-band),1)
    noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=13;noise.inputs['Detail'].default_value=2.5
    mult=n.new('ShaderNodeMath');mult.operation='MULTIPLY';mult.inputs[1].default_value=(2.3 if band%3 else 3.8)*(1-band/24)
    l.new(noise.outputs['Fac'],mult.inputs[0]);l.new(mult.outputs[0],e.inputs[1]);l.new(e.outputs[0],output.inputs['Surface'])
    mesh.materials.append(m)

# Non-linguistic inlays make this observatory feel alien without adding UI text.
def line(name,a,b,r=.008):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='3D';curve.bevel_depth=r;curve.bevel_resolution=2
    spline=curve.splines.new('POLY');spline.points.add(1)
    for p,v in zip(spline.points,(a,b)):p.co=(*v,1)
    o=bpy.data.objects.new(name,curve);scene.collection.objects.link(o);curve.materials.append(teal)
for z in (1.8,2.6,3.4,4.2,5):
    x=8.7;y=4.285
    line('Alien diamond inlay',(x-.08,y,z),(x,y,z+.13))
    line('Alien diamond inlay',(x,y,z+.13),(x+.08,y,z))
    line('Alien diamond inlay',(x+.08,y,z),(x,y,z-.13))
    line('Alien diamond inlay',(x,y,z-.13),(x-.08,y,z))

scene.render.filepath=str(out/'TitleBlackHole.png')
scene.cycles.samples=96;scene.cycles.use_denoising=True
scene.render.resolution_x=1920;scene.render.resolution_y=1080;scene.render.resolution_percentage=100
bpy.data.libraries.write(str(out/'Title_BlackHole_Observatory.blend'),{scene},fake_user=True)
bpy.context.window.scene=original
print(json.dumps({'scene':scene.name,'objects':len(scene.objects),'original_restored':original.name,'saved':str(out/'Title_BlackHole_Observatory.blend')}))
