import model, html, json, sys
from PIL import ImageFont
W,H=2592,1728
F={'r':'IBMPlexSans-Regular.ttf','s':'IBMPlexSans-SemiBold.ttf','b':'IBMPlexSans-Bold.ttf'}
def tw(t,size,w='r'): return ImageFont.truetype(F[w],size).getlength(t)
TS,LS,GS=22,18,24
def nodes(d): return {e['id']:e for e in d['el']}
def route(a,b):
    if b['x']>=a['x']+a['w']-1:
        sy=a['y']+a['h']/2; ty=b['y']+b['h']/2; sx=a['x']+a['w']; tx=b['x']; mx=tx-35
        return [(sx,sy),(mx,sy),(mx,ty),(tx,ty)] if abs(sy-ty)>1 else [(sx,sy),(tx,ty)]
    if b['x']+b['w']<=a['x']:
        sy=a['y']+a['h']/2; ty=b['y']+b['h']/2; return [(a['x'],sy),(b['x']+b['w']+35,sy),(b['x']+b['w']+35,ty),(b['x']+b['w'],ty)]
    x=max(a['x'],b['x'])+min(a['w'],b['w'])/2
    return [(x,a['y']+a['h']),(x,b['y'])] if b['y']>a['y'] else [(x,a['y']),(x,b['y']+b['h'])]
def check(d):
    P=[];N=[e for e in d['el'] if e['kind']=='node'];Gs=[e for e in d['el'] if e['kind']=='group'];nm=nodes(d)
    for i,a in enumerate(N):
        for b in N[i+1:]:
            if a['x']<b['x']+b['w'] and b['x']<a['x']+a['w'] and a['y']<b['y']+b['h'] and b['y']<a['y']+a['h']: P.append(f"overlap {a['id']} {b['id']}")
        if tw(a['title']+(' *' if a['star'] else ''),TS,'s')>a['w']-28: P.append(f"title too wide {a['id']}")
        for l in a['lines']:
            if tw(l,LS)>a['w']-28: P.append(f"line too wide {a['id']}: {l}")
        if 50+26*len(a['lines'])>a['h']: P.append(f"text too tall {a['id']}")
        if not any(g['x']<=a['x'] and g['y']<=a['y'] and a['x']+a['w']<=g['x']+g['w'] and a['y']+a['h']<=g['y']+g['h'] for g in Gs): P.append(f"outside group {a['id']}")
    for g in Gs:
        if tw(g['title'],GS,'b')>g['w']-30: P.append(f"group title wide {g['id']}")
        if g['y']+g['h']>1470: P.append(f"group into band {g['id']}")
        for g2 in Gs:
            if g is not g2 and g['x']<g2['x']+g2['w'] and g2['x']<g['x']+g['w'] and g['y']<g2['y']+g2['h'] and g2['y']<g['y']+g['h']: P.append(f"group overlap {g['id']} {g2['id']}")
    for a,b,l in d['ed']:
        pts=route(nm[a],nm[b])
        for (x1,y1),(x2,y2) in zip(pts,pts[1:]):
            for n in N:
                if n['id'] in (a,b): continue
                if min(x1,x2)<n['x']+n['w']-1 and max(x1,x2)>n['x']+1 and min(y1,y2)<n['y']+n['h']-1 and max(y1,y2)>n['y']+1: P.append(f"edge {a}->{b} crosses {n['id']}")
    for n in d['notes']:
        if tw(n,19)>1560: P.append('note too wide: '+n[:40])
    return P
