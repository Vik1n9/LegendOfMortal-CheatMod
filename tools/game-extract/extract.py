#!/usr/bin/env python3
"""
從本機安裝的《活俠傳》抽出「事件判定」資料。

用途：把遊戲內的 MissionData / MissionCheckData / FlagData / LibraryItemData
（存於 Mortal_Data/globalgamemanagers.assets）與 Lua 劇本
（存於 Mortal_Data/resources.assets）轉成 JSON，供 tools/guide-verify 比對攻略內容。

用法：
    pip install UnityPy TypeTreeGeneratorAPI
    python tools/game-extract/extract.py "/path/to/LegendOfMortal" -o tools/game-extract/out

產出：
    out/flags.json     旗標編號 → 開發備註（值域說明）、預設值
    out/missions.json  任務 → 檢查點（時間、地點、條件、成功/失敗時寫入的旗標）
    out/endings.json   汗青書（LibraryItemData 20000~20065）
    out/scripts.json   Lua 劇本 → 開啟的結局、寫入的旗標、下一個劇本
"""
import argparse, json, os, re, sys
from collections import defaultdict


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("game", help="遊戲安裝目錄（含 Mortal.exe）")
    ap.add_argument("-o", "--out", default=os.path.join(os.path.dirname(__file__), "out"))
    args = ap.parse_args()

    import UnityPy
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

    data = os.path.join(args.game, "Mortal_Data")
    assets = os.path.join(data, "globalgamemanagers.assets")
    if not os.path.isfile(assets):
        sys.exit(f"找不到 {assets}")

    env = UnityPy.load(assets)
    # 先用「不套 typetree」的方式辨識 MonoBehaviour 的腳本類別與名稱，
    # 之後再掛上 TypeTreeGenerator 才能讀出自訂欄位。
    groups = defaultdict(list)
    name_of = {}
    for o in env.objects:
        if o.type.name != "MonoBehaviour":
            continue
        try:
            d = o.read(check_read=False)
            cls = d.m_Script.read().m_Name
        except Exception:
            continue
        groups[cls].append(o)
        name_of[o.path_id] = (cls, d.m_Name)

    gen = TypeTreeGenerator(env.assets[0].unity_version)
    gen.load_local_game(os.path.abspath(args.game))
    env.typetree_generator = gen

    def read(o):
        return o.read()

    def pptr_name(p):
        if p is None or p.m_PathID == 0:
            return None
        return name_of.get(p.m_PathID, (None, None))[1]

    def dump_obj(o):
        try:
            return o.read() if hasattr(o, "read") else o
        except Exception as e:
            print(f"warn: {getattr(o, 'path_id', '?')} {e}", file=sys.stderr)
            return None

    # ---------------- 旗標 ----------------
    flags = {}
    for o in groups.get("FlagData", []):
        d = dump_obj(o)
        if d:
            flags[d.m_Name] = {"note": d._devNote, "default": d._default}

    # ---------------- 任務與檢查點 ----------------
    missions = []
    for o in groups.get("MissionData", []):
        m = dump_obj(o)
        if not m:
            continue
        checks = []
        for p in m._missions:
            c = dump_obj(p.read())
            if not c:
                continue
            conds = []
            for ci in c._conditions:
                items = []
                for it in ci._items:
                    items.append({
                        "op": ["Equal", "NotEqual", "Less", "LessEqual", "Greater", "GreaterEqual"][it._compareOp],
                        "left": var_of(it._value1, name_of, pptr_name),
                        "right": var_of(it._value2, name_of, pptr_name),
                    })
                conds.append({"logic": "OR" if ci._logicOp else "AND",
                              "devNote": ci._devNote, "items": items})
            checks.append({
                "key": c._key,
                "active": bool(c._active),
                "desc": c._desc,
                "position": POSITIONS[c._position] if c._position < len(POSITIONS) else str(c._position),
                "beforeMainMission": bool(c._beforeMainMission),
                "dayCheck": bool(c._dayCheck),
                "time": time_of(c, name_of, pptr_name),
                "startScript": c._startScript,
                "nextState": c._nextStateName,
                "conditions": conds,
                "setWhenTrue": [{"flag": pptr_name(f._data) or f._data.m_PathID, "value": f._value}
                                for f in (c._modifyFlagsWhenTrue or [])],
                "setWhenFalse": [{"flag": pptr_name(f._data) or f._data.m_PathID, "value": f._value}
                                 for f in (c._modifyFlagsWhenFalse or [])],
            })
        missions.append({"name": m._name, "desc": m._desc, "init": m._initKey,
                         "current": m._currentKey, "checks": checks})

    # ---------------- 汗青書 ----------------
    # LibraryItemData 的 _title 只是開發用舊名（例：20000＝「逃亡」），
    # 實際顯示名稱在語系字串表（level0：Library/Title/<id>）。兩種都輸出，讓驗證器比對。
    localization = read_localization(data, "level0")
    endings = []
    for o in groups.get("LibraryItemData", []):
        d = dump_obj(o)
        if not d:
            continue
        if not re.fullmatch(r"2\d{4}", d.m_Name):
            continue
        endings.append({"id": d.m_Name, "no": int(d.m_Name) - 19999,
                        "title": localization.get("Title/" + d.m_Name) or d._title,
                        "desc": localization.get("Desc/" + d.m_Name) or d._desc,
                        "devTitle": d._title, "count": d._count})
    endings.sort(key=lambda e: e["no"])

    # ---------------- Lua 劇本 ----------------
    # 除了明確的 SetNextScript，劇本之間也會用其他 API 互指（對話、劇情管理器等），
    # 因此把「任何字串常數剛好是另一個劇本名稱」都當成一條邊，用來算可達性。
    scripts = {}
    env2 = UnityPy.load(os.path.join(data, "resources.assets"))
    texts = []
    for o in env2.objects:
        if o.type.name != "TextAsset":
            continue
        d = o.read()
        texts.append((d.m_Name, d.m_Script if isinstance(d.m_Script, str) else d.m_Script.decode("utf-8", "ignore")))
    # 同名 TextAsset 會有兩份（新舊版本並存），內容不同，必須合併而不是覆蓋。
    known = {n for n, _ in texts}
    quote = re.compile(r'"([^"\n]{1,80})"')
    open_pat = re.compile(r'endgamepanel\.Open\(\s*"([^"]+)"')
    flag_pat = re.compile(r'statmodifymanager\.SetFlag\(\s*"([^"]+)"\s*,\s*(-?\d+)\s*\)')
    next_pat = re.compile(r'luamanager\.SetNextScript\(\s*"([^"]+)"')
    for name, txt in texts:
        e = scripts.setdefault(name, {"openEndings": set(), "setFlags": set(),
                                      "nextScripts": set(), "refs": set(), "endingOpens": {}})
        for m in open_pat.finditer(txt):
            e["openEndings"].add(m.group(1))
        for m in flag_pat.finditer(txt):
            e["setFlags"].add((m.group(1), int(m.group(2))))
        for m in next_pat.finditer(txt):
            e["nextScripts"].add(m.group(1))
        for q in quote.findall(txt):
            if q in known and q != name:
                e["refs"].add(q)
        for op in ending_opens(txt):
            e["endingOpens"][(op["id"], json.dumps(op["guards"], sort_keys=True))] = op

    out_scripts = {}
    for name, e in scripts.items():
        if not (e["openEndings"] or e["setFlags"] or e["nextScripts"] or e["refs"]):
            continue
        out_scripts[name] = {
            "openEndings": sorted(e["openEndings"]),
            "setFlags": [{"flag": f, "value": v} for f, v in sorted(e["setFlags"])],
            "nextScripts": sorted(e["nextScripts"]),
            "refs": sorted(e["refs"]),
            "endingOpens": sorted(e["endingOpens"].values(), key=lambda x: (x["id"], x["line"])),
        }
    scripts = out_scripts

    os.makedirs(args.out, exist_ok=True)
    write(os.path.join(args.out, "flags.json"), flags)
    write(os.path.join(args.out, "missions.json"), missions)
    write(os.path.join(args.out, "endings.json"), endings)
    write(os.path.join(args.out, "scripts.json"), scripts)
    write_report(os.path.join(args.out, "report.md"), missions, endings)
    print(f"flags={len(flags)} missions={len(missions)} checks={sum(len(m['checks']) for m in missions)} "
          f"endings={len(endings)} scripts={len(scripts)}")


