# 1) 抽 resources.assets 的 Lua 劇本到 lua.json；2) 從 level0 抽 Position/title、Story/P_ 中文
import sys,os,re,json,UnityPy
game=sys.argv[1]; data=os.path.join(game,"Mortal_Data")
env=UnityPy.load(os.path.join(data,"resources.assets"))
texts={}
for o in env.objects:
    if o.type.name!="TextAsset": continue
    d=o.read(); t=d.m_Script if isinstance(d.m_Script,str) else d.m_Script.decode("utf-8","ignore")
    texts.setdefault(d.m_Name.lower(),[]).append(t)
json.dump(texts,open(sys.argv[2],'w'),ensure_ascii=False)
blob=open(os.path.join(data,"level0"),"rb").read()
CJK=re.compile(r"[㐀-鿿]"); HAN=re.compile(r"[가-힣]"); KANA=re.compile(r"[぀-ヿ]")
# 同一個 key 有繁、簡、日文多份；只收全部字元都在 Big5 內的（繁中）
def big5(t):
    try: t.encode("big5"); return True
    except UnicodeEncodeError: return False
loc={}
for m in re.finditer(rb"(Position/(?:title|type)/[A-Za-z0-9_]+|Story/P_[A-Za-z0-9_]+|PlayerStat/[A-Za-z0-9_]+)",blob):
    for skip in range(0,4):
        p=m.end()+skip
        n=int.from_bytes(blob[p:p+4],"little")
        if 0<n<300:
            try: t=blob[p+4:p+4+n].decode("utf-8")
            except UnicodeDecodeError: continue
            if CJK.search(t) and not HAN.search(t) and not KANA.search(t) and big5(t):
                loc.setdefault(m.group(1).decode(),t); break
json.dump(loc,open(sys.argv[3],'w'),ensure_ascii=False,indent=0)
print(len(texts),len(loc),[ (k,v) for k,v in loc.items()][:12])
