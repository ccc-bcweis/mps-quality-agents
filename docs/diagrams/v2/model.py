# Diagram models for Baseline v2 architecture drafts. Coordinates in points on ARCH D (2592 x 1728).
T={ 'edge':('#E8F0FE','#1D4ED8','#1E3A8A'), 'bus':('#FFF6D8','#A16207','#7A5C00'), 'det':('#E7F8FB','#0E7490','#155E75'),
    'ai':('#F3EEFF','#6D28D9','#5B21B6'), 'data':('#E7F8EF','#047857','#065F46'), 'ux':('#E7F7F4','#0F766E','#115E59'),
    'gov':('#F1F5F9','#475569','#1E293B'), 'mock':('#F8FAFC','#64748B','#334155'), 'harness':('#FEF3C7','#B45309','#92400E'),
    'sec':('#FFF1E6','#C2410C','#9A3412')}
def G(id,x,y,w,h,title,tone): return dict(kind='group',id=id,x=x,y=y,w=w,h=h,title=title,tone=tone)
def N(id,x,y,w,h,title,lines=(),tone=None,star=False): return dict(kind='node',id=id,x=x,y=y,w=w,h=h,title=title,lines=list(lines),tone=tone,star=star)
COLW=440; GAP=70; X0=56
def col(i): return X0+i*(COLW+GAP)
def stack(prefix,ci,y0,items,tone,h=None,gapy=18,inset=20):
    out=[];y=y0
    for k,(t,ls,*s) in enumerate(items):
        hh=h or (64+30*len(ls))
        out.append(N(f'{prefix}{k}',col(ci)+inset,y,COLW-2*inset,hh,t,ls,tone,bool(s and s[0]))); y+=hh+gapy
    return out,y

