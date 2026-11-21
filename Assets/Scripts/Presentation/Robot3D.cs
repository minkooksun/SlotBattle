using System.Collections.Generic;
using SlotBattle.Core.Battle;
using SlotBattle.Core.Data;
using UnityEngine;

namespace SlotBattle.Presentation
{
    /// <summary>로봇의 관절. 화면 코드는 관절 이름으로만 움직인다(모델을 바꿔도 같은 이름을 쓴다).</summary>
    public enum RobotJoint
    {
        Body,
        LegL,
        LegR,
        /// <summary>검, 방패, 창, 포신, 박격포, 충각, 붐봇의 코어.</summary>
        Weapon,
        /// <summary>드론봇의 두 번째 포신.</summary>
        Weapon2,
    }

    /// <summary>
    /// 로봇 공급자(설계서 4장 "3D 전장"): 유닛 종류·편·단계로 3D 로봇을 만든다.
    /// 지금은 Unity 기본 도형을 디자인 시안의 좌표대로 조립한다. 구매한 에셋으로 바꿀 때는 이 클래스만 바꾼다.
    /// 시안 좌표: x = 오른쪽, y = 앞(적 방향), z = 위. Unity 좌표로는 (x, z, y).
    /// </summary>
    public static class RobotFactory
    {
        public static readonly Color Metal = Ui.Hex("#AEB8C8");
        public static readonly Color Dark = Ui.Hex("#4A5366");
        public static readonly Color Steel = Ui.Hex("#DCE3EE");
        public static readonly Color Gun = Ui.Hex("#6C778C");
        public static readonly Color Gold = Ui.Hex("#F2C14E");
        public static readonly Color PlayerEye = Ui.Hex("#56E0FF");
        public static readonly Color EnemyEye = Ui.Hex("#FF5470");

        public static Color TeamColor(Side side) => side == Side.Player ? PlayerEye : EnemyEye;

        /// <summary>로봇 하나를 만든다. worldSize: 로봇 폭(전장 단위), footprint: 시안의 기준 폭.</summary>
        public static RobotModel Create(UnitDef def, Side side, int tier, Transform parent, float worldSize, float footprint, float airHeight)
        {
            var style = UnitStyle.Get(def.Id);
            var team = TeamColor(side);
            var model = new RobotModel(parent, def.Id, style.Attack, team);
            var b = new RobotBuilder(model, tier, team);
            var acc = style.Color;
            switch (def.Id)
            {
                case "blade": Blade(b, acc, team); break;
                case "shooter": Shooter(b, acc, team); break;
                case "shield": Shield(b, acc, team); break;
                case "twin": Twin(b, acc, team); break;
                case "boom": Boom(b, acc, team); break;
                case "plasma": Plasma(b, acc, team); break;
                case "dash": Dash(b, acc, team); break;
                case "drone": Drone(b, acc, team); break;
                case "siege": Siege(b, acc, team); break;
                default: Generic(b, acc, team); break;
            }
            b.TierParts();
            // 발밑 고리: 편 색.
            var ring = Mat3D.Cylinder(model.Root, "TeamRing", new Vector3(0f, 0.02f, 0f), new Vector3(2.6f, 0.01f, 2.6f), Color.Lerp(team, Color.black, 0.35f));
            ring.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float scale = worldSize / Mathf.Max(0.1f, footprint) * style.Scale;
            model.Finish(scale, def.HasKeyword("air") ? airHeight : 0f, b.Top);
            return model;
        }

        // ───────────── 9종(디자인 시안과 같은 좌표) ─────────────

