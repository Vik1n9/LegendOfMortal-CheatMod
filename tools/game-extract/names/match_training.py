import json,re,sys,html
from collections import defaultdict
S=sys.argv[1]; WIKI=sys.argv[2]
pos=json.load(open(S+'/pos.json')); lua=json.load(open(S+'/lua.json')); loc=json.load(open(S+'/loc.json'))
# ---- 地點劇本：Position("X") → 選項標題、基礎心相/貢獻、地點名稱 ----
opt_re=re.compile(r'(\w+)\[(\d+)\]\s*=\s*"([^"+]+)\+([^"+]+)\+([^"+]+)"')
ctx={}
for name,vers in lua.items():
    for t in vers:
        opts={}
        for m in opt_re.finditer(t): opts[(m.group(1),int(m.group(2)))]=m.group(5)
        title=None; cur=None; mental=0; contrib=0
        for line in t.split('\n'):
            s=line.strip()
            m=re.search(r'mainui\.DisplayTitle\("([^"]+)"\)',s)
            if m: title=m.group(1)
            m=re.match(r'(?:if|elseif)\s+choice_(\w+)\s*==\s*(\d+)\s+then',s)
            if m: cur=(m.group(1),int(m.group(2))); mental=0; contrib=0; continue
            m=re.search(r'statmodifymanager\.Player\("(mental|contribution)",\s*(-?\d+)',s)
            if m:
                if m.group(1)=='mental': mental+=int(m.group(2))
                else: contrib+=int(m.group(2))
            m=re.search(r'checkpointmanager\.Position\("([^"]+)"\)',s)
            if m:
                key=opts.get(cur)
                place=loc.get('Story/'+title,title) if title else None
                ctx.setdefault(m.group(1),[]).append({"cmd":loc.get('Position/title/'+key,key) if key else None,"place":place,"mental":mental,"contrib":contrib,"script":name})
# ---- 結果劇本心相 ----
def res_delta(script):
    vs=lua.get(script.lower())
    if not vs: return None
    ds=[]
    for t in vs:
        ds.append(sum(int(x) for x in re.findall(r'statmodifymanager\.Player\("mental",\s*(-?\d+)',t)))
    return ds
STAT={k.split('/',1)[1]:v for k,v in loc.items() if k.startswith('PlayerStat/')}
def effects(script):
    """結果劇本改動的數值（顯示名稱），不含幾乎每個事件都有的心相、貢獻度。"""
    out=set()
    for t in lua.get(script.lower(),[]):
        for sid in re.findall(r'statmodifymanager\.Player\("([^"]+)",\s*-?\d+',t):
            if sid not in ('mental','contribution') and sid in STAT: out.add(STAT[sid])
    return out
# ---- wiki 列 ----
raw=open(WIKI,encoding='utf-8').read()
rows=[]
for tr in re.findall(r'<tr>(.*?)</tr>',raw,re.S):
    tds=re.findall(r'<td[^>]*>(.*?)</td>',tr,re.S)
    if len(tds)<9: continue
    clean=lambda x: re.sub(r'\s+',' ',re.sub(r'<[^>]+>',' ',x.replace('<br>','|'))).strip()
    fac=[p.strip() for p in tds[1].split('<br>')]
    rate=re.search(r'基礎:\s*(\d+)',tds[8])
    try: m=int(clean(tds[4]))
    except: m=None
    rows.append({"area":clean(tds[0]),"fac":fac[0],"cmd":fac[-1],"name":clean(tds[2]),"mental":m,"rate":int(rate.group(1)) if rate else None,
                 "fx":clean(tds[5])+' '+clean(tds[6])+' '+clean(tds[7])})
groups=defaultdict(list)
for r in rows: groups[(r['area'],r['fac'],r['cmd'])].append(r)
def totals(d,cs):
    """可能的心相值：結果劇本各版本 ×（地點劇本各版本的基礎值，或 0）。"""
    bases={c['mental'] for c in cs}|{0}
    return {(x or 0)+b for x in (d or [0]) for b in bases}
def fxscore(e,r): return sum(1 for n in e.get('fx',()) if n in r['fx'])
def related(a,b):
    g=lambda t:{t[i:i+2] for i in range(len(t)-1)}
    return bool(g(a)&g(b))
