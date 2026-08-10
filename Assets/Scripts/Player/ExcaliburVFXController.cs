using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_2D_LIGHTING
using UnityEngine.Rendering.Universal;
#endif

#if CINEMACHINE_V3
using Unity.Cinemachine;
#elif CINEMACHINE_PRESENT
using Cinemachine;
#endif

namespace TheLastKnight.Player
{
    /// <summary>
    /// Controller for the Excalibur Morgan 5-phase Ultimate Attack sequence inspired by Fate/stay night: Heaven's Feel.
    /// Manages sprite frame swapping, background dimming, anticipation aura, ground crack sigil, charge vortex streaks, 
    /// arcing red lightning, cape turbulence/vibration, vertical climax pillar, flying debris, hit-stop, white screen flash, 
    /// horizontal energy beam, expanding shockwave rings, ground scorch, and Cinemachine camera shake.
    /// Positioned accurately based on sprite pixel analysis for Frame 2 blade and Frame 3 thrust tip.
    /// Supports execution both in Play Mode and Edit Mode context menu cleanly.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class ExcaliburVFXController : MonoBehaviour
    {
        [Header("Sprites (3 Phases)")]
        [Tooltip("Phase 1: Anticipation Sprite (0.5s)")]
        [SerializeField] private Sprite spritePhase1;
        
        [Tooltip("Phase 2: Ultimate Charge Sprite (2.8s)")]
        [SerializeField] private Sprite spritePhase2;
        
        [Tooltip("Phase 3: The Thrust / Beam Release Sprite (0.5s)")]
        [SerializeField] private Sprite spritePhase3;

        [Header("Timing Configuration")]
        [SerializeField] private float anticipationDuration = 0.5f;
        [SerializeField] private float chargeDuration = 2.8f;
        [SerializeField] private float swordBurstDuration = 0.35f;
        [SerializeField] private float hitStopDuration = 0.12f;
        [SerializeField] private float hitStopTimeScale = 0.02f;
        [SerializeField] private float beamDuration = 0.5f;

        [Header("VFX Prefabs & References (Optional / Auto-Assigned)")]
        [Tooltip("Subtle dark aura spawned at sword position during Phase 1")]
        [SerializeField] private GameObject auraPrefabPhase1;
        
        [Tooltip("Inward pulling energy vortex prefab for Phase 2")]
        [SerializeField] private GameObject chargeVortexPrefab;
        
        [Tooltip("Violent magenta/red radial explosion & vertical pillar spawned at blade when charge completes")]
        [SerializeField] private GameObject swordBurstPrefab;
        
        [Tooltip("Legacy particle system reference for Phase 2")]
        [SerializeField] private ParticleSystem chargeVortexParticles;
        
        [Tooltip("Massive horizontal beam spawned during Phase 3")]
        [SerializeField] private GameObject beamPrefabPhase3;
        
        [Tooltip("Transform position where aura and beam emanate from (e.g. sword tip/center)")]
        [SerializeField] private Transform swordVFXAnchor;

        [Header("Phase 1 - Ground Crack")]
        [SerializeField] private GameObject groundCrackPrefab;

        [Header("Phase 2 - Lightning Arcs & Turbulence")]
        [SerializeField] private GameObject lightningArcsPrefab;
        [SerializeField] private bool enableCapeTurbulence = false;
        [SerializeField] private float turbulenceIntensity = 0.0f;

        [Header("Phase 3 - Debris")]
        [SerializeField] private GameObject debrisParticlesPrefab;

        [Header("Phase 4 - Shockwave")]
        [SerializeField] private GameObject shockwaveRingPrefab;
        [SerializeField] private int shockwaveRingCount = 3;
        [SerializeField] private float shockwaveRingDelay = 0.05f;

        [Header("Phase 5 - Post-Attack Residuals")]
        [SerializeField] private GameObject groundScorchPrefab;
        [SerializeField] private GameObject residualSmokePrefab;
        [SerializeField] private float scorchFadeDuration = 3.0f;

        [Header("Sword Blade Position Adjustments")]
        [Tooltip("Blade center offset for Phase 2 charge vortex gathering point (matches player-excalibur2.png raised sword blade height)")]
        [SerializeField] private Vector3 phase2BladeOffset = new Vector3(0.0f, 3.80f, 0f);

        [Tooltip("Beam origin offset for Phase 3 thrust (matches player-excalibur3.png sword tip)")]
        [SerializeField] private Vector3 phase3BeamOffset = new Vector3(2.67f, 1.47f, 0f);

        [Header("Background & Screen Effects (Optional)")]
        [Tooltip("Optional CanvasGroup overlay for dimming the screen")]
        [SerializeField] private CanvasGroup darkScreenOverlay;
        [SerializeField] private float darkOverlayAlpha = 0.85f;

        [Tooltip("Optional white screen flash overlay on beam release (Heaven's Feel style)")]
        [SerializeField] private CanvasGroup whiteScreenFlashOverlay;
        
        #if UNITY_2D_LIGHTING
        [Tooltip("Optional Global 2D Light to dim during ultimate attack")]
        [SerializeField] private Light2D globalLight2D;
        [SerializeField] private float dimmedLightIntensity = 0.1f;
        private float originalLightIntensity = 1.0f;
        #endif

        [Header("Camera Shake / Impulse (Optional)")]
        #if CINEMACHINE_V3
        [SerializeField] private CinemachineImpulseSource chargeImpulseSource;
        [SerializeField] private CinemachineImpulseSource beamImpulseSource;
        #elif CINEMACHINE_PRESENT
        [SerializeField] private CinemachineImpulseSource chargeImpulseSource;
        [SerializeField] private CinemachineImpulseSource beamImpulseSource;
        #else
        [Tooltip("Generic transform or camera shake trigger reference")]
        [SerializeField] private Component chargeImpulseSource;
        [SerializeField] private Component beamImpulseSource;
        #endif

        private SpriteRenderer spriteRenderer;
        private Coroutine ultimateCoroutine;
        private bool isExecutingUltimate = false;
        
        // Active instance trackers for cleanup
        private GameObject currentActiveVortex = null;
        private GameObject currentActiveAura = null;
        private GameObject currentActiveGroundCrack = null;
        private GameObject currentActiveLightning = null;
        private GameObject currentActiveBurst = null;
        private GameObject currentActiveDebris = null;
        private GameObject currentActiveBeam = null;

        public bool IsExecuting => isExecutingUltimate;

        private void Awake()
        {
            EnsureReferences();
            ValidateOffsets();
            AutoAssignPrefabsIfNull();
            StopAllChildParticleSystems();
        }

        private void OnValidate()
        {
            ValidateOffsets();
            AutoAssignPrefabsIfNull();
        }

        private void ValidateOffsets()
        {
            if (Mathf.Abs(phase2BladeOffset.y - 0.5f) < 0.1f || phase2BladeOffset.y < 1.0f)
            {
                phase2BladeOffset = new Vector3(0.0f, 3.80f, 0.0f);
            }
        }

        private void AutoAssignPrefabsIfNull()
        {
#if UNITY_EDITOR
            if (auraPrefabPhase1 == null)
                auraPrefabPhase1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Phase1Aura.prefab");
            if (chargeVortexPrefab == null)
                chargeVortexPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ChargeVortex.prefab");
            if (swordBurstPrefab == null)
            {
                swordBurstPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SwordBurst.prefab") 
                                ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ChargeBurstPillar.prefab");
            }
            if (beamPrefabPhase3 == null)
                beamPrefabPhase3 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ExcaliburBeam.prefab");
            
            if (groundCrackPrefab == null)
                groundCrackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GroundCrack.prefab");
            if (lightningArcsPrefab == null)
                lightningArcsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LightningArcs.prefab");
            if (debrisParticlesPrefab == null)
                debrisParticlesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DebrisParticles.prefab");
            if (shockwaveRingPrefab == null)
                shockwaveRingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ShockwaveRing.prefab");
            if (groundScorchPrefab == null)
                groundScorchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GroundScorch.prefab");
            if (residualSmokePrefab == null)
                residualSmokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ResidualSmoke.prefab");
#endif
        }

        private void StopAllChildParticleSystems()
        {
            if (chargeVortexParticles != null)
            {
                chargeVortexParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            ParticleSystem[] childPS = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in childPS)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void EnsureReferences()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (swordVFXAnchor == null)
            {
                swordVFXAnchor = transform;
            }
        }

        private void Reset()
        {
            EnsureReferences();
            ValidateOffsets();
            AutoAssignPrefabsIfNull();
#if UNITY_EDITOR
            if (spritePhase1 == null)
                spritePhase1 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sprites/Player/player-excalibur1.png");
            if (spritePhase2 == null)
                spritePhase2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sprites/Player/player-excalibur2.png");
            if (spritePhase3 == null)
                spritePhase3 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/sprites/Player/player-excalibur3.png");
#endif
        }

        private static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        private static void SafeDestroy(GameObject go, float delay)
        {
            if (go == null) return;
            if (Application.isPlaying)
            {
                Destroy(go, delay);
            }
            else
            {
                DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Call this method to trigger the Excalibur Morgan ultimate attack sequence.
        /// Safe to run even if all VFX prefabs are unassigned.
        /// </summary>
        [ContextMenu("Trigger Excalibur Morgan Ultimate")]
        public void ExecuteUltimateAttack()
        {
            if (isExecutingUltimate)
            {
                Debug.LogWarning("[ExcaliburVFXController] Ultimate attack is already in progress!");
                return;
            }

            EnsureReferences();
            ValidateOffsets();
            ultimateCoroutine = StartCoroutine(UltimateSequenceRoutine());
        }

        private IEnumerator UltimateSequenceRoutine()
        {
            isExecutingUltimate = true;
            Sprite originalSprite = (spriteRenderer != null) ? spriteRenderer.sprite : null;
            Vector3 originalSpritePos = (spriteRenderer != null) ? spriteRenderer.transform.localPosition : Vector3.zero;

            #if UNITY_2D_LIGHTING
            if (globalLight2D != null)
            {
                originalLightIntensity = globalLight2D.intensity;
            }
            #endif

            Vector3 GetAnchorPosition() => (swordVFXAnchor != null) ? swordVFXAnchor.position : transform.position;
            Quaternion GetAnchorRotation()
            {
                Quaternion rot = (swordVFXAnchor != null) ? swordVFXAnchor.rotation : transform.rotation;
                if ((spriteRenderer != null && spriteRenderer.flipX) || transform.localScale.x < 0f)
                {
                    rot *= Quaternion.Euler(0f, 180f, 0f);
                }
                return rot;
            }
            Transform GetAnchorParent() => (swordVFXAnchor != null) ? swordVFXAnchor : transform;

            // ==========================================
            // PHASE 1: Dark Anticipation & Ground Sigil (0.0s – 0.5s)
            // ==========================================
            if (spriteRenderer != null && spritePhase1 != null)
            {
                spriteRenderer.sprite = spritePhase1;
            }

            SetBackgroundDimmed(true);

            // Spawn Phase 1 Anticipation Aura at sword position
            if (auraPrefabPhase1 != null)
            {
                Vector3 auraPos = GetAnchorPosition() + GetAnchorRotation() * new Vector3(0f, 1.5f, 0f);
                currentActiveAura = Instantiate(auraPrefabPhase1, auraPos, GetAnchorRotation(), GetAnchorParent());
                
                ParticleSystem[] auraPS = currentActiveAura.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in auraPS) if (ps != null) ps.Play();
            }

            // Spawn Phase 1 Ground Crack at player feet
            if (groundCrackPrefab != null)
            {
                Vector3 crackPos = GetAnchorPosition() + GetAnchorRotation() * new Vector3(0f, -0.5f, 0f);
                currentActiveGroundCrack = Instantiate(groundCrackPrefab, crackPos, GetAnchorRotation(), GetAnchorParent());
                
                ParticleSystem[] crackPS = currentActiveGroundCrack.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in crackPS) if (ps != null) ps.Play();
            }

            yield return new WaitForSeconds(anticipationDuration);

            if (currentActiveAura != null)
            {
                SafeDestroy(currentActiveAura);
                currentActiveAura = null;
            }

            // ==========================================
            // PHASE 2: Spiral Vortex & Red Lightning (0.5s – 3.3s)
            // ==========================================
            if (spriteRenderer != null && spritePhase2 != null)
            {
                spriteRenderer.sprite = spritePhase2;
            }

            GameObject vortexPrefabToSpawn = (chargeVortexPrefab != null) 
                ? chargeVortexPrefab 
                : ((chargeVortexParticles != null) ? chargeVortexParticles.gameObject : null);

            ParticleSystem[] allVortexPS = null;
            if (vortexPrefabToSpawn != null)
            {
                Vector3 bladePos = GetAnchorPosition() + GetAnchorRotation() * phase2BladeOffset;

                currentActiveVortex = Instantiate(vortexPrefabToSpawn, bladePos, GetAnchorRotation(), GetAnchorParent());
                allVortexPS = currentActiveVortex.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in allVortexPS) if (ps != null) ps.Play();
            }

            ParticleSystem activeLightningPS = null;
            float[] originalBurstCounts = null;
            if (currentActiveVortex != null && lightningArcsPrefab != null)
            {
                currentActiveLightning = Instantiate(lightningArcsPrefab, currentActiveVortex.transform.position, GetAnchorRotation(), currentActiveVortex.transform);
                activeLightningPS = currentActiveLightning.GetComponent<ParticleSystem>();
                if (activeLightningPS == null)
                {
                    activeLightningPS = currentActiveLightning.GetComponentInChildren<ParticleSystem>();
                }

                if (activeLightningPS != null)
                {
                    var emission = activeLightningPS.emission;
                    if (emission.burstCount > 0)
                    {
                        originalBurstCounts = new float[emission.burstCount];
                        for(int i = 0; i < emission.burstCount; i++)
                        {
                            originalBurstCounts[i] = emission.GetBurst(i).count.constantMax;
                        }
                    }
                    activeLightningPS.Play();
                }
            }

            // Scale particle emission rate from 20 to 300 over 2.8s
            float elapsedCharge = 0f;

            while (elapsedCharge < chargeDuration)
            {
                elapsedCharge += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedCharge / chargeDuration);

                if (currentActiveVortex != null)
                {
                    currentActiveVortex.transform.position = GetAnchorPosition() + GetAnchorRotation() * phase2BladeOffset;
                }

                // Ensure player sprite remains completely steady during charge
                if (spriteRenderer != null)
                {
                    spriteRenderer.transform.localPosition = originalSpritePos;
                }

                // Ramp emission rate from 20 to 300 over duration for all charge vortex systems (glowing + dark mana)
                if (allVortexPS != null)
                {
                    foreach (var ps in allVortexPS)
                    {
                        if (ps != null)
                        {
                            var emissionModule = ps.emission;
                            emissionModule.rateOverTime = Mathf.Lerp(20f, 300f, progress * progress);
                        }
                    }
                }

                // Ramp lightning emission rate: multiply burst count by progress
                if (activeLightningPS != null && originalBurstCounts != null)
                {
                    var emissionModule = activeLightningPS.emission;
                    for (int i = 0; i < emissionModule.burstCount; i++)
                    {
                        var burst = emissionModule.GetBurst(i);
                        burst.count = new ParticleSystem.MinMaxCurve(originalBurstCounts[i] * progress);
                        emissionModule.SetBurst(i, burst);
                    }
                }

                if (chargeImpulseSource != null && progress > 0.35f && Random.value < (progress * 0.4f))
                {
                    GenerateImpulse(chargeImpulseSource);
                }

                yield return null;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localPosition = originalSpritePos;
            }