POSITIONS = ["無", "正心堂", "校場", "煉丹房", "鍛冶場", "後山", "弟子房", "大門",
             "講經堂", "伙房", "女弟子房", "神秘房子"]
CHECK_TYPE = ["常數", "角色數值", "好感度", "旗標", "書籍", "雜物", "貴重品", "隨機值",
              "數值群組", "遊戲時間", "技能", "秘笈等級", "已開發項目", "開發項目等級",
              "書籍精通", "風雲史", "條件設定"]
TIME_CHECK = {0: "無", 1: "特定", 2: "區間", 3: "週期"}


def gt(t):
    return None if t is None else [t.Year, t.Month, t.Stage]


def time_of(c, name_of, pptr_name):
    t = c._timeCheckType
    if not c._dayCheck:
        return {"type": "無"}
    if t == 1:
        return {"type": "特定", "at": gt(c._specificTime)}
    if t == 2:
        return {"type": "區間", "from": gt(c._minTime), "to": gt(c._maxTime)}
    if t == 3:
        return {"type": "週期", "from": gt(c._minTime), "to": gt(c._maxTime),
                "stage": c._periodStage}
    return {"type": "無"}


SWITCH_DECL = re.compile(r'local\s+(switch_\w+)_result\s*=\s*checkpointmanager\.Switch\("([^"]+)"\)')
FLAG_KEY = re.compile(r'^([A-Z]\d{5})_(\d+)$')


