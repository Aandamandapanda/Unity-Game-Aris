#!/usr/bin/env python3
"""Generate Aaris' original, lightweight open-cockpit off-road buggy OBJ.

Art-only offline build: real four-wheel physics/visual wheels are sourced from
the existing verified local FBX. No paid or third-party vehicle mesh required.
"""
from pathlib import Path
from collections import OrderedDict
from math import sqrt, sin, cos, pi
from uuid import uuid5, NAMESPACE_URL
import re

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/"Assets/Resources/AarisVehicles/Authored"
OBJ=OUT/"AarisBuggy.obj"
MTL=OUT/"AarisBuggy.mtl"
PALETTE=OrderedDict((
 ("AarisPaint",(.23,.34,.30)),
 ("AarisDark",(.045,.052,.058)),
 ("AarisMetal",(.43,.49,.51)),
 ("AarisSeat",(.10,.105,.10)),
 ("AarisGlass",(.08,.12,.15)),
 ("AarisHeadlight",(.84,.89,.85)),
 ("AarisTaillight",(.60,.04,.027)),
))
vertices=[]
faces={name:[] for name in PALETTE}
def mesh(mat,points,tris):
    offset=len(vertices)+1
    vertices.extend(points)
    faces[mat].extend(tuple(offset+i for i in tri) for tri in tris)
def box(mat,center,size):
    x,y,z=(n/2 for n in size);cx,cy,cz=center
    v=[(cx+a*x,cy+b*y,cz+c*z)
       for a,b,c in ((-1,-1,-1),(1,-1,-1),(-1,1,-1),(1,1,-1),
                     (-1,-1,1),(1,-1,1),(-1,1,1),(1,1,1))]
    t=((0,2,1),(1,2,3),(4,5,6),(5,7,6),(0,1,4),
       (1,5,4),(2,6,3),(3,6,7),(0,4,2),(2,4,6),
       (1,3,5),(3,7,5))
    mesh(mat,v,t)
def tube(mat,a,b,r=.02,sides=8):
    d=tuple(b[i]-a[i] for i in range(3))
    length=sqrt(sum(v*v for v in d))
    if length<.0001:return
    f=tuple(v/length for v in d)
    up=(0,1,0) if abs(f[1])<.87 else (0,0,1)
    x=(f[1]*up[2]-f[2]*up[1],f[2]*up[0]-f[0]*up[2],
       f[0]*up[1]-f[1]*up[0])
    n=sqrt(sum(v*v for v in x));x=tuple(v/n for v in x)
    y=(f[1]*x[2]-f[2]*x[1],f[2]*x[0]-f[0]*x[2],
       f[0]*x[1]-f[1]*x[0])
    v=[]
    for center in (a,b):
        for i in range(sides):
            theta=2*pi*i/sides
            v.append(tuple(center[j]+r*(cos(theta)*x[j]+sin(theta)*y[j])
                           for j in range(3)))
    t=[]
    for i in range(sides):
        k=(i+1)%sides
        t.extend(((i,k,sides+i),(k,sides+k,sides+i)))
    for i in range(1,sides-1):
        t.extend(((0,i+1,i),(sides,sides+i,sides+i+1)))
    mesh(mat,v,t)
def slant(mat,front,rear,frontY,rearY,width,height=.045):
    # Tapered hood/deflector panel with wedge-like longitudinal profile.
    hw=width*.5; hf=width*.42
    v=[(-hw,frontY-height,front),(hw,frontY-height,front),
       (-hf,rearY-height,rear),(hf,rearY-height,rear),
       (-hw,frontY,front),(hw,frontY,front),
       (-hf,rearY,rear),(hf,rearY,rear)]
    t=((0,1,2),(1,3,2),(4,6,5),(5,6,7),
       (0,2,4),(2,6,4),(1,5,3),(3,5,7),
       (2,3,6),(3,7,6),(0,4,1),(1,4,5))
    mesh(mat,v,t)

# Low-slung original buggy chassis, drawn WITHOUT visual wheels.
# Wheel centers are supplied by the existing wheel-truck FBX and WheelColliders.
box("AarisDark",(0,.48,0),(1.25,.21,2.76))
box("AarisMetal",(0,.35,0),(1.17,.13,2.68))
slant("AarisPaint",1.48,.52,.66,.83,1.53,.12)  # hood
box("AarisPaint",(0,.62,-1.12),(1.57,.29,.76)) # rear motor cover
box("AarisMetal",(0,.77,-1.44),(1.31,.21,.15))
box("AarisDark",(0,.91,-1.23),(1.12,.28,.30))
# Widened but open fender lips sit above the separate physical wheel art.
for sign in (-1,1):
    x=sign*.70
    box("AarisPaint",(x,.67,1.13),(.27,.10,.68))
    box("AarisPaint",(x,.67,-1.12),(.27,.10,.68))
    box("AarisDark",(x,.47,0),(.21,.22,1.14))
    box("AarisMetal",(sign*.74,.48,1.52),(.20,.15,.18))
    box("AarisMetal",(sign*.74,.48,-1.53),(.20,.15,.18))
    # Outer protective rails and rear bumper braces.
    for z in (-1.30,1.30):
        tube("AarisMetal",(sign*.74,.50,z),(sign*.82,.57,z),.029)
    tube("AarisMetal",(sign*.74,.50,-1.50),(sign*.74,.50,1.50),.028)
    tube("AarisMetal",(sign*.67,.49,.58),(sign*.70,1.20,-.54),.038)
    tube("AarisMetal",(sign*.67,1.20,-.54),(sign*.66,1.57,-.60),.033)
    tube("AarisMetal",(sign*.71,.49,-1.28),(sign*.66,1.57,-.60),.032)
    tube("AarisMetal",(sign*.62,1.57,.46),(sign*.66,1.57,-.60),.034)
    tube("AarisMetal",(sign*.62,1.57,.46),(sign*.70,.58,.85),.033)
    # One small side mirror per flank, no extra draw call.
    tube("AarisMetal",(sign*.65,1.13,.52),(sign*.83,1.20,.53),.019)
    box("AarisDark",(sign*.83,1.20,.55),(.15,.095,.12))
    box("AarisGlass",(sign*.83,1.20,.611),(.12,.068,.008))

