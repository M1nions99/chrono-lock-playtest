import bpy, math, random, json
from pathlib import Path
from mathutils import Vector
out=Path(r'C:\Users\bhb41\Documents\Codex\2026-09-14\sf\outputs\TitleBackground')
out.mkdir(parents=True,exist_ok=True)
original=bpy.data.scenes.get('CHRONO_LOCK_Asset_Preview') or bpy.context.window.scene
scene=bpy.data.scenes.get('CHRONO_Title_Observatory') or bpy.data.scenes.new('CHRONO_Title_Observatory')
assert len(scene.objects)==0, 'Title scene already populated'
bpy.context.window.scene=scene

def mat(name,c,metal=0,rough=.35,emit=0):
    m=bpy.data.materials.new('TITLE_'+name);m.diffuse_color=(*c,1);m.use_nodes=True
    p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=(*c,1)
    p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
    if emit:p.inputs['Emission Color'].default_value=(*c,1);p.inputs['Emission Strength'].default_value=emit
    return m
navy=mat('Navy',(.012,.022,.034),.55,.37)
metal=mat('Machined titanium',(.19,.27,.32),.86,.26)
white=mat('Ceramic',(.62,.72,.77),.18,.3)
teal=mat('Teal pilot lights',(.015,.55,.64),.2,.22,3)
warm=mat('Warm pilot lights',(1,.36,.095),.1,.25,2)
black=mat('Black polymer',(.004,.009,.015),.1,.48)

def finish(n,loc,m,b=.03):
    o=bpy.context.object;o.name=n;o.location=loc;o.data.materials.append(m)
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if b:
        mod=o.modifiers.new('Soft machined edges','BEVEL');mod.width=b;mod.segments=3
        o.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL')
    for p in o.data.polygons:p.use_smooth=True
    return o
def box(n,loc,size,m=metal,b=.03):
    bpy.ops.mesh.primitive_cube_add(size=1);bpy.context.object.scale=size;return finish(n,loc,m,b)
def cyl(n,loc,r,d,m=metal):
    bpy.ops.mesh.primitive_cylinder_add(vertices=64,radius=r,depth=d);return finish(n,loc,m,.025)
def torus(n,loc,r,t,m=metal,rot=(0,0,0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=96,minor_segments=12,major_radius=r,minor_radius=t)
    o=finish(n,loc,m,0);o.rotation_euler=rot;return o
def light(n,loc,target,power,color,size):
    bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.name=n
    o.data.energy=power;o.data.color=color;o.data.shape='DISK';o.data.size=size
    o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()

# A quiet, unlit left-hand wall deliberately reserves space for menu typography.
box('Shadow wall',(-6,4.8,4),(10,.45,11),navy,.09)
box('Shadow side wall',(-8,0,4),(.5,12,10),black,.04)
box('Deck foundation',(0,0,-.25),(24,25,.4),navy,.04)
for x in range(-8,11,2):
    for y in range(-8,7,2):box('Deck surface cassette',(x,y,-.025),(1.982,1.982,.05),navy,.015)
# Deeply recessed panoramic window: opening occupies the right of the shot.
for x in (-1.1,8.7):
    box('Observation structural jamb',(x,4.8,3.6),(.42,.78,7.5),metal,.07)
    box('Jamb ceramic facing',(x,4.36,3.6),(.27,.11,7.1),white,.035)
box('Observation lintel',(3.8,4.8,7.15),(10.2,.8,.44),metal,.07)
box('Observation sill',(3.8,4.6,.44),(10.2,1.1,.88),navy,.08)
box('Sill ceramic cap',(3.8,4.36,.91),(10.05,1.04,.1),white,.045)
box('Sill recessed lighting',(3.8,3.84,.83),(9.7,.035,.055),teal,.012)
for x in (2.5,6):box('Slender window mullion',(x,4.95,4),(.065,.16,6.15),metal,.02)
box('Overhead service volume',(3.8,3.9,7.6),(11,3,1),black,.04)
for x in (0,3,6):
    box('Overhead light recess',(x,3.6,7.05),(.55,1.8,.08),black)
    box('Overhead light diffuser',(x,3.6,7.0),(.32,1.56,.025),teal,.01)
for i in range(12):box('Sill ventilation grille',(6.6+i*.105,4.08,.98),(.035,.43,.018),black,.004)

# A precision temporal gyroscope, purpose-built rather than the in-game rotor.
cx,cy=4.0,1.45
cyl('Gyro foundation',(cx,cy,.16),1.13,.32,navy)
cyl('Ceramic plinth',(cx,cy,.37),1.02,.15,white)
cyl('Recessed pedestal neck',(cx,cy,.61),.58,.35,metal)
cyl('Gyro top platform',(cx,cy,.81),.91,.13,navy)
torus('Platform teal trace',(cx,cy,.88),.82,.014,teal)
center=(cx,cy,2.17)
torus('Outer gyroscopic suspension',center,1.17,.075,white,(math.pi/2,.22,.22))
torus('Inner titanium gimbal',center,.96,.056,metal,(1.06,.53,-.4))
torus('Inner ceramic gimbal',center,.72,.047,white,(.44,-.37,.2))
torus('Orbital light trace',center,.97,.012,teal,(1.06,.53,-.4))
torus('Axial ring',center,.51,.031,metal,(math.pi/2,0,0))
for side in (-1,1):
    box('Suspension fork',(cx+side*.94,cy,1.17),(.14,.3,.67),metal,.04)
    o=cyl('Suspension bearing',(cx+side*1.13,cy,2.17),.13,.15,metal);o.rotation_euler.y=math.pi/2
    for z in (.48,.77):cyl('Precision fastener',(cx+side*.73,cy-.44,z),.028,.025,metal)
bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=4,radius=.29)
core=finish('Contained temporal crystal',center,teal,.005);core.scale=(.7,.7,1.55)
for i in range(18):
    a=i*math.pi*2/18
    o=box('Calibration marks',(cx+1.175*math.cos(a),cy-.025,2.17+1.175*math.sin(a)),(.045,.035,.014),metal,.004)
    o.rotation_euler.y=-a
