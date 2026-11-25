using System.Collections;
using UnityEngine;
using LaserLock.Characters;

namespace LaserLock.Weapons
{
    [RequireComponent(typeof(LineRenderer))]
    public class LaserController : MonoBehaviour
    {
        [Header("Beam Setup")]
        [SerializeField] LineRenderer lineRenderer;
        [SerializeField] Transform muzzleTransform;
        [SerializeField] float maxDistance = 60f;

        [Header("Collision Layers")]
        [Tooltip("Layers the laser can collide with (Character and Arena/Obstacles)")]
        [SerializeField] LayerMask collisionMask = ~0;

        [Header("Owner")]
        [Tooltip("The character who owns this laser (prevents self-hits)")]
        [SerializeField] CharacterState ownerState;

        [Header("Visual Settings")]
        [SerializeField] Color beamColor = Color.red;
        [SerializeField] float beamWidth = 0.08f;

        [Header("Elimination Highlight")]
        [SerializeField] float surgeMultiplier = 3.5f;
        [SerializeField] float surgeDuration = 0.8f;

        [Header("Behavior")]
        [Tooltip("If true, laser eliminates immediately on touch. If false, MatchManager evaluates hits during the resolution phase.")]
        [SerializeField] bool autoEliminateOnUpdate = false;

        bool isLaserActive = true;
        Coroutine surgeCoroutine;

        public Transform MuzzleTransform => muzzleTransform;
        public bool IsLaserActive => isLaserActive;

        void Awake()
        {
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
            }
            if (ownerState == null)
            {
                ownerState = GetComponentInParent<CharacterState>();
            }
            if (muzzleTransform == null)
            {
                muzzleTransform = transform;
            }

            ConfigureLineRenderer();
        }

        void OnValidate()
        {
            if (lineRenderer != null)
            {
                ConfigureLineRenderer();
            }
        }

        void ConfigureLineRenderer()
        {
            lineRenderer.positionCount = 2;
            lineRenderer.startWidth = beamWidth;
            lineRenderer.endWidth = beamWidth;
            lineRenderer.startColor = beamColor;
            lineRenderer.endColor = beamColor;
            lineRenderer.useWorldSpace = true;
        }

        public void SetLaserActive(bool active)
        {
            isLaserActive = active;
            if (lineRenderer != null)
            {
                lineRenderer.enabled = active && (ownerState == null || ownerState.IsAlive);
            }
        }