            if (currentActiveVortex != null)
            {
                if (allVortexPS != null)
                {
                    foreach (var ps in allVortexPS) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                SafeDestroy(currentActiveVortex);
                currentActiveVortex = null;
                currentActiveLightning = null;
            }

            if (currentActiveGroundCrack != null)
            {
                SafeDestroy(currentActiveGroundCrack);
                currentActiveGroundCrack = null;
            }

            // ==========================================
            // PHASE 3: THE THRUST / BEAM RELEASE (0.5s)
            // (Seamless transition directly from Phase 2 Charge)
            // ==========================================
            if (spriteRenderer != null && spritePhase3 != null)
            {
                spriteRenderer.sprite = spritePhase3;
            }

            if (whiteScreenFlashOverlay != null)
            {
                StartCoroutine(TriggerScreenFlashRoutine());
            }

            // Real-time Hit-Stop Effect (0.02f timescale for 0.12s realtime)
            Time.timeScale = hitStopTimeScale;
            yield return new WaitForSecondsRealtime(hitStopDuration);
            Time.timeScale = 1.0f;

            Vector3 beamSpawnPos = GetAnchorPosition() + GetAnchorRotation() * phase3BeamOffset;
            Quaternion beamSpawnRot = GetAnchorRotation();

            StartCoroutine(SpawnShockwaveRingsRoutine(beamSpawnPos));

            if (beamImpulseSource != null)
            {
                GenerateImpulse(beamImpulseSource);
            }

            // Spawn Energy Beam Prefab facing player's direction
            if (beamPrefabPhase3 != null)
            {
                currentActiveBeam = Instantiate(beamPrefabPhase3, beamSpawnPos, beamSpawnRot);
                currentActiveBeam.transform.position = beamSpawnPos;

                ParticleSystem[] beamPS = currentActiveBeam.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in beamPS) if (ps != null) ps.Play();
            }

