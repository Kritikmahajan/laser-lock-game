using System;
using System.Collections;
using UnityEngine;

namespace LaserLock.Characters
{
    public class CharacterState : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] bool isAlive = true;

        [Header("Visual & Collider References")]
        [Tooltip("Root object containing character renderers. If empty, all child Renderers are used.")]
        [SerializeField] GameObject visualRoot;
        [SerializeField] Collider bodyCollider;

        [Header("Elimination Visual Effects")]
        [SerializeField] Color flashColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] float shrinkDuration = 0.45f;

        Renderer[] cachedRenderers;
        Material[] clonedMaterials;
        Color[] originalColors;
        Vector3 originalScale;

        public bool IsAlive => isAlive;
        public bool IsControlLocked { get; private set; }
        public bool IsVisible { get; private set; } = true;

        public event Action<CharacterState> OnEliminated;
        public event Action<bool> OnVisibilityChanged;

        void Awake()
        {
            if (bodyCollider == null)
            {
                bodyCollider = GetComponentInChildren<Collider>();
            }

            originalScale = transform.localScale;

            cachedRenderers = visualRoot != null
                ? visualRoot.GetComponentsInChildren<Renderer>()
                : GetComponentsInChildren<Renderer>();

            CacheOriginalColors();
        }

        void CacheOriginalColors()
        {
            if (cachedRenderers == null || cachedRenderers.Length == 0) return;

            clonedMaterials = new Material[cachedRenderers.Length];
            originalColors = new Color[cachedRenderers.Length];

            for (int i = 0; i < cachedRenderers.Length; i++)
            {
                var r = cachedRenderers[i];
                if (r != null && !(r is LineRenderer) && r.material != null)
                {
                    clonedMaterials[i] = r.material;
                    if (r.material.HasProperty("_BaseColor"))
                    {
                        originalColors[i] = r.material.GetColor("_BaseColor");
                    }
                    else if (r.material.HasProperty("_Color"))
                    {
                        originalColors[i] = r.material.color;
                    }
                    else
                    {
                        originalColors[i] = Color.white;
                    }
                }
            }
        }

        public void SetControlLocked(bool locked)
        {
            IsControlLocked = locked;
        }

        public void SetVisibility(bool visible)
        {
            // Dead characters must NEVER become visible again on the board!
            if (!isAlive && visible)
            {
                visible = false;
            }

            IsVisible = visible;

            if (visualRoot != null)
            {
                visualRoot.SetActive(visible);
            }
            else if (cachedRenderers != null)
            {
                foreach (var r in cachedRenderers)
                {
                    if (r != null && !(r is LineRenderer))
                    {
                        r.enabled = visible;
                    }
                }
            }

            if (bodyCollider != null)
            {
                bodyCollider.enabled = visible && isAlive;
            }

            OnVisibilityChanged?.Invoke(visible);
        }

        public void Eliminate()
        {
            Eliminate(transform.position + Vector3.up * 0.5f);
        }

        public void Eliminate(Vector3 hitPoint)
        {
            if (!isAlive) return;

            isAlive = false;

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            // Play elimination animation (flash + shrink + shockwave + remove)
            StartCoroutine(EliminationAnimationRoutine(hitPoint));

            OnEliminated?.Invoke(this);
        }

        IEnumerator EliminationAnimationRoutine(Vector3 hitPoint)
        {
            // 1. Flash character bright red/white
            FlashColor(flashColor);

            // 2. Spawn expanding shockwave ring at character position
            SpawnEliminationVFX(hitPoint != default ? hitPoint : transform.position + Vector3.up * 0.5f);

            // 3. Shake and shrink scale down to 0
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;

            while (elapsed < shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shrinkDuration);

                // Small jitter/shake
                Vector3 shake = UnityEngine.Random.insideUnitSphere * (0.08f * (1f - t));
                shake.y = 0f;

                // Squash & Shrink: stretch slightly on Y then vanish
                float scaleFactor = Mathf.Lerp(1f, 0f, t * t);
                transform.localScale = startScale * scaleFactor;
                transform.position += shake;

                yield return null;
            }

            // 4. Completely hide from the board
            transform.localScale = Vector3.zero;
            SetVisibility(false);
        }

        void FlashColor(Color color)
        {
            if (clonedMaterials == null) return;

            for (int i = 0; i < clonedMaterials.Length; i++)
            {
                var mat = clonedMaterials[i];
                if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", color);
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        mat.color = color;
                    }

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", color * 2f);
                        mat.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        void RestoreColors()
        {
            if (clonedMaterials == null || originalColors == null) return;

            for (int i = 0; i < clonedMaterials.Length; i++)
            {
                var mat = clonedMaterials[i];
                if (mat != null && i < originalColors.Length)
                {
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", originalColors[i]);
                    }
                    else if (mat.HasProperty("_Color"))
                    {
                        mat.color = originalColors[i];
                    }

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", Color.black);
                    }
                }
            }
        }

        void SpawnEliminationVFX(Vector3 point)
        {
            // Create a procedural expanding shockwave ring
            GameObject shockwave = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shockwave.name = "Elimination_Shockwave";
            shockwave.transform.position = new Vector3(point.x, 0.05f, point.z);
            shockwave.transform.localScale = new Vector3(0.2f, 0.02f, 0.2f);

            // Remove collider
            var col = shockwave.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = shockwave.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material vfxMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
                vfxMat.color = new Color(1f, 0.3f, 0.2f, 0.8f);
                mr.material = vfxMat;
            }

            // Animate expand and destroy
            StartCoroutine(AnimateShockwave(shockwave));
        }

        IEnumerator AnimateShockwave(GameObject wave)
        {
            if (wave == null) yield break;

            float duration = 0.5f;
            float elapsed = 0f;
            Vector3 initScale = new Vector3(0.2f, 0.02f, 0.2f);
            Vector3 targetScale = new Vector3(3.5f, 0.01f, 3.5f);

            var mr = wave.GetComponent<MeshRenderer>();
            Color startColor = mr != null ? mr.material.color : Color.red;

            while (elapsed < duration)
            {
                if (wave == null) yield break;

                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                wave.transform.localScale = Vector3.Lerp(initScale, targetScale, Mathf.SmoothStep(0f, 1f, t));

                if (mr != null && mr.material != null)
                {
                    Color c = startColor;
                    c.a = Mathf.Lerp(0.8f, 0f, t);
                    mr.material.color = c;
                }

                yield return null;
            }

            if (wave != null)
            {
                Destroy(wave);
            }
        }

        public void ResetState()
        {
            StopAllCoroutines();
            isAlive = true;
            IsControlLocked = false;
            transform.localScale = originalScale != Vector3.zero ? originalScale : Vector3.one;

            RestoreColors();
            SetVisibility(true);

            if (bodyCollider != null)
            {
                bodyCollider.enabled = true;
            }
        }
    }
}