        private static void Blade(RobotBuilder b, Color acc, Color team)
        {
            b.Joint(RobotJoint.LegL, -0.6f, 0f, 1.6f);
            b.Joint(RobotJoint.LegR, 0.6f, 0f, 1.6f);
            b.Box(RobotJoint.LegL, -0.9f, -0.4f, 0f, 0.6f, 0.8f, 1.6f, Dark);
            b.Box(RobotJoint.LegR, 0.3f, -0.4f, 0f, 0.6f, 0.8f, 1.6f, Dark);
            b.Box(RobotJoint.Body, -1f, -0.5f, 1.6f, 2f, 1f, 0.5f, Metal);
            b.Box(RobotJoint.Body, -1.2f, -0.7f, 2.1f, 2.4f, 1.4f, 1.6f, acc);
            b.Box(RobotJoint.Body, -0.8f, 0.7f, 2.4f, 1.6f, 0.15f, 1.0f, Steel);
            b.Box(RobotJoint.Body, -0.5f, -0.4f, 3.7f, 1f, 0.9f, 0.8f, Metal);
            b.Box(RobotJoint.Body, -0.4f, 0.5f, 3.95f, 0.8f, 0.08f, 0.25f, team, glow: true);
            b.Box(RobotJoint.Body, -1.75f, -0.3f, 2.2f, 0.55f, 0.6f, 1.4f, acc);
            b.Box(RobotJoint.Body, 1.2f, -0.3f, 2.0f, 0.55f, 0.6f, 1.4f, acc);
            // 검: 손을 축으로 휘두른다.
            b.Joint(RobotJoint.Weapon, 1.47f, 0.3f, 2.57f);
            b.Box(RobotJoint.Weapon, 1.3f, 0.15f, 2.15f, 0.35f, 0.3f, 0.85f, Dark);
            b.Box(RobotJoint.Weapon, 1.35f, 0.3f, 2.3f, 0.25f, 2.9f, 0.55f, Steel);
            b.Muzzle(RobotJoint.Weapon, 1.47f, 3.2f, 2.57f);
            b.Anchors(top: 4.5f, halfW: 1.2f, front: 0.85f, back: -0.7f, bodyZ: 2.1f);
        }

        private static void Shooter(RobotBuilder b, Color acc, Color team)
        {
            b.Box(RobotJoint.Body, -1.4f, -1.2f, 0f, 0.7f, 2.4f, 0.8f, Dark);
            b.Box(RobotJoint.Body, 0.7f, -1.2f, 0f, 0.7f, 2.4f, 0.8f, Dark);
            b.Box(RobotJoint.Body, -0.8f, -1f, 0.5f, 1.6f, 2f, 0.7f, Metal);
            b.Box(RobotJoint.Body, -0.9f, -0.8f, 1.2f, 1.8f, 1.6f, 1.3f, acc);
            b.Box(RobotJoint.Body, -0.4f, -0.5f, 2.5f, 0.8f, 0.8f, 0.5f, Metal);
            b.Box(RobotJoint.Body, -0.3f, 0.3f, 2.6f, 0.6f, 0.06f, 0.2f, team, glow: true);
            b.Box(RobotJoint.Body, 0.55f, -0.6f, 2.5f, 0.1f, 0.1f, 1.1f, Steel);
            b.Sphere(RobotJoint.Body, 0.6f, -0.55f, 3.65f, 0.15f, team, glow: true);
            b.Joint(RobotJoint.Weapon, 0f, 0.6f, 1.97f);
            b.Box(RobotJoint.Weapon, -0.22f, 0.6f, 1.75f, 0.44f, 2.5f, 0.44f, Gun);
            b.Box(RobotJoint.Weapon, -0.3f, 3.0f, 1.68f, 0.6f, 0.35f, 0.58f, Steel);
            b.Muzzle(RobotJoint.Weapon, 0f, 3.4f, 1.97f);
            b.Anchors(top: 3.0f, halfW: 0.9f, front: 0.8f, back: -0.8f, bodyZ: 1.2f);
        }

        private static void Shield(RobotBuilder b, Color acc, Color team)
        {
            b.Joint(RobotJoint.LegL, -0.85f, 0f, 1.2f);
            b.Joint(RobotJoint.LegR, 0.85f, 0f, 1.2f);
            b.Box(RobotJoint.LegL, -1.3f, -0.6f, 0f, 0.9f, 1.2f, 1.2f, Dark);
            b.Box(RobotJoint.LegR, 0.4f, -0.6f, 0f, 0.9f, 1.2f, 1.2f, Dark);
            b.Box(RobotJoint.Body, -1.4f, -0.9f, 1.2f, 2.8f, 1.8f, 1.6f, acc);
            b.Box(RobotJoint.Body, -0.5f, -0.5f, 2.8f, 1f, 0.8f, 0.5f, Metal);
            b.Box(RobotJoint.Body, -0.4f, 0.3f, 2.95f, 0.8f, 0.06f, 0.2f, team, glow: true);
            // 방패: 몸 앞에서 밀어친다.
            b.Joint(RobotJoint.Weapon, 0f, 1.0f, 1.8f);
            b.Box(RobotJoint.Weapon, -1.9f, 1.0f, 0.3f, 3.8f, 0.4f, 3.0f, Steel);
            b.Box(RobotJoint.Weapon, -1.7f, 1.41f, 1.55f, 3.4f, 0.03f, 0.3f, team, glow: true);
            b.Muzzle(RobotJoint.Weapon, 0f, 1.5f, 1.8f);
            b.Anchors(top: 3.3f, halfW: 1.4f, front: 0.9f, back: -0.9f, bodyZ: 1.2f);
        }