def ending_opens(text):
    """找出每個 endgamepanel.Open("2xxxx")，以及包住它的 if 條件。

    遊戲腳本用 `checkpointmanager.Switch("M20008_02")` 表示「旗標 M20008 == 2」；
    `Ch_...`／`choice_...` 則是玩家選項（一定可選）。回傳的 guards 就是該結局的分支條件。
    """
    lines = text.split("\n")
    sw = {m.group(1): m.group(2) for m in SWITCH_DECL.finditer(text)}
    stack = []  # (indent, kind, condition)
    result = []
    for i, line in enumerate(lines):
        s = line.strip()
        ind = len(line) - len(line.lstrip("\t"))
        if s == "end" or s.startswith("end)") or s.startswith("end "):
            if stack and stack[-1][0] == ind:
                stack.pop()
        elif s.startswith("elseif ") or s == "else":
            if stack and stack[-1][0] == ind and stack[-1][1] == "if":
                stack[-1] = (ind, "if", s if s != "else" else None)
        elif s.startswith("if ") and s.endswith("then"):
            stack.append((ind, "if", s))
        elif s.startswith("for ") or s.startswith("while ") or s == "do" or s.startswith("function"):
            stack.append((ind, "other", None))

        m = re.search(r'endgamepanel\.Open\("(\d+)"\)', line)
        if not m:
            continue
        guards = []
        for _, kind, cond in stack:
            if kind != "if" or not cond:
                continue
            mm = re.match(r'(?:else)?if\s+(switch_\w+)_result\s*==\s*(\d+)', cond)
            if mm:
                key, value = sw.get(mm.group(1), mm.group(1)), int(mm.group(2))
            else:
                mm = re.search(r'checkpointmanager\.Switch\("([^"]+)"\)\s*==\s*(\d+)', cond)
                if not mm:
                    text = cond
                    for pre in ("elseif ", "if "):
                        if text.startswith(pre):
                            text = text[len(pre):]
                            break
                    if text.endswith(" then"):
                        text = text[:-5]
                    guards.append({"kind": "script", "text": text})
                    continue
                key, value = mm.group(1), int(mm.group(2))
            fk = FLAG_KEY.match(key)
            guards.append({"kind": "flag", "key": fk.group(1), "value": int(fk.group(2))} if fk
                          else {"kind": "checkpoint", "key": key, "value": value})
        result.append({"id": m.group(1), "line": i + 1, "guards": guards})
    return result


