"""用一句話描述劇情，在《活俠傳》劇本對白裡找出是哪一段。

兩段式：
  1. 本機：把每個劇本的對白切成段落，用中文兩字詞（BM25）挑出前 N 段候選。
  2. TypeSafe：每段候選各問一題 Score「這段對白是不是描述中的那一幕」，依分數排序。
  3. 第二輪：第一輪判為相關的劇本（最多 --expand 個），其餘段落全部再評一次。

  --dry-run   只做第 1 段，印出候選與預估 token 數，不呼叫 API
  其餘情況需要 TYPESAFE_API_KEY（可放在 tools/text-search/.env），結果快取在 .cache/

用法：
  PY=tools/game-extract/.venv/bin/python
  $PY tools/text-search/search.py "師父說要幫趙活找媳婦" --dry-run
  $PY tools/text-search/search.py "師父說要幫趙活找媳婦" -n 12
"""
import argparse
import hashlib
import json
import math
import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
STORY = os.path.join(HERE, "out", "story.json")
MISSIONS = os.path.join(ROOT, "tools", "game-extract", "out", "missions.json")
MISSION_NAMES = os.path.join(ROOT, "LegendOfMortal_InternalMod", "Core", "MissionNames.cs")
CACHE = os.path.join(HERE, ".cache")

WINDOW = 24  # 每段幾行對白
STRIDE = 12  # 段落重疊一半，避免一幕被切在兩段中間

# ---- 問題措辭集中在這裡（要調整判斷標準只改這裡）----
INSTRUCTIONS = (
    "玩家用一句話描述了《活俠傳》裡的一段劇情：`描述`。"
    "下面是遊戲劇本中的一段對白（主角是趙活）。這段對白演出的，是不是玩家描述的那一幕？"
)
CRITERIA = [
    "無關：對白沒有演出描述中的事件，也沒有談到它",
    "只是提到：對白談到描述中的人物或話題，但演的是別的場合，例如事後回想、轉述或其他事件",
    "部分吻合：對白演出了描述中那一幕的一部分，例如前因、後續或其中一個段落",
    "就是這一幕：對白直接演出了描述中的事件",
]
MATCH, PARTIAL = 3, 2
EXPAND_AT = 0.3  # 第一輪「部分吻合＋就是這一幕」機率達此值的劇本，第二輪展開其餘段落
EXPAND_MAX = 40  # 第二輪最多送幾段


def load_env():
    path = os.path.join(HERE, ".env")
    if not os.path.exists(path):
        return
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        if line and not line.startswith("#") and "=" in line:
            k, v = line.split("=", 1)
            os.environ.setdefault(k.strip(), v.strip().strip("'\""))


def grams(text):
    """中文取兩字詞，英數取整個詞；標點與空白略過。"""
    out = []
    for run in re.findall(r"[一-鿿]+|[A-Za-z0-9]+", text):
        if run.isascii():
            out.append(run.lower())
        elif len(run) == 1:
            out.append(run)
        else:
            out.extend(run[i:i + 2] for i in range(len(run) - 1))
    return out


def windows(story, names=None):
    for name in story if names is None else names:
        lines = story[name]["lines"]
        starts = range(0, max(1, len(lines) - WINDOW + STRIDE), STRIDE)
        for s in starts:
            chunk = lines[s:s + WINDOW]
            if chunk:
                yield name, s, chunk


def as_text(chunk):
    return "\n".join(f"{who}：{text}" for who, text in chunk)


def retrieve(story, query, n):
    docs = [(name, s, chunk, Counter(grams(as_text(chunk)))) for name, s, chunk in windows(story)]
    df = Counter()
    for *_, tf in docs:
        df.update(tf.keys())
    avg = sum(sum(tf.values()) for *_, tf in docs) / len(docs)
    q = set(grams(query))
    N = len(docs)

    def bm25(tf):
        dl = sum(tf.values())
        s = 0.0
        for g in q:
            f = tf.get(g)
            if f:
                idf = math.log(1 + (N - df[g] + 0.5) / (df[g] + 0.5))
                s += idf * f * 2.2 / (f + 1.2 * (0.25 + 0.75 * dl / avg))
        return s

    ranked = sorted(((bm25(tf), name, s, chunk) for name, s, chunk, tf in docs), key=lambda x: -x[0])
    # 同一劇本只留分數最高的兩段，讓候選涵蓋更多劇本
    picked, per = [], Counter()
    for score, name, s, chunk in ranked:
        if score <= 0 or per[name] >= 2:
            continue
        per[name] += 1
        picked.append({"script": name, "start": s, "lines": chunk, "bm25": round(score, 2)})
        if len(picked) >= n:
            break
    return picked