        /// <summary>트윈봇은 Core에서 2기로 나오므로 모델은 작은 로봇 1기다.</summary>
        private static void Twin(RobotBuilder b, Color acc, Color team)
        {
            b.Joint(RobotJoint.LegL, -0.22f, 0f, 0.6f);
            b.Joint(RobotJoint.LegR, 0.22f, 0f, 0.6f);
            b.Box(RobotJoint.LegL, -0.35f, -0.2f, 0f, 0.25f, 0.4f, 0.6f, Dark);
            b.Box(RobotJoint.LegR, 0.1f, -0.2f, 0f, 0.25f, 0.4f, 0.6f, Dark);
            b.Sphere(RobotJoint.Body, 0f, 0f, 1.1f, 0.75f, acc);
            b.Box(RobotJoint.Body, -0.3f, 0.62f, 1.15f, 0.6f, 0.08f, 0.18f, team, glow: true);
            b.Joint(RobotJoint.Weapon, 0.67f, 0f, 0.97f);
            b.Box(RobotJoint.Weapon, 0.6f, -0.1f, 0.9f, 0.14f, 2.0f, 0.14f, Steel);
            b.Box(RobotJoint.Weapon, 0.55f, 1.85f, 0.85f, 0.24f, 0.35f, 0.24f, Ui.Hex("#F2F5FA"));
            b.Muzzle(RobotJoint.Weapon, 0.67f, 2.2f, 0.97f);
            b.Anchors(top: 1.85f, halfW: 0.75f, front: 0.7f, back: -0.7f, bodyZ: 0.5f);
        }

        private static void Boom(RobotBuilder b, Color acc, Color team)
        {
            b.Box(RobotJoint.Body, -0.9f, -0.6f, 0f, 0.4f, 0.4f, 0.6f, Dark);
            b.Box(RobotJoint.Body, 0.5f, -0.6f, 0f, 0.4f, 0.4f, 0.6f, Dark);
            b.Box(RobotJoint.Body, -0.9f, 0.4f, 0f, 0.4f, 0.4f, 0.6f, Dark);
            b.Box(RobotJoint.Body, 0.5f, 0.4f, 0f, 0.4f, 0.4f, 0.6f, Dark);
            b.Sphere(RobotJoint.Body, 0f, 0f, 1.7f, 1.45f, acc);
            b.Box(RobotJoint.Body, -0.35f, 0.95f, 2.6f, 0.7f, 0.06f, 0.2f, team, glow: true);
            b.Box(RobotJoint.Body, -0.06f, -0.06f, 3.1f, 0.12f, 0.12f, 0.8f, Steel);
            b.Sphere(RobotJoint.Body, 0f, 0f, 4.0f, 0.2f, Palette.Energy, glow: true);
            // 코어: 부풀며 깜빡인다.
            b.Joint(RobotJoint.Weapon, 0.2f, 1.05f, 1.9f);
            b.Sphere(RobotJoint.Weapon, 0.2f, 1.05f, 1.9f, 0.5f, Palette.Energy, glow: true);
            b.Muzzle(RobotJoint.Weapon, 0.2f, 1.5f, 1.9f);
            b.Anchors(top: 3.15f, halfW: 1.45f, front: 1.4f, back: -1.4f, bodyZ: 0.6f);
        }

        private static void Plasma(RobotBuilder b, Color acc, Color team)
        {
            float[,] legs = { { -1.6f, -1.6f }, { 1.2f, -1.6f }, { -1.6f, 1.2f }, { 1.2f, 1.2f } };
            for (int i = 0; i < 4; i++)
            {
                b.Box(RobotJoint.Body, legs[i, 0], legs[i, 1], 0f, 0.4f, 0.4f, 1.2f, Dark);
                b.Box(RobotJoint.Body, legs[i, 0] * 0.6f, legs[i, 1] * 0.6f, 0.9f, 0.4f, 0.4f, 0.25f, Gun);
            }
            b.Box(RobotJoint.Body, -1.1f, -1.1f, 1.0f, 2.2f, 2.2f, 0.9f, acc);
            b.Box(RobotJoint.Body, -0.5f, 1.1f, 1.3f, 1.0f, 0.06f, 0.25f, team, glow: true);
            b.Joint(RobotJoint.Weapon, 0f, 0f, 1.9f);
            b.Cylinder(RobotJoint.Weapon, 0f, 0f, 1.9f, 0.65f, 1.3f, Gun);
            b.Sphere(RobotJoint.Weapon, 0f, 0f, 3.35f, 0.42f, Ui.Hex("#D9B8FF"), glow: true);
            b.Muzzle(RobotJoint.Weapon, 0f, 0f, 3.5f);
            b.Anchors(top: 3.3f, halfW: 1.1f, front: 1.1f, back: -1.1f, bodyZ: 1.0f);
        }