# Real open cockpit: two detailed bucket seats, roll cage + cross bars.
for sign in (-1,1):
    x=sign*.31
    box("AarisSeat",(x,.74,-.41),(.46,.15,.43))
    box("AarisSeat",(x,1.03,-.69),(.45,.59,.12))
    box("AarisDark",(x,1.12,-.624),(.28,.30,.025))
    box("AarisMetal",(x,.91,-.02),(.05,.44,.14))
    tube("AarisMetal",(x*.88,1.08,-.91),(x*.88,1.56,-.60),.023)
tube("AarisMetal",(-.66,1.57,-.60),(.66,1.57,-.60),.037)
tube("AarisMetal",(-.62,1.57,.46),(.62,1.57,.46),.033)
tube("AarisMetal",(0,1.57,.46),(0,1.57,-.60),.025)
# Thin tinted wind deflector tucked inside the structural roll cage.
slant("AarisGlass",.60,.37,1.00,1.44,1.10,.019)
box("AarisDark",(0,.91,.15),(.24,.11,.33))  # center console
# Stainless corner bumpers, work lights and restrained rear optics.
tube("AarisMetal",(-.67,.49,1.58),(.67,.49,1.58),.043)
tube("AarisMetal",(-.67,.51,-1.59),(.67,.51,-1.59),.040)
for sign in (-1,1):
    box("AarisDark",(sign*.57,.74,1.474),(.30,.21,.061))
    box("AarisHeadlight",(sign*.57,.75,1.514),(.24,.12,.025))
    box("AarisDark",(sign*.56,.76,-1.487),(.30,.18,.04))
    box("AarisTaillight",(sign*.56,.77,-1.516),(.24,.095,.024))
    # Bright mechanical shock struts behind wheels (visual only).
    tube("AarisMetal",(sign*.53,.45,-1.0),(sign*.63,.79,-.73),.021)
    tube("AarisMetal",(sign*.53,.45,1.07),(sign*.63,.79,.79),.021)
# Engine grille bars use the same metal material.
for x in (-.39,-.19,0,.19,.39):
    tube("AarisDark",(x,.81,-1.523),(x,.91,-1.523),.010,6)

OUT.mkdir(parents=True,exist_ok=True)
lines=["# Aaris original open-buggy body, authored locally, CC0-compatible",
       "mtllib AarisBuggy.mtl","o AarisBuggy"]
lines.extend("v %.6f %.6f %.6f"%v for v in vertices)
for name,tris in faces.items():
    if not tris:continue
    lines.extend(("usemtl "+name,"s 1" if name in
                 ("AarisMetal","AarisGlass") else "s off"))
    lines.extend("f %d %d %d"%tri for tri in tris)
OBJ.write_text("\n".join(lines)+"\n")
material=["# Original authored paint, rubber and glass. URP material conversion happens once per type."]
for name,rgb in PALETTE.items():
    material.extend(["newmtl "+name,"Kd %.3f %.3f %.3f"%rgb,
                     "Ks .30 .30 .30","Ns 180"])
    if name=="AarisGlass":material.append("d 0.32")
    material.append("")
MTL.write_text("\n".join(material).rstrip()+"\n")
template=(OUT/"QuaterniusSports.obj.meta").read_text()
guid=uuid5(NAMESPACE_URL,"aaris-original-offroad-buggy-obj-v1").hex
meta=re.sub(r"(?m)^guid: [a-f0-9]{32}$","guid: "+guid,template)
Path(str(OBJ)+".meta").write_text(meta)
mtl_meta="fileFormatVersion: 2\nguid: "+uuid5(NAMESPACE_URL,"aaris-original-offroad-buggy-mtl-v1").hex+"\n"
Path(str(MTL)+".meta").write_text(mtl_meta)
print("AARIS_ORIGINAL_BUGGY",len(vertices),"vertices",
      sum(map(len,faces.values())),"triangles",
      len(PALETTE),"materials",OBJ.stat().st_size,"bytes")