# A restrained angled control plate and service detailing on the foreground base.
panel=box('Gyroscope diagnostic panel',(cx,cy-.84,.78),(.66,.24,.065),black,.025);panel.rotation_euler.x=.25
for i in range(5):box('Diagnostic status segments',(cx-.23+i*.11,cy-.87,.825),(.053,.07,.011),teal if i<4 else warm,.004)

# Procedural ocean/cloud planet: no downloaded texture or exact real geography.
bpy.ops.mesh.primitive_uv_sphere_add(segments=128,ring_count=64,radius=10.5,location=(8.3,21,2.7))
earth=bpy.context.object;earth.name='Blue orbital world'
for p in earth.data.polygons:p.use_smooth=True
em=bpy.data.materials.new('TITLE_Procedural ocean and clouds');em.use_nodes=True
n=em.node_tree.nodes;l=em.node_tree.links;p=next(q for q in n if q.type=='BSDF_PRINCIPLED');p.inputs['Roughness'].default_value=.58
noise=n.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=3.2;noise.inputs['Detail'].default_value=5;noise.inputs['Roughness'].default_value=.72
ramp=n.new('ShaderNodeValToRGB');ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
for idx,(pos,col) in enumerate([(.22,(.005,.025,.09,1)),(.46,(.018,.09,.2,1)),(.54,(.07,.22,.26,1)),(.61,(.37,.57,.66,1)),(.72,(.8,.9,.96,1))]):
    e=ramp.color_ramp.elements[0] if idx==0 else ramp.color_ramp.elements.new(pos);e.position=pos;e.color=col
l.new(noise.outputs['Fac'],ramp.inputs[0]);l.new(ramp.outputs[0],p.inputs['Base Color']);earth.data.materials.append(em)
# Fine stars remain behind the planet and are never large glowing foreground dots.
star=mat('Distant starlight',(.55,.72,1),0,.5,3)
random.seed(19)
for i in range(150):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=random.uniform(.008,.028),location=(random.uniform(-8,26),34,random.uniform(-3,23)))
    finish('Distant star',bpy.context.object.location,star,0)

world=bpy.data.worlds.new('TITLE deep space');world.use_nodes=True
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.009,.018,.032,1)
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs[1].default_value=.16;scene.world=world
light('Cool window bounce',(7,4,7),(3,0,1.5),1850,(.33,.62,1),5)
light('Broad ceramic key',(4,-3,7),(4,1.4,1.7),1700,(.7,.83,1),5)
light('Orbital sunlight',(2,12,18),(8,21,2),6000,(.64,.8,1),7)
light('Warm edge accent',(6,-.5,1),(4,1,1.8),95,(1,.4,.18),2)
bpy.ops.object.camera_add(location=(-.2,-12.4,3.85));cam=bpy.context.object;cam.name='Title composition camera'
cam.rotation_euler=(Vector((.55,3.1,2.95))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=28
scene.camera=cam;scene.render.engine='CYCLES';scene.cycles.samples=96;scene.cycles.use_denoising=True
scene.render.resolution_x=1920;scene.render.resolution_y=1080;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(out/'TitleBackground.png')
scene.view_settings.view_transform='AgX'
scene.render.film_transparent=False
bpy.data.libraries.write(str(out/'Title_Observatory.blend'),{scene},fake_user=True)
bpy.context.window.scene=original
print(json.dumps({'created_scene':scene.name,'objects':len(scene.objects),'saved':str(out/'Title_Observatory.blend'),'restored_scene':original.name}))