def logical():
    el=[];ed=[]
    top=170
    # col0 edge
    items=[('Tool fleet, three plants',['Etch, deposition, strip, clean tools','Sensors via plant IoT gateways'],True),
           ('Offline instruments',['Oscilloscope, RF meter printouts','Upload telemetry, read and confirmed']),
           ('Operator report',['Manual ticket with tool and sensor']),
           ('Quality and supplier events',['QMS nonconformance, supplier notice']),
           ('Simulator control plane',['Scenarios, fault injection, hard and','soft reset, replay vs live'],True)]
    ns,yb=stack('e',0,top+56,items,'edge'); el.append(G('gE',col(0),top,COLW,yb-top+4,'Plant edge and intake','edge')); el+=ns
    items=[('Telemetry stream',['Azure Event Hubs (IoT Hub ingress)','Partitioned by tool, ordered'],True),
           ('Command and event topics',['Azure Service Bus topics','System and application commands'],True),
           ('Message envelope',['eventId, type, correlationId,','causationId, parentEventId,','sequence, source, site, tool, time,','payload, schema version']),
           ('Command catalog',['Defined as data: who may issue','it and which events it emits']),
           ('Event log',['Append-only, indexed by correlation','Rebuilds every causal chain'])]
    ns,yb2=stack('b',1,top+56,items,'bus'); el.append(G('gB',col(1),top,COLW,yb2-top+4,'Event backbone (single entry point)','bus')); el+=ns
    items=[('SPC monitoring',['Control limits, Western Electric rules']),
           ('Business rules engine',['Ticket creation, escalation, routing','Rules and process maps are data']),
           ('Lifecycle state machines',['Alert, ticket, investigation,','approval, CAPA']),
           ('Evidence gate and grounding check',['Weak or conflicting evidence','escalates to a person']),
           ('Approval and action service',['Human approves before any CAPA'])]
    ns,yb3=stack('d',2,top+56,items,'det'); el.append(G('gD',col(2),top,COLW,yb3-top+4,'Application layer, deterministic','det')); el+=ns
    items=[('Investigation orchestrator',['Microsoft Agent Framework']),
           ('Investigation agent',['Read-only tools over enterprise','systems and documents below']),
           ('Resolution agent',['Ranks root cause, no tools']),
           ('Model gateway',['Routing, retries, daily spend cap','Azure OpenAI gpt-5.4-mini, replay'])]
    ns,yb4=stack('a',3,top+56,items,'ai'); el.append(G('gA',col(3),top,COLW,yb4-top+4,'Agent layer, probabilistic','ai')); el+=ns
    y5=yb4+30
    items=[('Mocked enterprise systems',['MES, QMS, PLM, ERP, maintenance']),('Document store',['SOPs, FMEA, bulletins, search index'],True)]
    ns,yb5=stack('m',3,y5+56,items,'mock'); el.append(G('gM',col(3),y5,COLW,yb5-y5+4,'Enterprise context','mock')); el+=ns
    items=[('Metadata store',['Company, site, capability, line, tool,','parameter, limits, rules, process maps','Relational (Azure SQL)'],True),
           ('Telemetry store',['Time series (Azure Data Explorer)','Retention policy, fast windows'],True),
           ('Transaction store',['Alerts, tickets, investigations,','approvals, CAPA, audit (Azure SQL)'],True),
           ('Causal graph',['Event lineage and genealogy','Graph store, optional (Cosmos DB)'],True)]
    ns,yb6=stack('s',4,top+56,items,'data'); el.append(G('gS',col(4),top,COLW,yb6-top+4,'Data stores','data')); el+=ns
    y7=yb6+30
    items=[('Meridian FabOps console',['Sites, fleet, alerts, tickets,','investigation, approval, CAPA,','rules and process, causal trace']),
           ('Live push',['SignalR fed by bus subscriptions'])]
    ns,yb7=stack('u',4,y7+56,items,'ux'); el.append(G('gU',col(4),y7,COLW,yb7-y7+4,'Experience','ux')); el+=ns
    el.append(G('gX',col(1),1270,4*COLW+3*GAP,190,'Publish and subscribe on the backbone','bus'))
    el.append(N('x0',col(1)+20,1318,4*COLW+3*GAP-40,124,'Every layer is a bus participant',['Rules, SPC, lifecycle, agents, stores and live push subscribe to typed topics and publish results with causationId set to the triggering event,','so one fault command fans out into readings, alert, ticket, investigation steps, approval and CAPA, all on one correlationId.'],'bus'))
    for a,b,l in [('e0','b0',''),('e1','b1',''),('e2','b1',''),('e3','b1',''),('e4','b1',''),
                  ('b0','d0',''),('b1','d1',''),('d4','a3','') ,('d1','a0','')]: ed.append((a,b,l))
    ed=[e for e in ed if e[0]!='d4']
    notes=['* The running demo is intentionally thin. One Azure Container App, one replica, hosts the API, simulator and agents.',
           '* Demo bus: in-process, same envelope and command catalog (ADR-008). Measured: 7-day nominal seed, 466,662 readings in 7 s (~66k events/s).',
           '* Demo stores: metadata in YAML loaded at startup (ADR-009); telemetry, event log and transactions in SQLite; 7-day chart read under 1 ms.',
           '* Demo tools: three simulated plants; only the Manufacturing capability is simulated. Document search runs in process.',
           'Every telemetry reading, alert, ticket, investigation step, approval, CAPA and command travels through the event backbone.']
    return dict(name='logical',kicker='LOGICAL ARCHITECTURE',title='Logical architecture, target design',el=el,ed=ed,notes=notes)

