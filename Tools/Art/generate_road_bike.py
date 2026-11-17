#!/usr/bin/env python3
"""Original reproducible motorcycle OBJ for streamed, static roadside scenery."""
from collections import defaultdict
from math import sin, cos, pi, sqrt
from pathlib import Path
import uuid

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Resources/AarisVehicles/Authored"
NAME = "AarisRoadBike"
PALETTE = {
 "Paint": (.095,.20,.34), "Tyre": (.035,.039,.045),
 "Rim": (.53,.58,.64), "Engine": (.17,.19,.22),
 "Seat": (.058,.063,.071), "Headlamp": (.95,.91,.74),
 "TailLamp": (.76,.055,.038), "Amber": (.92,.45,.04),
}
verts=[]
faces=defaultdict(list)
def add(material, points, triangles):
 start=len(verts)+1
 verts.extend(points)
 faces[material].extend(tuple(start+n for n in face) for face in triangles)

def ring(material, cy, cz, major, minor, sides=36, profile=8, shift_x=0):
 points=[]
 for u in range(sides):
  angle=2*pi*u/sides
  for v in range(profile):
   section=2*pi*v/profile
   radial=major+minor*cos(section)
   points.append((shift_x+minor*sin(section),
                  cy+radial*cos(angle), cz+radial*sin(angle)))
 tris=[]
 for u in range(sides):
  for v in range(profile):
   a=u*profile+v;b=((u+1)%sides)*profile+v
   c=u*profile+(v+1)%profile;d=((u+1)%sides)*profile+(v+1)%profile
   tris.extend(((a,b,c),(b,d,c)))
 add(material,points,tris)

def tube(material,a,b,radius,sides=10):
 delta=tuple(b[i]-a[i] for i in range(3))
 n=sqrt(sum(x*x for x in delta))
 if n<.001:return
 direction=tuple(x/n for x in delta)
 guide=(0,1,0) if abs(direction[1])<.90 else (1,0,0)
 side=(direction[1]*guide[2]-direction[2]*guide[1],
       direction[2]*guide[0]-direction[0]*guide[2],
       direction[0]*guide[1]-direction[1]*guide[0])
 sl=sqrt(sum(q*q for q in side));side=tuple(q/sl for q in side)
 up=(direction[1]*side[2]-direction[2]*side[1],
     direction[2]*side[0]-direction[0]*side[2],
     direction[0]*side[1]-direction[1]*side[0])
 points=[]
 for p in (a,b):
  for i in range(sides):
   t=2*pi*i/sides
   points.append(tuple(p[j]+radius*(cos(t)*side[j]+sin(t)*up[j])
                       for j in range(3)))
 tris=[]
 for i in range(sides):
  j=(i+1)%sides
  tris.extend(((i,j,sides+i),(j,sides+j,sides+i)))
 for i in range(1,sides-1):
  tris.extend(((0,i+1,i),(sides,sides+i,sides+i+1)))
 add(material,points,tris)

def box(material,center,size):
 cx,cy,cz=center;x,y,z=(v/2 for v in size)
 pts=[(cx+i*x,cy+j*y,cz+k*z)
      for i,j,k in ((-1,-1,-1),(1,-1,-1),(-1,1,-1),(1,1,-1),
                    (-1,-1,1),(1,-1,1),(-1,1,1),(1,1,1))]
 tris=((0,2,1),(1,2,3),(4,5,6),(5,7,6),
       (0,1,4),(1,5,4),(2,6,3),(3,6,7),
       (0,4,2),(2,4,6),(1,3,5),(3,7,5))
 add(material,pts,tris)

def ellipsoid(material,center,radii,longitude=22,latitude=10):
 cx,cy,cz=center;rx,ry,rz=radii
 points=[]
 for j in range(latitude+1):
  phi=-pi/2+pi*j/latitude
  for i in range(longitude):
   theta=2*pi*i/longitude
   points.append((cx+rx*cos(phi)*cos(theta),cy+ry*sin(phi),
                  cz+rz*cos(phi)*sin(theta)))
 tris=[]
 for j in range(latitude):
  for i in range(longitude):
   a=j*longitude+i;b=j*longitude+(i+1)%longitude
   c=(j+1)*longitude+i;d=(j+1)*longitude+(i+1)%longitude
   tris.extend(((a,c,b),(b,c,d)))
 add(material,points,tris)

