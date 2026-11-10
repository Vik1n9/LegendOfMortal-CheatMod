import sys,os,re,json,UnityPy
from collections import defaultdict
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
game=sys.argv[1]; data=os.path.join(game,"Mortal_Data")
env=UnityPy.load(os.path.join(data,"sharedassets2.assets"))
targets=[]
for o in env.objects:
    if o.type.name!="MonoBehaviour": continue
    try: d0=o.read(check_read=False); sc=d0.m_Script.read().m_Name
    except Exception: continue
    if sc=="PositionResultData": targets.append(o)
gen=TypeTreeGenerator(env.assets[0].unity_version); gen.load_local_game(os.path.abspath(game)); env.typetree_generator=gen
pos=[]
for o in targets:
    d=o.read()
    evs=[]
    for p in d._list:
        e=p.read()
        c=e._config.read()
        evs.append({"name":e.m_Name,"note":e._devNote,"cfg":c.m_Name,"value":getattr(c,"Value",None),
                    "rate":e._defaultRate,"mods":len(e._eventRateItem or [])})
    pos.append({"name":d.m_Name,"note":d._devNote,"prefix":d._scriptPrefix,"events":evs})
json.dump(pos,open(sys.argv[2],'w'),ensure_ascii=False,indent=1)
print(len(pos), pos[0]["name"], pos[0]["prefix"], pos[0]["events"][:2])
