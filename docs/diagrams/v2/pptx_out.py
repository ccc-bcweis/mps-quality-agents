import json, model
from pptx import Presentation
from pptx.util import Emu, Pt
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE, MSO_CONNECTOR
import render
P=Presentation(); P.slide_width=Emu(int(13.333*914400)); P.slide_height=Emu(int(7.5*914400))
S=13.333*914400/2592
def e(v): return Emu(int(v*S))
def rgb(h): return RGBColor.from_string(h[1:])
def box(sl,x,y,w,h,fill,line,rad=True):
    s=sl.shapes.add_shape(MSO_SHAPE.ROUNDED_RECTANGLE if rad else MSO_SHAPE.RECTANGLE,e(x),e(y),e(w),e(h))
    s.fill.solid(); s.fill.fore_color.rgb=rgb(fill); s.line.color.rgb=rgb(line); s.line.width=Pt(1); s.shadow.inherit=False
    if rad: s.adjustments[0]=0.04
    return s
def txt(s,parts,anchor_top=True):
    tf=s.text_frame; tf.word_wrap=True; tf.margin_left=tf.margin_right=Emu(int(14*S)); tf.margin_top=Emu(int(6*S))
    from pptx.enum.text import MSO_ANCHOR; tf.vertical_anchor=MSO_ANCHOR.TOP
    for i,(t,sz,b,c) in enumerate(parts):
        p=tf.paragraphs[0] if i==0 else tf.add_paragraph(); r=p.add_run(); r.text=t; r.font.size=Pt(sz); r.font.bold=b; r.font.color.rgb=rgb(c); r.font.name='IBM Plex Sans'
for f in (model.logical,model.physical,model.er):
    d=f(); sl=P.slides.add_slide(P.slide_layouts[6]); k=0.3855  # pt per diagram unit
    bg=box(sl,0,0,2592,1728,'#F7F4EA','#F7F4EA',False)
    t=sl.shapes.add_textbox(e(56),e(56),e(1800),e(100)); txt(t,[(d['kicker']+' · BASELINE V2 · DRAFT',22*k,True,'#7A5C00'),(d['title'],40*k,True,'#0B1B33')])
    ids={}
    for g in [x for x in d['el'] if x['kind']=='group']:
        z,s_,c=model.T[g['tone']]; s=box(sl,g['x'],g['y'],g['w'],g['h'],z,s_); txt(s,[(g['title'],24*k,True,c)])
    for n in [x for x in d['el'] if x['kind']=='node']:
        z,s_,c=model.T[n['tone']]; s=box(sl,n['x'],n['y'],n['w'],n['h'],'#FFFFFF',s_); ids[n['id']]=s
        txt(s,[(n['title']+(' *' if n['star'] else ''),22*k,True,c)]+[(l,18*k,False,'#1F2937') for l in n['lines']])
    nm=render.nodes(d)
    for a,b,l in d['ed']:
        pts=render.route(nm[a],nm[b])
        for (x1,y1),(x2,y2) in zip(pts,pts[1:]):
            c=sl.shapes.add_connector(MSO_CONNECTOR.STRAIGHT,e(x1),e(y1),e(x2),e(y2)); c.line.color.rgb=rgb('#334155'); c.line.width=Pt(1)
        last=c.line._get_or_add_ln(); from lxml import etree
        last.append(etree.SubElement(last,'{http://schemas.openxmlformats.org/drawingml/2006/main}tailEnd',type='triangle'))
    nb=box(sl,48,1486,1580,210,'#FFFFFF','#0B1B33',False); txt(nb,[('NOTES',20*k,True,'#0B1B33')]+[(x,19*k,False,'#111827') for x in d['notes']])
    tb=box(sl,1644,1486,900,210,'#0B1B33','#0B1B33',False); txt(tb,[('Meridian Precision Systems · FabOps',26*k,True,'#FAC400'),(d['title'],20*k,False,'#FFFFFF'),('Draft 2026-10-15 · Fictional company, synthetic data',18*k,False,'#F3E8C2')])
P.save('architecture-v2-drafts.pptx'); print('ok')