        public void SetBeamColor(Color color)
        {
            beamColor = color;
            if (lineRenderer != null)
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }
        }

        /// <summary>
        /// Visually surges and highlights the laser beam when it scores an elimination!
        /// </summary>
        public void TriggerEliminationSurge(Vector3 hitPoint)
        {
            if (lineRenderer == null || !gameObject.activeInHierarchy) return;

            if (surgeCoroutine != null)
            {
                StopCoroutine(surgeCoroutine);
            }
            surgeCoroutine = StartCoroutine(SurgeRoutine(hitPoint));
        }

        IEnumerator SurgeRoutine(Vector3 hitPoint)
        {
            float targetWidth = beamWidth * surgeMultiplier;
            Color brightColor = Color.Lerp(beamColor, Color.white, 0.6f);

            // Instant surge
            lineRenderer.startWidth = targetWidth;
            lineRenderer.endWidth = targetWidth;
            lineRenderer.startColor = brightColor;
            lineRenderer.endColor = brightColor;

            // Spawn impact spark at the hit point
            SpawnImpactFlash(hitPoint);

            // Hold surge, then smoothly interpolate back
            float elapsed = 0f;
            while (elapsed < surgeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / surgeDuration;

                float w = Mathf.Lerp(targetWidth, beamWidth, t);
                lineRenderer.startWidth = w;
                lineRenderer.endWidth = w;

                Color c = Color.Lerp(brightColor, beamColor, t);
                lineRenderer.startColor = c;
                lineRenderer.endColor = c;

                yield return null;
            }

            lineRenderer.startWidth = beamWidth;
            lineRenderer.endWidth = beamWidth;
            lineRenderer.startColor = beamColor;
            lineRenderer.endColor = beamColor;
        }

        void SpawnImpactFlash(Vector3 point)
        {
            GameObject spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spark.name = "Laser_Impact_Flash";
            spark.transform.position = point;
            spark.transform.localScale = Vector3.one * 0.35f;

            var col = spark.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = spark.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                mat.color = Color.Lerp(beamColor, Color.white, 0.7f);
                mr.material = mat;
            }

            StartCoroutine(AnimateImpactFlash(spark));
        }

        IEnumerator AnimateImpactFlash(GameObject spark)
        {
            if (spark == null) yield break;

            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 initScale = Vector3.one * 0.4f;

            while (elapsed < duration)
            {
                if (spark == null) yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                spark.transform.localScale = Vector3.Lerp(initScale, Vector3.zero, t * t);
                yield return null;
            }

            if (spark != null)
            {
                Destroy(spark);
            }
        }

        void LateUpdate()
        {
            UpdateLaserBeam();
        }

        public void UpdateLaserBeam()
        {
            // If character is eliminated or laser is deactivated, turn off the beam
            if ((ownerState != null && !ownerState.IsAlive) || !isLaserActive)
            {
                if (lineRenderer != null && lineRenderer.enabled)
                {
                    lineRenderer.enabled = false;
                }
                return;
            }

            if (lineRenderer != null && !lineRenderer.enabled)
            {
                lineRenderer.enabled = true;
            }

            Vector3 origin = muzzleTransform.position;
            Vector3 direction = muzzleTransform.forward;
            Vector3 targetPoint = origin + (direction * maxDistance);

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance, collisionMask, QueryTriggerInteraction.Ignore);

            if (hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    CharacterHitBox hitBox = hit.collider.GetComponent<CharacterHitBox>();
                    if (hitBox != null)
                    {
                        if (hitBox.OwnerState != null && hitBox.OwnerState == ownerState)
                        {
                            continue; // Skip self
                        }

                        targetPoint = hit.point;
                        if (autoEliminateOnUpdate)
                        {
                            hitBox.TakeHit(hit.point);
                        }
                        break;
                    }

                    CharacterState otherState = hit.collider.GetComponentInParent<CharacterState>();
                    if (otherState != null)
                    {
                        if (otherState == ownerState)
                        {
                            continue; // Skip self
                        }

                        targetPoint = hit.point;
                        if (autoEliminateOnUpdate)
                        {
                            otherState.Eliminate(hit.point);
                        }
                        break;
                    }

                    if (!hit.collider.isTrigger)
                    {
                        targetPoint = hit.point;
                        break;
                    }
                }
            }

            if (lineRenderer != null)
            {
                lineRenderer.SetPosition(0, origin);
                lineRenderer.SetPosition(1, targetPoint);
            }
        }

        public bool CheckTarget(out CharacterHitBox targetHitBox, out Vector3 hitPoint)
        {
            targetHitBox = null;
            hitPoint = Vector3.zero;

            if (ownerState != null && !ownerState.IsAlive) return false;

            Vector3 origin = muzzleTransform.position;
            Vector3 direction = muzzleTransform.forward;

            RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance, collisionMask, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                CharacterHitBox hitBox = hit.collider.GetComponent<CharacterHitBox>();
                if (hitBox != null)
                {
                    if (hitBox.OwnerState != null && hitBox.OwnerState == ownerState)
                    {
                        continue;
                    }

                    targetHitBox = hitBox;
                    hitPoint = hit.point;
                    return true;
                }

                CharacterState otherState = hit.collider.GetComponentInParent<CharacterState>();
                if (otherState != null)
                {
                    if (otherState == ownerState) continue;

                    targetHitBox = hit.collider.GetComponentInChildren<CharacterHitBox>();
                    hitPoint = hit.point;
                    return true;
                }

                if (!hit.collider.isTrigger)
                {
                    hitPoint = hit.point;
                    return false;
                }
            }

            return false;
        }
    }
}
