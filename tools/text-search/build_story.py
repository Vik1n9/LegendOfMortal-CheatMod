"""從本機《活俠傳》抽出每個 Lua 劇本的繁中文本，寫成 out/story.json（含遊戲原文，不提交）。

每個劇本依出場順序收錄：對白（含說話者、旁白、心想）、選項（含有條件／停用的選項）、
擲骰選項（含擲骰標題與點數條件）、風雲史條目（AddStory 寫入的劇情摘要）。

  Mortal_Data/resources.assets  Lua 劇本：say(…GetStoryText("D_…"))、選項表 "O_…"、Dice(…)、AddStory(…)
  Mortal_Data/level0            語系字串表：Story/、Character/、LegendInfo/、DiceHeader/，同一鍵有簡、繁、韓三份

用法：
  tools/game-extract/.venv/bin/python tools/text-search/build_story.py "$GAME" [-o tools/text-search/out]
"""
import argparse
import json
import os
import re
import subprocess
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
CJK = re.compile(r"[一-鿿]")
HANGUL = re.compile(r"[가-힣]")
KEY = re.compile(rb"(Story|Character|LegendInfo|DiceHeader)/")


def read_table(level0_path):
    """回傳 {(類別, 鍵): 繁中字串}。

    條目格式為 Unity 字串：4 位元組長度＋UTF-8，對齊到 4 位元組；鍵後面緊接值。
    同一鍵有簡中、繁中、韓文多份，排列順序不固定；取能用 Big5 編碼的中文版本。
    """
    blob = open(level0_path, "rb").read()
    cands = defaultdict(list)
    for m in KEY.finditer(blob):
        s = m.start()
        klen = int.from_bytes(blob[s - 4:s], "little")
        if not 6 < klen < 200:
            continue
        try:
            key = blob[s:s + klen].decode("ascii")
        except UnicodeDecodeError:
            continue
        if not re.fullmatch(r"(Story|Character|LegendInfo|DiceHeader)/[A-Za-z0-9_]+", key):
            continue
        p = s + klen
        p += (-p) % 4
        vlen = int.from_bytes(blob[p:p + 4], "little")
        if not 0 < vlen < 20000:
            continue
        try:
            val = blob[p + 4:p + 4 + vlen].decode("utf-8")
        except UnicodeDecodeError:
            continue
        cat, name = key.split("/", 1)
        cands[(cat, name)].append(val)
    table = {}
    for k, vals in cands.items():
        zh = [v for v in vals if CJK.search(v) and not HANGUL.search(v)]
        if zh:
            # 簡中常有 Big5 沒有的字（赵、这…），繁中偶爾也有（祢、・），所以取 Big5 外字元最少的一份
            table[k] = min(reversed(zh), key=non_big5)
    return table


def non_big5(text):
    n = 0
    for ch in text:
        try:
            ch.encode("cp950")
        except UnicodeEncodeError:
            n += 1
    return n


def read_lua(resources_path):
    import UnityPy

    env = UnityPy.load(resources_path)
    scripts = {}
    for o in env.objects:
        if o.type.name != "TextAsset":
            continue
        d = o.read()
        t = d.m_Script if isinstance(d.m_Script, str) else d.m_Script.decode("utf-8", "ignore")
        scripts.setdefault(d.m_Name.lower(), t)
    return scripts


SAY = re.compile(r'say\(luamanager\.GetStoryText\("([^"]+)"\)\)')
SETCHAR = re.compile(r'setcharacter\(characters\.Get\("([^"]+)"\)')
DIALOG = re.compile(r"setsaydialog\(saydialogs\.(\w+)\)")
# 一般選項    option1_1[1] = "O_…"
# 有條件選項  option_001[2] = S0301_02 and "O_…" or "~O_…"（條件不成立時顯示為停用）
# 擲骰選項    dice_001[3] = "O_…|>=66"（| 後是點數條件）
OPTION = re.compile(r'(option|dice)\w*\[\d+\]\s*=\s*(?:(\w+)\s+and\s+)?"~?([^"|]+)(?:\|([^"]*))?"')
CONDITION = re.compile(r'(\w+)\s*=\s*checkpointmanager\.Condition\("([^"]*)"\)')
DEAD_COND = re.compile(r"^(xxx|.*_temp)$", re.I)  # 劇本用不存在的條件名稱停用選項（見 tools/game-extract）
DICE = re.compile(r'checkpointmanager\.Dice\("([^"]+)"')
CHOOSE = re.compile(r"choose\(|ExecuteRoll\(")
LEGEND = re.compile(r'statmodifymanager\.AddStory\("([^"]+)"\)')
NEXT = re.compile(r'luamanager\.SetNextScript\(\s*"([^"]+)"')


def norm(text):
    return re.sub(r"\s*\r?\n\s*", "", text).strip()