def svg(d):
    o=[f'<svg xmlns="http://www.w3.org/2000/svg" width="36in" height="24in" viewBox="0 0 {W} {H}" font-family="IBM Plex Sans, Inter, Arial, sans-serif">',
       f'<title>{d["title"]} - Meridian Precision Systems</title>',
       '<defs><marker id="ar" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="9" markerHeight="9" orient="auto"><path d="M0,0L10,5L0,10z" fill="#334155"/></marker></defs>',
       f'<rect width="{W}" height="{H}" fill="#F7F4EA"/><rect x="24" y="24" width="{W-48}" height="{H-48}" fill="none" stroke="#0B1B33" stroke-width="3"/>',
       f'<text x="56" y="88" font-size="22" font-weight="700" fill="#7A5C00" letter-spacing="3">{d["kicker"]} &#183; BASELINE V2 &#183; DRAFT</text>',
       f'<text x="56" y="134" font-size="40" font-weight="700" fill="#0B1B33">{html.escape(d["title"])}</text>',
       f'<text x="{W-56}" y="134" font-size="20" fill="#1F2937" text-anchor="end">* marks target-design items that the running demo implements in a thinner form (see notes)</text>']
    for g in [e for e in d['el'] if e['kind']=='group']:
        z,s,t=model.T[g['tone']]
        o.append(f'<g id="{g["id"]}"><rect x="{g["x"]}" y="{g["y"]}" width="{g["w"]}" height="{g["h"]}" rx="14" fill="{z}" stroke="{s}" stroke-width="2.5"/><text x="{g["x"]+18}" y="{g["y"]+36}" font-size="{GS}" font-weight="700" fill="{t}">{html.escape(g["title"])}</text></g>')
    nm=nodes(d)
    for a,b,l in d['ed']:
        pts=route(nm[a],nm[b]); p=' '.join(f'{x:.1f},{y:.1f}' for x,y in pts)
        o.append(f'<polyline points="{p}" fill="none" stroke="#334155" stroke-width="2.2" marker-end="url(#ar)"/>')
        if l:
            (x1,y1),(x2,y2)=pts[-2],pts[-1]; o.append(f'<text x="{(x1+x2)/2+8}" y="{(y1+y2)/2-6}" font-size="15" fill="#1F2937">{l}</text>')
    for n in [e for e in d['el'] if e['kind']=='node']:
        z,s,t=model.T[n['tone']]
        o.append(f'<g id="{n["id"]}"><rect x="{n["x"]}" y="{n["y"]}" width="{n["w"]}" height="{n["h"]}" rx="8" fill="#FFFFFF" stroke="{s}" stroke-width="2"/><rect x="{n["x"]}" y="{n["y"]}" width="8" height="{n["h"]}" fill="{s}"/>')
        o.append(f'<text x="{n["x"]+20}" y="{n["y"]+32}" font-size="{TS}" font-weight="600" fill="{t}">{html.escape(n["title"])}{" <tspan fill=\"#B45309\" font-weight=\"700\">*</tspan>" if n["star"] else ""}</text>')
        for i,l in enumerate(n['lines']): o.append(f'<text x="{n["x"]+20}" y="{n["y"]+60+26*i}" font-size="{LS}" fill="#1F2937">{html.escape(l)}</text>')
        o.append('</g>')
    o.append(f'<rect x="48" y="1486" width="1580" height="210" fill="#FFFFFF" stroke="#0B1B33" stroke-width="2"/><text x="64" y="1516" font-size="20" font-weight="700" fill="#0B1B33">NOTES</text>')
    for i,n in enumerate(d['notes']): o.append(f'<text x="64" y="{1548+30*i}" font-size="19" fill="#111827">{html.escape(n)}</text>')
    o.append(f'<rect x="1644" y="1486" width="900" height="210" fill="#0B1B33"/><text x="1668" y="1528" font-size="26" font-weight="700" fill="#FAC400">Meridian Precision Systems &#183; FabOps</text>'
             f'<text x="1668" y="1566" font-size="20" fill="#FFFFFF">{html.escape(d["title"])}</text><text x="1668" y="1600" font-size="18" fill="#F3E8C2">Prepared by StatusNeo with Common Consulting &#183; Draft 2026-10-15</text>'
             f'<text x="1668" y="1634" font-size="18" fill="#F3E8C2">Sheet ARCH D 36 x 24 in &#183; Fictional company, synthetic data</text><text x="1668" y="1668" font-size="16" fill="#F3E8C2">Copyright (c) 2026 Bryan C. Weis and Common Consulting Company</text>')
    o.append('</svg>'); return '\n'.join(o)
def drawio(d):
    c=['<mxfile host="app.diagrams.net"><diagram name="%s"><mxGraphModel dx="2592" dy="1728" grid="1" gridSize="10" pageWidth="2592" pageHeight="1728" page="1"><root><mxCell id="0"/><mxCell id="1" parent="0"/>'%html.escape(d['title'])]
    def cell(i,v,st,x,y,w,h): c.append(f'<mxCell id="{i}" value="{html.escape(v)}" style="{st}" vertex="1" parent="1"><mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>')
    cell('kick',d['kicker']+' · BASELINE V2 · DRAFT','text;fontSize=22;fontStyle=1;fontColor=#7A5C00;align=left;',56,60,1200,36)
    cell('title',d['title'],'text;fontSize=40;fontStyle=1;fontColor=#0B1B33;align=left;',56,96,1600,50)
    for g in [e for e in d['el'] if e['kind']=='group']:
        z,s,t=model.T[g['tone']]; cell(g['id'],g['title'],f'rounded=1;arcSize=3;whiteSpace=wrap;fillColor={z};strokeColor={s};strokeWidth=2;fontSize=24;fontStyle=1;fontColor={t};verticalAlign=top;align=left;spacingLeft=14;',g['x'],g['y'],g['w'],g['h'])
    for n in [e for e in d['el'] if e['kind']=='node']:
        z,s,t=model.T[n['tone']]; v=f'<b><font color="{t}" style="font-size:22px">{html.escape(n["title"])}{" *" if n["star"] else ""}</font></b><br>'+'<br>'.join(html.escape(l) for l in n['lines'])
        cell(n['id'],v,f'rounded=1;arcSize=6;whiteSpace=wrap;html=1;fillColor=#FFFFFF;strokeColor={s};strokeWidth=2;fontSize=18;align=left;verticalAlign=top;spacingLeft=14;spacingTop=4;',n['x'],n['y'],n['w'],n['h'])
    for k,(a,b,l) in enumerate(d['ed']): c.append(f'<mxCell id="e{k}" value="{html.escape(l)}" style="edgeStyle=orthogonalEdgeStyle;rounded=0;strokeColor=#334155;strokeWidth=2;endArrow=block;" edge="1" parent="1" source="{a}" target="{b}"><mxGeometry relative="1" as="geometry"/></mxCell>')
    cell('notes','NOTES\n'+'\n'.join(d['notes']),'text;whiteSpace=wrap;fontSize=19;align=left;verticalAlign=top;strokeColor=#0B1B33;fillColor=#FFFFFF;spacing=12;',48,1486,1580,210)
    cell('tb','Meridian Precision Systems · FabOps\n'+d['title']+'\nDraft 2026-10-15 · ARCH D 36 x 24 in','text;whiteSpace=wrap;fontSize=22;fontColor=#FAC400;fillColor=#0B1B33;align=left;verticalAlign=top;spacing=16;',1644,1486,900,210)
    c.append('</root></mxGraphModel></diagram></mxfile>'); return ''.join(c)
if __name__=='__main__':
    allp={}
    for f in (model.logical,model.physical,model.er):
        d=f(); p=check(d); allp[d['name']]=p
        open(f"{d['name']}-archD.svg",'w').write(svg(d)); open(f"{d['name']}.drawio",'w').write(drawio(d)); json.dump(d,open(f"{d['name']}.json",'w'))
    json.dump(allp,open('overlap-check.json','w'),indent=1); print(json.dumps(allp,indent=1))
