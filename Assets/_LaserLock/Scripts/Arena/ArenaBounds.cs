using UnityEngine;

namespace LaserLock.Arena
{
    public class ArenaBounds : MonoBehaviour
    {
        [Header("Arena Bounds")]
        [Tooltip("Outer radius of the hexagonal arena")]
        [SerializeField] float radius = 7f;
        [SerializeField] float wallHeight = 4f;

        public float Radius => radius;

        /// <summary>
        /// Inradius of the regular hexagon (distance from center to edge midpoint)
        /// Inradius = radius * cos(30°) ≈ radius * 0.866025
        /// </summary>
        public float Inradius => radius * 0.866025f;

        public Vector3 ClampPosition(Vector3 position, float characterRadius = 0.5f)
        {
            Vector3 center = transform.position;
            Vector3 offset = position - center;
            offset.y = 0f;

            float maxAllowedDist = Mathf.Max(0.5f, Inradius - characterRadius);
            if (offset.magnitude > maxAllowedDist)
            {
                offset = offset.normalized * maxAllowedDist;
            }

            return new Vector3(center.x + offset.x, position.y, center.z + offset.z);
        }

        public bool IsInsideArena(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude <= Inradius;
        }

        public Vector3 GetRandomPosition(float margin = 1f)
        {
            float maxR = Mathf.Max(1f, Inradius - margin);
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(0.5f, maxR);

            return transform.position + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        }

        /// <summary>
        /// Generates evenly distributed spawn positions inside the arena ring.
        /// </summary>
        public Vector3[] GetSpawnPositions(int count, float margin = 1.8f)
        {
            if (count <= 0) return new Vector3[0];

            Vector3[] positions = new Vector3[count];
            float spawnRadius = Mathf.Max(1.5f, Inradius - margin);

            for (int i = 0; i < count; i++)
            {
                float angle = (360f / count) * i * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spawnRadius;
                positions[i] = transform.position + offset;
            }

            return positions;
        }

        [ContextMenu("Build Hex Arena Geometry (Editor Only)")]
        public void BuildArenaGeometryEditor()
        {
            ClearArenaGeometryEditor();

            GameObject wallsParent = new GameObject("Walls");
            wallsParent.transform.SetParent(transform, false);

            for (int i = 0; i < 6; i++)
            {
                float a1 = 60f * i * Mathf.Deg2Rad;
                float a2 = 60f * (i + 1) * Mathf.Deg2Rad;

                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Vector3 p2 = new Vector3(Mathf.Cos(a2) * radius, 0f, Mathf.Sin(a2) * radius);

                Vector3 midPoint = (p1 + p2) * 0.5f + Vector3.up * (wallHeight * 0.5f);
                Vector3 edgeDir = p2 - p1;

                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{i + 1}";
                wall.transform.SetParent(wallsParent.transform, false);
                wall.transform.localPosition = midPoint;
                wall.transform.localRotation = Quaternion.LookRotation(edgeDir.normalized) * Quaternion.Euler(0, 90, 0);
                wall.transform.localScale = new Vector3(0.1f, wallHeight, edgeDir.magnitude);
                wall.layer = LayerMask.NameToLayer("Arena") != -1 ? LayerMask.NameToLayer("Arena") : 0;

                // Corner post
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = $"Post_{i + 1}";
                post.transform.SetParent(wallsParent.transform, false);
                post.transform.localPosition = p1 + Vector3.up * (wallHeight * 0.5f);
                post.transform.localScale = new Vector3(0.25f, wallHeight * 0.5f, 0.25f);
                post.layer = LayerMask.NameToLayer("Arena") != -1 ? LayerMask.NameToLayer("Arena") : 0;
            }

            // Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(radius * 1.732f, 0.05f, radius * 1.732f);
            floor.layer = LayerMask.NameToLayer("Arena") != -1 ? LayerMask.NameToLayer("Arena") : 0;

            Debug.Log("[ArenaBounds] Hex arena geometry created in scene under " + name);
        }

        [ContextMenu("Clear Arena Geometry (Editor Only)")]
        public void ClearArenaGeometryEditor()
        {
            Transform existingWalls = transform.Find("Walls");
            if (existingWalls != null)
            {
                DestroyImmediate(existingWalls.gameObject);
            }
            Transform existingFloor = transform.Find("Floor");
            if (existingFloor != null)
            {
                DestroyImmediate(existingFloor.gameObject);
            }
        }

        void OnDrawGizmosSelected()
        {
            DrawHexGizmo(transform.position, radius, Color.cyan);
            DrawHexGizmo(transform.position, Inradius, Color.yellow);
        }

        void DrawHexGizmo(Vector3 center, float r, Color color)
        {
            Gizmos.color = color;
            for (int i = 0; i < 6; i++)
            {
                float a1 = 60f * i * Mathf.Deg2Rad;
                float a2 = 60f * (i + 1) * Mathf.Deg2Rad;

                Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r);
                Vector3 p2 = center + new Vector3(Mathf.Cos(a2) * r, 0f, Mathf.Sin(a2) * r);
                Gizmos.DrawLine(p1, p2);
            }
        }
    }
}