        private static void Dash(RobotBuilder b, Color acc, Color team)
        {
            float[,] wheels = { { -1.1f, -1.0f }, { 0.75f, -1.0f }, { -1.1f, 0.8f }, { 0.75f, 0.8f } };
            for (int i = 0; i < 4; i++)
            {
                b.Box(RobotJoint.Body, wheels[i, 0], wheels[i, 1], 0f, 0.35f, 0.7f, 0.7f, Dark);
            }
            b.Box(RobotJoint.Body, -0.9f, -1.5f, 0.35f, 1.8f, 3.0f, 0.75f, acc);
            b.Box(RobotJoint.Body, -0.6f, -0.6f, 1.1f, 1.2f, 1.4f, 0.5f, Metal);
            b.Box(RobotJoint.Body, -0.45f, 0.8f, 1.2f, 0.9f, 0.06f, 0.25f, team, glow: true);
            b.Sphere(RobotJoint.Body, 0f, -1.7f, 0.75f, 0.35f, Ui.Hex("#FF9F43"), glow: true);
            // 충각: 돌진할 때 커진다.
            b.Joint(RobotJoint.Weapon, 0f, 1.5f, 0.67f);
            b.Box(RobotJoint.Weapon, -0.45f, 1.5f, 0.45f, 0.9f, 1.0f, 0.45f, Steel);
            b.Box(RobotJoint.Weapon, -0.15f, 2.5f, 0.55f, 0.3f, 0.6f, 0.25f, Ui.Hex("#F2F5FA"));
            b.Muzzle(RobotJoint.Weapon, 0f, 3.1f, 0.67f);
            b.Anchors(top: 1.6f, halfW: 0.9f, front: 1.5f, back: -1.5f, bodyZ: 0.4f);
        }

        private static void Drone(RobotBuilder b, Color acc, Color team)
        {
            b.Box(RobotJoint.Body, -0.8f, -0.8f, 2.2f, 1.6f, 1.6f, 0.6f, acc);
            b.Box(RobotJoint.Body, -0.4f, -0.4f, 2.8f, 0.8f, 0.8f, 0.3f, Metal);
            b.Box(RobotJoint.Body, -0.45f, 0.8f, 2.35f, 0.9f, 0.06f, 0.2f, team, glow: true);
            // 프로펠러를 잇는 틀.
            b.Box(RobotJoint.Body, -1.7f, -1.78f, 2.45f, 3.4f, 0.16f, 0.12f, Gun);
            b.Box(RobotJoint.Body, -1.7f, 1.62f, 2.45f, 3.4f, 0.16f, 0.12f, Gun);
            b.Box(RobotJoint.Body, -0.08f, -1.7f, 2.45f, 0.16f, 3.4f, 0.12f, Gun);
            float[,] rotors = { { -1.7f, -1.7f }, { 1.7f, -1.7f }, { -1.7f, 1.7f }, { 1.7f, 1.7f } };
            for (int i = 0; i < 4; i++)
            {
                float rx = rotors[i, 0], ry = rotors[i, 1];
                b.Box(RobotJoint.Body, rx - 0.08f, ry - 0.08f, 2.45f, 0.16f, 0.16f, 0.25f, Dark);
                b.Rotor(rx, ry, 2.7f, 0.8f, Ui.Hex("#E6F7FF"));
            }
            b.Joint(RobotJoint.Weapon, -0.46f, 0.2f, 1.94f);
            b.Box(RobotJoint.Weapon, -0.55f, 0.2f, 1.85f, 0.18f, 1.3f, 0.18f, Gun);
            b.Joint(RobotJoint.Weapon2, 0.46f, 0.2f, 1.94f);
            b.Box(RobotJoint.Weapon2, 0.37f, 0.2f, 1.85f, 0.18f, 1.3f, 0.18f, Gun);
            b.Muzzle(RobotJoint.Body, 0f, 1.5f, 1.94f);
            b.Anchors(top: 3.1f, halfW: 0.8f, front: 0.8f, back: -0.8f, bodyZ: 2.2f);
        }

