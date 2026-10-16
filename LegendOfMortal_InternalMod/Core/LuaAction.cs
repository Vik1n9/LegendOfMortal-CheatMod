using System.Linq;
using System.Text.RegularExpressions;
using Mortal.Core;

namespace LegendOfMortal_InternalMod.Core
{
    public class LuaAction
    {
        public ActionType Type { get; set; }
        public bool IsConditional { get; set; }
        public string Content { get; set; }
        public string Description { get; private set; }

        public LuaAction(ActionType type, string content, bool isConditional = false)
        {
            Type = type;
            Content = content;
            IsConditional = isConditional;
            Description = ParseAction();
        }

        private string ParseAction()
        {
            return Type switch
            {
                ActionType.ModifyAction => ParseModifyAction(),
                ActionType.BattleScene => Content.Contains("Combat") ? "對決" : "混戰",
                _ => string.Empty,
            };
        }

        private string ParseModifyAction()
        {
            Match match = Regex.Match(Content, @"statmodifymanager\.(\w+)\(([^)]*)\)");
            if (!match.Success) return string.Empty;
            return GetActionDescription(match.Groups[1].Value, match.Groups[2].Value);
        }

        private string GetActionDescription(string actionType, string parameters)
        {
            string[] array = (from p in parameters.Split(',')
                              select p.Trim().Trim('"')).ToArray();
            if (array.Length < 1) return string.Empty;
            string text = array[0];
            text = actionType switch
            {
                "Player" => DataHelper.TryParseEnum<GameStatType>(text, out GameStatType r) ? r.ToString() : text,
                "UpdateSetPlayerStat" => DataHelper.TryParseEnum<GameStatType>(text, out GameStatType r2) ? r2.ToString() : text,
                "Character" => DataHelper.TryParseEnum<RelationshipStatType>(text, out RelationshipStatType r3) ? r3.ToString() : text,
                "AddBook" => DataHelper.GetItemName((GameItemType)1, text),
                "AddMisc" => DataHelper.GetItemName((GameItemType)2, text),
                "RemoveMisc" => DataHelper.GetItemName((GameItemType)2, text),
                "AddSpecial" => DataHelper.GetItemName((GameItemType)3, text),
                "AddTalent" => DataHelper.GetTalentName(text),
                "AddFlag" => DataHelper.GetFlagName(text),
                "SetFlag" => DataHelper.GetFlagName(text),
                "ModifyEnemyTeam" => DataHelper.GetTeamName(text),
                _ => text,
            };
            if (array.Length == 1)
            {
                if (actionType == "RemoveMisc") return "丟棄" + text;
                return string.Empty;
            }
            if (!int.TryParse(array[1], out var v)) return string.Empty;
            return GetDescription(actionType, text, v);
        }

        private string GetDescription(string actionType, string id, int value)
        {
            string t = value > 0 ? $"+{value}" : $"{value}";
            return actionType switch
            {
                "Player" => $"<color=#7CFC00>{id} {t}</color>",
                "Character" => $"<color=#7CFC00>{id}好感 {t}</color>",
                "AddBook" => $"{id} x{value}",
                "AddMisc" => $"{id} x{value}",
                "AddSpecial" => $"{id} x{value}",
                "AddTalent" => $"天賦{id} lv{value}",
                "UpdateSetPlayerStat" => $"設置{id} {value}",
                "AddFlag" => string.IsNullOrEmpty(id) ? string.Empty : $"{id} {value}",
                "SetFlag" => string.IsNullOrEmpty(id) ? string.Empty : $"設置{id}{value}",
                "ModifyEnemyTeam" => $"<color=#7CFC00>{id}向心力 {t}</color>",
                _ => string.Empty,
            };
        }
    }
}
