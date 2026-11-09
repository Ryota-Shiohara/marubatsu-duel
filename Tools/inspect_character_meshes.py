from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
def islands(mesh):
    links=[[] for v in mesh.vertices]
    for e in mesh.edges:
        a,b=e.vertices
        links[a].append(b);links[b].append(a)
    seen=set()
    result=[]
    for v in mesh.vertices:
        if v.index in seen:continue
        todo=[v.index];seen.add(v.index);group=[]
        while todo:
            i=todo.pop();group.append(i)
            for j in links[i]:
                if j not in seen:seen.add(j);todo.append(j)
        result.append(group)
    return result
if __name__=='__main__':
    for name in ('Mitanuki','Yagapon'):
        bpy.ops.wm.open_mainfile(filepath=str(ROOT/f'ArtSource/{name}/{name}_Unity.blend'))
        ob=next(o for o in bpy.context.scene.objects if o.type=='MESH')
        mats={v:ob.data.materials[p.material_index].name for p in ob.data.polygons for v in p.vertices}
        for ids in islands(ob.data):
            coords=[ob.matrix_world @ ob.data.vertices[i].co for i in ids]
            c=sum(coords,Vector())/len(coords)
            print(name,len(ids),mats[ids[0]],'center',tuple(round(v,3) for v in c),'range',[(round(min(p[k] for p in coords),3),round(max(p[k] for p in coords),3)) for k in range(3)])