        private static void Siege(RobotBuilder b, Color acc, Color team)
        {
            b.Joint(RobotJoint.LegL, -1.0f, 0f, 1.4f);
            b.Joint(RobotJoint.LegR, 1.0f, 0f, 1.4f);
            b.Box(RobotJoint.LegL, -1.5f, -0.7f, 0f, 1.0f, 1.4f, 1.4f, Dark);
            b.Box(RobotJoint.LegR, 0.5f, -0.7f, 0f, 1.0f, 1.4f, 1.4f, Dark);
            b.Box(RobotJoint.Body, -1.6f, -1.0f, 1.4f, 3.2f, 2.0f, 1.6f, acc);
            b.Box(RobotJoint.Body, -0.5f, -0.4f, 3.0f, 1f, 0.8f, 0.5f, Metal);
            b.Box(RobotJoint.Body, -0.4f, 0.4f, 3.15f, 0.8f, 0.06f, 0.2f, team, glow: true);
            b.Cylinder(RobotJoint.Body, -1.0f, -0.6f, 3.0f, 0.3f, 1.2f, Gun);
            b.Joint(RobotJoint.Weapon, 0f, 1.0f, 2.2f);
            b.Box(RobotJoint.Weapon, -0.4f, 1.0f, 1.8f, 0.8f, 2.2f, 0.8f, Gun);
            b.Box(RobotJoint.Weapon, -0.75f, 3.1f, 1.45f, 1.5f, 0.9f, 1.5f, Steel);
            b.Muzzle(RobotJoint.Weapon, 0f, 4.0f, 2.2f);
            b.Anchors(top: 3.5f, halfW: 1.6f, front: 1.0f, back: -1.0f, bodyZ: 1.4f);
        }

        /// <summary>표에 없는 유닛: 공격 방식만 보이는 기본형.</summary>
        private static void Generic(RobotBuilder b, Color acc, Color team)
        {
            b.Joint(RobotJoint.LegL, -0.5f, 0f, 1.2f);
            b.Joint(RobotJoint.LegR, 0.5f, 0f, 1.2f);
            b.Box(RobotJoint.LegL, -0.8f, -0.4f, 0f, 0.6f, 0.8f, 1.2f, Dark);
            b.Box(RobotJoint.LegR, 0.2f, -0.4f, 0f, 0.6f, 0.8f, 1.2f, Dark);
            b.Box(RobotJoint.Body, -1f, -0.7f, 1.2f, 2f, 1.4f, 1.6f, acc);
            b.Box(RobotJoint.Body, -0.4f, 0.7f, 2.3f, 0.8f, 0.06f, 0.25f, team, glow: true);
            b.Joint(RobotJoint.Weapon, 0.9f, 0.3f, 2f);
            b.Box(RobotJoint.Weapon, 0.8f, 0.3f, 1.8f, 0.3f, 1.6f, 0.3f, Gun);
            b.Muzzle(RobotJoint.Weapon, 0.95f, 1.9f, 2f);
            b.Anchors(top: 2.8f, halfW: 1f, front: 0.7f, back: -0.7f, bodyZ: 1.2f);
        }
    }

    /// <summary>시안 좌표로 부품을 붙이는 도우미. 관절의 축 위치를 기준으로 부품의 위치를 정한다.</summary>
    internal sealed class RobotBuilder
    {
        private readonly RobotModel _model;
        private readonly int _tier;
        private readonly Color _team;
        private readonly Dictionary<RobotJoint, Vector3> _pivots = new Dictionary<RobotJoint, Vector3>();
        private float _top;
        private float _halfW;
        private float _front;
        private float _back;
        private float _bodyZ;

        public RobotBuilder(RobotModel model, int tier, Color team)
        {
            _model = model;
            _tier = tier;
            _team = team;
            _pivots[RobotJoint.Body] = Vector3.zero;
        }

        /// <summary>단계 장식까지 포함한 로봇의 꼭대기 높이(시안 단위).</summary>
        public float Top => _top + (_tier >= 3 ? 0.7f : 0f);

        private static Vector3 U(float x, float y, float z) => new Vector3(x, z, y);

        public void Joint(RobotJoint joint, float x, float y, float z)
        {
            var pivot = U(x, y, z);
            _pivots[joint] = pivot;
            _model.AddJoint(joint, pivot);
        }

