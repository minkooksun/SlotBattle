using System.Collections.Generic;
using SlotBattle.Core.Data;
using UnityEngine;

namespace SlotBattle.Presentation
{
    /// <summary>공격 연출의 모양(설계서 8장 "유닛별 공격 연출"). 규칙(공격 방식)은 Core의 AttackType이 정한다.</summary>
    public enum AttackAnim
    {
        /// <summary>검 휘두르기와 호 모양 궤적.</summary>
        Slash,
        /// <summary>포구 섬광과 레이저 탄.</summary>
        Shoot,
        /// <summary>방패 밀어치기와 충격파.</summary>
        Bash,
        /// <summary>번갈아 찌르기.</summary>
        Stab,
        /// <summary>부풀다가 폭발.</summary>
        Burst,
        /// <summary>포물선 구체.</summary>
        Lob,
        /// <summary>잔상을 남기는 돌진.</summary>
        Dash,
        /// <summary>위에서 아래로 연사.</summary>
        Strafe,
    }

    /// <summary>
    /// 1차 개발용 로봇 외형: 도형과 색. 실제 그림(M5)으로 바꿀 때 이곳만 고친다.
    /// 편은 테두리 색(내 편 파랑, 적 빨강)으로 구분한다.
    /// </summary>
    public static class UnitStyle
    {
        public struct Style
        {
            public Shape Shape;
            public Color Color;
            /// <summary>도형 크기 배율(물량 유닛은 작게).</summary>
            public float Scale;
            public string Label;
            public AttackAnim Attack;
        }

        private static readonly Dictionary<string, Style> Styles = new Dictionary<string, Style>
        {
            { "blade", new Style { Shape = Shape.Triangle, Color = Ui.Hex("#FF8A3D"), Scale = 1f, Label = "블레이드", Attack = AttackAnim.Slash } },
            { "shooter", new Style { Shape = Shape.Circle, Color = Ui.Hex("#4FC3F7"), Scale = 0.95f, Label = "슈터", Attack = AttackAnim.Shoot } },
            { "shield", new Style { Shape = Shape.RoundedSquare, Color = Ui.Hex("#9FB3C8"), Scale = 1.1f, Label = "실드", Attack = AttackAnim.Bash } },
            { "twin", new Style { Shape = Shape.Circle, Color = Ui.Hex("#A5D66B"), Scale = 0.75f, Label = "트윈", Attack = AttackAnim.Stab } },
            { "boom", new Style { Shape = Shape.Hexagon, Color = Ui.Hex("#FF5A5A"), Scale = 0.9f, Label = "붐", Attack = AttackAnim.Burst } },
            { "plasma", new Style { Shape = Shape.Ring, Color = Ui.Hex("#B98CFF"), Scale = 1f, Label = "플라즈마", Attack = AttackAnim.Lob } },
            { "dash", new Style { Shape = Shape.Diamond, Color = Ui.Hex("#FFD740"), Scale = 1f, Label = "대시", Attack = AttackAnim.Dash } },
            { "drone", new Style { Shape = Shape.Diamond, Color = Ui.Hex("#5FF5D2"), Scale = 1.25f, Label = "드론", Attack = AttackAnim.Strafe } },
        };

        /// <summary>로봇 얼굴 아이콘을 주는 곳(전투 화면이 RobotIcons로 채운다). 없으면 도형으로 그린다.</summary>
        public static System.Func<string, Sprite> IconProvider;

        public static Sprite Icon(string unitId) => IconProvider?.Invoke(unitId);

        /// <summary>이미지에 유닛 그림을 넣는다: 로봇 아이콘이 있으면 아이콘, 없으면 색 도형.</summary>
        public static bool ApplyIcon(UnityEngine.UI.Image image, string unitId, float alpha = 1f)
        {
            var sprite = Icon(unitId);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = UnityEngine.UI.Image.Type.Simple;
                image.preserveAspect = true;
                image.color = new Color(1f, 1f, 1f, alpha);
                return true;
            }
            var style = Get(unitId);
            Ui.SetShape(image, style.Shape);
            image.color = Ui.WithAlpha(style.Color, alpha);
            return false;
        }

        public static Style Get(string unitId)
        {
            if (Styles.TryGetValue(unitId, out var style))
            {
                return style;
            }
            return new Style { Shape = Shape.Square, Color = Color.gray, Scale = 1f, Label = unitId, Attack = AttackAnim.Slash };
        }

        /// <summary>표에 없는 유닛은 공격 방식에서 기본 연출을 고른다.</summary>
        public static AttackAnim AttackOf(UnitDef def)
        {
            if (Styles.TryGetValue(def.Id, out var style))
            {
                return style.Attack;
            }
            switch (def.AttackType)
            {
                case AttackType.Bolt: return AttackAnim.Shoot;
                case AttackType.Lob: return AttackAnim.Lob;
                case AttackType.Explode: return AttackAnim.Burst;
                case AttackType.Charge: return AttackAnim.Dash;
                default: return AttackAnim.Slash;
            }
        }

        /// <summary>단계 표시 이름(설계서 1장: 기본형, 강화형, 합체형).</summary>
        public static string TierName(int tier)
        {
            switch (tier)
            {
                case 1: return "기본형";
                case 2: return "강화형";
                default: return "합체형";
            }
        }
    }
}
