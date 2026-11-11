import bpy, math, sys, time
from mathutils import Vector
FBX=sys.argv[-2]   # usage: python rig.py <model.fbx> <out_dir>
OUT=sys.argv[-1]
t0=time.time()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
body=[x for x in bpy.context.scene.objects if x.type=="MESH"][0]; body.name="Squirrel"
# --- armature ---
arm_data=bpy.data.armatures.new("SquirrelRig"); arm=bpy.data.objects.new("SquirrelRig",arm_data)
bpy.context.scene.collection.objects.link(arm); bpy.context.view_layer.objects.active=arm
bpy.ops.object.mode_set(mode='EDIT'); eb=arm_data.edit_bones
def bone(name,h,t,parent=None,connect=False,deform=True):
    b=eb.new(name); b.head=Vector(h); b.tail=Vector(t); b.use_deform=deform
    if parent: b.parent=eb[parent]; b.use_connect=connect
    return b
bone("root",(0,-0.3,-0.81),(0,-0.1,-0.81),deform=False)
bone("hips",(0,-0.02,-0.43),(0,-0.25,-0.41),"root")
bone("spine",(0,-0.25,-0.41),(0,-0.45,-0.39),"hips",True)
bone("chest",(0,-0.45,-0.39),(0,-0.6,-0.33),"spine",True)
bone("neck",(0,-0.6,-0.33),(0,-0.7,-0.2),"chest",True)
bone("head",(0,-0.7,-0.2),(0,-0.93,-0.17),"neck",True)
bone("ear.L",(0.12,-0.72,-0.02),(0.17,-0.57,0.37),"head")
bone("ear.R",(-0.14,-0.66,-0.02),(-0.21,-0.46,0.34),"head")
for s,x in (("L",0.09),("R",-0.09)):
    bone(f"arm_upper.{s}",(x,-0.58,-0.44),(x,-0.6,-0.62),"chest")
    bone(f"arm_lower.{s}",(x,-0.6,-0.62),(x,-0.57,-0.76),f"arm_upper.{s}",True)
    bone(f"paw_front.{s}",(x,-0.57,-0.76),(x,-0.67,-0.8),f"arm_lower.{s}",True)
for s,pts in (("L",[(0.14,-0.03,-0.44),(0.15,-0.09,-0.62),(0.16,-0.06,-0.76),(0.16,-0.24,-0.8)]),
              ("R",[(-0.17,0.0,-0.44),(-0.19,0.04,-0.62),(-0.2,0.1,-0.76),(-0.2,-0.02,-0.8)])):
    bone(f"thigh.{s}",pts[0],pts[1],"hips")
    bone(f"shin.{s}",pts[1],pts[2],f"thigh.{s}",True)
    bone(f"paw_hind.{s}",pts[2],pts[3],f"shin.{s}",True)
tail=[(0.02,-0.42),(0.18,-0.36),(0.34,-0.26),(0.48,-0.12),(0.58,0.04),(0.64,0.2),(0.63,0.38),(0.55,0.52),(0.42,0.6),(0.28,0.56),(0.2,0.44)]
prev="hips"
for i in range(len(tail)-1):
    n=f"tail.{i+1:02d}"
    bone(n,(0,tail[i][0],tail[i][1]),(0,tail[i+1][0],tail[i+1][1]),prev,i>0); prev=n
bpy.ops.object.mode_set(mode='OBJECT')
print("bones",len(arm_data.bones), round(time.time()-t0,1))
# --- weights: auto weights on a light copy, then transfer to the full mesh ---
proxy=body.copy(); proxy.data=body.data.copy(); proxy.name="WeightProxy"; bpy.context.scene.collection.objects.link(proxy)
dec=proxy.modifiers.new("dec","DECIMATE"); dec.ratio=0.05
bpy.context.view_layer.objects.active=proxy; bpy.ops.object.modifier_apply(modifier="dec")
print("proxy faces",len(proxy.data.polygons), round(time.time()-t0,1))
bpy.ops.object.select_all(action='DESELECT'); proxy.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
empty=[g.name for g in proxy.vertex_groups if not any(g.index in [x.group for x in v.groups] for v in proxy.data.vertices[:1])]
print("proxy groups",len(proxy.vertex_groups), round(time.time()-t0,1))
# how many proxy verts have no weight at all (heat weighting failures)
nw=sum(1 for v in proxy.data.vertices if not any(g.weight>0.01 for g in v.groups)); print("unweighted proxy verts",nw,"/",len(proxy.data.vertices))
for g in proxy.vertex_groups: body.vertex_groups.new(name=g.name)
dt=body.modifiers.new("dt","DATA_TRANSFER"); dt.object=proxy; dt.use_vert_data=True; dt.data_types_verts={'VGROUP_WEIGHTS'}; dt.vert_mapping='POLYINTERP_NEAREST'
dt.layers_vgroup_select_src='ALL'; dt.layers_vgroup_select_dst='NAME'
bpy.ops.object.select_all(action='DESELECT'); body.select_set(True); bpy.context.view_layer.objects.active=body
bpy.ops.object.modifier_apply(modifier="dt")
print("transferred", round(time.time()-t0,1))
bpy.data.objects.remove(proxy)
body.parent=arm; am=body.modifiers.new("Armature","ARMATURE"); am.object=arm
bpy.ops.wm.save_as_mainfile(filepath=OUT+"/squirrel_rig.blend")
print("saved", round(time.time()-t0,1))