        private Transform Parent(RobotJoint joint) => _model.JointTransform(joint);

        public void Box(RobotJoint joint, float x, float y, float z, float w, float d, float h, Color c, bool glow = false)
        {
            var center = U(x + w / 2f, y + d / 2f, z + h / 2f) - _pivots[joint];
            _model.AddPart(Mat3D.Box(Parent(joint), "Box", center, new Vector3(w, h, d), c, glow), c);
        }

        public void Sphere(RobotJoint joint, float x, float y, float z, float r, Color c, bool glow = false)
        {
            _model.AddPart(Mat3D.Sphere(Parent(joint), "Sphere", U(x, y, z) - _pivots[joint], Vector3.one * r * 2f, c, glow), c);
        }

        public void Cylinder(RobotJoint joint, float x, float y, float z, float r, float h, Color c)
        {
            _model.AddPart(Mat3D.Cylinder(Parent(joint), "Cylinder", U(x, y, z + h / 2f) - _pivots[joint], new Vector3(r * 2f, h / 2f, r * 2f), c), c);
        }

        /// <summary>프로펠러: 도는 얇은 원판.</summary>
        public void Rotor(float x, float y, float z, float r, Color c)
        {
            var go = Mat3D.Cylinder(Parent(RobotJoint.Body), "Rotor", U(x, y, z), new Vector3(r * 2f, 0.02f, r * 2f), c);
            var blade = Mat3D.Box(go.transform, "RotorBlade", Vector3.zero, new Vector3(1f, 3f, 0.12f), Ui.Hex("#9AA6BD"));
            _model.AddRotor(go.transform);
            _model.AddPart(go, c);
            _model.AddPart(blade, Ui.Hex("#9AA6BD"));
        }

        public void Muzzle(RobotJoint joint, float x, float y, float z)
        {
            _model.SetMuzzle(joint, U(x, y, z) - _pivots[joint]);
        }

        public void Anchors(float top, float halfW, float front, float back, float bodyZ)
        {
            _top = top;
            _halfW = halfW;
            _front = front;
            _back = back;
            _bodyZ = bodyZ;
        }

        /// <summary>단계 장식(설계서 8장 "로봇 모습"): 2단계 어깨 갑옷과 가슴 불빛, 3단계 금색 뿔과 등 장갑.</summary>
        public void TierParts()
        {
            if (_tier >= 2)
            {
                float padZ = _top - 0.45f;
                Box(RobotJoint.Body, -_halfW - 0.35f, -0.5f, padZ, 0.7f, 1.0f, 0.35f, RobotFactory.Steel);
                Box(RobotJoint.Body, _halfW - 0.35f, -0.5f, padZ, 0.7f, 1.0f, 0.35f, RobotFactory.Steel);
                Box(RobotJoint.Body, -0.3f, _front, (_bodyZ + _top) / 2f, 0.6f, 0.08f, 0.35f, _team, glow: true);
            }
            if (_tier >= 3)
            {
                Box(RobotJoint.Body, -0.15f, -0.15f, _top, 0.3f, 0.3f, 0.7f, RobotFactory.Gold);
                // 등 장갑: 뒤에서 보는 카메라가 몸을 가리지 않게 낮고 좁은 판 두 개.
                float plateH = Mathf.Max(0.3f, (_top - _bodyZ) * 0.35f);
                Box(RobotJoint.Body, -_halfW * 0.8f, _back - 0.25f, _top - plateH, _halfW * 0.6f, 0.25f, plateH, RobotFactory.Gold);
                Box(RobotJoint.Body, _halfW * 0.2f, _back - 0.25f, _top - plateH, _halfW * 0.6f, 0.25f, plateH, RobotFactory.Gold);
            }
        }
    }

    /// <summary>
    /// 3D 로봇 하나. 화면 코드는 이 클래스의 동작(걷기, 준비 동작, 공격, 번쩍임)과 포구 위치만 쓴다.
    /// 구매한 에셋으로 바꿀 때도 같은 동작을 제공하면 된다.
    /// </summary>
    public sealed class RobotModel
    {
        private readonly Transform _root;
        private readonly Transform _body;
        private readonly Dictionary<RobotJoint, Transform> _joints = new Dictionary<RobotJoint, Transform>();
        private readonly Dictionary<RobotJoint, Vector3> _rest = new Dictionary<RobotJoint, Vector3>();
        private readonly List<Transform> _rotors = new List<Transform>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly AttackAnim _attack;
        private Transform _muzzle;
        private float _tint = -1f;
        private Color _tintColor;
        private float _scale = 1f;
        private float _hover;
        private float _top;