def parse_script(src, table, used_legend):
    lines = []
    speaker = None
    mode = "character"
    opts = []
    nexts = []
    conds = dict(CONDITION.findall(src))  # 條件變數 → 條件名稱
    dice_header = None
    for raw in src.splitlines():
        if m := DIALOG.search(raw):
            mode = m.group(1)
            continue
        if m := SETCHAR.search(raw):
            speaker = m.group(1)
            continue
        if m := DICE.search(raw):
            dice_header = norm(table.get(("DiceHeader", m.group(1)), ""))
            continue
        if m := OPTION.search(raw):
            opts.append(m.groups())
            continue
        if CHOOSE.search(raw) and opts:
            texts = []
            for kind, var, key, roll in opts:
                # 少數選項沒有字串鍵，直接把簡中原文寫在劇本裡；main() 已先轉成繁體放進 table
                t = norm(table.get(("Story", key)) or table.get(("Literal", key), ""))
                if not t:
                    continue
                notes = []
                if roll:
                    notes.append(roll)
                if var:
                    notes.append("停用" if DEAD_COND.match(conds.get(var, "")) else "需條件")
                texts.append(t + (f"（{'，'.join(notes)}）" if notes else ""))
            if texts:
                if opts[0][0] == "dice":
                    lines.append(["擲骰", (f"{dice_header}：" if dice_header else "") + " ／ ".join(texts)])
                else:
                    lines.append(["選項", " ／ ".join(texts)])
            opts = []
            dice_header = None
            continue
        if m := LEGEND.search(raw):
            text = table.get(("LegendInfo", m.group(1)))
            if text:
                used_legend.add(m.group(1))
                lines.append(["風雲史", norm(text)])
            continue
        if m := NEXT.search(raw):
            nexts.append(m.group(1).lower())
            continue
        if m := SAY.search(raw):
            text = table.get(("Story", m.group(1)))
            if not text:
                continue
            if mode == "narrative":
                who = "旁白"
            elif mode == "think":
                who = name_of(speaker or "player", table) + "（心想）"
            elif mode.startswith("center"):
                who = "字幕"
            else:
                who = name_of(speaker, table)
            lines.append([who, norm(text)])
    return lines, sorted(set(nexts))


def s2tw(texts):
    """簡轉繁（台灣用字）。需要系統的 opencc 指令；沒有就原樣保留並提醒。"""
    if not texts:
        return []
    try:
        out = subprocess.run(
            ["opencc", "-c", "s2tw"], input="\n".join(texts), capture_output=True, text=True, check=True
        ).stdout.rstrip("\n").split("\n")
    except (OSError, subprocess.CalledProcessError):
        print("警告：找不到 opencc，劇本內直接寫死的簡中選項保留原文", file=sys.stderr)
        return texts
    return out if len(out) == len(texts) else texts


def name_of(char_id, table):
    if not char_id:
        return "？"
    return table.get(("Character", char_id)) or char_id


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("game", help="《活俠傳》安裝目錄（含 Mortal_Data）")
    ap.add_argument("-o", "--out", default=os.path.join(HERE, "out"))
    a = ap.parse_args()
    data = os.path.join(a.game, "Mortal_Data")
    table = read_table(os.path.join(data, "level0"))
    scripts = read_lua(os.path.join(data, "resources.assets"))
    literals = sorted(
        {m.group(3) for src in scripts.values() for m in OPTION.finditer(src)
         if ("Story", m.group(3)) not in table and CJK.search(m.group(3))}
    )
    for lit, trad in zip(literals, s2tw(literals)):
        table[("Literal", lit)] = trad
    story = {}
    used_legend = set()
    for name, src in sorted(scripts.items()):
        lines, nexts = parse_script(src, table, used_legend)
        if lines:
            story[name] = {"lines": lines, "next": nexts}
    # 少數風雲史條目沒有劇本用 AddStory 寫入（多半是遊戲程式直接寫）；
    # 依鍵名對回劇本（S2321_07_02_005 → s2321_07_02，Ch_3_5_1_010 → ch3_5_1），對不到的另放一組
    for key in sorted(k for c, k in table if c == "LegendInfo" and k not in used_legend):
        base = re.sub(r"_\d+$", "", key).lower()
        base = re.sub(r"^ch_", "ch", base)
        target = base if base in story else "風雲史（無對應劇本）"
        story.setdefault(target, {"lines": [], "next": []})["lines"].append(["風雲史", norm(table[("LegendInfo", key)])])
    os.makedirs(a.out, exist_ok=True)
    path = os.path.join(a.out, "story.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(story, f, ensure_ascii=False, separators=(",", ":"))
    kinds = Counter(w if w in ("選項", "擲骰", "風雲史") else "對白" for v in story.values() for w, _ in v["lines"])
    print(f"劇本 {len(story)}/{len(scripts)} 個有文本：" + "、".join(f"{k} {n}" for k, n in kinds.most_common()) + f" → {path}")


if __name__ == "__main__":
    sys.exit(main())
