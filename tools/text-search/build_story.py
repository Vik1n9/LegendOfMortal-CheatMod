"""從本機《活俠傳》抽出每個 Lua 劇本的繁中對白，寫成 out/story.json（含遊戲原文，不提交）。

  Mortal_Data/resources.assets  Lua 劇本：say(luamanager.GetStoryText("D_…")) 與選項表 "O_…"
  Mortal_Data/level0            語系字串表：Story/<鍵>、Character/<角色 id>，同一鍵有簡、繁、韓三份

用法：
  tools/game-extract/.venv/bin/python tools/text-search/build_story.py "$GAME" [-o tools/text-search/out]
"""
import argparse
import json
import os
import re
import sys
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
CJK = re.compile(r"[一-鿿]")
HANGUL = re.compile(r"[가-힣]")
KEY = re.compile(rb"(Story|Character)/")


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
        if not re.fullmatch(r"(Story|Character)/[A-Za-z0-9_]+", key):
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
OPTION = re.compile(r'option[\w]*\[\d+\]\s*=\s*"([^"]+)"')
CHOOSE = re.compile(r"choose\(")
NEXT = re.compile(r'luamanager\.SetNextScript\(\s*"([^"]+)"')


def norm(text):
    return re.sub(r"\s*\r?\n\s*", "", text).strip()


def parse_script(src, table):
    lines = []
    speaker = None
    mode = "character"
    opts = []
    nexts = []
    for raw in src.splitlines():
        if m := DIALOG.search(raw):
            mode = m.group(1)
            continue
        if m := SETCHAR.search(raw):
            speaker = m.group(1)
            continue
        if m := OPTION.search(raw):
            opts.append(m.group(1))
            continue
        if CHOOSE.search(raw) and opts:
            texts = [norm(table.get(("Story", o), "")) for o in opts]
            texts = [t for t in texts if t]
            if texts:
                lines.append(["選項", " ／ ".join(texts)])
            opts = []
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
    story = {}
    for name, src in sorted(scripts.items()):
        lines, nexts = parse_script(src, table)
        if lines:
            story[name] = {"lines": lines, "next": nexts}
    os.makedirs(a.out, exist_ok=True)
    path = os.path.join(a.out, "story.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(story, f, ensure_ascii=False, separators=(",", ":"))
    n_lines = sum(len(v["lines"]) for v in story.values())
    n_story = sum(1 for k in table if k[0] == "Story")
    print(f"字串表 {n_story} 句、角色 {len(table) - n_story} 名；劇本 {len(story)}/{len(scripts)} 個有對白，共 {n_lines} 行 → {path}")


if __name__ == "__main__":
    sys.exit(main())