        public RobotModel(Transform parent, string id, AttackAnim attack, Color team)
        {
            var go = new GameObject("Robot_" + id);
            go.layer = World3D.Layer;
            go.transform.SetParent(parent, false);
            _root = go.transform;
            _body = new GameObject("Body").transform;
            _body.gameObject.layer = World3D.Layer;
            _body.SetParent(_root, false);
            _joints[RobotJoint.Body] = _body;
            _attack = attack;
        }

        public Transform Root => _root;
        /// <summary>로봇 머리 위 높이(전장 단위). 체력 막대를 그 위에 둔다.</summary>
        public float Height => _top * _scale + _hover;
        public float Hover => _hover;

        public Vector3 MuzzleLocal(Transform space)
        {
            var world = (_muzzle != null ? _muzzle : _body).position;
            return space.InverseTransformPoint(world);
        }

        internal void AddJoint(RobotJoint joint, Vector3 pivot)
        {
            var t = new GameObject(joint.ToString()).transform;
            t.gameObject.layer = World3D.Layer;
            t.SetParent(_body, false);
            t.localPosition = pivot;
            _joints[joint] = t;
            _rest[joint] = pivot;
        }

        internal Transform JointTransform(RobotJoint joint) => _joints.TryGetValue(joint, out var t) ? t : _body;

        internal void AddPart(GameObject part, Color color)
        {
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                _renderers.Add(renderer);
                _colors.Add(color);
            }
        }

        internal void AddRotor(Transform rotor) => _rotors.Add(rotor);

        internal void SetMuzzle(RobotJoint joint, Vector3 local)
        {
            _muzzle = new GameObject("Muzzle").transform;
            _muzzle.SetParent(JointTransform(joint), false);
            _muzzle.localPosition = local;
        }

        internal void Finish(float scale, float hover, float top)
        {
            _scale = scale;
            _hover = hover;
            _top = top;
            _root.localScale = Vector3.one * scale;
            _body.localPosition = new Vector3(0f, hover / Mathf.Max(0.01f, scale), 0f);
            Mat3D.SetLayerRecursively(_root.gameObject, World3D.Layer);
        }

        public void Place(Vector3 position, bool facingEnemy, Vector2 squash)
        {
            _root.localPosition = position;
            _root.localRotation = Quaternion.Euler(0f, facingEnemy ? 0f : 180f, 0f);
            _root.localScale = new Vector3(_scale * squash.x, _scale * squash.y, _scale * squash.x);
        }

