using System.Collections.Generic;
using System.IO;
using HackSlash.Core;
using HackSlash.Enemies;
using HackSlash.Player;
using HackSlash.UI;
using HackSlash.Waves;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HackSlash.EditorTools
{
    /// <summary>
    /// One-shot setup: generates AnimatorControllers, prefabs, WaveDefinition assets,
    /// and rebuilds SampleScene with player + spawner + HUD wired up.
    /// </summary>
    public static class SampleSceneBuilder
    {
        private const string GenRoot = "Assets/Generated";
        private const string AnimatorsDir = GenRoot + "/Animators";
        private const string PrefabsDir = GenRoot + "/Prefabs";
        private const string WavesDir = GenRoot + "/Waves";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string HeroAnimDir = "Assets/DouglasAvila/CobraRobot/Animations/HeroSkin";
        private const string EnemyAnimDir = "Assets/DouglasAvila/CobraRobot/Animations/EnemySkin";
        private const string HeroIdleSprite = "Assets/DouglasAvila/CobraRobot/Sprites/CobraRobot/HeroSkin/HeroSkin_HighRes_Idle/0.png";
        private const string EnemyIdleSprite = "Assets/DouglasAvila/CobraRobot/Sprites/CobraRobot/EnemySkin/EnemySkin_HighRes_Idle/0.png";
        private const string InputActionsAsset = "Assets/InputSystem_Actions.inputactions";

        private const int GroundLayer = 6;
        private const int PlayerLayer = 7;
        private const int EnemyLayer = 8;
        private const int ProjectileLayer = 9;
        private const string GroundLayerName = "Ground";
        private const string PlayerLayerName = "Player";
        private const string EnemyLayerName = "Enemy";
        private const string ProjectileLayerName = "Projectile";

        [MenuItem("111Percent/Build Sample Scene")]
        public static void BuildAll()
        {
            EnsureDirectories();
            EnsureLayers();

            var heroController = BuildHeroAnimator();
            var meleeController = BuildEnemyAnimator(EnemyAnimDir, "MeleeEnemyAnimator", includeShooting: false);
            var rangedController = BuildEnemyAnimator(EnemyAnimDir, "RangedEnemyAnimator", includeShooting: true);

            AssetDatabase.SaveAssets();

            var projectilePrefab = BuildProjectilePrefab();
            var playerPrefab = BuildPlayerPrefab(heroController);
            var meleePrefab = BuildMeleeEnemyPrefab(meleeController);
            var rangedPrefab = BuildRangedEnemyPrefab(rangedController, projectilePrefab);

            var waves = BuildWaves();

            BuildScene(playerPrefab, meleePrefab, rangedPrefab, waves);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[111Percent] Sample scene rebuilt. Open Scenes/SampleScene and press Play.");
        }

        // ---------------------------------------------------------------------
        // Directories & layers
        // ---------------------------------------------------------------------
        private static void EnsureDirectories()
        {
            foreach (var dir in new[] { GenRoot, AnimatorsDir, PrefabsDir, WavesDir })
            {
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    string parent = Path.GetDirectoryName(dir).Replace("\\", "/");
                    string leaf = Path.GetFileName(dir);
                    AssetDatabase.CreateFolder(parent, leaf);
                }
            }
        }

        private static void EnsureLayers()
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            SetLayer(layers, GroundLayer, GroundLayerName);
            SetLayer(layers, PlayerLayer, PlayerLayerName);
            SetLayer(layers, EnemyLayer, EnemyLayerName);
            SetLayer(layers, ProjectileLayer, ProjectileLayerName);
            tagManager.ApplyModifiedProperties();
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            var prop = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(prop.stringValue)) prop.stringValue = name;
        }

        // ---------------------------------------------------------------------
        // Animator builders
        // ---------------------------------------------------------------------
        private static AnimatorController BuildHeroAnimator()
        {
            string path = $"{AnimatorsDir}/HeroAnimator.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("VerticalVelocity", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Attack2", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            SetParamDefault(ctrl, "Grounded", true);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var sm = ctrl.layers[0].stateMachine;
            sm.entryPosition = new Vector3(0, 0);
            sm.anyStatePosition = new Vector3(0, 100);
            sm.exitPosition = new Vector3(0, 200);

            var idle = AddState(sm, "Idle", LoadClip(HeroAnimDir, "Idle"), new Vector3(300, -100));
            sm.defaultState = idle;
            var run = AddState(sm, "Running", LoadClip(HeroAnimDir, "Running"), new Vector3(550, -100));
            var jump = AddState(sm, "Jumping", LoadClip(HeroAnimDir, "Jumping"), new Vector3(300, 50));
            var fall = AddState(sm, "Falling", LoadClip(HeroAnimDir, "Falling"), new Vector3(550, 50));
            var punch = AddState(sm, "Attack_Punch", LoadClip(HeroAnimDir, "Attack_Punch"), new Vector3(800, -200));
            var whip = AddState(sm, "Attack_Whip", LoadClip(HeroAnimDir, "Attack_Whip"), new Vector3(800, -50));
            var damaged = AddState(sm, "Damaged", LoadClip(HeroAnimDir, "Damaged"), new Vector3(800, 100));
            var dead = AddState(sm, "Dead", LoadClip(HeroAnimDir, "Dead"), new Vector3(800, 250));

            // Locomotion
            AddTransition(idle, run, true, ("Speed", AnimatorConditionMode.Greater, 0.1f));
            AddTransition(run, idle, true, ("Speed", AnimatorConditionMode.Less, 0.1f));
            AddTransition(idle, jump, true, ("Grounded", AnimatorConditionMode.IfNot, 0f), ("VerticalVelocity", AnimatorConditionMode.Greater, 0.05f));
            AddTransition(run, jump, true, ("Grounded", AnimatorConditionMode.IfNot, 0f), ("VerticalVelocity", AnimatorConditionMode.Greater, 0.05f));
            AddTransition(jump, fall, true, ("VerticalVelocity", AnimatorConditionMode.Less, 0f));
            AddTransition(idle, fall, true, ("Grounded", AnimatorConditionMode.IfNot, 0f), ("VerticalVelocity", AnimatorConditionMode.Less, 0f));
            AddTransition(run, fall, true, ("Grounded", AnimatorConditionMode.IfNot, 0f), ("VerticalVelocity", AnimatorConditionMode.Less, 0f));
            AddTransition(fall, idle, true, ("Grounded", AnimatorConditionMode.If, 0f));
            AddTransition(jump, idle, true, ("Grounded", AnimatorConditionMode.If, 0f));

            // Combat / reactions
            AddAnyStateTransition(sm, punch, ("Attack1", AnimatorConditionMode.If, 0f));
            AddAnyStateTransition(sm, whip, ("Attack2", AnimatorConditionMode.If, 0f));
            AddAnyStateTransition(sm, damaged, ("Hurt", AnimatorConditionMode.If, 0f));
            AddAnyStateTransition(sm, dead, ("Dead", AnimatorConditionMode.If, 0f));

            AddExitTransition(punch, idle, 0.85f);
            AddExitTransition(whip, idle, 0.85f);
            AddExitTransition(damaged, idle, 0.9f);

            return ctrl;
        }

        private static AnimatorController BuildEnemyAnimator(string animDir, string name, bool includeShooting)
        {
            string path = $"{AnimatorsDir}/{name}.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Attack1", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            SetParamDefault(ctrl, "Grounded", true);
            if (includeShooting) ctrl.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            var idle = AddState(sm, "Idle", LoadClip(animDir, "Idle"), new Vector3(300, -100));
            sm.defaultState = idle;
            var run = AddState(sm, "Running", LoadClip(animDir, "Running"), new Vector3(550, -100));
            var attack = AddState(sm, "Attack_Punch", LoadClip(animDir, "Attack_Punch"), new Vector3(800, -200));
            var damaged = AddState(sm, "Damaged", LoadClip(animDir, "Damaged"), new Vector3(800, 0));
            var dead = AddState(sm, "Dead", LoadClip(animDir, "Dead"), new Vector3(800, 150));

            AddTransition(idle, run, true, ("Speed", AnimatorConditionMode.Greater, 0.1f));
            AddTransition(run, idle, true, ("Speed", AnimatorConditionMode.Less, 0.1f));

            AddAnyStateTransition(sm, attack, ("Attack1", AnimatorConditionMode.If, 0f));
            AddAnyStateTransition(sm, damaged, ("Hurt", AnimatorConditionMode.If, 0f));
            AddAnyStateTransition(sm, dead, ("Dead", AnimatorConditionMode.If, 0f));
            AddExitTransition(attack, idle, 0.85f);
            AddExitTransition(damaged, idle, 0.9f);

            if (includeShooting)
            {
                var shoot = AddState(sm, "Shooting", LoadClip(animDir, "Shooting"), new Vector3(800, -350));
                AddAnyStateTransition(sm, shoot, ("Shoot", AnimatorConditionMode.If, 0f));
                AddExitTransition(shoot, idle, 0.85f);
            }

            return ctrl;
        }

        private static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, Vector3 pos)
        {
            var state = sm.AddState(name, pos);
            if (clip != null) state.motion = clip;
            state.writeDefaultValues = false;
            return state;
        }

        private static void AddTransition(AnimatorState from, AnimatorState to, bool hasExitTime,
            params (string param, AnimatorConditionMode mode, float threshold)[] conditions)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.exitTime = 0f;
            t.hasFixedDuration = true;
            t.duration = 0.05f;
            foreach (var c in conditions)
                t.AddCondition(c.mode, c.threshold, c.param);
        }

        private static void AddAnyStateTransition(AnimatorStateMachine sm, AnimatorState to,
            params (string param, AnimatorConditionMode mode, float threshold)[] conditions)
        {
            var t = sm.AddAnyStateTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.05f;
            t.canTransitionToSelf = false;
            foreach (var c in conditions)
                t.AddCondition(c.mode, c.threshold, c.param);
        }

        private static void AddExitTransition(AnimatorState from, AnimatorState to, float exitTime)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.hasFixedDuration = true;
            t.duration = 0.05f;
        }

        private static void SetParamDefault(AnimatorController ctrl, string name, bool value)
        {
            var ps = ctrl.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].name == name) ps[i].defaultBool = value;
            }
            ctrl.parameters = ps;
        }

        private static AnimationClip LoadClip(string dir, string name)
        {
            return AssetDatabase.LoadAssetAtPath<AnimationClip>($"{dir}/{name}.anim");
        }

        // ---------------------------------------------------------------------
        // Prefab builders
        // ---------------------------------------------------------------------
        private static GameObject BuildProjectilePrefab()
        {
            var root = new GameObject("Projectile");
            root.layer = ProjectileLayer;

            var sr = root.AddComponent<SpriteRenderer>();
            sr.sprite = LoadAnySprite(EnemyIdleSprite);
            sr.color = new Color(1f, 0.8f, 0.2f);
            root.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.freezeRotation = true;

            var col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var p = root.AddComponent<Projectile>();
            SetPrivateField(p, "sprite", sr);
            SetPrivateField(p, "hitMask", (LayerMask)((1 << GroundLayer) | (1 << PlayerLayer)));

            string path = $"{PrefabsDir}/Projectile.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildPlayerPrefab(AnimatorController animator)
        {
            var root = new GameObject("Player");
            root.layer = PlayerLayer;

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.5f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = root.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.7f, 1.6f);
            col.direction = CapsuleDirection2D.Vertical;
            col.offset = new Vector2(0f, 0.8f);

            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(root.transform, false);
            var sr = sprite.AddComponent<SpriteRenderer>();
            sr.sprite = LoadAnySprite(HeroIdleSprite);
            sprite.transform.localPosition = new Vector3(0.073f, 1.166f, 0f);
            sprite.transform.localScale = new Vector3(0.192f, 0.192f, 0.192f);

            var anim = sprite.AddComponent<Animator>();
            anim.runtimeAnimatorController = animator;
            anim.applyRootMotion = false;

            var probe = new GameObject("GroundProbe");
            probe.transform.SetParent(root.transform, false);
            probe.transform.localPosition = new Vector3(0f, 0.05f, 0f);

            var ground = root.AddComponent<GroundCheck>();
            SetPrivateField(ground, "probe", probe.transform);
            SetPrivateField(ground, "radius", 0.18f);
            SetPrivateField(ground, "groundMask", (LayerMask)(1 << GroundLayer));

            var hitboxGo = new GameObject("Hitbox");
            hitboxGo.transform.SetParent(sprite.transform, false);
            var hitbox = hitboxGo.AddComponent<Hitbox>();
            SetPrivateField(hitbox, "owner", Faction.Player);
            SetPrivateField(hitbox, "damage", 25f);
            SetPrivateField(hitbox, "size", new Vector2(1.4f, 1.2f));
            SetPrivateField(hitbox, "offset", new Vector2(0.9f, -0.46f));
            SetPrivateField(hitbox, "targetMask", (LayerMask)(1 << EnemyLayer));

            var health = root.AddComponent<Health>();
            SetPrivateField(health, "faction", Faction.Player);
            SetPrivateField(health, "maxHealth", 100f);

            var playerHealth = root.AddComponent<PlayerHealth>();
            SetPrivateField(playerHealth, "animator", anim);

            var combat = root.AddComponent<PlayerCombat>();
            SetPrivateField(combat, "animator", anim);
            SetPrivateField(combat, "hitbox", hitbox);

            var controller = root.AddComponent<PlayerController>();
            SetPrivateField(controller, "groundCheck", ground);
            SetPrivateField(controller, "spriteRoot", sprite.transform);
            SetPrivateField(controller, "animator", anim);
            SetPrivateField(controller, "playerHealth", playerHealth);
            SetPrivateField(controller, "combat", combat);

            // PlayerInput with SendMessages so OnMove / OnJump / OnAttack / OnDodge get called.
            var playerInput = root.AddComponent<PlayerInput>();
            var actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAsset);
            playerInput.actions = actionsAsset;
            playerInput.defaultActionMap = "Player";
            playerInput.notificationBehavior = PlayerNotifications.SendMessages;

            string path = $"{PrefabsDir}/Player.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildMeleeEnemyPrefab(AnimatorController animator)
        {
            var root = new GameObject("MeleeEnemy");
            root.layer = EnemyLayer;

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.5f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = root.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.7f, 1.6f);
            col.direction = CapsuleDirection2D.Vertical;
            col.offset = new Vector2(0f, 0.8f);

            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(root.transform, false);
            var sr = sprite.AddComponent<SpriteRenderer>();
            sr.sprite = LoadAnySprite(EnemyIdleSprite);
            sprite.transform.localPosition = new Vector3(0.073f, 1.166f, 0f);
            sprite.transform.localScale = new Vector3(0.192f, 0.192f, 0.192f);

            var anim = sprite.AddComponent<Animator>();
            anim.runtimeAnimatorController = animator;
            anim.applyRootMotion = false;

            var probe = new GameObject("GroundProbe");
            probe.transform.SetParent(root.transform, false);
            probe.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var ground = root.AddComponent<GroundCheck>();
            SetPrivateField(ground, "probe", probe.transform);
            SetPrivateField(ground, "radius", 0.18f);
            SetPrivateField(ground, "groundMask", (LayerMask)(1 << GroundLayer));

            var hitboxGo = new GameObject("Hitbox");
            hitboxGo.transform.SetParent(sprite.transform, false);
            var hitbox = hitboxGo.AddComponent<Hitbox>();
            SetPrivateField(hitbox, "owner", Faction.Enemy);
            SetPrivateField(hitbox, "damage", 12f);
            SetPrivateField(hitbox, "size", new Vector2(1.2f, 1.1f));
            SetPrivateField(hitbox, "offset", new Vector2(0.8f, -0.46f));
            SetPrivateField(hitbox, "targetMask", (LayerMask)(1 << PlayerLayer));

            var health = root.AddComponent<Health>();
            SetPrivateField(health, "faction", Faction.Enemy);
            SetPrivateField(health, "maxHealth", 40f);

            var enemy = root.AddComponent<MeleeEnemy>();
            SetPrivateField(enemy, "spriteRoot", sprite.transform);
            SetPrivateField(enemy, "animator", anim);
            SetPrivateField(enemy, "groundCheck", ground);
            SetPrivateField(enemy, "hitbox", hitbox);

            string path = $"{PrefabsDir}/MeleeEnemy.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildRangedEnemyPrefab(AnimatorController animator, GameObject projectilePrefab)
        {
            var root = new GameObject("RangedEnemy");
            root.layer = EnemyLayer;

            var rb = root.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.5f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = root.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.7f, 1.6f);
            col.direction = CapsuleDirection2D.Vertical;
            col.offset = new Vector2(0f, 0.8f);

            var sprite = new GameObject("Sprite");
            sprite.transform.SetParent(root.transform, false);
            var sr = sprite.AddComponent<SpriteRenderer>();
            sr.sprite = LoadAnySprite(EnemyIdleSprite);
            sr.color = new Color(0.8f, 0.85f, 1f);
            sprite.transform.localPosition = new Vector3(0.073f, 1.166f, 0f);
            sprite.transform.localScale = new Vector3(0.192f, 0.192f, 0.192f);

            var anim = sprite.AddComponent<Animator>();
            anim.runtimeAnimatorController = animator;
            anim.applyRootMotion = false;

            var probe = new GameObject("GroundProbe");
            probe.transform.SetParent(root.transform, false);
            probe.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var ground = root.AddComponent<GroundCheck>();
            SetPrivateField(ground, "probe", probe.transform);
            SetPrivateField(ground, "radius", 0.18f);
            SetPrivateField(ground, "groundMask", (LayerMask)(1 << GroundLayer));

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(sprite.transform, false);
            muzzle.transform.localPosition = new Vector3(3.81f, -1.09f, 0f);

            var health = root.AddComponent<Health>();
            SetPrivateField(health, "faction", Faction.Enemy);
            SetPrivateField(health, "maxHealth", 30f);

            var enemy = root.AddComponent<RangedEnemy>();
            SetPrivateField(enemy, "spriteRoot", sprite.transform);
            SetPrivateField(enemy, "animator", anim);
            SetPrivateField(enemy, "groundCheck", ground);
            SetPrivateField(enemy, "projectilePrefab", projectilePrefab.GetComponent<Projectile>());
            SetPrivateField(enemy, "muzzle", muzzle.transform);

            string path = $"{PrefabsDir}/RangedEnemy.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------------
        // Wave assets
        // ---------------------------------------------------------------------
        private static List<WaveDefinition> BuildWaves()
        {
            var list = new List<WaveDefinition>();
            list.Add(CreateWave("Wave1", 1.5f, 0.4f,
                (EnemyKind.Melee, 2)));
            list.Add(CreateWave("Wave2", 2f, 0.4f,
                (EnemyKind.Melee, 2),
                (EnemyKind.Ranged, 1)));
            list.Add(CreateWave("Wave3", 2.5f, 0.4f,
                (EnemyKind.Melee, 3),
                (EnemyKind.Ranged, 2)));
            return list;
        }

        private static WaveDefinition CreateWave(string name, float startDelay, float spawnInterval,
            params (EnemyKind kind, int count)[] entries)
        {
            string path = $"{WavesDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<WaveDefinition>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            wave.startDelay = startDelay;
            wave.spawnInterval = spawnInterval;
            foreach (var e in entries)
                wave.entries.Add(new WaveEntry { kind = e.kind, count = e.count });
            AssetDatabase.CreateAsset(wave, path);
            return wave;
        }

        // ---------------------------------------------------------------------
        // Scene
        // ---------------------------------------------------------------------
        private static void BuildScene(GameObject playerPrefab, GameObject meleePrefab, GameObject rangedPrefab,
            List<WaveDefinition> waves)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Wipe existing roots — fresh start.
            foreach (var go in scene.GetRootGameObjects())
                Object.DestroyImmediate(go);

            // Camera.
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0f, 3f, -10f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<HitFeel>();

            // EventSystem for UI.
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // Ground.
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.layer = GroundLayer;
            Object.DestroyImmediate(ground.GetComponent<BoxCollider>());
            Object.DestroyImmediate(ground.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(ground.GetComponent<MeshFilter>());
            ground.AddComponent<BoxCollider2D>();
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 1f);
            var gsr = ground.AddComponent<SpriteRenderer>();
            gsr.sprite = MakeBoxSprite();
            gsr.color = new Color(0.18f, 0.22f, 0.28f);
            gsr.drawMode = SpriteDrawMode.Sliced;
            gsr.size = new Vector2(1f, 1f);

            // Side walls so enemies don't wander off forever.
            BuildWall("WallLeft", new Vector3(-20f, 5f, 0f), new Vector3(1f, 12f, 1f));
            BuildWall("WallRight", new Vector3(20f, 5f, 0f), new Vector3(1f, 12f, 1f));

            // Spawn points.
            var spawnRoot = new GameObject("SpawnPoints").transform;
            var sp1 = new GameObject("Spawn_Left").transform;
            var sp2 = new GameObject("Spawn_Right").transform;
            var sp3 = new GameObject("Spawn_FarRight").transform;
            sp1.SetParent(spawnRoot); sp2.SetParent(spawnRoot); sp3.SetParent(spawnRoot);
            sp1.position = new Vector3(-15f, 1f, 0f);
            sp2.position = new Vector3(15f, 1f, 0f);
            sp3.position = new Vector3(10f, 1f, 0f);

            // Player.
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.position = new Vector3(0f, 1f, 0f);

            // GameManager.
            var gmGo = new GameObject("GameManager");
            gmGo.AddComponent<GameManager>();

            // Spawner.
            var spawnerGo = new GameObject("WaveSpawner");
            var spawner = spawnerGo.AddComponent<WaveSpawner>();
            SetPrivateField(spawner, "meleePrefab", meleePrefab.GetComponent<EnemyBase>());
            SetPrivateField(spawner, "rangedPrefab", rangedPrefab.GetComponent<EnemyBase>());
            SetPrivateField(spawner, "spawnPoints", new Transform[] { sp1, sp2, sp3 });
            SetPrivateField(spawner, "waves", waves);

            // HUD.
            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            var healthGo = new GameObject("HealthBar");
            healthGo.transform.SetParent(canvasGo.transform, false);
            var slider = healthGo.AddComponent<Slider>();
            var sliderRect = healthGo.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 1f);
            sliderRect.anchorMax = new Vector2(0f, 1f);
            sliderRect.pivot = new Vector2(0f, 1f);
            sliderRect.anchoredPosition = new Vector2(20f, -20f);
            sliderRect.sizeDelta = new Vector2(260f, 22f);
            BuildSliderVisual(slider);

            var waveLabel = BuildHUDText("WaveLabel", canvasGo.transform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), 320f, 26f, TextAnchor.MiddleCenter);
            var statusLabel = BuildHUDText("StatusLabel", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), 500f, 40f, TextAnchor.MiddleCenter);
            statusLabel.fontSize = 28;

            var hud = canvasGo.AddComponent<HUD>();
            SetPrivateField(hud, "healthBar", slider);
            SetPrivateField(hud, "waveLabel", waveLabel);
            SetPrivateField(hud, "statusLabel", statusLabel);
            SetPrivateField(hud, "spawner", spawner);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void BuildWall(string name, Vector3 pos, Vector3 scale)
        {
            var wall = new GameObject(name);
            wall.layer = GroundLayer;
            wall.transform.position = pos;
            wall.transform.localScale = scale;
            wall.AddComponent<BoxCollider2D>();
        }

        private static Text BuildHUDText(string name, Transform parent, Vector2 anchor, Vector2 anchored,
            float width, float height, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = align;
            text.color = Color.white;
            text.fontSize = 20;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = new Vector2(width, height);
            return text;
        }

        private static void BuildSliderVisual(Slider slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            var bg = new GameObject("Background");
            bg.transform.SetParent(slider.transform, false);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.5f);
            Stretch(bg.GetComponent<RectTransform>());

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(slider.transform, false);
            var fillAreaRt = fillArea.AddComponent<RectTransform>();
            Stretch(fillAreaRt);
            fillAreaRt.offsetMin = new Vector2(2f, 2f);
            fillAreaRt.offsetMax = new Vector2(-2f, -2f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.9f, 0.2f, 0.25f);
            var fillRt = fill.GetComponent<RectTransform>();
            Stretch(fillRt);

            slider.fillRect = fillRt;
            slider.targetGraphic = fillImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = false;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite MakeBoxSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite LoadAnySprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ---------------------------------------------------------------------
        // Reflection helper — Inspector-private fields stay private but the
        // builder still wires them up.
        // ---------------------------------------------------------------------
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var f = type.GetField(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic);
                if (f != null) { f.SetValue(target, value); return; }
                type = type.BaseType;
            }
            Debug.LogWarning($"[SampleSceneBuilder] Could not find field '{fieldName}' on {target.GetType().Name}");
        }
    }
}