def var_of(p, name_of, pptr_name):
    """把 StatGroupVariable PPtr 轉成可判讀的結構。"""
    if p is None or p.m_PathID == 0:
        return None
    name = pptr_name(p)
    try:
        v = p.read()
    except Exception:
        return {"name": name}
    if not hasattr(v, "_groupType"):
        return {"name": name}
    refs = []
    for r in v._value:
        ct = CHECK_TYPE[r._checkType] if r._checkType < len(CHECK_TYPE) else str(r._checkType)
        ref = {"type": ct, "negative": bool(r._negative)}
        target = None
        if ct == "常數":
            ref["constant"] = r._constant
        elif ct == "旗標":
            target = pptr_name(r._flag)
            ref["flag"] = target
        elif ct == "角色數值":
            target = pptr_name(r._stat)
            ref["stat"] = target
        elif ct == "好感度":
            target = pptr_name(r._relationship)
            ref["relationship"] = target
        elif ct == "隨機值":
            ref["random"] = [r._randomMin, r._randomMax]
        elif ct == "貴重品":
            ref["item"] = pptr_name(r._specialItem)
        elif ct == "書籍":
            ref["book"] = pptr_name(r._book)
        elif ct == "數值群組":
            ref["group"] = pptr_name(r._statGroup)
        elif ct == "遊戲時間":
            bref = pptr_name(r._gameTime)
            ref["gameTime"] = bref
            try:
                g = r._gameTime.read().GameTime
                ref["at"] = [g.Year, g.Month, g.Stage]
            except Exception:
                pass
        elif ct == "技能":
            ref["talent"] = pptr_name(r._talentData)
        elif ct == "風雲史":
            ref["library"] = pptr_name(r._libraryAchieve)
        refs.append(ref)
    return {"name": name, "group": ["Sum", "Max", "Min", "Multiply", "And", "Or"][v._groupType],
            "refs": refs}



HANGUL = re.compile(r"[\uac00-\ud7a3]")
CJK = re.compile(r"[\u3400-\u9fff]")


def read_localization(data_dir, filename):
    """從 Unity 語系字串表抽出中文條目。

    字串表把 key 與各語言的值連續存放，格式為：
        <4-byte 長度><key><padding><4-byte 長度><value>
    同一個 key 會出現多次（每個語言一次），這裡只留含中文、不含韓文的版本。
    """
    path = os.path.join(data_dir, filename)
    if not os.path.isfile(path):
        return {}
    blob = open(path, "rb").read()
    out = {}
    for m in re.finditer(rb"Library/(Title|Desc)/(\d{5})", blob):
        start = m.end()
        for skip in range(0, 4):
            p = start + skip
            if p + 4 > len(blob):
                break
            n = int.from_bytes(blob[p:p + 4], "little")
            if 0 < n < 2000 and p + 4 + n <= len(blob):
                try:
                    text = blob[p + 4:p + 4 + n].decode("utf-8")
                except UnicodeDecodeError:
                    continue
                if CJK.search(text) and not HANGUL.search(text):
                    key = m.group(1).decode() + "/" + m.group(2).decode()
                    out.setdefault(key, text)
                    break
    return out



OPS = {"Equal": "=", "NotEqual": "≠", "Less": "<", "LessEqual": "≤",
       "Greater": ">", "GreaterEqual": "≥"}
GROUPS = {"Sum": "合計", "Max": "取最大", "Min": "取最小", "Multiply": "相乘", "And": "全部成立", "Or": "任一成立"}


