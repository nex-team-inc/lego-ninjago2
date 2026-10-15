#nullable enable

using System;
using UnityEngine;
using UnityEngine.UI;

namespace Nex.Ninjago
{
    /// <summary>
    /// A big blocky monster on the gauntlet floor. It never moves toward the ninja and never attacks: only the current
    /// one is lit and shows its health bar; it flinches on each hit and breaks when the bar empties.
    /// </summary>
    public class GauntletMonster : MonoBehaviour
    {
        [Header("Body")]
        [SerializeField] Transform body = null!;
        [Header("Lit Renderers")]
        [Tooltip("Swap to the dim material while this monster is not the one being fought.")]
        [SerializeField] Renderer[] litRenderers = null!;
        [Header("Dim Material")]
        [SerializeField] Material dimMaterial = null!;
        [Header("Health Bar Root")]
        [SerializeField] GameObject healthBarRoot = null!;
        [Header("Health Bar Fill")]
        [SerializeField] Image healthBarFill = null!;
        [Header("Chest Point")]
        [Tooltip("Where hits land and effects play.")]
        [SerializeField] Transform chestPoint = null!;
        [Header("Flinch Seconds")]
        [SerializeField] float flinchSeconds = 0.18f;

        Material[] litMaterials = Array.Empty<Material>();
        float flinchElapsed = float.PositiveInfinity;

        public Vector3 ChestPosition => chestPoint.position;

        #region Life Cycle

        void Awake()
        {
            litMaterials = new Material[litRenderers.Length];
            for (var i = 0; i < litRenderers.Length; i++) litMaterials[i] = litRenderers[i].sharedMaterial;
        }

        void Update()
        {
            flinchElapsed += Time.deltaTime;
            var flinch = 1f - Mathf.Clamp01(flinchElapsed / flinchSeconds);
            body.localScale = new Vector3(1f + 0.12f * flinch, 1f - 0.1f * flinch, 1f + 0.12f * flinch);
        }

        #endregion

        #region Public API

        /// <summary>Lit with its health bar while it is the one being fought; dim otherwise.</summary>
        public void SetActive(bool active)
        {
            for (var i = 0; i < litRenderers.Length; i++) litRenderers[i].sharedMaterial = active ? litMaterials[i] : dimMaterial;
            healthBarRoot.SetActive(active);
            if (active) healthBarFill.fillAmount = 1f;
        }

        public void Hit(float health01)
        {
            healthBarFill.fillAmount = health01;
            flinchElapsed = 0f;
        }

        public void Break()
        {
            gameObject.SetActive(false);
        }

        #endregion
    }
}