        /// <summary>
        /// 관절을 움직인다. walkPhase: 걸은 거리에 따른 위상, moving: 걷는 중인가,
        /// phase 0 쉼 · 1 준비 동작(e: 0~1, 끝으로 갈수록 빠름) · 2 공격(t: 0~1).
        /// </summary>
        public void Animate(float time, float walkPhase, bool moving, float swing, int phase, float e, float t)
        {
            float step = moving ? Mathf.Sin(walkPhase) * swing : 0f;
            if (_joints.TryGetValue(RobotJoint.LegL, out var legL))
            {
                legL.localRotation = Quaternion.Euler(step, 0f, 0f);
            }
            if (_joints.TryGetValue(RobotJoint.LegR, out var legR))
            {
                legR.localRotation = Quaternion.Euler(-step, 0f, 0f);
            }
            var bodyPos = _body.localPosition;
            bodyPos.y = _hover / Mathf.Max(0.01f, _scale) + (moving ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.08f : 0f)
                + (_hover > 0f ? Mathf.Sin(time * 3f) * 0.1f : 0f);
            _body.localPosition = bodyPos;
            for (int i = 0; i < _rotors.Count; i++)
            {
                _rotors[i].localRotation = Quaternion.Euler(0f, time * 1080f + i * 45f, 0f);
            }

            if (!_joints.TryGetValue(RobotJoint.Weapon, out var weapon))
            {
                return;
            }
            var rest = _rest[RobotJoint.Weapon];
            float k = phase == 2 ? Mathf.Sin(t * Mathf.PI) : 0f;
            float ew = phase == 1 ? e : 0f;
            weapon.localRotation = Quaternion.identity;
            weapon.localPosition = rest;
            weapon.localScale = Vector3.one;
            switch (_attack)
            {
                case AttackAnim.Slash:
                {
                    // 쉬는 자세 -10도, 바깥으로 치켜들기 +80도, 앞을 가로질러 -70도까지 휘두른 뒤 돌아온다.
                    float yaw = -10f;
                    float pitch = 0f;
                    if (phase == 1)
                    {
                        yaw = Mathf.Lerp(-10f, 80f, ew);
                        pitch = -25f * ew;
                    }
                    else if (phase == 2)
                    {
                        yaw = t < 0.45f ? Mathf.Lerp(80f, -70f, t / 0.45f) : Mathf.Lerp(-70f, -10f, (t - 0.45f) / 0.55f);
                        pitch = t < 0.45f ? Mathf.Lerp(-25f, 10f, t / 0.45f) : Mathf.Lerp(10f, 0f, (t - 0.45f) / 0.55f);
                    }
                    weapon.localRotation = Quaternion.Euler(pitch, yaw, 0f);
                    break;
                }
                case AttackAnim.Bash:
                    weapon.localPosition = rest + new Vector3(0f, 0f, -0.3f * ew + 0.9f * k);
                    break;
                case AttackAnim.Stab:
                    weapon.localPosition = rest + new Vector3(0f, 0f, -0.5f * ew + 1.2f * k);
                    break;
                case AttackAnim.Shoot:
                case AttackAnim.Lob:
                    weapon.localPosition = rest + new Vector3(0f, 0f, -0.35f * k);
                    weapon.localScale = new Vector3(1f + 0.15f * ew, 1f, 1f + 0.15f * ew);
                    break;
                case AttackAnim.Strafe:
                {
                    float k1 = phase == 2 ? Mathf.Sin(Mathf.Clamp01(t * 2f) * Mathf.PI) : 0f;
                    float k2 = phase == 2 ? Mathf.Sin(Mathf.Clamp01(t * 2f - 1f) * Mathf.PI) : 0f;
                    weapon.localPosition = rest + new Vector3(0f, 0f, -0.3f * k1);
                    if (_joints.TryGetValue(RobotJoint.Weapon2, out var second))
                    {
                        second.localPosition = _rest[RobotJoint.Weapon2] + new Vector3(0f, 0f, -0.3f * k2);
                    }
                    break;
                }
                case AttackAnim.Dash:
                    weapon.localScale = Vector3.one * (1f + 0.4f * ew + 0.3f * k);
                    break;
                case AttackAnim.Burst:
                    weapon.localScale = Vector3.one * (1f + 0.9f * ew + (phase == 1 ? 0.15f * Mathf.Sin(time * 70f) : 0f));
                    break;
            }
        }

        /// <summary>몸 전체 색을 target 쪽으로 amount만큼(맞음 번쩍임, 준비 동작 빛, 기절).</summary>
        public void SetTint(float amount, Color target)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Abs(amount - _tint) < 0.01f && target == _tintColor)
            {
                return;
            }
            _tint = amount;
            _tintColor = target;
            for (int i = 0; i < _renderers.Count; i++)
            {
                Mat3D.Tint(_renderers[i], _colors[i], amount, target);
            }
        }

        private Transform _barrier;
        private Renderer _barrierRenderer;

        /// <summary>방어진: 앞에 편 색 방어막을 세운다(시안 단위, 몸 앞쪽).</summary>
        public void ShowBarrier(bool visible, Color team, float time)
        {
            if (visible && _barrier == null)
            {
                var go = Mat3D.Box(_root, "Barrier", new Vector3(0f, 0.8f, 2.3f), new Vector3(4.6f, 1.5f, 0.12f), Color.Lerp(team, Color.white, 0.2f), true);
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _barrier = go.transform;
                _barrierRenderer = go.GetComponent<Renderer>();
                // 방패를 땅에 박는다.
                if (_rest.ContainsKey(RobotJoint.Weapon))
                {
                    _rest[RobotJoint.Weapon] = _rest[RobotJoint.Weapon] + new Vector3(0f, -0.25f, 0.3f);
                }
            }
            if (_barrier != null)
            {
                _barrier.gameObject.SetActive(visible);
                if (visible)
                {
                    // 숨 쉬듯 살짝 커졌다 작아진다.
                    float pulse = 1f + 0.04f * Mathf.Sin(time * 4f);
                    _barrier.localScale = new Vector3(4.6f * pulse, 1.5f, 0.12f);
                }
            }
        }

        /// <summary>돌격 중: 몸을 앞으로 기울인다.</summary>
        public void SetLean(float degrees)
        {
            _body.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root.gameObject);
            }
        }
    }
}
