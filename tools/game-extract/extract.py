#!/usr/bin/env python3
"""
從本機安裝的《活俠傳》抽出「事件判定」資料。

用途：把遊戲內的 MissionData / MissionCheckData / FlagData / LibraryItemData
（存於 Mortal_Data/globalgamemanagers.assets）、劇情檢查點 SwitchResultData
（存於 Mortal_Data/sharedassets*.assets）與 Lua 劇本（存於 Mortal_Data/resources.assets）
轉成 JSON，供 tools/guide-verify 比對攻略內容。

用法：
    pip install UnityPy TypeTreeGeneratorAPI
    python tools/game-extract/extract.py "/path/to/LegendOfMortal" -o tools/game-extract/out

產出（只留驗證需要的最低資料：名稱、編號、旗標值，不含劇本內文與說明文字）：
    out/flags.json     旗標編號 → 預設值、開發備註列出的值（只留數字）、是否出現在遊戲程式碼（code）
    out/missions.json  任務名稱 → 檢查點編號、時間、起始劇本名稱
    out/endings.json   汗青書編號與中文名稱（LibraryItemData 20000~20065）
    out/scripts.json   劇本名稱 → 開啟的結局、寫入／累加的旗標、互指的劇本名稱、結局開啟點的旗標條件
    out/switches.json  劇情檢查點（checkpointmanager.Switch）名稱 → 各分支條件（旗標／常數比較）
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
    # 先用「不套 typetree」的方式辨識 MonoBehaviour 的腳本類別，
    # 之後再掛上 TypeTreeGenerator 才能讀出自訂欄位。
    groups = defaultdict(list)
    for o in env.objects:
        if o.type.name != "MonoBehaviour":
            continue
        try:
            d = o.read(check_read=False)
            cls = d.m_Script.read().m_Name
        except Exception:
            continue
        groups[cls].append(o)

    gen = TypeTreeGenerator(env.assets[0].unity_version)
    gen.load_local_game(os.path.abspath(args.game))
    env.typetree_generator = gen

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
            flags[d.m_Name] = {"default": d._default, "values": note_values(d._devNote)}

    # 戰鬥結果等旗標由遊戲程式（C#）直接寫入，不經過 Lua；名稱若出現在 Managed/*.dll 的字串常數裡，
    # 就標成 code，驗證時視為「任何值都可能」，避免把這類分支誤判成走不到
    for name in code_flags(data, flags):
        flags[name]["code"] = True

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
            checks.append({"key": c._key, "time": time_of(c), "startScript": c._startScript})
        missions.append({"name": m._name, "checks": checks})

    # ---------------- 汗青書 ----------------
    # LibraryItemData 的 _title 只是開發用舊名（例：20000＝「逃亡」），
    # 實際顯示名稱在語系字串表（level0：Library/Title/<id>），找不到才退回舊名。
    localization = read_localization(data, "level0")
    endings = []
    for o in groups.get("LibraryItemData", []):
        d = dump_obj(o)
        if not d:
            continue
        if not re.fullmatch(r"2\d{4}", d.m_Name):
            continue
        endings.append({"id": d.m_Name, "no": int(d.m_Name) - 19999,
                        "title": localization.get("Title/" + d.m_Name) or d._title})
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
    # AddFlag 是累加，值無法靜態列舉；驗證時把這些旗標當成「任何值都可能」
    add_pat = re.compile(r'statmodifymanager\.AddFlag\(\s*"([^"]+)"')
    next_pat = re.compile(r'luamanager\.SetNextScript\(\s*"([^"]+)"')
    for name, txt in texts:
        e = scripts.setdefault(name, {"openEndings": set(), "setFlags": set(), "addFlags": set(),
                                      "nextScripts": set(), "refs": set(), "endingOpens": {}})
        for m in open_pat.finditer(txt):
            e["openEndings"].add(m.group(1))
        for m in flag_pat.finditer(txt):
            e["setFlags"].add((m.group(1), int(m.group(2))))
        for m in add_pat.finditer(txt):
            e["addFlags"].add(m.group(1))
        for m in next_pat.finditer(txt):
            e["nextScripts"].add(m.group(1))
        for q in quote.findall(txt):
            if q in known and q != name:
                e["refs"].add(q)
        for op in ending_opens(txt):
            e["endingOpens"][(op["id"], json.dumps(op["guards"], sort_keys=True))] = op

    out_scripts = {}
    for name, e in scripts.items():
        if not (e["openEndings"] or e["setFlags"] or e["addFlags"] or e["nextScripts"] or e["refs"]):
            continue
        out_scripts[name] = {
            "openEndings": sorted(e["openEndings"]),
            "setFlags": [{"flag": f, "value": v} for f, v in sorted(e["setFlags"])],
            "addFlags": sorted(e["addFlags"]),
            "nextScripts": sorted(e["nextScripts"]),
            "refs": sorted(e["refs"]),
            "endingOpens": sorted(e["endingOpens"].values(),
                                  key=lambda x: (x["id"], json.dumps(x["guards"], sort_keys=True))),
        }
    scripts = out_scripts

    switches = read_switches(args.game, data)

    os.makedirs(args.out, exist_ok=True)
    write(os.path.join(args.out, "flags.json"), flags)
    write(os.path.join(args.out, "missions.json"), missions)
    write(os.path.join(args.out, "endings.json"), endings)
    write(os.path.join(args.out, "scripts.json"), scripts)
    write(os.path.join(args.out, "switches.json"), switches)
    print(f"flags={len(flags)} missions={len(missions)} checks={sum(len(m['checks']) for m in missions)} "
          f"endings={len(endings)} scripts={len(scripts)} switches={len(switches)}")


def note_values(note):
    """開發備註裡列出的值域（例：「1:青城 2:崆峒」→ [1, 2]），只留數字，不帶說明文字。"""
    values = set()
    for line in re.split(r"[\r\n]", note or ""):
        m = re.match(r"\s*(-?\d+)\s*[:.．]\s*\S", line)
        if m:
            values.add(int(m.group(1)))
    return sorted(values)


def gt(t):
    return None if t is None else [t.Year, t.Month, t.Stage]


def time_of(c):
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
    `Ch_...`／`choice_...` 則是玩家選項（一定可選）。回傳的 guards 就是該結局的分支條件；
    其他 Lua 條件只記成 {"kind": "script"}，不保留劇本原文。
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
                    guards.append({"kind": "script"})
                    continue
                key, value = mm.group(1), int(mm.group(2))
            fk = FLAG_KEY.match(key)
            guards.append({"kind": "flag", "key": fk.group(1), "value": int(fk.group(2))} if fk
                          else {"kind": "checkpoint", "key": key, "value": value})
        result.append({"id": m.group(1), "guards": guards})
    return result


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



def code_flags(data, flags):
    """找出名稱出現在遊戲程式 DLL（UTF-16 字串常數）裡的旗標。"""
    import glob
    pat = re.compile(rb"(?:[A-Za-z0-9_]\x00){4,16}")
    seen = set()
    for dll in glob.glob(os.path.join(data, "Managed", "*.dll")):
        blob = open(dll, "rb").read()
        for m in pat.finditer(blob):
            seen.add(m.group().decode("utf-16le"))
    return sorted(seen & set(flags))


def read_switches(game, data):
    """抽出所有劇情檢查點（SwitchResultData）的分支條件。

    遊戲的 Switch 依序檢查 _items，回傳第一個成立的分支編號（從 1 起）；全部不成立時回傳
    「分支數＋1」。_useCount 為真時改依執行次數判斷（_countItems），這裡只標記，不展開。
    每個比較只保留驗證需要的兩種值：旗標名稱與常數；其他數值（屬性、好感、心上人等）
    記成 {"other": 名稱}，驗證時一律視為可能成立。
    """
    import glob
    import UnityPy
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

    paths = [os.path.join(data, "globalgamemanagers.assets")] + sorted(glob.glob(os.path.join(data, "sharedassets*.assets")))
    envs = {os.path.basename(p): UnityPy.load(p) for p in paths if os.path.isfile(p)}
    # 先辨識類別（不套 typetree），再掛上 TypeTreeGenerator 讀欄位，順序與 main() 相同
    found = []
    for name, env in envs.items():
        for o in env.assets[0].objects.values():
            if o.type.name != "MonoBehaviour":
                continue
            try:
                d = o.read(check_read=False)
                if d.m_Script.read().m_Name == "SwitchResultData":
                    found.append((name, o))
            except Exception:
                continue
    gen = TypeTreeGenerator(next(iter(envs.values())).assets[0].unity_version)
    gen.load_local_game(os.path.abspath(game))
    files = {}
    for name, env in envs.items():
        env.typetree_generator = gen
        files[name] = env.assets[0]

    def deref(owner, pp):
        """PPtr 的 m_FileID 以「物件所在檔案」的 externals 為準（0＝同一檔案）。"""
        if not pp or pp.get("m_PathID", 0) == 0:
            return None, None
        f = owner if pp["m_FileID"] == 0 else files.get(os.path.basename(owner.externals[pp["m_FileID"] - 1].path))
        if f is None:
            return None, None
        try:
            return f, f.objects[pp["m_PathID"]].read_typetree()
        except Exception:
            return None, None

    group_cache = {}

    def value_of(owner, pp):
        key = (owner.name, pp["m_FileID"], pp["m_PathID"])
        if key in group_cache:
            return group_cache[key]
        f, g = deref(owner, pp)
        v = {"other": "?"}
        if g is not None:
            refs = g.get("_value") or []
            v = {"other": g.get("m_Name", "?")}
            if len(refs) == 1 and not refs[0].get("_negative"):
                r = refs[0]
                if r.get("_checkType") == 0:
                    v = {"const": r.get("_constant", 0)}
                elif r.get("_flag") and r["_flag"].get("m_PathID"):
                    _, fl = deref(f, r["_flag"])
                    if fl is not None:
                        v = {"flag": fl["m_Name"]}
        group_cache[key] = v
        return v

    out = {}
    for fname, o in found:
        owner = files[fname]
        try:
            t = o.read_typetree()
        except Exception as e:
            print(f"warn: switch {o.path_id} {e}", file=sys.stderr)
            continue
        sw = {"useCount": bool(t.get("_useCount")), "branches": []}
        for it in t.get("_items") or []:
            sw["branches"].append({
                "or": it.get("_logicOp", 0) != 0,
                "items": [[value_of(owner, c["_value1"]), c["_compareOp"], value_of(owner, c["_value2"])]
                          for c in it.get("_items") or []],
            })
        # 同名的檢查點可能不只一份；全部保留，驗證時任一份可能成立即可
        out.setdefault(t["m_Name"], []).append(sw)
    return out


def write(path, obj):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False, indent=1, sort_keys=True)


if __name__ == "__main__":
    main()
