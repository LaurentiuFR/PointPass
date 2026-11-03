"""Extract native vector geometry from the supplied menu PDF, without flattening UI."""
import json
from pathlib import Path
import sys
import pdfplumber

root = Path(__file__).resolve().parents[2]
source = Path(sys.argv[1])

def cross(a, b, c):
    return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])

def triangulate(points):
    ids = list(range(len(points)))
    area = sum(points[i][0]*points[(i+1)%len(points)][1]-points[(i+1)%len(points)][0]*points[i][1] for i in ids)
    if area < 0: ids.reverse()
    triangles = []
    while len(ids) > 3:
        found = False
        for j, b in enumerate(ids):
            a, c = ids[j-1], ids[(j+1)%len(ids)]
            if cross(points[a],points[b],points[c]) <= 0.000001: continue
            if any(cross(points[a],points[b],points[v]) > 0.00001 and cross(points[b],points[c],points[v]) > 0.00001 and cross(points[c],points[a],points[v]) > 0.00001 for v in ids if v not in (a,b,c)): continue
            triangles += [a,b,c]; ids.pop(j); found=True; break
        if not found:
            # A collinear point from a font outline can prevent an otherwise valid ear.
            j=min(range(len(ids)), key=lambda k:abs(cross(points[ids[k-1]],points[ids[k]],points[ids[(k+1)%len(ids)]])))
            ids.pop(j)
    if len(ids)==3: triangles += ids
    return triangles

def flatten(path):
    contours, current = [], []
    for item in path:
        if item[0]=='m':
            if len(current)>2: contours.append(current)
            current=[tuple(item[1])]
        elif item[0]=='l': current.append(tuple(item[1]))
        elif item[0]=='c':
            a=current[-1]; b,c,d=item[1:]
            for n in range(1,13):
                t=n/12; u=1-t
                current.append((u*u*u*a[0]+3*u*u*t*b[0]+3*u*t*t*c[0]+t*t*t*d[0],u*u*u*a[1]+3*u*u*t*b[1]+3*u*t*t*c[1]+t*t*t*d[1]))
        elif item[0]=='h':
            if len(current)>2: contours.append(current)
            current=[]
    if len(current)>2: contours.append(current)
    return contours

with pdfplumber.open(source) as doc:
    page=doc.pages[0]
    shapes=[o for o in page.curves+page.rects if o['x0']>=570 and o['x1']<=960 and o['top']>=1680 and o['bottom']<=2175 and o.get('fill')]
    result=[]
    for index,o in enumerate(shapes):
        color=o.get('non_stroking_color',(1,1,1))
        if isinstance(color,(int,float)): color=[color]*3
        part=2 if o['x0']>=809 and o['x1']<=862 and o['top']>=2080 else 1 if o['top']>=2105 and o['x1']-o['x0']>100 else 0
        path=o.get('path')
        if not path: path=[('m',(o['x0'],o['top'])),('l',(o['x1'],o['top'])),('l',(o['x1'],o['bottom'])),('l',(o['x0'],o['bottom'])),('h',)]
        for contour in flatten(path):
            clean=[]
            for point in contour:
                if not clean or abs(point[0]-clean[-1][0])+abs(point[1]-clean[-1][1])>0.0001: clean.append(point)
            if len(clean)>2 and abs(clean[0][0]-clean[-1][0])+abs(clean[0][1]-clean[-1][1])<0.0001: clean.pop()
            if len(clean)<3: continue
            points=[(x-576,2161-y) for x,y in clean]
            result.append({'part':part,'color':list(color)+[o.get('fill_opacity',1)],'vertices':[{'x':round(x,4),'y':round(y,4)} for x,y in points],'triangles':triangulate(points)})
    # pdfplumber groups rectangles separately from paths. Restore the shirt stripe's
    # position below the Type 3 jersey numerals, and preserve the zero's counter.
    stripes=[s for s in result if len(s['vertices'])==4 and abs(s['color'][0]-.698039)<.00001]
    for stripe in stripes:
        result.remove(stripe)
        digit=next((i for i,s in enumerate(result) if min(v['x'] for v in s['vertices'])>=170 and max(v['x'] for v in s['vertices'])<=204 and min(v['y'] for v in s['vertices'])>=275 and max(v['y'] for v in s['vertices'])<=310),len(result))
        result.insert(digit,stripe)
    for shape in result:
        xs=[v['x'] for v in shape['vertices']]; ys=[v['y'] for v in shape['vertices']]
        if min(xs)>=170 and max(xs)<=204 and min(ys)>=275 and max(ys)<=310:
            shape['part']=6
        if min(xs)>=190 and max(xs)<=198 and min(ys)>=280 and max(ys)<=305:
            shape['color']=[.698039,.894118,.960784,1]
    output=root/'Assets/_Project/Resources/Menu/ClubhouseFootballer.json'
    output.write_text(json.dumps({'width':376,'height':461,'shapes':result},separators=(',',':')),encoding='utf-8')
    print(f'Extracted {len(result)} editable vector contours; {sum(len(s["vertices"]) for s in result)} vertices')
