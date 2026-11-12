using System.Collections.Generic;
using UnityEngine;

namespace RPGGame
{
    /// <summary>
    /// 보스 공격이 닿을 바닥 범위를 미리 보여주는 예고 표시.
    /// 연한 붉은 판 위로 진한 색이 가운데(원)나 시작점(직선)에서 차오르고, 다 차는 순간이 실제 피해 시점이다.
    /// 패턴은 "어디에, 얼마나 오래"만 알려 주면 되도록 정적 함수로 모아 두었다.
    /// 메시와 머티리얼은 한 번만 만들어 모든 예고가 공유한다
    /// </summary>
    public static class BossTelegraph
    {
        private const string MaterialPath = "Materials/BossTelegraph";
        private const int CircleSegments = 48;
        // 지면 위로 띄우는 높이 (지면과 겹쳐 깜빡이는 현상을 막는다)
        private const float GroundOffset = 0.12f;

        private static readonly Color BaseColor = new Color(1f, 0.18f, 0.08f, 0.12f);
        private static readonly Color FillColor = new Color(1f, 0.3f, 0.1f, 0.3f);

        private static readonly List<BossTelegraphView> active = new List<BossTelegraphView>();
        private static readonly RaycastHit[] groundHits = new RaycastHit[8];

        private static Material material;
        private static Mesh circleMesh;
        private static Mesh rectMesh;
        // 원뿔 예고용 부채꼴 메시. 각도마다 모양이 달라 각도(정수)별로 한 번만 만들어 재사용한다
        private static readonly Dictionary<int, Mesh> coneMeshes = new Dictionary<int, Mesh>();

        /// <summary>원형 범위를 예고한다. 가운데에서 바깥으로 차오른다. 공격이 발동되는 순간 반환값의 Dismiss를 호출한다.</summary>
        public static BossTelegraphView Circle(Vector3 center, float radius, float duration)
        {
            if (radius <= 0f)
            {
                return null;
            }

            GameObject root = new GameObject("Boss Telegraph (Circle)");
            root.transform.position = new Vector3(center.x, GroundHeight(center) + GroundOffset, center.z);

            Transform baseTransform = CreatePart(root.transform, "Base", GetCircleMesh(), BaseColor, 0);
            baseTransform.localScale = new Vector3(radius, 1f, radius);

            Transform fillTransform = CreatePart(root.transform, "Fill", GetCircleMesh(), FillColor, 1);
            fillTransform.localScale = Vector3.zero;

            return Register(root, fillTransform, false, radius, 1f, duration);
        }

        /// <summary>
        /// 원뿔(부채꼴) 범위를 예고한다. origin에서 direction 쪽으로 length만큼, 좌우 합쳐 angle도 벌어진다.
        /// 시작점에서 바깥으로 차오르고 다 차는 순간이 피해 시점이다.
        /// </summary>
        public static BossTelegraphView Cone(Vector3 origin, Vector3 direction, float length, float angle, float duration)
        {
            direction.y = 0f;
            if (length <= 0f || angle <= 0f || direction.sqrMagnitude < 0.0001f)
            {
                return null;
            }

            GameObject root = new GameObject("Boss Telegraph (Cone)");
            root.transform.SetPositionAndRotation(
                new Vector3(origin.x, GroundHeight(origin) + GroundOffset, origin.z),
                Quaternion.LookRotation(direction.normalized));

            Mesh mesh = GetConeMesh(angle);

            Transform baseTransform = CreatePart(root.transform, "Base", mesh, BaseColor, 0);
            baseTransform.localScale = new Vector3(length, 1f, length);

            Transform fillTransform = CreatePart(root.transform, "Fill", mesh, FillColor, 1);
            fillTransform.localScale = Vector3.zero;

            // 반지름이 1인 부채꼴을 균등하게 키우므로 원 예고와 같은 채움 방식을 쓴다 (isLine=false)
            return Register(root, fillTransform, false, length, 1f, duration);
        }

        /// <summary>직선 범위를 예고한다. 시작점에서 끝으로 차오른다.</summary>
        public static BossTelegraphView Line(Vector3 origin, Vector3 direction, float length, float width, float duration)
        {
            direction.y = 0f;
            if (length <= 0f || width <= 0f || direction.sqrMagnitude < 0.0001f)
            {
                return null;
            }

            GameObject root = new GameObject("Boss Telegraph (Line)");
            root.transform.SetPositionAndRotation(
                new Vector3(origin.x, GroundHeight(origin) + GroundOffset, origin.z),
                Quaternion.LookRotation(direction.normalized));

            Transform baseTransform = CreatePart(root.transform, "Base", GetRectMesh(), BaseColor, 0);
            baseTransform.localScale = new Vector3(width, 1f, length);

            Transform fillTransform = CreatePart(root.transform, "Fill", GetRectMesh(), FillColor, 1);
            fillTransform.localScale = new Vector3(width, 1f, 0f);

            return Register(root, fillTransform, true, length, width, duration);
        }