def where(story):
    """劇本 → 「任務檢查點（wiki 名稱）時間」，沿 SetNextScript 往下傳。"""
    if not os.path.exists(MISSIONS):
        return {}
    names = {}
    if os.path.exists(MISSION_NAMES):
        names = dict(re.findall(r'\["([^"]+)"\]\s*=\s*"([^"]+)"', open(MISSION_NAMES, encoding="utf-8").read()))
    out = defaultdict(set)
    for m in json.load(open(MISSIONS, encoding="utf-8")):
        for c in m["checks"]:
            t = c.get("time") or {}
            when = ""
            if t.get("at"):
                when = "第{}年{}月{}旬".format(*t["at"])
            elif t.get("from") and t.get("to"):
                when = "第{}年{}月{}旬起".format(*t["from"])
            label = c["key"] + (f"「{names[c['key']]}」" if c["key"] in names else "") + (f" {when}" if when else "")
            todo, seen = [(c.get("startScript") or "").lower()], set()
            while todo:
                s = todo.pop()
                if not s or s in seen:
                    continue
                seen.add(s)
                out[s].add(label)
                todo.extend((story.get(s) or {}).get("next", []))
    return out


def ask(query, cands, model):
    from typesafe_sdk import Score, TypeSafeClient

    state = {"描述": query}
    questions = {
        f"c{i}": Score(instructions={"問題": INSTRUCTIONS, "對白": as_text(c["lines"])}, criteria=CRITERIA)
        for i, c in enumerate(cands)
    }
    key = hashlib.sha256(
        json.dumps([query, model, INSTRUCTIONS, CRITERIA, [as_text(c["lines"]) for c in cands]], ensure_ascii=False).encode()
    ).hexdigest()[:24]
    path = os.path.join(CACHE, key + ".json")
    if os.path.exists(path):
        result = json.load(open(path, encoding="utf-8"))
        # JSON 的鍵一律是字串，讀回後轉回層級整數
        for a in result["answers"].values():
            a["probabilities"] = {int(k): v for k, v in a["probabilities"].items()}
        return result, True
    kwargs = {"model": model} if model else {}
    with TypeSafeClient() as client:
        r = client.system_one(state=state, questions=questions, **kwargs)
    result = {
        "model": getattr(r, "model", None),
        "usage": {k: getattr(r.usage, k, None) for k in ("input_tokens", "output_tokens")},
        "answers": {
            qid: {
                "score": float(a.score),
                "confidence": float(a.confidence),
                "probabilities": {int(k): float(v) for k, v in a.probabilities.items()},
            }
            for qid, a in r.scores.items()
        },
    }
    os.makedirs(CACHE, exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=1)
    return result, False


def related(a):
    p = a["probabilities"]
    return p.get(MATCH, 0) + p.get(PARTIAL, 0)


def verdict(a):
    p = a["probabilities"]
    if p.get(MATCH, 0) >= 0.6:
        return "吻合"
    if p.get(MATCH, 0) + p.get(PARTIAL, 0) >= 0.6:
        return "相關段落"
    if a["confidence"] < 0.3:
        return "不確定"
    return ""


