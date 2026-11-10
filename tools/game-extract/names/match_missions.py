import json,os,re,sys
S=sys.argv[1]
m=json.load(open(S+'/missions_full.json')); lua=json.load(open(S+'/lua.json')); flags=json.load(open(S+'/flags_full.json'))
W=S+'/wiki/repo/docs/event/simple'
NUM={'一':1,'二':2,'三':3,'四':4,'五':5,'六':6,'七':7,'八':8,'九':9,'十':10,'十一':11,'十二':12}
STG={'上':1,'中':2,'下':3}
tre=re.compile(r'第?([一二三四])年([一二三四五六七八九十]{1,3})月([上中下])旬')
def R(y,mo,s): return (y-1)*36+(mo-1)*3+s
wiki={}
for f in os.listdir(W):
    mm=re.match(r'(\d+)-(\d+)-(\d+)-(.+)\.md$',f)
    if not mm: continue
    name=mm.group(4); txt=open(os.path.join(W,f),encoding='utf-8').read()
    rounds={R(*map(int,mm.groups()[:3]))}
    sec=re.search(r'## 事件時間(.*?)\n## ',txt,re.S)
    if sec:
        body=sec.group(1)
        ts=[R(NUM[a] if len(a)==1 else NUM[a],NUM[b],STG[c]) for a,b,c in tre.findall(body)]
        # 「A～B」區間
        for a,b in re.findall(r'(第?[一二三四]年[一-十]{1,3}月[上中下]旬)\s*[～~至到]\s*(第?[一二三四]年[一-十]{1,3}月[上中下]旬|[一-十]{1,3}月[上中下]旬)',body):
            ta=tre.search(a); tb=tre.search(b) or tre.search(ta.group(1)+'年'+b) if ta else None
            if ta and tb:
                x=R(NUM[ta.group(1)],NUM[ta.group(2)],STG[ta.group(3)]); y=R(NUM[tb.group(1)],NUM[tb.group(2)],STG[tb.group(3)])
                rounds|=set(range(x,y+1))
        rounds|=set(ts)
    fl=set(re.findall(r'🚩「([^」]+)」',txt))|set(re.findall(r'🚩([^\s<:：、，」]+)',txt))
    wiki[name]={'rounds':rounds,'flags':fl,'text':txt}
def grams(t): return {t[i:i+2] for i in range(len(t)-1)}
def flagnote(fid):
    n=(flags.get(fid) or {}).get('note') or ''
    return n.split('\n')[0]
res={}; stat={'unique':0,'tie':0,'none':0}
by_script={}
for x in m:
    for c in x['checks']:
        if x['desc'].strip(): continue
        t=c['time']; rs=set()
        if t.get('at'): rs={R(*t['at'])}
        elif t.get('from') and t.get('to'): rs=set(range(R(*t['from']),R(*t['to'])+1))
        cands=[n for n,w in wiki.items() if rs & w['rounds']]
        script=(c['startScript'] or '').lower()
        body=''.join(lua.get(script,['']))
        fids=set(re.findall(r'(?:AddFlag|SetFlag)\("([A-Z]\d{5})"',body))|set(re.findall(r'Switch\("([A-Z]\d{5})',body))
        notes=' '.join(flagnote(f) for f in fids)
        def score(n):
            w=wiki[n]; s=0
            s+=len(grams(notes)&grams(' '.join(w['flags'])))
            s+=3*sum(1 for f in fids if f in w['text'])
            if x['desc'] and x['desc'] in w['text']: s+=2
            return s
        sc=sorted(((score(n),n) for n in cands),reverse=True)
        pick=None
        if len(cands)==1: pick=cands[0]
        elif sc and sc[0][0]>=6 and sc[0][0]-sc[1][0]>=3: pick=sc[0][1]
        stat['unique' if pick else ('tie' if cands else 'none')]+=1
        if pick: res[c['key']]=pick; by_script.setdefault(script,pick)
        print(c['key'],repr(x['desc']),sorted(rs)[:1],c['startScript'],'→',pick,'|',sc[:3])
# 同劇本補齊
for x in m:
    for c in x['checks']:
        s=(c['startScript'] or '').lower()
        if c['key'] not in res and s in by_script and not x['desc'].strip(): res[c['key']]=by_script[s]
# 沒有時間可比對、只能從條件認出的事件（依 wiki〈主支線簡易表格〉）
OVERRIDES={'S8500_01':'變心夢'}   # 變心≥10，不限時間
res.update(OVERRIDES)
json.dump(res,open(S+'/missionnames.json','w'),ensure_ascii=False,indent=1)
print(stat,len(res))