def physical():
    el=[];ed=[];top=170
    items=[('Invited reviewers and operators',['Browser, Entra ID sign-in']),('Plant IoT gateways',['Three plants, tool sensors'],True),('GitHub Actions',['Build, test, eval gate, OIDC'])]
    ns,yb=stack('p',0,top+56,items,'edge'); el.append(G('gP',col(0),top,COLW,yb-top+4,'Outside Azure','edge')); el+=ns
    items=[('Azure Front Door and WAF',['Global entry, TLS, rate limits'],True),('Microsoft Entra ID',['Single-tenant app, assignment','required, B2B guests']),
           ('API Management AI gateway',['Token limits, model routing'],True)]
    ns,yb2=stack('f',1,top+56,items,'sec'); el.append(G('gF',col(1),top,COLW,yb2-top+4,'Edge and identity','sec')); el+=ns
    items=[('Web console',['React, served by the API container']),('API service',['.NET 10, SignalR, scale 1 to 3'],True),('Investigation workers',['Scale on queue depth'],True),
           ('Simulator service',['Telemetry and scenarios, 1 replica'],True),('Mock enterprise APIs',['MES, QMS, PLM, ERP, maintenance'])]
    ns,yb3=stack('c',2,top+56,items,'det'); el.append(G('gC',col(2),top,COLW,yb3-top+4,'Container Apps environment','det')); el+=ns
    items=[('Azure IoT Hub',['Device ingress from gateways'],True),('Azure Event Hubs',['Telemetry stream, per-tool partitions'],True),('Azure Service Bus',['Command and domain event topics'],True),
           ('Azure OpenAI',['gpt-5.4-mini, eastus2, quota capped'])]
    ns,yb4=stack('m',3,top+56,items,'bus'); el.append(G('gM',col(3),top,COLW,yb4-top+4,'Messaging and AI','bus')); el+=ns
    items=[('Azure SQL',['Metadata, transactions, audit'],True),('Azure Data Explorer',['Telemetry time series'],True),('Azure AI Search',['Document index'],True),
           ('Key Vault and managed identity',['No keys in code or Git']),('Monitoring',['App Insights, Log Analytics,','budget alert'])]
    ns,yb5=stack('s',4,top+56,items,'data'); el.append(G('gS',col(4),top,COLW,yb5-top+4,'Data, secrets and monitoring','data')); el+=ns
    y6=max(yb,yb2)+30
    items=[('Container Registry',['Signed images, revisions']),('Resource group rg-mps-demo',['Isolated, own identity'])]
    ns,yb6=stack('r',1,y6+56,items,'gov'); el.append(G('gR',col(1),y6,COLW,yb6-y6+4,'Deployment','gov')); el+=ns
    for a,b in [('p0','f0'),('p1','f2'),('p2','r0'),('f0','c0'),('f2','c2'),('c1','m2'),('c3','m1'),('c2','m3'),('m0','s1'),('m1','s1'),('m2','s0'),('m3','s3')]: ed.append((a,b,''))
    ed=[e for e in ed if e[0]!='p1']+[('p1','f1','')]
    ed=[e for e in ed if not (e[0]=='p1' and e[1]=='f1')]
    notes=['* Running demo today: ONE Container App (mpsdemo-api, 1 replica) in rg-mps-demo holds the web console, API, simulator, agents and mock systems.',
           '* Demo messaging: in-process bus in that container instead of IoT Hub, Event Hubs and Service Bus. Same envelope, so it can be swapped later.',
           '* Demo storage: SQLite on the container plus YAML metadata instead of Azure SQL and Data Explorer; data resets on redeploy by design.',
           '* Not deployed in the demo: Front Door, WAF, API Management, AI Search, separate workers. Deployed: Entra ID, Key Vault, managed identity,',
           '  Container Registry, Azure OpenAI, App Insights and Log Analytics. Deploys run from GitHub Actions with OIDC, no stored cloud secrets.']
    return dict(name='physical',kicker='PHYSICAL ARCHITECTURE',title='Physical architecture, target Azure deployment',el=el,ed=ed,notes=notes)

