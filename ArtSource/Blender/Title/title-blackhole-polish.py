import bpy,json
from pathlib import Path
s=bpy.data.scenes['CHRONO_Title_BlackHole_Observatory']
group=bpy.data.node_groups.new('Black hole cinematic glow','CompositorNodeTree')
group.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
layers=group.nodes.new('CompositorNodeRLayers');layers.scene=s
glare=group.nodes.new('CompositorNodeGlare');glare.inputs['Type'].default_value='Fog Glow';glare.inputs['Quality'].default_value='High'
for name,value in [('Threshold',1.0),('Strength',.3),('Size',.35)]:
    if glare.inputs.get(name):glare.inputs[name].default_value=value
output=group.nodes.new('NodeGroupOutput')
group.links.new(layers.outputs['Image'],glare.inputs['Image'])
group.links.new(glare.outputs['Image'],output.inputs['Image'])
s.compositing_node_group=group
bpy.data.libraries.write(r'C:\Users\bhb41\Documents\Codex\2026-09-14\sf\outputs\TitleBackground\Title_BlackHole_Observatory.blend',{s},fake_user=True)
print(json.dumps({'compositor':group.name,'active_preserved':bpy.context.scene.name,'inputs':[i.name for i in glare.inputs]}))

