using System.Collections.Generic;
using UnityEngine;

namespace SlotBattle.Presentation
{
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
        }

        private static readonly Dictionary<string, Style> Styles = new Dictionary<string, Style>
        {
            { "blade", new Style { Shape = Shape.Triangle, Color = Ui.Hex("#FF8A3D"), Scale = 1f, Label = "블레이드" } },
            { "shooter", new Style { Shape = Shape.Circle, Color = Ui.Hex("#4FC3F7"), Scale = 0.95f, Label = "슈터" } },
            { "shield", new Style { Shape = Shape.RoundedSquare, Color = Ui.Hex("#9FB3C8"), Scale = 1.1f, Label = "실드" } },
            { "swarm", new Style { Shape = Shape.Circle, Color = Ui.Hex("#A5D66B"), Scale = 0.6f, Label = "스웜" } },
            { "boom", new Style { Shape = Shape.Hexagon, Color = Ui.Hex("#FF5A5A"), Scale = 0.9f, Label = "붐" } },
            { "plasma", new Style { Shape = Shape.Ring, Color = Ui.Hex("#B98CFF"), Scale = 1f, Label = "플라즈마" } },
            { "dash", new Style { Shape = Shape.Diamond, Color = Ui.Hex("#FFD740"), Scale = 1f, Label = "대시" } },
            { "drone", new Style { Shape = Shape.Diamond, Color = Ui.Hex("#5FF5D2"), Scale = 0.85f, Label = "드론" } },
        };

        public static Style Get(string unitId)
        {
            if (Styles.TryGetValue(unitId, out var style))
            {
                return style;
            }
            return new Style { Shape = Shape.Square, Color = Color.gray, Scale = 1f, Label = unitId };
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