        /// <summary>남아 있는 예고를 모두 지운다 (보스 사망·전투 리셋 때).</summary>
        public static void ClearAll()
        {
            for (int ix = active.Count - 1; ix >= 0; --ix)
            {
                if (active[ix] != null)
                {
                    Object.Destroy(active[ix].gameObject);
                }
            }

            active.Clear();

            // 예고와 함께 남아 있는 장판·화상도 정리한다. 전투가 끝났는데 계속 피해를 주면 안 된다
            BossDamageZone.ClearAll();
            BurnEffect.ClearAll();
            PlayerSlowEffect.ClearAll();
        }

        internal static void Unregister(BossTelegraphView view)
        {
            active.Remove(view);
        }

        private static BossTelegraphView Register(GameObject root, Transform fill, bool isLine, float size, float width, float duration)
        {
            BossTelegraphView view = root.AddComponent<BossTelegraphView>();
            view.Initialize(fill, isLine, size, width, duration);
            active.Add(view);
            return view;
        }

        private static Transform CreatePart(Transform parent, string name, Mesh mesh, Color color, int sortingOrder)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);

            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;

            // 같은 머티리얼에 색만 다르게 주려고 인스턴스를 만들지 않고 프로퍼티 블록을 쓴다
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);

            return part.transform;
        }

        // 지형 높이를 구한다. 캐릭터 콜라이더(보스·플레이어)에 맞으면 바닥이 아니라 머리 높이가 나오므로,
        // 모든 충돌 중 지형 콜라이더만 고른다
        private static float GroundHeight(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 60f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 200f, ~0, QueryTriggerInteraction.Ignore);

            float best = float.NegativeInfinity;
            for (int ix = 0; ix < count; ++ix)
            {
                if (groundHits[ix].collider is TerrainCollider && groundHits[ix].point.y > best)
                {
                    best = groundHits[ix].point.y;
                }
            }

            return float.IsNegativeInfinity(best) ? position.y : best;
        }

        private static Material GetMaterial()
        {
            if (material == null)
            {
                material = Resources.Load<Material>(MaterialPath);
            }

            return material;
        }

        // 반지름 1짜리 원판. 스케일로 크기를 맞춘다
        private static Mesh GetCircleMesh()
        {
            if (circleMesh != null)
            {
                return circleMesh;
            }

            Vector3[] vertices = new Vector3[CircleSegments + 1];
            int[] triangles = new int[CircleSegments * 3];

            vertices[0] = Vector3.zero;
            for (int ix = 0; ix < CircleSegments; ++ix)
            {
                float angle = ix * Mathf.PI * 2f / CircleSegments;
                vertices[ix + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            for (int ix = 0; ix < CircleSegments; ++ix)
            {
                triangles[ix * 3] = 0;
                // 위에서 봤을 때 앞면이 되도록 감는 순서를 정한다
                triangles[ix * 3 + 1] = (ix + 1) % CircleSegments + 1;
                triangles[ix * 3 + 2] = ix + 1;
            }

            circleMesh = new Mesh { name = "Boss Telegraph Circle", vertices = vertices, triangles = triangles };
            circleMesh.RecalculateBounds();
            return circleMesh;
        }

        // 반지름 1, 정면(+Z)을 중심으로 좌우 합쳐 angle도인 부채꼴. 스케일로 길이를 맞춘다
        private static Mesh GetConeMesh(float angle)
        {
            int key = Mathf.RoundToInt(angle);
            if (coneMeshes.TryGetValue(key, out Mesh cached) && cached != null)
            {
                return cached;
            }

            int segments = Mathf.Max(6, Mathf.CeilToInt(angle / 5f));
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            float half = angle * 0.5f * Mathf.Deg2Rad;
            for (int ix = 0; ix <= segments; ++ix)
            {
                float a = Mathf.Lerp(-half, half, ix / (float)segments);
                vertices[ix + 1] = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            }

            for (int ix = 0; ix < segments; ++ix)
            {
                // 왼쪽에서 오른쪽으로 감아 위에서 봤을 때 앞면이 되게 한다 (직사각형 예고와 같은 방향)
                triangles[ix * 3] = 0;
                triangles[ix * 3 + 1] = ix + 1;
                triangles[ix * 3 + 2] = ix + 2;
            }

            Mesh mesh = new Mesh { name = "Boss Telegraph Cone " + key, vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds();
            coneMeshes[key] = mesh;
            return mesh;
        }

        // 폭 1, 길이 1짜리 직사각형 (x는 -0.5~0.5, z는 0~1). 시작점이 원점이라 길이만 늘리면 앞으로 자란다
        private static Mesh GetRectMesh()
        {
            if (rectMesh != null)
            {
                return rectMesh;
            }

            rectMesh = new Mesh
            {
                name = "Boss Telegraph Rect",
                vertices = new[]
                {
                    new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f),
                    new Vector3(0.5f, 0f, 1f), new Vector3(0.5f, 0f, 0f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            rectMesh.RecalculateBounds();
            return rectMesh;
        }
    }
}
