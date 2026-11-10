import json,re,os,sys,subprocess
S=sys.argv[1]; OUT=sys.argv[2]
pos=json.load(open(S+'/pos.json')); names=json.load(open(S+'/posnames.json'))
def tw(xs):
    r=subprocess.run(['opencc','-c','s2tw'],input='\n'.join(xs),capture_output=True,text=True).stdout.split('\n')
    return r[:len(xs)]
FALLBACK=[('Section_01','崆峒'),('Section_02','青城'),('Travel','旅途'),('Casino','賭坊'),('Chess','下棋'),('Fish','釣魚'),
 ('Walk','散步'),('Door','大門'),('GirlRoom','女弟子房'),('Room','弟子房'),('Craft','鍛冶場'),('Study','講經堂'),('Spa','溫泉'),
 ('S2800','地牢'),('S2321','劇情'),('Ch_','劇情')]
CAT={'大失敗':'大失敗','大成功':'大成功','成功':'成功','失敗':'失敗','正常':'一般','特殊':'特殊','其他':'其他'}
place={}; ev={}
for p in pos:
    n=names.get(p['name'],{})
    if n.get('wiki'):
        area,fac,cmd=n['wiki'].split('/'); label=f"{fac}・{cmd}"
    elif n.get('place') or n.get('cmd'):
        label='・'.join(x for x in (n.get('place'),n.get('cmd')) if x)
    else:
        label=next((v for k,v in FALLBACK if p['name'].startswith(k)),'場所')
    place[p['name']]=label
    seen=set()
    for e in p['events']:
        k=p['name']+'/'+e['value']
        if k in seen: continue
        seen.add(k)
        nm=n.get('events',{}).get(e['value'])
        if not nm and len(e['note'].strip())>2: nm=e['note'].strip()
        if not nm:
            m=re.match(r'P_(.+?)_(\d+)$',e['cfg'])
            nm=(CAT.get(m.group(1),m.group(1))+'（'+str(int(m.group(2)))+'）') if m else e['cfg']
        ev[k]=re.sub(r'\s*\|\s*','',nm)
pk=list(place); ek=list(ev)
pv=tw([place[k] for k in pk]); evv=tw([ev[k] for k in ek])
esc=lambda s: s.replace('\\','\\\\').replace('"','\\"')
L=['using System.Collections.Generic;','','namespace LegendOfMortal_InternalMod.Core','{',
'    /// <summary>',
'    /// 養成場所與場所隨機事件的中文名稱（由 tools/game-extract/training_names.py 產生，請勿手改）。',
'    /// 事件名稱依活俠傳 wiki〈養成指令〉表，以「指令＋機率權重＋心相變化」比對遊戲資料；',
'    /// wiki 沒收錄的用遊戲開發備註，再沒有就用結果類別（失敗／成功…）。',
'    /// </summary>',
'    public static class TrainingNames','    {',
'        /// <summary>場所 ID（PositionResultData 名稱）→「設施・指令」。</summary>',
'        public static readonly Dictionary<string, string> Places = new()','        {']
L+= [f'            ["{k}"] = "{esc(v)}",' for k,v in zip(pk,pv)]
L+=['        };','','        /// <summary>「場所 ID/結果編號」→ 事件名稱。</summary>','        public static readonly Dictionary<string, string> Events = new()','        {']
L+= [f'            ["{k}"] = "{esc(v)}",' for k,v in zip(ek,evv)]
L+=['        };','    }','}','']
open(OUT,'w',encoding='utf-8').write('\n'.join(L))
print(len(pk),len(ek))