def snippet(lines, k=2):
    return " ／ ".join(f"{w}：{t[:40]}" for w, t in lines[:k])


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("query", help="用一句話描述要找的劇情")
    ap.add_argument("-n", type=int, default=12, help="送給 TypeSafe 的候選段數（預設 12）")
    ap.add_argument("--dry-run", action="store_true", help="只列出本機候選，不呼叫 API")
    ap.add_argument("--model", default=os.environ.get("TYPESAFE_MODEL", ""), help="例如 jev-1.14.0（預設 jev-latest）")
    ap.add_argument("--show", type=int, default=0, help="結果另外印出前幾名的完整對白")
    ap.add_argument("--expand", type=int, default=2, help="第二輪最多展開幾個劇本（0 = 不做第二輪）")
    ap.add_argument("--top", type=int, default=12, help="結果只列前幾名")
    a = ap.parse_args()

    if not os.path.exists(STORY):
        sys.exit(f"找不到 {STORY}，請先跑 build_story.py")
    story = json.load(open(STORY, encoding="utf-8"))
    cands = retrieve(story, a.query, a.n)
    if not cands:
        sys.exit("本機候選為空：描述裡的字詞沒有出現在任何對白中，換個說法試試。")
    loc = where(story)
    chars = len(a.query) * len(cands) + sum(len(as_text(c["lines"])) for c in cands) + len(INSTRUCTIONS + "".join(CRITERIA)) * len(cands)

    if a.dry_run:
        print(f"候選 {len(cands)} 段，約 {chars} 字（中文大約一字一 token，以 $0.042/百萬 token 計約 ${chars * 0.042e-6:.5f}）\n")
        for i, c in enumerate(cands):
            print(f"[{i:2}] bm25={c['bm25']:<6} {c['script']}#{c['start']}  {'、'.join(sorted(loc.get(c['script'], []))) or ''}")
            print(f"      {snippet(c['lines'])}")
        return

    load_env()
    if not os.environ.get("TYPESAFE_API_KEY"):
        sys.exit("TYPESAFE_API_KEY 未設定：放在 tools/text-search/.env，或先用 --dry-run")
    try:
        result, cached = ask(a.query, cands, a.model)
    except ImportError:
        sys.exit("缺少 typesafe-sdk：tools/game-extract/.venv/bin/pip install typesafe-sdk")
    rows = [(result["answers"][f"c{i}"], c) for i, c in enumerate(cands)]
    print(f"第一輪：模型 {result.get('model')}　{'（快取）' if cached else ''}用量 {result.get('usage')}")

    # 第二輪：本機候選只靠字面重疊，同一劇本裡換了說法的段落會漏掉。
    # 第一輪判為相關的劇本，把其餘段落全部再評一次。
    hot = []
    for ans, c in sorted(rows, key=lambda x: -related(x[0])):
        if related(ans) >= EXPAND_AT and c["script"] not in hot:
            hot.append(c["script"])
    hot = hot[: a.expand]
    asked = {(c["script"], c["start"]) for _, c in rows}
    more = [
        {"script": name, "start": s, "lines": chunk, "bm25": None}
        for name, s, chunk in windows(story, hot)
        if (name, s) not in asked
    ][:EXPAND_MAX]
    if more:
        result2, cached2 = ask(a.query, more, a.model)
        rows += [(result2["answers"][f"c{i}"], c) for i, c in enumerate(more)]
        print(f"第二輪：展開 {'、'.join(hot)} 其餘 {len(more)} 段　{'（快取）' if cached2 else ''}用量 {result2.get('usage')}")
    print()
    rows.sort(key=lambda x: (-x[0]["probabilities"].get(MATCH, 0), -x[0]["score"]))
    rows = rows[: a.top]
    print(" 分數  信心  P(吻合)  判定      劇本#起始行  任務檢查點")
    for ans, c in rows:
        print(
            f" {ans['score']:.2f}  {ans['confidence']:.2f}  {ans['probabilities'].get(MATCH, 0):.2f}     "
            f"{verdict(ans):<8}  {c['script']}#{c['start']}  {'、'.join(sorted(loc.get(c['script'], [])))}"
        )
        print(f"       {snippet(c['lines'])}")
    if any(c["bm25"] is None for _, c in rows):
        print("\n（劇本#起始行 來自第二輪展開者，本機候選原本沒有選到）")
    for ans, c in rows[: a.show]:
        print(f"\n==== {c['script']}#{c['start']} ====\n{as_text(c['lines'])}")


if __name__ == "__main__":
    main()
