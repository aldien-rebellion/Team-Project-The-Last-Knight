using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheLastKnight.EditorTools
{
    public class MonsterMove
    {
        public string Name;
        public Sprite[] Frames;
        public float FPS;
        public bool IsLooping;
    }

    public class MoveVerificationResult
    {
        public string MoveName;
        public int FrameCount;
        public bool IsSizeConsistent;
        public Vector2 MinSize;
        public Vector2 MaxSize;
        public bool IsPivotConsistent;
        public Vector2 MinPivot;
        public Vector2 MaxPivot;
        public string Note;
    }

    public class MonsterVerificationResult
    {
        public string MonsterName;
        public List<MoveVerificationResult> MoveResults = new List<MoveVerificationResult>();
        public bool CrossMoveSizeConsistent;
        public bool CrossMovePivotConsistent;
        public bool CrossMovePPUConsistent;
        public float CommonPPU;
        public List<string> Issues = new List<string>();

        public string SummaryStatus => (MoveResults.All(m => m.IsSizeConsistent && m.IsPivotConsistent) && CrossMoveSizeConsistent && CrossMovePivotConsistent && CrossMovePPUConsistent) ? "PASS" : "WITH_NOTES";
    }

    public static class MonsterAnimationProcessor
    {
        public static string OutputBaseDir = "Assets/Animations/Enemies";

        public static MoveVerificationResult VerifyMove(MonsterMove move)
        {
            var res = new MoveVerificationResult
            {
                MoveName = move.Name,
                FrameCount = move.Frames != null ? move.Frames.Length : 0,
                IsSizeConsistent = true,
                IsPivotConsistent = true,
                Note = "OK"
            };

            if (move.Frames == null || move.Frames.Length == 0)
            {
                res.IsSizeConsistent = false;
                res.IsPivotConsistent = false;
                res.Note = "No frames found";
                return res;
            }

            var f0 = move.Frames[0];
            res.MinSize = res.MaxSize = new Vector2(f0.rect.width, f0.rect.height);
            res.MinPivot = res.MaxPivot = f0.pivot;

            List<string> notes = new List<string>();

            for (int i = 0; i < move.Frames.Length; i++)
            {
                var f = move.Frames[i];
                if (f == null)
                {
                    notes.Add($"Frame {i} is null");
                    continue;
                }

                if (Mathf.Abs(f.rect.width - res.MinSize.x) > 0.5f || Mathf.Abs(f.rect.height - res.MinSize.y) > 0.5f)
                {
                    res.IsSizeConsistent = false;
                }
                res.MinSize = Vector2.Min(res.MinSize, new Vector2(f.rect.width, f.rect.height));
                res.MaxSize = Vector2.Max(res.MaxSize, new Vector2(f.rect.width, f.rect.height));

                if (Vector2.Distance(f.pivot, res.MinPivot) > 0.5f)
                {
                    res.IsPivotConsistent = false;
                }
                res.MinPivot = Vector2.Min(res.MinPivot, f.pivot);
                res.MaxPivot = Vector2.Max(res.MaxPivot, f.pivot);
            }

            if (!res.IsSizeConsistent)
            {
                notes.Add($"Variable frame size: [{res.MinSize.x:F0}x{res.MinSize.y:F0}] to [{res.MaxSize.x:F0}x{res.MaxSize.y:F0}]");
            }
            if (!res.IsPivotConsistent)
            {
                notes.Add($"Pivot variation: ({res.MinPivot.x:F1}, {res.MinPivot.y:F1}) to ({res.MaxPivot.x:F1}, {res.MaxPivot.y:F1})");
            }

            if (notes.Count > 0)
            {
                res.Note = string.Join("; ", notes);
            }

            return res;
        }

        public static MonsterVerificationResult VerifyMonster(string monsterName, List<MonsterMove> moves)
        {
            var mRes = new MonsterVerificationResult
            {
                MonsterName = monsterName,
                CrossMoveSizeConsistent = true,
                CrossMovePivotConsistent = true,
                CrossMovePPUConsistent = true
            };

            var validMoves = moves.Where(m => m.Frames != null && m.Frames.Length > 0 && m.Frames[0] != null).ToList();
            if (validMoves.Count == 0)
            {
                mRes.Issues.Add("No valid moves found for monster.");
                return mRes;
            }

            float firstPPU = validMoves[0].Frames[0].pixelsPerUnit;
            mRes.CommonPPU = firstPPU;

            foreach (var m in validMoves)
            {
                foreach (var f in m.Frames)
                {
                    if (f == null) continue;
                    if (Mathf.Abs(f.pixelsPerUnit - firstPPU) > 0.1f)
                    {
                        mRes.CrossMovePPUConsistent = false;
                        mRes.Issues.Add($"PPU mismatch: {m.Name} has PPU={f.pixelsPerUnit}, expected {firstPPU}");
                        break;
                    }
                }
            }

            var allMovesSizes = validMoves.Select(m => new { Name = m.Name, W = m.Frames[0].rect.width, H = m.Frames[0].rect.height }).ToList();
            var minW = allMovesSizes.Min(s => s.W);
            var maxW = allMovesSizes.Max(s => s.W);
            var minH = allMovesSizes.Min(s => s.H);
            var maxH = allMovesSizes.Max(s => s.H);

            if (Mathf.Abs(maxW - minW) > 2f || Mathf.Abs(maxH - minH) > 2f)
            {
                mRes.CrossMoveSizeConsistent = false;
                mRes.Issues.Add($"Canvas size differs between moves: width ranges from {minW:F0} to {maxW:F0}, height ranges from {minH:F0} to {maxH:F0}");
            }

            var pivotsY = validMoves.Select(m => new { Name = m.Name, PivotY = m.Frames[0].pivot.y }).ToList();
            var minPy = pivotsY.Min(p => p.PivotY);
            var maxPy = pivotsY.Max(p => p.PivotY);
            if (Mathf.Abs(maxPy - minPy) > 5f)
            {
                mRes.CrossMovePivotConsistent = false;
                mRes.Issues.Add($"Ground / Pivot Y baseline variation across moves: {minPy:F1} to {maxPy:F1} (diff {maxPy - minPy:F1}px)");
            }

            return mRes;
        }

        public static AnimationClip CreateClip(string targetFolder, string monsterName, MonsterMove move)
        {
            if (move.Frames == null || move.Frames.Length == 0) return null;

            string clipPath = $"{targetFolder}/{monsterName}_{move.Name}.anim";
            
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
            }
            else
            {
                clip.ClearCurves();
            }

            clip.frameRate = move.FPS > 0 ? move.FPS : 12f;

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = "",
                propertyName = "m_Sprite"
            };

            ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[move.Frames.Length];
            for (int i = 0; i < move.Frames.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time = i / clip.frameRate,
                    value = move.Frames[i]
                };
            }

            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = move.IsLooping;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (!File.Exists(clipPath))
            {
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else
            {
                EditorUtility.SetDirty(clip);
            }

            return clip;
        }

        public static AnimatorController CreateController(string targetFolder, string monsterName, List<MonsterMove> moves, Dictionary<string, AnimationClip> clips)
        {
            string controllerPath = $"{targetFolder}/{monsterName}Controller.controller";

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var rootStateMachine = controller.layers[0].stateMachine;

            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("ActionIndex", AnimatorControllerParameterType.Int);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsDead", AnimatorControllerParameterType.Bool);

            var idleMove = moves.FirstOrDefault(m => m.Name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0) ?? moves.FirstOrDefault();
            
            AnimatorState defaultState = null;
            var statesByName = new Dictionary<string, AnimatorState>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                if (!clips.ContainsKey(m.Name) || clips[m.Name] == null) continue;

                string stateName = m.Name.Replace(".", "_");
                var state = rootStateMachine.AddState(stateName);
                state.motion = clips[m.Name];
                statesByName[m.Name] = state;
                statesByName[stateName] = state;

                if (m == idleMove && defaultState == null)
                {
                    defaultState = state;
                    rootStateMachine.defaultState = state;
                }

                // AnyState transition via ActionIndex
                var anyTrans = rootStateMachine.AddAnyStateTransition(state);
                anyTrans.hasExitTime = false;
                anyTrans.hasFixedDuration = true;
                anyTrans.duration = 0f;
                anyTrans.canTransitionToSelf = false;
                anyTrans.AddCondition(AnimatorConditionMode.Equals, i, "ActionIndex");

                // Attack trigger
                if (m.Name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Atk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Swing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Combo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Skill", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var atkTrans = rootStateMachine.AddAnyStateTransition(state);
                    atkTrans.hasExitTime = false;
                    atkTrans.hasFixedDuration = true;
                    atkTrans.duration = 0f;
                    atkTrans.canTransitionToSelf = false;
                    atkTrans.AddCondition(AnimatorConditionMode.If, 0, "Attack");

                    if (defaultState != null && defaultState != state)
                    {
                        var exitToIdle = state.AddTransition(defaultState);
                        exitToIdle.hasExitTime = true;
                        exitToIdle.exitTime = 0.95f;
                        exitToIdle.hasFixedDuration = true;
                        exitToIdle.duration = 0f;
                    }
                }

                // Hurt trigger
                if (m.Name.IndexOf("Hurt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Hit", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var hurtTrans = rootStateMachine.AddAnyStateTransition(state);
                    hurtTrans.hasExitTime = false;
                    hurtTrans.hasFixedDuration = true;
                    hurtTrans.duration = 0f;
                    hurtTrans.canTransitionToSelf = false;
                    hurtTrans.AddCondition(AnimatorConditionMode.If, 0, "Hurt");

                    if (defaultState != null && defaultState != state)
                    {
                        var exitToIdle = state.AddTransition(defaultState);
                        exitToIdle.hasExitTime = true;
                        exitToIdle.exitTime = 0.95f;
                        exitToIdle.hasFixedDuration = true;
                        exitToIdle.duration = 0f;
                    }
                }

                // Death bool
                if (m.Name.IndexOf("Dead", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Death", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Die", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var deadTrans = rootStateMachine.AddAnyStateTransition(state);
                    deadTrans.hasExitTime = false;
                    deadTrans.hasFixedDuration = true;
                    deadTrans.duration = 0f;
                    deadTrans.canTransitionToSelf = false;
                    deadTrans.AddCondition(AnimatorConditionMode.If, 0, "IsDead");
                }
            }

            var walkMove = moves.FirstOrDefault(m => m.Name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0);
            if (defaultState != null && walkMove != null && statesByName.ContainsKey(walkMove.Name))
            {
                var walkState = statesByName[walkMove.Name];
                var idleToWalk = defaultState.AddTransition(walkState);
                idleToWalk.hasExitTime = false;
                idleToWalk.duration = 0f;
                idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

                var walkToIdle = walkState.AddTransition(defaultState);
                walkToIdle.hasExitTime = false;
                walkToIdle.duration = 0f;
                walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        public static MonsterVerificationResult ProcessMonster(string monsterName, List<MonsterMove> moves, string customSubfolder = null)
        {
            string folderName = customSubfolder ?? monsterName;
            string targetFolder = $"{OutputBaseDir}/{folderName}";
            
            // Ensure parent directory exists
            if (!AssetDatabase.IsValidFolder(OutputBaseDir))
            {
                AssetDatabase.CreateFolder("Assets/Animations", "Enemies");
            }
            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                var parts = targetFolder.Split('/');
                string cur = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = cur + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(cur, parts[i]);
                    }
                    cur = next;
                }
            }

            var clips = new Dictionary<string, AnimationClip>();
            var mRes = new MonsterVerificationResult { MonsterName = monsterName };

            foreach (var move in moves)
            {
                var vMove = VerifyMove(move);
                mRes.MoveResults.Add(vMove);

                var clip = CreateClip(targetFolder, monsterName, move);
                clips[move.Name] = clip;
            }

            var crossRes = VerifyMonster(monsterName, moves);
            mRes.CrossMoveSizeConsistent = crossRes.CrossMoveSizeConsistent;
            mRes.CrossMovePivotConsistent = crossRes.CrossMovePivotConsistent;
            mRes.CrossMovePPUConsistent = crossRes.CrossMovePPUConsistent;
            mRes.CommonPPU = crossRes.CommonPPU;
            mRes.Issues.AddRange(crossRes.Issues);

            CreateController(targetFolder, monsterName, moves, clips);

            AssetDatabase.SaveAssets();
            return mRes;
        }

        public static int ExtractFrameNumber(string name)
        {
            var matches = Regex.Matches(name, @"\d+");
            if (matches.Count > 0 && int.TryParse(matches[matches.Count - 1].Value, out int num))
            {
                return num;
            }
            return 0;
        }

        public static Sprite ConfigureSingleSprite(string assetPath, Vector2 pivot, float ppu = 100f)
        {
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp == null) return null;

            imp.maxTextureSize = 4096;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            imp.SetTextureSettings(settings);

            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        public static Sprite[] SliceGridStrip(string assetPath, int frameWidth, int frameHeight, Vector2 pivot, float ppu = 100f, int maxTexSize = 4096)
        {
            var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (imp == null) return new Sprite[0];

            imp.maxTextureSize = maxTexSize;
            imp.spriteImportMode = SpriteImportMode.Multiple;
            imp.spritePixelsPerUnit = ppu;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;

            var bytes = File.ReadAllBytes(assetPath);
            var rawTex = new Texture2D(2, 2);
            rawTex.LoadImage(bytes);
            int cols = rawTex.width / frameWidth;
            int rows = rawTex.height / frameHeight;

            var metas = new List<SpriteMetaData>();
            string baseName = Path.GetFileNameWithoutExtension(assetPath);
            int idx = 0;
            for (int r = rows - 1; r >= 0; r--)
            {
                for (int c = 0; c < cols; c++)
                {
                    var meta = new SpriteMetaData
                    {
                        name = $"{baseName}_{idx:D4}",
                        rect = new Rect(c * frameWidth, r * frameHeight, frameWidth, frameHeight),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = pivot
                    };
                    metas.Add(meta);
                    idx++;
                }
            }
            imp.spritesheet = metas.ToArray();
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();

            return AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>()
                .OrderBy(s => ExtractFrameNumber(s.name)).ToArray();
        }

        public static Sprite[] LoadSpritesNaturally(string assetPath)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
            return all.OrderBy(s => ExtractFrameNumber(s.name)).ToArray();
        }

        public static Sprite[] LoadIndividualFrameSprites(string folderPath, string prefix = "")
        {
            if (!Directory.Exists(folderPath)) return new Sprite[0];
            string pattern = string.IsNullOrEmpty(prefix) ? "*.png" : (prefix.EndsWith(".png") ? prefix : prefix + "*.png");
            var files = Directory.GetFiles(folderPath, pattern)
                .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => ExtractFrameNumber(Path.GetFileNameWithoutExtension(f)))
                .ToArray();

            var list = new List<Sprite>();
            foreach(var f in files)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(f.Replace("\\", "/"));
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        public static List<MonsterMove> LoadMovesFromAseprite(string asePath, float defaultFPS = 10f)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(asePath).OfType<AnimationClip>().ToArray();
            var list = new List<MonsterMove>();
            foreach(var c in clips)
            {
                var bindings = AnimationUtility.GetObjectReferenceCurveBindings(c);
                var spriteBinding = bindings.FirstOrDefault(b => b.propertyName == "m_Sprite");
                if (spriteBinding.propertyName == "m_Sprite")
                {
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(c, spriteBinding);
                    var sprites = keyframes.Select(k => k.value as Sprite).Where(s => s != null).ToArray();
                    list.Add(new MonsterMove
                    {
                        Name = c.name,
                        Frames = sprites,
                        FPS = c.frameRate > 0 ? c.frameRate : defaultFPS,
                        IsLooping = c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  c.name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  c.name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  c.name.IndexOf("flight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  c.name.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0
                    });
                }
            }
            return list;
        }

        // ==========================================
        // Specialized Monster Processors (Overhaul)
        // ==========================================

        public static void ProcessForestMushroom()
        {
            string dir = "Assets/sprites/Monsters/Forest_Monsters_FREE/Mushroom/Mushroom with VFX";
            Vector2 piv = new Vector2(0.5f, 0.0f); // 80x64 ground is at Y=0
            var moves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Attack", Frames = SliceGridStrip($"{dir}/Mushroom-Attack.png", 80, 64, piv), FPS = 12f, IsLooping = false },
                new MonsterMove { Name = "AttackWithStun", Frames = SliceGridStrip($"{dir}/Mushroom-AttackWithStun.png", 80, 64, piv), FPS = 14f, IsLooping = false },
                new MonsterMove { Name = "Die", Frames = SliceGridStrip($"{dir}/Mushroom-Die.png", 80, 64, piv), FPS = 12f, IsLooping = false },
                new MonsterMove { Name = "Hit", Frames = SliceGridStrip($"{dir}/Mushroom-Hit.png", 80, 64, piv), FPS = 8f, IsLooping = false },
                new MonsterMove { Name = "Idle", Frames = SliceGridStrip($"{dir}/Mushroom-Idle.png", 80, 64, piv), FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Run", Frames = SliceGridStrip($"{dir}/Mushroom-Run.png", 80, 64, piv), FPS = 10f, IsLooping = true },
                new MonsterMove { Name = "Stun", Frames = SliceGridStrip($"{dir}/Mushroom-Stun.png", 80, 64, piv), FPS = 12f, IsLooping = true }
            };
            ProcessMonster("ForestMushroom", moves);
        }

        public static void ProcessFox()
        {
            string dir = "Assets/sprites/Monsters/สร้างเกม/สุนัขจิ้งจอก/x256_Spritesheets";
            Vector2 piv = new Vector2(0.5f, 74f / 256f); // 256x256 ground is at Y=74
            var moves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Appear", Frames = SliceGridStrip($"{dir}/Appear.png", 256, 256, piv), FPS = 14f, IsLooping = false },
                new MonsterMove { Name = "Attack", Frames = SliceGridStrip($"{dir}/Attack.png", 256, 256, piv), FPS = 14f, IsLooping = false },
                new MonsterMove { Name = "Death", Frames = SliceGridStrip($"{dir}/Death.png", 256, 256, piv), FPS = 14f, IsLooping = false },
                new MonsterMove { Name = "Disappear", Frames = SliceGridStrip($"{dir}/Disappear.png", 256, 256, piv), FPS = 14f, IsLooping = false },
                new MonsterMove { Name = "Hit", Frames = SliceGridStrip($"{dir}/Hit.png", 256, 256, piv), FPS = 14f, IsLooping = false },
                new MonsterMove { Name = "Idle", Frames = SliceGridStrip($"{dir}/Idle.png", 256, 256, piv), FPS = 12f, IsLooping = true },
                new MonsterMove { Name = "Run", Frames = SliceGridStrip($"{dir}/Run.png", 256, 256, piv), FPS = 14f, IsLooping = true }
            };
            ProcessMonster("Fox", moves);
        }

        public static void ProcessJinn()
        {
            string dir = "Assets/sprites/Monsters/craftpix-561178-free-rpg-monster-sprites-pixel-art/PNG/jinn_animation";
            Vector2 piv = new Vector2(0.5f, 16f / 128f);

            var attackSprites = new List<Sprite>();
            for (int i = 1; i <= 4; i++) attackSprites.Add(ConfigureSingleSprite($"{dir}/Attack{i}.png", piv));

            var deathSprites = new List<Sprite>();
            for (int i = 1; i <= 6; i++) deathSprites.Add(ConfigureSingleSprite($"{dir}/Death{i}.png", piv));

            var flightSprites = new List<Sprite>();
            for (int i = 1; i <= 4; i++) flightSprites.Add(ConfigureSingleSprite($"{dir}/Flight{i}.png", piv));

            var hurtSprites = new List<Sprite>();
            for (int i = 1; i <= 2; i++) hurtSprites.Add(ConfigureSingleSprite($"{dir}/Hurt{i}.png", piv));

            var idleSprites = new List<Sprite>();
            for (int i = 1; i <= 3; i++) idleSprites.Add(ConfigureSingleSprite($"{dir}/Idle{i}.png", piv));

            var moves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Attack", Frames = attackSprites.ToArray(), FPS = 8f, IsLooping = false },
                new MonsterMove { Name = "Death", Frames = deathSprites.ToArray(), FPS = 8f, IsLooping = false },
                new MonsterMove { Name = "Flight", Frames = flightSprites.ToArray(), FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Hurt", Frames = hurtSprites.ToArray(), FPS = 8f, IsLooping = false },
                new MonsterMove { Name = "Idle", Frames = idleSprites.ToArray(), FPS = 8f, IsLooping = true }
            };
            ProcessMonster("Jinn", moves);

            // Projectile / Spell: Jinn_Magic
            string projFolder = $"{OutputBaseDir}/Projectiles/Jinn_Magic";
            if (!AssetDatabase.IsValidFolder(projFolder)) AssetDatabase.CreateFolder($"{OutputBaseDir}/Projectiles", "Jinn_Magic");

            var magicSprites = new List<Sprite>();
            int[] magicIndices = new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 22, 23 };
            foreach(int idx in magicIndices)
            {
                magicSprites.Add(ConfigureSingleSprite($"{dir}/Magic_Attack{idx}.png", new Vector2(0.5f, 0.5f)));
            }
            var magicMove = new MonsterMove { Name = "Cast", Frames = magicSprites.ToArray(), FPS = 12f, IsLooping = false };
            CreateClip(projFolder, "Jinn_Magic", magicMove);
        }

        public static void ProcessMinotaurs()
        {
            for (int m = 1; m <= 3; m++)
            {
                string dir = $"Assets/sprites/Monsters/craftpix-net-170637-free-minotaur-sprite-sheet-pixel-art-pack/Minotaur_{m}";
                Vector2 piv = new Vector2(0.5f, 0.0f); // 128x128 ground Y=0
                string mName = $"Minotaur_{m}";

                var moves = new List<MonsterMove>
                {
                    new MonsterMove { Name = "Attack", Frames = SliceGridStrip($"{dir}/Attack.png", 128, 128, piv), FPS = 10f, IsLooping = false },
                    new MonsterMove { Name = "Dead", Frames = SliceGridStrip($"{dir}/Dead.png", 128, 128, piv), FPS = 8f, IsLooping = false },
                    new MonsterMove { Name = "Hurt", Frames = SliceGridStrip($"{dir}/Hurt.png", 128, 128, piv), FPS = 8f, IsLooping = false },
                    new MonsterMove { Name = "Idle", Frames = SliceGridStrip($"{dir}/Idle.png", 128, 128, piv), FPS = 10f, IsLooping = true },
                    new MonsterMove { Name = "Walk", Frames = SliceGridStrip($"{dir}/Walk.png", 128, 128, piv), FPS = 12f, IsLooping = true }
                };
                ProcessMonster(mName, moves);
            }
        }

        public static void ProcessMoonstoneKeeper()
        {
            string baseDir = "Assets/sprites/Monsters/Moonstone_Keeper Eldermoon_Grove - By SUCART";
            Vector2 piv = new Vector2(0.5f, 40f / 150f); // 150x150 ground Y=40

            var actionMap = new (string Folder, string Name, float FPS, bool Loop)[]
            {
                ("Attack1-MoonstoneKeeper-SUCART", "Attack1", 12f, false),
                ("Attack2-MoonstoneKeeper-SUCART", "Attack2", 12f, false),
                ("Dash-MoonstoneKeeper-SUCART", "Dash", 12f, false),
                ("Death-MoonstoneKeeper-SUCART", "Death", 12f, false),
                ("Hit-MoonstoneKeeper-SUCART", "Hit", 12f, false),
                ("Idle-MoonstoneKeeper-SUCART", "Idle", 12f, true),
                ("Jump Loop-MoonstoneKeeper-SUCART", "JumpLoop", 12f, true),
                ("Jump Start-MoonstoneKeeper-SUCART", "JumpStart", 12f, false),
                ("Land-MoonstoneKeeper-SUCART", "Land", 12f, false),
                ("Run Alt-MoonstoneKeeper-SUCART", "RunAlt", 12f, true),
                ("Run-MoonstoneKeeper-SUCART", "Run", 12f, true),
                ("Walk-MoonstoneKeeper-SUCART", "Walk", 12f, true)
            };

            var moves = new List<MonsterMove>();
            foreach (var a in actionMap)
            {
                string folder = $"{baseDir}/{a.Folder}/No BG";
                var files = Directory.GetFiles(folder, "*.png")
                    .Where(f => !f.EndsWith(".meta"))
                    .OrderBy(f => ExtractFrameNumber(Path.GetFileNameWithoutExtension(f)))
                    .ToArray();

                var sprites = new List<Sprite>();
                foreach (var f in files)
                {
                    sprites.Add(ConfigureSingleSprite(f.Replace("\\", "/"), piv));
                }
                moves.Add(new MonsterMove { Name = a.Name, Frames = sprites.ToArray(), FPS = a.FPS, IsLooping = a.Loop });
            }
            ProcessMonster("MoonstoneKeeper", moves);
        }

        public static void ProcessShadowDemonDragon()
        {
            string dir = "Assets/sprites/Monsters/Shadow_Demon_Dragon_Asset_Pack/Shadow_Demon_Dragon_Asset_Pack/Animations";
            Vector2 piv = new Vector2(0.5f, 246f / 1584f); // 1408x1584 ground Y=246

            var moves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Idle_Left", Frames = SliceGridStrip($"{dir}/Idle_Left/Idle_left.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = true },
                new MonsterMove { Name = "Idle_Right", Frames = SliceGridStrip($"{dir}/Idle_Right/Idle_Right.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = true },
                new MonsterMove { Name = "Attack_Left", Frames = SliceGridStrip($"{dir}/Attack_Left/Attack_left.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = false },
                new MonsterMove { Name = "Attack_Right", Frames = SliceGridStrip($"{dir}/Attack_Right/Attack_Right.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = false },
                new MonsterMove { Name = "Walk_Left", Frames = SliceGridStrip($"{dir}/Walk_Left/Walk_left.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = true },
                new MonsterMove { Name = "Walk_Right", Frames = SliceGridStrip($"{dir}/Walk_Right/Walk_Right.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = true },
                new MonsterMove { Name = "Death_Left", Frames = SliceGridStrip($"{dir}/Death_Left/Death_left.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = false },
                new MonsterMove { Name = "Death_Right", Frames = SliceGridStrip($"{dir}/Death_Right/Death_Right.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = false },
                new MonsterMove { Name = "Hit_Left", Frames = SliceGridStrip($"{dir}/Hit_Left/Hit_left.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = false },
                new MonsterMove { Name = "Hit_Right", Frames = SliceGridStrip($"{dir}/Hit_Right/Hit_Right.png", 1408, 1584, piv, 100f, 8192), FPS = 10f, IsLooping = false }
            };
            ProcessMonster("ShadowDemonDragon", moves);
        }

        public static void ProcessTrader_1()
        {
            string dir = "Assets/sprites/Monsters/Free-City-Trader-Character-Sprite-Sheets-Pixel-Art/Trader_1";
            Vector2 piv = new Vector2(0.5f, 0.0f); // 128x128 ground Y=0

            var moves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Approval", Frames = SliceGridStrip($"{dir}/Approval.png", 128, 128, piv), FPS = 10f, IsLooping = false },
                new MonsterMove { Name = "Dialogue", Frames = SliceGridStrip($"{dir}/Dialogue.png", 128, 128, piv), FPS = 12f, IsLooping = true },
                new MonsterMove { Name = "Idle", Frames = SliceGridStrip($"{dir}/Idle.png", 128, 128, piv), FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Idle_2", Frames = SliceGridStrip($"{dir}/Idle_2.png", 128, 128, piv), FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Idle_3", Frames = SliceGridStrip($"{dir}/Idle_3.png", 128, 128, piv), FPS = 8f, IsLooping = true }
            };
            ProcessMonster("Trader_1", moves);
        }

        public static void ProcessSmallDragon()
        {
            string dir = "Assets/sprites/Monsters/craftpix-561178-free-rpg-monster-sprites-pixel-art/PNG/small_dragon";
            Vector2 piv = new Vector2(0.5f, 46f / 128f);

            var attackSprites = new List<Sprite>();
            for (int i = 1; i <= 3; i++) attackSprites.Add(ConfigureSingleSprite($"{dir}/Attack{i}.png", piv));

            var deathSprites = new List<Sprite>();
            for (int i = 1; i <= 4; i++) deathSprites.Add(ConfigureSingleSprite($"{dir}/Death{i}.png", piv));

            var hurtSprites = new List<Sprite>();
            for (int i = 1; i <= 2; i++) hurtSprites.Add(ConfigureSingleSprite($"{dir}/Hurt{i}.png", piv));

            var idleSprites = new List<Sprite>();
            for (int i = 1; i <= 3; i++) idleSprites.Add(ConfigureSingleSprite($"{dir}/Idle{i}.png", piv));

            var walkSprites = new List<Sprite>();
            for (int i = 1; i <= 4; i++) walkSprites.Add(ConfigureSingleSprite($"{dir}/Walk{i}.png", piv));

            var moves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Attack", Frames = attackSprites.ToArray(), FPS = 8f, IsLooping = false },
                new MonsterMove { Name = "Death", Frames = deathSprites.ToArray(), FPS = 8f, IsLooping = false },
                new MonsterMove { Name = "Hurt", Frames = hurtSprites.ToArray(), FPS = 8f, IsLooping = false },
                new MonsterMove { Name = "Idle", Frames = idleSprites.ToArray(), FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Walk", Frames = walkSprites.ToArray(), FPS = 8f, IsLooping = true }
            };
            ProcessMonster("Small_dragon", moves);

            // Clean up old Fire_Attack clip in Small_dragon folder if present
            string oldClip = $"{OutputBaseDir}/Small_dragon/Small_dragon_Fire_Attack.anim";
            if (File.Exists(oldClip)) AssetDatabase.DeleteAsset(oldClip);

            // Separate FireBall projectile into Projectiles/SmallDragon_FireBall
            string projFolder = $"{OutputBaseDir}/Projectiles/SmallDragon_FireBall";
            if (!AssetDatabase.IsValidFolder(projFolder)) AssetDatabase.CreateFolder($"{OutputBaseDir}/Projectiles", "SmallDragon_FireBall");

            var fireSprites = new List<Sprite>();
            for (int i = 1; i <= 9; i++) fireSprites.Add(ConfigureSingleSprite($"{dir}/Fire_Attack{i}.png", new Vector2(0.5f, 0.5f)));

            var projMove = new MonsterMove { Name = "FireBall", Frames = fireSprites.ToArray(), FPS = 10f, IsLooping = true };
            CreateClip(projFolder, "SmallDragon_FireBall", projMove);
        }

        public static void ProcessFantasyRangedAttacks()
        {
            string projBase = "Assets/sprites/Monsters/Monsters_Creatures_Fantasy/Monster_Creatures_Fantasy(Projectile_Attack)";
            string baseDir = "Assets/sprites/Monsters/Monsters_Creatures_Fantasy";

            // 1. Flying Eye
            var feAttack3Frames = SliceGridStrip($"{projBase}/Flying eye/Attack3.png", 150, 150, new Vector2(0.5f, 0.5f));
            CreateClip($"{OutputBaseDir}/FlyingEye", "FlyingEye", new MonsterMove { Name = "Attack3", Frames = feAttack3Frames, FPS = 10f, IsLooping = false });
            
            string feProjDir = $"{OutputBaseDir}/Projectiles/FlyingEye_Projectile";
            if (!AssetDatabase.IsValidFolder(feProjDir)) AssetDatabase.CreateFolder($"{OutputBaseDir}/Projectiles", "FlyingEye_Projectile");
            var feProjFrames = SliceGridStrip($"{projBase}/Flying eye/projectile_sprite.png", 48, 48, new Vector2(0.5f, 0.5f));
            CreateClip(feProjDir, "FlyingEye_Projectile", new MonsterMove { Name = "Projectile", Frames = feProjFrames, FPS = 10f, IsLooping = true });

            // Rebuild FlyingEye controller to include Attack3
            var feMoves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Attack", Frames = SliceGridStrip($"{baseDir}/Flying eye/Attack.png", 150, 150, new Vector2(0.5f, 0.5f)), FPS = 10f },
                new MonsterMove { Name = "Attack3", Frames = feAttack3Frames, FPS = 10f },
                new MonsterMove { Name = "Death", Frames = SliceGridStrip($"{baseDir}/Flying eye/Death.png", 150, 150, new Vector2(0.5f, 0.5f)), FPS = 8f },
                new MonsterMove { Name = "Flight", Frames = SliceGridStrip($"{baseDir}/Flying eye/Flight.png", 150, 150, new Vector2(0.5f, 0.5f)), FPS = 10f, IsLooping = true },
                new MonsterMove { Name = "TakeHit", Frames = SliceGridStrip($"{baseDir}/Flying eye/Take Hit.png", 150, 150, new Vector2(0.5f, 0.5f)), FPS = 8f }
            };
            ProcessMonster("FlyingEye", feMoves);

            // 2. Goblin
            var gobAttack3Frames = SliceGridStrip($"{projBase}/Goblin/Attack3.png", 150, 150, new Vector2(0.5f, 49f / 150f));
            CreateClip($"{OutputBaseDir}/Goblin", "Goblin", new MonsterMove { Name = "Attack3", Frames = gobAttack3Frames, FPS = 12f, IsLooping = false });

            string gobProjDir = $"{OutputBaseDir}/Projectiles/Goblin_Bomb";
            if (!AssetDatabase.IsValidFolder(gobProjDir)) AssetDatabase.CreateFolder($"{OutputBaseDir}/Projectiles", "Goblin_Bomb");
            var gobProjFrames = SliceGridStrip($"{projBase}/Goblin/Bomb_sprite.png", 100, 100, new Vector2(0.5f, 0.5f));
            CreateClip(gobProjDir, "Goblin_Bomb", new MonsterMove { Name = "Bomb", Frames = gobProjFrames, FPS = 12f, IsLooping = true });

            var gobMoves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Attack", Frames = SliceGridStrip($"{baseDir}/Goblin/Attack.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 10f },
                new MonsterMove { Name = "Attack3", Frames = gobAttack3Frames, FPS = 12f },
                new MonsterMove { Name = "Death", Frames = SliceGridStrip($"{baseDir}/Goblin/Death.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f },
                new MonsterMove { Name = "Idle", Frames = SliceGridStrip($"{baseDir}/Goblin/Idle.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Run", Frames = SliceGridStrip($"{baseDir}/Goblin/Run.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 10f, IsLooping = true },
                new MonsterMove { Name = "TakeHit", Frames = SliceGridStrip($"{baseDir}/Goblin/Take Hit.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f }
            };
            ProcessMonster("Goblin", gobMoves);

            // 3. FantasyMushroom
            var mushAttack3Frames = SliceGridStrip($"{projBase}/Mushroom/Attack3.png", 150, 150, new Vector2(0.5f, 49f / 150f));
            CreateClip($"{OutputBaseDir}/FantasyMushroom", "FantasyMushroom", new MonsterMove { Name = "Attack3", Frames = mushAttack3Frames, FPS = 11f, IsLooping = false });

            string mushProjDir = $"{OutputBaseDir}/Projectiles/FantasyMushroom_Projectile";
            if (!AssetDatabase.IsValidFolder(mushProjDir)) AssetDatabase.CreateFolder($"{OutputBaseDir}/Projectiles", "FantasyMushroom_Projectile");
            var mushProjFrames = SliceGridStrip($"{projBase}/Mushroom/Projectile_sprite.png", 50, 50, new Vector2(0.5f, 0.5f));
            CreateClip(mushProjDir, "FantasyMushroom_Projectile", new MonsterMove { Name = "Projectile", Frames = mushProjFrames, FPS = 10f, IsLooping = true });

            var mushMoves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Attack", Frames = SliceGridStrip($"{baseDir}/Mushroom/Attack.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 10f },
                new MonsterMove { Name = "Attack3", Frames = mushAttack3Frames, FPS = 11f },
                new MonsterMove { Name = "Death", Frames = SliceGridStrip($"{baseDir}/Mushroom/Death.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f },
                new MonsterMove { Name = "Idle", Frames = SliceGridStrip($"{baseDir}/Mushroom/Idle.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Run", Frames = SliceGridStrip($"{baseDir}/Mushroom/Run.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 10f, IsLooping = true },
                new MonsterMove { Name = "TakeHit", Frames = SliceGridStrip($"{baseDir}/Mushroom/Take Hit.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f }
            };
            ProcessMonster("FantasyMushroom", mushMoves);

            // 4. Skeleton
            var skelAttack3Frames = SliceGridStrip($"{projBase}/Skeleton/Attack3.png", 150, 150, new Vector2(0.5f, 49f / 150f));
            CreateClip($"{OutputBaseDir}/Skeleton", "Skeleton", new MonsterMove { Name = "Attack3", Frames = skelAttack3Frames, FPS = 10f, IsLooping = false });

            string skelProjDir = $"{OutputBaseDir}/Projectiles/Skeleton_Sword";
            if (!AssetDatabase.IsValidFolder(skelProjDir)) AssetDatabase.CreateFolder($"{OutputBaseDir}/Projectiles", "Skeleton_Sword");
            var skelProjFrames = SliceGridStrip($"{projBase}/Skeleton/Sword_sprite.png", 92, 102, new Vector2(0.5f, 0.5f));
            CreateClip(skelProjDir, "Skeleton_Sword", new MonsterMove { Name = "Sword", Frames = skelProjFrames, FPS = 10f, IsLooping = true });

            var skelMoves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Attack", Frames = SliceGridStrip($"{baseDir}/Skeleton/Attack.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 10f },
                new MonsterMove { Name = "Attack3", Frames = skelAttack3Frames, FPS = 10f },
                new MonsterMove { Name = "Death", Frames = SliceGridStrip($"{baseDir}/Skeleton/Death.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f },
                new MonsterMove { Name = "Idle", Frames = SliceGridStrip($"{baseDir}/Skeleton/Idle.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Shield", Frames = SliceGridStrip($"{baseDir}/Skeleton/Shield.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "TakeHit", Frames = SliceGridStrip($"{baseDir}/Skeleton/Take Hit.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f },
                new MonsterMove { Name = "Walk", Frames = SliceGridStrip($"{baseDir}/Skeleton/Walk.png", 150, 150, new Vector2(0.5f, 49f / 150f)), FPS = 8f, IsLooping = true }
            };
            ProcessMonster("Skeleton", skelMoves);

            // Clean up obsolete FantasyProjectiles folder if all 4 individual subfolders exist
            string oldFantasyProj = $"{OutputBaseDir}/Projectiles/FantasyProjectiles";
            if (Directory.Exists(oldFantasyProj)) AssetDatabase.DeleteAsset(oldFantasyProj);
        }

        public static void ProcessUndeadExecutioner()
        {
            string dir = "Assets/sprites/Monsters/Undead executioner/Undead executioner puppet/png";
            Vector2 piv = new Vector2(0.5f, 15f / 100f);

            var idleSprites = SliceGridStrip($"{dir}/idle.png", 100, 100, piv).Take(4).ToArray();
            var idle2Sprites = SliceGridStrip($"{dir}/idle2.png", 100, 100, piv);
            var attackSprites = SliceGridStrip($"{dir}/attacking.png", 100, 100, piv).Take(13).ToArray();
            var deathSprites = SliceGridStrip($"{dir}/death.png", 100, 100, piv).Take(18).ToArray();
            var skill1Sprites = SliceGridStrip($"{dir}/skill1.png", 100, 100, piv);
            var summonSprites = SliceGridStrip($"{dir}/summon.png", 100, 100, piv).Take(5).ToArray();

            var moves = new List<MonsterMove>
            {
                new MonsterMove { Name = "Attack", Frames = attackSprites, FPS = 10f, IsLooping = false },
                new MonsterMove { Name = "Death", Frames = deathSprites, FPS = 10f, IsLooping = false },
                new MonsterMove { Name = "Idle", Frames = idleSprites, FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Idle2", Frames = idle2Sprites, FPS = 8f, IsLooping = true },
                new MonsterMove { Name = "Skill1", Frames = skill1Sprites, FPS = 10f, IsLooping = false },
                new MonsterMove { Name = "Summon", Frames = summonSprites, FPS = 8f, IsLooping = false }
            };
            ProcessMonster("UndeadExecutioner", moves);
        }

        public static void FixAllFromFlyingEyeDownwards()
        {
            Debug.Log("[MonsterProcessor] Starting overhaul of all monsters from FlyingEye downwards...");
            ProcessFantasyRangedAttacks();
            ProcessForestMushroom();
            ProcessFox();
            ProcessJinn();
            ProcessMinotaurs();
            ProcessMoonstoneKeeper();
            ProcessShadowDemonDragon();
            ProcessTrader_1();
            ProcessSmallDragon();
            ProcessUndeadExecutioner();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MonsterProcessor] Overhaul complete!");
        }
    }
}