def text_var(v):
    if not v:
        return "（未設定）"
    refs = v.get("refs") or []
    parts = []
    for r in refs:
        t = r.get("type")
        if t == "常數":
            body = str(r.get("constant"))
        elif t == "旗標":
            body = "旗標 " + str(r.get("flag"))
        elif t == "角色數值":
            body = "數值 " + str(r.get("stat"))
        elif t == "好感度":
            body = "好感 " + str(r.get("relationship"))
        elif t == "遊戲時間":
            at = r.get("at")
            body = "時間 " + ("%d年%d月%s" % (at[0], at[1], {1: "上旬", 2: "中旬", 3: "下旬"}.get(at[2], at[2])) if at else str(r.get("gameTime")))
        elif t == "隨機值":
            body = "隨機 %s~%s" % tuple(r.get("random") or ("?", "?"))
        elif t == "貴重品":
            body = "貴重品 " + str(r.get("item"))
        elif t == "書籍":
            body = "書 " + str(r.get("book"))
        elif t == "技能":
            body = "技能 " + str(r.get("talent"))
        elif t == "數值群組":
            body = "群組 " + str(r.get("group"))
        elif t == "風雲史":
            body = "風雲史 " + str(r.get("library"))
        else:
            body = t
        if r.get("negative"):
            body = "-" + body
        parts.append(body)
    if not parts:
        return v.get("name") or "（空）"
    if len(parts) == 1:
        return parts[0]
    return "%s（%s）" % (GROUPS.get(v.get("group"), v.get("group")), "、".join(parts))


def text_time(t):
    if not t or t.get("type") == "無":
        return "不限時間"

    def s(v):
        return "%d年%d月%s" % (v[0], v[1], {1: "上旬", 2: "中旬", 3: "下旬"}.get(v[2], v[2])) if v else "?"

    if t["type"] == "特定":
        return s(t.get("at"))
    if t["type"] == "區間":
        return s(t.get("from")) + " ～ " + s(t.get("to"))
    return s(t.get("from")) + " 起（週期）"


def text_cond(c):
    items = ["%s %s %s" % (text_var(i.get("left")), OPS.get(i.get("op"), i.get("op")), text_var(i.get("right")))
             for i in c["items"]]
    return (" %s " % ("且" if c["logic"] == "AND" else "或")).join(items) if items else "（無條件）"


def write_report(path, missions, endings):
    out = ["# 遊戲內事件判定（自動產生）", "",
           "由 `tools/game-extract/extract.py` 從遊戲本體抽出，僅供整理攻略時對照，請勿手改。", ""]
    for m in missions:
        out.append("")
        out.append("## %s%s" % (m["name"], ("　" + m["desc"]) if m["desc"] else ""))
        for c in m["checks"]:
            out.append("")
            out.append("- 檢查點：`%s`%s%s" % (c["key"], "（停用）" if not c["active"] else "",
                                            "　地點：" + c["position"] if c["position"] != "無" else ""))
            out.append("  - 時間：%s" % text_time(c["time"]))
            if c["conditions"]:
                for cond in c["conditions"]:
                    out.append("  - 條件：%s" % text_cond(cond))
            if c["startScript"]:
                out.append("  - 劇本：`%s`" % c["startScript"])
            if c["setWhenTrue"]:
                out.append("  - 成立時設定：" + "、".join("%s=%d" % (f["flag"], f["value"]) for f in c["setWhenTrue"]))
            if c["setWhenFalse"]:
                out.append("  - 不成立時設定：" + "、".join("%s=%d" % (f["flag"], f["value"]) for f in c["setWhenFalse"]))
    out += ["", "# 汗青書", ""]
    for e in endings:
        out.append("- %d　%s" % (e["no"], e["title"]))
    open(path, "w", encoding="utf-8").write("\n".join(out) + "\n")


def write(path, obj):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=1, sort_keys=True)


if __name__ == "__main__":
    main()