            yield return new WaitForSeconds(beamDuration);

            if (currentActiveBeam != null)
            {
                SafeDestroy(currentActiveBeam);
                currentActiveBeam = null;
            }

            // ==========================================
            // PHASE 5: Ground Scorch & Residual Smoke (Post-Attack)
            // ==========================================
            Vector3 scorchPos = GetAnchorPosition() + GetAnchorRotation() * new Vector3(phase3BeamOffset.x, -0.5f, 0f);

            if (groundScorchPrefab != null)
            {
                GameObject activeScorch = Instantiate(groundScorchPrefab, scorchPos, GetAnchorRotation());
                StartCoroutine(FadeScorchRoutine(activeScorch));
            }

            if (residualSmokePrefab != null)
            {
                GameObject smoke1 = Instantiate(residualSmokePrefab, scorchPos, GetAnchorRotation());
                Vector3 midPoint = scorchPos + GetAnchorRotation() * new Vector3(8f, 0f, 0f);
                GameObject smoke2 = Instantiate(residualSmokePrefab, midPoint, GetAnchorRotation());

                ParticleSystem[] smokePS1 = smoke1.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in smokePS1) if (ps != null) ps.Play();
                
                ParticleSystem[] smokePS2 = smoke2.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in smokePS2) if (ps != null) ps.Play();

                SafeDestroy(smoke1, 3.5f);
                SafeDestroy(smoke2, 3.5f);
            }

            // ==========================================
            // SEQUENCE CLEANUP & RESET
            // ==========================================
            SetBackgroundDimmed(false);

            if (spriteRenderer != null)
            {
                spriteRenderer.transform.localPosition = originalSpritePos;
                if (originalSprite != null) spriteRenderer.sprite = originalSprite;
            }

            isExecutingUltimate = false;
        }

        private IEnumerator SpawnShockwaveRingsRoutine(Vector3 position)
        {
            for (int i = 0; i < shockwaveRingCount; i++)
            {
                if (shockwaveRingPrefab != null)
                {
                    GameObject ring = Instantiate(shockwaveRingPrefab, position, Quaternion.identity);
                    StartCoroutine(FadeAndExpandRingRoutine(ring, 0.3f));
                }
                yield return new WaitForSeconds(shockwaveRingDelay);
            }
        }

        private IEnumerator FadeAndExpandRingRoutine(GameObject ring, float duration = 0.3f)
        {
            float elapsed = 0f;
            Vector3 startScale = new Vector3(0.5f, 0.5f, 1f);
            Vector3 endScale = new Vector3(15f, 15f, 1f);
            SpriteRenderer sr = ring.GetComponent<SpriteRenderer>();
            
            if (sr == null)
            {
                SafeDestroy(ring);
                yield break;
            }

            Color startColor = sr.color;
            startColor.a = 0.8f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                ring.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(0.8f, 0f, t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            SafeDestroy(ring);
        }

        private IEnumerator FadeScorchRoutine(GameObject scorch)
        {
            SpriteRenderer sr = scorch.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                SafeDestroy(scorch, scorchFadeDuration);
                yield break;
            }

            Color startColor = sr.color;
            float elapsed = 0f;
            while (elapsed < scorchFadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / scorchFadeDuration;
                sr.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, t));
                yield return null;
            }
            SafeDestroy(scorch);
        }

        private IEnumerator TriggerScreenFlashRoutine()
        {
            if (whiteScreenFlashOverlay == null) yield break;

            whiteScreenFlashOverlay.alpha = 1.0f;
            float elapsed = 0f;
            float flashDuration = 0.15f;

            while (elapsed < flashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                whiteScreenFlashOverlay.alpha = Mathf.Lerp(1.0f, 0.0f, elapsed / flashDuration);
                yield return null;
            }

            whiteScreenFlashOverlay.alpha = 0.0f;
        }

        private void SetBackgroundDimmed(bool dim)
        {
            if (darkScreenOverlay != null)
            {
                darkScreenOverlay.alpha = dim ? darkOverlayAlpha : 0f;
            }

            #if UNITY_2D_LIGHTING
            if (globalLight2D != null)
            {
                globalLight2D.intensity = dim ? dimmedLightIntensity : originalLightIntensity;
            }
            #endif
        }

        private void GenerateImpulse(Component impulseSource)
        {
            if (impulseSource == null) return;

            #if CINEMACHINE_V3
            if (impulseSource is CinemachineImpulseSource cV3)
            {
                cV3.GenerateImpulse();
            }
            #elif CINEMACHINE_PRESENT
            if (impulseSource is CinemachineImpulseSource cV2)
            {
                cV2.GenerateImpulse();
            }
            #else
            Debug.Log("[ExcaliburVFXController] Triggering Camera Shake Impulse");
            #endif
        }

        private void OnDisable()
        {
            if (isExecutingUltimate)
            {
                Time.timeScale = 1.0f;
                SetBackgroundDimmed(false);

                if (whiteScreenFlashOverlay != null)
                {
                    whiteScreenFlashOverlay.alpha = 0f;
                }

                if (currentActiveAura != null) SafeDestroy(currentActiveAura);
                if (currentActiveGroundCrack != null) SafeDestroy(currentActiveGroundCrack);
                if (currentActiveVortex != null) SafeDestroy(currentActiveVortex);
                if (currentActiveLightning != null) SafeDestroy(currentActiveLightning);
                if (currentActiveBurst != null) SafeDestroy(currentActiveBurst);
                if (currentActiveDebris != null) SafeDestroy(currentActiveDebris);
                if (currentActiveBeam != null) SafeDestroy(currentActiveBeam);

                isExecutingUltimate = false;
            }
        }
    }
}