def er():
    el=[];ed=[];top=170
    def E(id,ci,y,title,fields,tone,star=False): return N(id,col(ci)+20,y,COLW-40,64+28*len(fields),title,fields,tone,star)
    W=[]
    W.append(G('gm',col(0),top,2*COLW+GAP,1150,'Metadata (durable, kept by every reset)','data'))
    m=[E('company',0,top+60,'Company',['name, purpose, industry','description'],'data'),
       E('site',0,top+230,'Site',['name, region, timezone','company (FK)'],'data'),
       E('cap',0,top+400,'Capability',['type: Manufacturing, Warehousing,','R and D, Sales, Back office, Other','simulated (yes or no), site (FK)'],'data'),
       E('line',0,top+620,'Production line',['type: etch, deposition, strip,','clean, end-to-end; characteristics','capability (FK)'],'data'),
       E('tool',1,top+60,'Tool',['identity, model, tool type','line (FK) or capability (FK)','inputs, outputs (wafer, gas, power)'],'data'),
       E('param',1,top+290,'Tool parameter',['name, unit, nominal','UCL, LCL, alert thresholds','tool (FK), chamber'],'data'),
       E('rule',1,top+520,'Business rule',['when (condition), then (action)','priority, plain-language reason','applies to site, tool or class'],'data'),
       E('pmap',1,top+750,'Process map',['current or future state','ordered steps and transitions','rule (FK) per transition'],'data'),
       E('cmd',0,top+880,'Command catalog',['name, system or application','payload schema, events emitted'],'data')]
    W+=m
    W.append(G('gt',col(2),top,3*COLW+2*GAP,1150,'Transactions and telemetry (purged by hard reset)','bus'))
    t=[E('evt',2,top+60,'Event (bus envelope)',['eventId, type, sequence, time','correlationId, causationId,','parentEventId, source, site, tool'],'bus'),
       E('tel',2,top+320,'Telemetry reading',['tool parameter (FK), time, value','eventId (FK)'],'bus'),
       E('alert',2,top+520,'Alert',['SPC rule, severity, value vs limit','triggering event (FK)'],'bus'),
       E('ticket',3,top+60,'Ticket',['source: SPC, operator, upload,','QMS, supplier; status, priority','rule fired (FK)'],'bus'),
       E('inv',3,top+320,'Investigation',['status, confidence, cause','ticket (FK), escalated (yes or no)'],'bus'),
       E('evid',3,top+550,'Evidence item',['finding, value vs spec, time','source events (FK to Event)'],'bus'),
       E('appr',4,top+60,'Approval',['decision, approver, reason','investigation (FK)'],'bus'),
       E('capa',4,top+290,'CAPA',['action, owner, due, status','approval (FK)'],'bus'),
       E('scen',4,top+520,'Scenario',['plain-language card, sensors moved','recorded messages for replay'],'bus'),
       E('doc',2,top+760,'Uploaded document',['instrument printout, readings found','confirmed by (person)'],'bus')]
    W+=t
    el=W
    rels=[('company','site','1 to many'),('site','cap','1 to many'),('cap','line','1 to many'),('line','tool','1 to many'),('tool','param','1 to many'),('param','rule',''),
          ('rule','pmap',''),('evt','tel','1 to 1'),('tel','alert','causes'),('alert','ticket','opens'),('ticket','inv','1 to many'),('inv','evid','1 to many'),('ticket','appr',''),('appr','capa','1 to 1'),('cmd','evt','emits')]
    ed=[(a,b,l) for a,b,l in rels]
    notes=['* Demo store is intentionally thin: metadata lives in YAML files with exactly this structure; transactions and telemetry live in SQLite.',
           '* Target store: relational (Azure SQL) for metadata and transactions, time series for telemetry, optional graph store for event lineage.',
           '* Every transaction row keeps the eventId that created it, so any item can be traced back through causationId and parentEventId to its root.',
           '* Hard reset keeps every metadata entity and purges every transaction entity, then seeds nominal telemetry. Soft reset keeps all rows.',
           'FK = reference to another entity. Capability types are all modeled; only Manufacturing is simulated in the demo.']
    return dict(name='data-model',kicker='DATA MODEL',title='Data model, entities and relationships',el=el,ed=ed,notes=notes)
