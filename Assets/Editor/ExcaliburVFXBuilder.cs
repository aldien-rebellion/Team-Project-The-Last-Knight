using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastKnight.Editor
{
    public static class ExcaliburVFXBuilder
    {
        [MenuItem("Tools/Build Excalibur VFX Prefabs")]
        public static void BuildPrefabs()
        {
            string texturesFolder = "Assets/Textures";
            if (!AssetDatabase.IsValidFolder(texturesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Textures");
            }

            string prefabsFolder = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(prefabsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }

            string materialsFolder = "Assets/Materials";
            if (!AssetDatabase.IsValidFolder(materialsFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Materials");
            }

            Material softParticleMat = GetOrCreateSoftParticleMaterial(texturesFolder, materialsFolder);
            Material crescentParticleMat = GetOrCreateCrescentParticleMaterial(texturesFolder, materialsFolder);

            BuildPhase1AuraPrefab(prefabsFolder, softParticleMat);
            BuildChargeVortexPrefab(prefabsFolder, softParticleMat);
            BuildSwordBurstPrefab(prefabsFolder, materialsFolder, softParticleMat);
            BuildExcaliburBeamPrefab(prefabsFolder, materialsFolder, softParticleMat);
            BuildGroundCrackPrefab(prefabsFolder, softParticleMat);
            BuildLightningArcsPrefab(prefabsFolder, softParticleMat);
            BuildDebrisParticlesPrefab(prefabsFolder, softParticleMat);
            BuildShockwaveRingPrefab(prefabsFolder, materialsFolder, softParticleMat);
            BuildGroundScorchPrefab(prefabsFolder, materialsFolder);
            BuildResidualSmokePrefab(prefabsFolder, softParticleMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ExcaliburVFXBuilder] Successfully built soft, dark, and multi-colored streak line particle materials, and updated all VFX prefabs in Assets/Prefabs/");
        }

        private static Material GetOrCreateSoftParticleMaterial(string texFolderPath, string matFolderPath)
        {
            string texPath = Path.Combine(texFolderPath, "T_SoftParticleDot.png").Replace("\\", "/");

            if (!File.Exists(texPath))
            {
                int size = 64;
                Texture2D tempTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float maxRadius = size * 0.5f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        float normDist = Mathf.Clamp01(dist / maxRadius);
                        float alpha = Mathf.Pow(Mathf.Clamp01(1.0f - normDist), 2.0f);
                        tempTex.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, alpha));
                    }
                }
                tempTex.Apply();

                byte[] pngData = tempTex.EncodeToPNG();
                File.WriteAllBytes(texPath, pngData);
                Object.DestroyImmediate(tempTex);

                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

                TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
            }

            Texture2D softTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string matPath = Path.Combine(matFolderPath, "M_SoftParticle_Unlit.mat").Replace("\\", "/");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader urp2DShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") 
                              ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                              ?? Shader.Find("Sprites/Default");

            if (mat == null)
            {
                mat = new Material(urp2DShader);
                mat.mainTexture = softTex;
                mat.color = Color.white;
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = urp2DShader;
                mat.mainTexture = softTex;
                mat.color = Color.white;
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static Material GetOrCreateCrescentParticleMaterial(string texFolderPath, string matFolderPath)
        {
            string texPath = Path.Combine(texFolderPath, "T_CrescentParticle.png").Replace("\\", "/");

            if (!File.Exists(texPath))
            {
                int size = 256;
                Texture2D tempTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float radius = 85f;    // Larger mid-radius of crescent arc
                float thickness = 32f; // Much thicker bold arc line

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 pos = new Vector2(x, y) - center;
                        float dist = pos.magnitude;
                        float angleDeg = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;

                        float radialDiff = Mathf.Abs(dist - radius);
                        float radialAlpha = Mathf.Pow(Mathf.Clamp01(1.0f - (radialDiff / thickness)), 1.8f);
                        float angleAlpha = Mathf.Pow(Mathf.Clamp01(1.0f - (Mathf.Abs(angleDeg) / 110.0f)), 1.8f);

                        float finalAlpha = radialAlpha * angleAlpha;
                        tempTex.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, finalAlpha));
                    }
                }
                tempTex.Apply();

                byte[] pngData = tempTex.EncodeToPNG();
                File.WriteAllBytes(texPath, pngData);
                Object.DestroyImmediate(tempTex);
            }

            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            Texture2D crescentTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string matPath = Path.Combine(matFolderPath, "M_CrescentParticle_Unlit.mat").Replace("\\", "/");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader urp2DShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") 
                              ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                              ?? Shader.Find("Sprites/Default");

            if (mat == null)
            {
                mat = new Material(urp2DShader);
                mat.mainTexture = crescentTex;
                mat.color = Color.white;
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = urp2DShader;
                mat.mainTexture = crescentTex;
                mat.color = Color.white;
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static void BuildPhase1AuraPrefab(string folderPath, Material softParticleMat)
        {
            GameObject auraGO = new GameObject("Phase1Aura");
            ParticleSystem ps = auraGO.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = true;
            main.playOnAwake = false; // Prevent automatic spawning before skill trigger
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startColor = new Color(0.9f, 0.0f, 0.5f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 80f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1.0f),
                new Keyframe(1f, 0.0f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer psr = auraGO.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = softParticleMat;
            psr.sortingOrder = 10;

            string prefabPath = Path.Combine(folderPath, "Phase1Aura.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(auraGO, prefabPath);
            Object.DestroyImmediate(auraGO);
        }

        private static void BuildChargeVortexPrefab(string folderPath, Material softParticleMat)
        {
            GameObject vortexGO = new GameObject("ChargeVortex");
            ParticleSystem ps = vortexGO.AddComponent<ParticleSystem>();

            // --- 1. Main Glowing Round Energy Particles System ---
            var main = ps.main;
            main.duration = 2.8f;
            main.loop = false;
            main.playOnAwake = false; // Prevent automatic spawning before skill trigger
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;

            AnimationCurve emissionCurve = new AnimationCurve(
                new Keyframe(0f, 20f),
                new Keyframe(1f, 300f)
            );
            emission.rateOverTime = new ParticleSystem.MinMaxCurve(1.0f, emissionCurve);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Donut;
            shape.radius = 5.5f;
            shape.donutRadius = 4.5f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space = ParticleSystemSimulationSpace.Local;
            vol.radial = -9.0f; // Rapid inward pull into blade center
            vol.orbitalY = new ParticleSystem.MinMaxCurve(Mathf.Deg2Rad * 450f);

            var col = ps.colorOverLifetime;
            col.enabled = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.0f, 0.5f, 1.0f), 0.3f),
                    new GradientColorKey(new Color(0.6f, 0.0f, 0.1f, 1.0f), 0.7f),
                    new GradientColorKey(Color.black, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1.0f),
                new Keyframe(1f, 0.0f)
            );

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer psr = vortexGO.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = softParticleMat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sortingOrder = 10;

            // --- 2. Secondary Dark Black Mana Particles Sub-Emitter ---
            GameObject darkGO = new GameObject("DarkManaParticles");
            darkGO.transform.SetParent(vortexGO.transform, false);
            darkGO.transform.localPosition = Vector3.zero;

            ParticleSystem darkPS = darkGO.AddComponent<ParticleSystem>();
            var darkMain = darkPS.main;
            darkMain.duration = 2.8f;
            darkMain.loop = false;
            darkMain.playOnAwake = false;
            darkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.85f);
            darkMain.startSpeed = 0f;
            darkMain.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.65f); // Bold dark round particles
            darkMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.02f, 0.0f, 0.03f, 0.95f), // Deep pitch black
                new Color(0.08f, 0.0f, 0.02f, 0.95f)  // Dark crimson-black
            );
            darkMain.maxParticles = 1200;
            darkMain.simulationSpace = ParticleSystemSimulationSpace.Local;

            var darkEmission = darkPS.emission;
            darkEmission.enabled = true;
            AnimationCurve darkEmissionCurve = new AnimationCurve(
                new Keyframe(0f, 25f),
                new Keyframe(1f, 250f)
            );
            darkEmission.rateOverTime = new ParticleSystem.MinMaxCurve(1.0f, darkEmissionCurve);

            var darkShape = darkPS.shape;
            darkShape.enabled = true;
            darkShape.shapeType = ParticleSystemShapeType.Donut;
            darkShape.radius = 5.5f;
            darkShape.donutRadius = 4.5f;

            var darkVol = darkPS.velocityOverLifetime;
            darkVol.enabled = true;
            darkVol.space = ParticleSystemSimulationSpace.Local;
            darkVol.radial = -8.5f; // Pull into blade center
            darkVol.orbitalY = new ParticleSystem.MinMaxCurve(Mathf.Deg2Rad * 420f);

            var darkSol = darkPS.sizeOverLifetime;
            darkSol.enabled = true;
            darkSol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer darkPsr = darkGO.GetComponent<ParticleSystemRenderer>();
            darkPsr.sharedMaterial = softParticleMat;
            darkPsr.renderMode = ParticleSystemRenderMode.Billboard;
            darkPsr.sortingOrder = 9; // Render slightly behind/between glowing particles for contrast

            // --- 3. Tertiary Multi-Colored Energy Streak Lines Sub-Emitter ---
            GameObject streakGO = new GameObject("StreakLineParticles");
            streakGO.transform.SetParent(vortexGO.transform, false);
            streakGO.transform.localPosition = Vector3.zero;

            ParticleSystem streakPS = streakGO.AddComponent<ParticleSystem>();
            var streakMain = streakPS.main;
            streakMain.duration = 2.8f;
            streakMain.loop = false;
            streakMain.playOnAwake = false;
            streakMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            streakMain.startSpeed = 0f;
            streakMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.20f);
            
            // Random multi-color gradient keys for streaks: White, Magenta, Dark Blood Red, Electric Purple, Crimson-Black
            Gradient streakGradient = new Gradient();
            streakGradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.0f, 0.5f, 1.0f), 0.25f),  // Glowing Magenta (#FF007F)
                    new GradientColorKey(new Color(0.6f, 0.0f, 0.1f, 1.0f), 0.50f),  // Dark Blood Red (#99001A)
                    new GradientColorKey(new Color(0.6f, 0.0f, 1.0f, 1.0f), 0.75f),  // Electric Purple (#9900FF)
                    new GradientColorKey(new Color(0.05f, 0.0f, 0.05f, 1.0f), 1.0f)  // Pitch Black
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            streakMain.startColor = new ParticleSystem.MinMaxGradient(streakGradient);
            streakMain.maxParticles = 2500;
            streakMain.simulationSpace = ParticleSystemSimulationSpace.Local;

            var streakEmission = streakPS.emission;
            streakEmission.enabled = true;
            AnimationCurve streakEmissionCurve = new AnimationCurve(
                new Keyframe(0f, 150f),
                new Keyframe(1f, 1200f)
            );
            streakEmission.rateOverTime = new ParticleSystem.MinMaxCurve(1.0f, streakEmissionCurve);

            var streakShape = streakPS.shape;
            streakShape.enabled = true;
            streakShape.shapeType = ParticleSystemShapeType.Donut;
            streakShape.radius = 5.5f;
            streakShape.donutRadius = 4.5f;

            var streakVol = streakPS.velocityOverLifetime;
            streakVol.enabled = true;
            streakVol.space = ParticleSystemSimulationSpace.Local;
            streakVol.radial = -12.0f; // Rapid inward streak pull
            streakVol.orbitalY = new ParticleSystem.MinMaxCurve(Mathf.Deg2Rad * 480f);

            var streakSol = streakPS.sizeOverLifetime;
            streakSol.enabled = true;
            streakSol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer streakPsr = streakGO.GetComponent<ParticleSystemRenderer>();
            streakPsr.sharedMaterial = softParticleMat;
            streakPsr.renderMode = ParticleSystemRenderMode.Stretch;
            streakPsr.cameraVelocityScale = 0f;
            streakPsr.velocityScale = -0.35f;
            streakPsr.lengthScale = 5.0f; // Stretched energy streak lines
            streakPsr.sortingOrder = 11; // Render on top for sharp, vibrant energy streak contrast

            string prefabPath = Path.Combine(folderPath, "ChargeVortex.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(vortexGO, prefabPath);
            Object.DestroyImmediate(vortexGO);
        }

        private static void BuildSwordBurstPrefab(string folderPath, string matFolderPath, Material softParticleMat)
        {
            GameObject burstGO = new GameObject("SwordBurst");

            SpriteRenderer sr = burstGO.AddComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = 11;

            Sprite defaultSquare = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            if (defaultSquare != null)
            {
                sr.sprite = defaultSquare;
            }

            burstGO.transform.localScale = new Vector3(5.0f, 22.0f, 1.0f);

            string matPath = Path.Combine(matFolderPath, "M_ExcaliburPillar_Placeholder.mat").Replace("\\", "/");
            Material pillarMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader pillarShader = Shader.Find("Custom/URP_ExcaliburPillar") 
                               ?? Shader.Find("Custom/URP_ExcaliburBeam")
                               ?? Shader.Find("Sprites/Default");

            if (pillarMat == null)
            {
                pillarMat = new Material(pillarShader);
                pillarMat.SetColor("_BeamColor", new Color(4.5f, 0.0f, 2.2f, 1.0f));
                pillarMat.SetColor("_CoreColor", new Color(8.0f, 8.0f, 8.0f, 1.0f));
                pillarMat.SetFloat("_PanSpeed1Y", -12.0f);
                pillarMat.SetFloat("_PanSpeed2Y", -18.0f);
                AssetDatabase.CreateAsset(pillarMat, matPath);
            }
            else
            {
                pillarMat.shader = pillarShader;
                pillarMat.SetColor("_BeamColor", new Color(4.5f, 0.0f, 2.2f, 1.0f));
                pillarMat.SetColor("_CoreColor", new Color(8.0f, 8.0f, 8.0f, 1.0f));
                pillarMat.SetFloat("_PanSpeed1Y", -12.0f);
                pillarMat.SetFloat("_PanSpeed2Y", -18.0f);
                EditorUtility.SetDirty(pillarMat);
            }
            sr.sharedMaterial = pillarMat;

            GameObject radialGO = new GameObject("RadialExplosion");
            radialGO.transform.SetParent(burstGO.transform, false);
            radialGO.transform.localPosition = Vector3.zero;

            ParticleSystem radialPS = radialGO.AddComponent<ParticleSystem>();
            var main = radialPS.main;
            main.duration = 0.35f;
            main.loop = false;
            main.playOnAwake = false; // Prevent automatic spawning before skill trigger
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12.0f, 25.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startColor = new Color(1.0f, 0.1f, 0.6f, 1.0f);
            main.gravityModifier = -4.0f;

            var emission = radialPS.emission;
            emission.enabled = true;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 250) });

            var shape = radialPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var sol = radialPS.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1.0f),
                new Keyframe(1f, 0.0f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer psr = radialGO.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = softParticleMat;
            psr.sortingOrder = 12;

            string prefabPath = Path.Combine(folderPath, "SwordBurst.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(burstGO, prefabPath);

            string pillarPrefabPath = Path.Combine(folderPath, "ChargeBurstPillar.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(burstGO, pillarPrefabPath);

            Object.DestroyImmediate(burstGO);
        }

        private static void BuildExcaliburBeamPrefab(string folderPath, string matFolderPath, Material softParticleMat)
        {
            GameObject beamGO = new GameObject("ExcaliburBeam");
            GameObject beamVisualGO = new GameObject("BeamVisual");
            beamVisualGO.transform.SetParent(beamGO.transform, false);
            
            SpriteRenderer sr = beamVisualGO.AddComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Simple;
            sr.sortingOrder = 10;

            Sprite defaultSquare = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            float spriteWidth = 1.0f;
            float spriteHeight = 1.0f;
            if (defaultSquare != null)
            {
                sr.sprite = defaultSquare;
                spriteWidth = defaultSquare.bounds.size.x;
                spriteHeight = defaultSquare.bounds.size.y;
            }

            // Ensure the beam is exactly 25 units long and 3.5 units thick, regardless of sprite PPU/bounds
            beamGO.transform.localScale = new Vector3(25.0f / spriteWidth, 3.5f / spriteHeight, 1.0f);

            // Offset by half the sprite width in local space so the left edge aligns perfectly with the origin (sword tip)
            beamVisualGO.transform.localPosition = new Vector3(spriteWidth / 2.0f, 0.0f, 0.0f);

            string matPath = Path.Combine(matFolderPath, "M_ExcaliburBeam_Placeholder.mat").Replace("\\", "/");
            Material beamMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader beamShader = Shader.Find("Custom/URP_ExcaliburBeam") 
                             ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                             ?? Shader.Find("Sprites/Default");

            if (beamMat == null)
            {
                beamMat = new Material(beamShader);
                beamMat.SetColor("_BeamColor", new Color(3.5f, 0.0f, 1.8f, 1.0f));
                beamMat.SetColor("_CoreColor", new Color(6.0f, 6.0f, 6.0f, 1.0f));
                beamMat.SetColor("_AuraColor", new Color(0.3f, 0.0f, 0.5f, 1.0f));
                beamMat.SetColor("_NoiseStreakColor", new Color(2.5f, 0.0f, 0.2f, 1.0f));
                beamMat.SetFloat("_PanSpeed1", -8.0f);
                beamMat.SetFloat("_PanSpeed2", -14.0f);
                beamMat.SetFloat("_PanSpeed3", -6.0f);
                beamMat.SetFloat("_CoreThickness", 8.0f);
                beamMat.SetFloat("_AuraWidth", 1.2f);
                beamMat.SetFloat("_TipTaperStart", 0.7f);
                beamMat.SetFloat("_TipTaperPower", 2.0f);
                AssetDatabase.CreateAsset(beamMat, matPath);
            }
            else
            {
                beamMat.shader = beamShader;
                beamMat.SetColor("_BeamColor", new Color(3.5f, 0.0f, 1.8f, 1.0f));
                beamMat.SetColor("_CoreColor", new Color(6.0f, 6.0f, 6.0f, 1.0f));
                beamMat.SetColor("_AuraColor", new Color(0.3f, 0.0f, 0.5f, 1.0f));
                beamMat.SetColor("_NoiseStreakColor", new Color(2.5f, 0.0f, 0.2f, 1.0f));
                beamMat.SetFloat("_PanSpeed1", -8.0f);
                beamMat.SetFloat("_PanSpeed2", -14.0f);
                beamMat.SetFloat("_PanSpeed3", -6.0f);
                beamMat.SetFloat("_CoreThickness", 8.0f);
                beamMat.SetFloat("_AuraWidth", 1.2f);
                beamMat.SetFloat("_TipTaperStart", 0.7f);
                beamMat.SetFloat("_TipTaperPower", 2.0f);
                EditorUtility.SetDirty(beamMat);
            }
            sr.sharedMaterial = beamMat;

            GameObject sparksGO = new GameObject("ImpactSparks");
            sparksGO.transform.SetParent(beamGO.transform, false);
            // Place sparks at the exact tip of the beam
            sparksGO.transform.localPosition = new Vector3(spriteWidth, 0.0f, 0.0f);

            ParticleSystem sparksPS = sparksGO.AddComponent<ParticleSystem>();
            var main = sparksPS.main;
            main.duration = 0.5f;
            main.loop = false;
            main.playOnAwake = false; // Prevent automatic spawning before skill trigger
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10.0f, 20.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startColor = new Color(1.0f, 0.95f, 0.95f, 1.0f);

            var emission = sparksPS.emission;
            emission.enabled = true;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 80) });

            var shape = sparksPS.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.3f;

            var sol = sparksPS.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1.0f),
                new Keyframe(1f, 0.0f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer psr = sparksGO.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = softParticleMat;
            psr.sortingOrder = 11;

            string prefabPath = Path.Combine(folderPath, "ExcaliburBeam.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(beamGO, prefabPath);
            Object.DestroyImmediate(beamGO);
        }

        private static void BuildGroundCrackPrefab(string folderPath, Material softParticleMat)
        {
            GameObject crackGO = new GameObject("GroundCrack");
            ParticleSystem ps = crackGO.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.25f);
            main.startColor = new Color(5.0f, 0.0f, 1.5f, 1.0f);
            main.gravityModifier = -0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 40) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(3.0f, 0.1f, 1.0f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1.0f),
                new Keyframe(1f, 0.0f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(5.0f, 0.0f, 1.5f, 1.0f), 0.5f), new GradientColorKey(new Color(5.0f, 0.0f, 1.5f, 1.0f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer psr = crackGO.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = softParticleMat;
            psr.sortingOrder = 10;

            string prefabPath = Path.Combine(folderPath, "GroundCrack.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(crackGO, prefabPath);
            Object.DestroyImmediate(crackGO);
        }

        private static void BuildLightningArcsPrefab(string folderPath, Material softParticleMat)
        {
            GameObject lightningGO = new GameObject("LightningArcs");
            ParticleSystem ps = lightningGO.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 2.8f;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 30f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.6f, 0.0f, 0.05f, 1.0f),
                new Color(0.3f, 0.0f, 0.35f, 1.0f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            
            ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[8];
            for (int i = 0; i < 8; i++)
            {
                bursts[i] = new ParticleSystem.Burst(i * 0.35f, 3, 6);
            }
            emission.SetBursts(bursts);

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.5f;

            ParticleSystemRenderer psr = lightningGO.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = softParticleMat;
            psr.renderMode = ParticleSystemRenderMode.Stretch;
            psr.velocityScale = 0.1f;
            psr.lengthScale = 4.0f;
            psr.sortingOrder = 12;

            string prefabPath = Path.Combine(folderPath, "LightningArcs.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(lightningGO, prefabPath);
            Object.DestroyImmediate(lightningGO);
        }

        private static void BuildDebrisParticlesPrefab(string folderPath, Material softParticleMat)
        {
            GameObject debrisGO = new GameObject("DebrisParticles");
            ParticleSystem ps = debrisGO.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 1.5f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.25f, 0.22f, 0.2f),
                new Color(0.4f, 0.3f, 0.2f)
            );
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 30) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 1.0f;
            
            var rotOverLifetime = ps.rotationOverLifetime;
            rotOverLifetime.enabled = true;
            rotOverLifetime.z = new ParticleSystem.MinMaxCurve(90f * Mathf.Deg2Rad, 180f * Mathf.Deg2Rad);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1.0f),
                new Keyframe(1f, 0.3f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            ParticleSystemRenderer psr = debrisGO.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = softParticleMat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sortingOrder = 11;

            string prefabPath = Path.Combine(folderPath, "DebrisParticles.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(debrisGO, prefabPath);
            Object.DestroyImmediate(debrisGO);
        }

        private static void BuildShockwaveRingPrefab(string folderPath, string matFolderPath, Material softParticleMat)
        {
            GameObject ringGO = new GameObject("ShockwaveRing");
            SpriteRenderer sr = ringGO.AddComponent<SpriteRenderer>();

            Sprite spr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/T_SoftParticleDot.png");
            if (spr != null)
            {
                sr.sprite = spr;
            }

            string matPath = Path.Combine(matFolderPath, "M_ShockwaveRing.mat").Replace("\\", "/");
            Material ringMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            
            if (ringMat == null)
            {
                ringMat = new Material(shader);
                ringMat.color = new Color(3.0f, 2.0f, 3.0f, 0.8f);
                AssetDatabase.CreateAsset(ringMat, matPath);
            }
            else
            {
                ringMat.shader = shader;
                ringMat.color = new Color(3.0f, 2.0f, 3.0f, 0.8f);
                EditorUtility.SetDirty(ringMat);
            }
            sr.sharedMaterial = ringMat;
            
            ringGO.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            sr.sortingOrder = 13;

            string prefabPath = Path.Combine(folderPath, "ShockwaveRing.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(ringGO, prefabPath);
            Object.DestroyImmediate(ringGO);
        }

        private static void BuildGroundScorchPrefab(string folderPath, string matFolderPath)
        {
            GameObject scorchGO = new GameObject("GroundScorch");
            SpriteRenderer sr = scorchGO.AddComponent<SpriteRenderer>();

            Sprite defaultBg = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            if (defaultBg != null)
            {
                sr.sprite = defaultBg;
            }

            string matPath = Path.Combine(matFolderPath, "M_GroundScorch.mat").Replace("\\", "/");
            Material scorchMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");

            if (scorchMat == null)
            {
                scorchMat = new Material(shader);
                scorchMat.color = new Color(2.0f, 0.0f, 0.0f, 0.8f);
                AssetDatabase.CreateAsset(scorchMat, matPath);
            }
            else
            {
                scorchMat.shader = shader;
                scorchMat.color = new Color(2.0f, 0.0f, 0.0f, 0.8f);
                EditorUtility.SetDirty(scorchMat);
            }
            sr.sharedMaterial = scorchMat;
            
            scorchGO.transform.localScale = new Vector3(12f, 0.3f, 1f);
            sr.sortingOrder = 5;

            string prefabPath = Path.Combine(folderPath, "GroundScorch.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(scorchGO, prefabPath);
            Object.DestroyImmediate(scorchGO);
        }

        private static void BuildResidualSmokePrefab(string folderPath, Material softParticleMat)
        {
            GameObject smokeGO = new GameObject("ResidualSmoke");
            ParticleSystem ps = smokeGO.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 3.0f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.15f, 0.05f, 0.2f, 0.6f),
                new Color(0.05f, 0.05f, 0.05f, 0.5f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 15f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6.0f, 0.5f, 1.0f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(1f, 1.5f)
            );
            sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer psr = smokeGO.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = softParticleMat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sortingOrder = 8;

            string prefabPath = Path.Combine(folderPath, "ResidualSmoke.prefab").Replace("\\", "/");
            PrefabUtility.SaveAsPrefabAsset(smokeGO, prefabPath);
            Object.DestroyImmediate(smokeGO);
        }
    }
}
