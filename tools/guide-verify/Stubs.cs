using System;
using System.Collections.Generic;
namespace Mortal.Core
{
    public enum GameStatType { 心理衛生, 修養, 性情, 處世, 道德, 學問, 門派名聲, 門派規模, 個人貢獻度, 行動次數, 變心, 銀兩, 命運, 體力, 內力, 輕功, 魅力, 嘴力, 門派資產, 門派人數, 向心力 }
    public enum RelationshipStatType { 四師兄, 虞小梅, 瑞笙, 南宮淺, 郁竹, 申屠龍, 葉雲裳, 葉雲舟, 夏侯蘭, 龍湘 }
    public class FlagData { public int State; }
    public class StatItem { public int FinalValue; public int Value; }
    public class StatSet { public static Dictionary<GameStatType,int> V = new(); public StatItem Get(GameStatType t) => new StatItem { FinalValue = V.TryGetValue(t, out var x) ? x : 0 }; }
    public class RelSet { public static Dictionary<RelationshipStatType,int> V = new(); public StatItem Get(RelationshipStatType t) => new StatItem { Value = V.TryGetValue(t, out var x) ? x : 0 }; }
    public class PlayerStatManagerData { public static PlayerStatManagerData Instance = new(); public StatSet Stats = new(); public RelSet Relationships = new(); }
    public class MissionManagerData { public static MissionManagerData Instance = new(); public static Dictionary<string,int> Flags = new(); public FlagData GetFlag(string id) => new FlagData { State = Flags.TryGetValue(id, out var x) ? x : 0 }; }
}
namespace LegendOfMortal_InternalMod { public static class Plugin { public static void LogError(string s) => Console.Error.WriteLine(s); } }
namespace LegendOfMortal_InternalMod.UI
{
    using UnityEngine;
    public static class CheatWindow { public static string OnPaper(string s) => s; }
    public static class GameStyleUI
    {
        public static Color Paper, PaperDark, Ink, InkSoft, Vermilion, Frame;
        public static GUISkin Skin = new GUISkin();
        public static GUIStyle Label = new(), Desc = new(), Entry = new(), Row = new(), RowDesc = new();
        public static int Px(float v) => (int)v;
    }
}
namespace UnityEngine
{
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white = new Color(1,1,1,1); public static Color Lerp(Color a, Color b, float t) => a; }
    public struct Vector2 { public float x, y; }
    public struct Rect { public float x, y, width, height; public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float yMax => y + height; public float xMax => x + width; public Vector2 center => new Vector2 { x = x + width / 2, y = y + height / 2 }; }
    public class RectOffset { public RectOffset(int a, int b, int c, int d) { } }
    public enum TextAnchor { MiddleCenter, MiddleLeft }
    public enum FontStyle { Normal, Bold }
    public enum TextClipping { Overflow, Clip }
    public class GUIStyleState { public Color textColor; }
    public class GUIStyle { public GUIStyle() { } public GUIStyle(GUIStyle o) { } public static GUIStyle none = new();
        public int fontSize; public TextAnchor alignment; public bool wordWrap, richText; public TextClipping clipping; public FontStyle fontStyle; public RectOffset padding, margin; public GUIStyleState normal = new(); }
    public class GUISkin { public GUIStyle box = new(); }
    public class GUIContent { public static GUIContent none = new(); public GUIContent() { } public GUIContent(string s) { } }
    public class GUILayoutOption { }
    public enum EventType { Layout, Repaint, MouseDown }
    public class Event { public static Event current = new(); public EventType type; }
    public class Texture2D { public static Texture2D whiteTexture = new(); }
    public static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); public static float Min(float a, float b) => Math.Min(a, b); public static float Abs(float a) => Math.Abs(a); }
    public static class GUI { public static Color color; public static GUISkin skin = new();
        public static void DrawTexture(Rect r, Texture2D t) { }
        public static void Label(Rect r, string s, GUIStyle st) { }
        public static bool Button(Rect r, GUIContent c, GUIStyle s) => false; }
    public static class GUILayout {
        public static void Label(string s, GUIStyle st, params GUILayoutOption[] o) { }
        public static bool Button(string s, params GUILayoutOption[] o) => false;
        public static void BeginHorizontal() { } public static void EndHorizontal() { }
        public static void BeginVertical() { } public static void BeginVertical(GUIStyle s) { } public static void EndVertical() { }
        public static void FlexibleSpace() { } public static void Space(float f) { }
        public static GUILayoutOption Width(float w) => null; public static GUILayoutOption ExpandWidth(bool b) => null;
        public static int Toolbar(int i, string[] s, params GUILayoutOption[] o) => i;
        public static Rect GetRectDummy() => default; }
    public static class GUILayoutUtility { public static Rect GetRect(float w, float h, params GUILayoutOption[] o) => new Rect(0, 0, 740, h); }
}