# Separate front and rear tyre/rim assemblies with disc and spokes.
for z in (-.78,.78):
 ring('Tyre',.36,z,.29,.065)
 ring('Rim',.36,z,.225,.021,32,6)
 tube('Rim',(-.055,.36,z),(.055,.36,z),.065,20)
 for i in range(10):
  angle=2*pi*i/10
  y=.36+.215*cos(angle);wz=z+.215*sin(angle)
  tube('Rim',(0,.36,z),(0,y,wz),.009,6)
 ring('Rim',.36,z,.17,.009,24,5,shift_x=.083)

# Steel chassis, front forks and twin-cylinder engine.
for sign in (-1,1):
 x=sign*.18
 tube('Rim',(x,.57,-.65),(x,.98,-.12),.025)
 tube('Engine',(x,.98,-.12),(x,.67,.29),.036)
 tube('Engine',(x,.67,.29),(x,.57,-.65),.026)
 tube('Rim',(x,.36,.78),(x,1.36,.54),.038,12)
 tube('Engine',(x,.53,-.13),(x,.70,-.10),.11,18)
 tube('Engine',(x,.53,-.22),(x,.53,-.05),.11,16)
 tube('Seat',(x,.47,-.44),(x,.54,-.79),.05)
ellipsoid('Engine',(0,.67,-.11),(.26,.23,.29))
ellipsoid('Paint',(0,1.065,-.14),(.28,.215,.43))
ellipsoid('Paint',(0,.965,.31),(.24,.18,.24))
ellipsoid('Seat',(0,1.153,-.53),(.245,.085,.38))
ellipsoid('Paint',(0,1.085,-.84),(.17,.115,.22))
box('Engine',(0,.72,-.55),(.3,.095,.54))

# Suspension, handlebars, brake lights, turn signals and fairing panels.
tube('Rim',(-.42,1.35,.56),(.42,1.35,.56),.027,14)
for sign in (-1,1):
 tube('Seat',(sign*.40,1.35,.56),(sign*.48,1.35,.56),.037)
 tube('Rim',(sign*.17,1.30,.56),(sign*.29,1.38,.56),.012,8)
 ellipsoid('Amber',(sign*.38,1.19,.69),(.085,.055,.05),12,7)
 ellipsoid('Amber',(sign*.29,1.07,-.99),(.066,.049,.055),12,7)
 ellipsoid('Paint',(sign*.205,.90,.34),(.07,.17,.31),12,8)
 tube('Rim',(sign*.11,.59,-.25),(sign*.29,.52,-.58),.035)
 tube('Rim',(sign*.19,.60,-.48),(sign*.36,.66,-.82),.065,10)
ellipsoid('Headlamp',(0,1.15,.70),(.18,.105,.065),18,9)
ellipsoid('Engine',(0,1.34,.52),(.18,.19,.059),18,9)
ellipsoid('TailLamp',(0,1.105,-1.01),(.14,.052,.055),14,7)
tube('Rim',(-.16,.49,-.21),(-.40,.07,-.30),.018,8)
tube('Engine',(.24,.62,-.34),(.28,.46,-.81),.068,12)
tube('Rim',(.28,.46,-.81),(.27,.51,-1.00),.046,12)

OUT.mkdir(parents=True,exist_ok=True)
obj=OUT/(NAME+'.obj')
lines=['# Aaris original motorcycle geometry - no third-party assets',
       'mtllib '+NAME+'.mtl','o '+NAME]
lines.extend('v %.6f %.6f %.6f'%v for v in verts)
for material,triangles in faces.items():
 lines.extend(('usemtl '+material,'s 1'))
 lines.extend('f %d %d %d'%triangle for triangle in triangles)
obj.write_text('\n'.join(lines)+'\n')
mtl=OUT/(NAME+'.mtl')
with mtl.open('w') as f:
 for name,rgb in PALETTE.items():
  f.write('newmtl '+name+'\n')
  f.write('Kd %.3f %.3f %.3f\nKa 0 0 0\nKs 0.2 0.2 0.2\nNs 60\n\n'%rgb)
mtl.write_text(mtl.read_text().rstrip()+'\n')
for path in (obj,mtl):
 meta=path.with_name(path.name+'.meta')
 guid=uuid.uuid5(uuid.NAMESPACE_URL,'aaris-original-roadside-bike-'+path.suffix).hex
 if path.suffix=='.obj':
  original=(OUT/'QuaterniusSports.obj.meta').read_text()
  import re
  original=re.sub(r'(?m)^guid: [0-9a-f]{32}$','guid: '+guid,original)
  meta.write_text(original)
 else:meta.write_text('fileFormatVersion: 2\nguid: '+guid+'\n')
print('ORIGINAL_BIKE_MODEL',len(verts),'vertices',
      sum(map(len,faces.values())),'triangles',len(PALETTE),'materials',
      obj.stat().st_size,'bytes')