def base(c): return re.split(r'[．・]',c or '')[0]
out={}; report=[]
for p in pos:
    cs=ctx.get(p['name'],[])
    cmds={base(c['cmd']) for c in cs if c['cmd']}
    evs=[]; seen=set()
    for e in p['events']:
        if e['name'] in seen: continue
        seen.add(e['name'])
        d=res_delta(p['prefix']+'_'+e['value'])
        e['fx']=effects(p['prefix']+'_'+e['value'])
        evs.append((e,d))
    # 選最吻合的 wiki 群組
    best=None
    for g,rs in groups.items():
        if g[2] not in cmds and base(g[2]) not in cmds: continue
        score=0
        for e,d in evs:
            for c in cs[:1]:
                tot=totals(d,cs)
                if any(r['rate']==e['rate'] and r['mental'] in tot for r in rs): score+=2
                elif any(r['rate']==e['rate'] for r in rs): score+=1
        if best is None or score>best[0]: best=(score,g)
    names={}
    if best:
        rs=list(groups[best[1]]); used=set()
        # 前置：wiki 名稱與開發備註相同者先配對，避免被別的事件以數值巧合搶走
        norm=lambda t: re.sub(r'[\s|]','',t or '')
        for e,d in evs:
            c=[i for i in range(len(rs)) if i not in used and norm(e['note']) and norm(rs[i]['name'])==norm(e['note'])]
            if c: used.add(c[0]); names[e['value']]=rs[c[0]]['name']
        for e,d in evs:
            tot=totals(d,cs)
            if e['value'] in names: continue
            cand=[i for i,r in enumerate(rs) if i not in used and r['rate']==e['rate'] and r['mental'] in tot] or \
                 [i for i,r in enumerate(rs) if i not in used and r['mental'] in tot and r['rate'] is None]
            # 有開發備註時，wiki 名稱要與備註有共同的兩字詞才採用，避免機率／心相巧合配錯
            cand=[i for i in cand if len(e['note'].strip())<=2 or related(e['note'],rs[i]['name'])]
            cand.sort(key=lambda i:-fxscore(e,rs[i]))
            if len(cand)>=1:
                used.add(cand[0]); names[e['value']]=rs[cand[0]]['name']
        # 效果輪：心相吻合、效果數值重疊最多且唯一（至少 2 項）
        for e,d in evs:
            if e['value'] in names or e['note'].strip(): continue
            tot=totals(d,cs)
            sc=sorted(((fxscore(e,rs[i]),i) for i in range(len(rs)) if i not in used and rs[i]['mental'] in tot),reverse=True)
            if sc and sc[0][0]>=2 and (len(sc)==1 or sc[0][0]>sc[1][0]): used.add(sc[0][1]); names[e['value']]=rs[sc[0][1]]['name']
        # 第二輪：剩下的列裡心相或機率唯一吻合者
        for e,d in evs:
            if e['value'] in names or e['note'].strip(): continue
            tot=totals(d,cs)
            left=[i for i in range(len(rs)) if i not in used]
            for cond in (lambda r: r['mental'] in tot, lambda r: r['rate']==e['rate']):
                c=[i for i in left if cond(rs[i])]
                if len(c)==1: used.add(c[0]); names[e['value']]=rs[c[0]]['name']; break
        # 第三輪：有開發備註者，剩下的列中只有一列名稱與備註相關就採用（機率可能因版本不同）
        for e,d in evs:
            if e['value'] in names or len(e['note'].strip())<=2: continue
            c=[i for i in range(len(rs)) if i not in used and related(e['note'],rs[i]['name'])]
            if len(c)==1: used.add(c[0]); names[e['value']]=rs[c[0]]['name']
    out[p['name']]={"place":cs[0]['place'] if cs else None,"cmd":cs[0]['cmd'] if cs else None,
                    "wiki":"/".join(best[1]) if best else None,"events":names}
    report.append((p['name'],out[p['name']]['place'],out[p['name']]['cmd'],out[p['name']]['wiki'],f"{len(names)}/{len(evs)}",[e['cfg']+('('+e['note']+')' if e['note'] else '') for e,_ in evs if e['value'] not in names]))
json.dump(out,open(S+'/posnames.json','w'),ensure_ascii=False,indent=1)
for r in report: print(*r,sep=' | ')
