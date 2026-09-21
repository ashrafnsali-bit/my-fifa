using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Football.Core;
using Football.Data;
using Football.PhysicsEngine;
using Football.Locomotion;
using Football.Tactics;
using Football.Engine;
using Football.Audio;
using Football.Presentation;
using Football.Procedural;
using Football.Modes;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MatchBootstrapper : MonoBehaviour
{
    [Header("Bootstrapper Settings")]
    public bool autoStartOnPlay = true;

    private static MatchBootstrapper instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitializeOnPlay()
    {
        if (instance == null && FindFirstObjectByType<MatchBootstrapper>() == null)
        {
            var bootstrapperObj = new GameObject("FIFA_MatchBootstrapper");
            instance = bootstrapperObj.AddComponent<MatchBootstrapper>();
        }
    }

#if UNITY_EDITOR
    [MenuItem("FIFA/Setup Match In Current Scene")]
    public static void EditorSetupMatch()
    {
        var existing = FindFirstObjectByType<MatchBootstrapper>();
        if (existing == null)
        {
            var obj = new GameObject("FIFA_MatchBootstrapper");
            existing = obj.AddComponent<MatchBootstrapper>();
        }
        existing.BuildCompleteMatchScene();

        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        if (scene.isLoaded)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
    }

    [InitializeOnLoad]
    public static class MatchBootstrapperEditorInitializer
    {
        static MatchBootstrapperEditorInitializer()
        {
            EditorApplication.delayCall += CheckAndPopulateScene;
        }

        private static void CheckAndPopulateScene()
        {
            if (!EditorApplication.isPlaying && GameObject.Find("StadiumPitch") == null)
            {
                Debug.Log("<color=cyan>[FIFA World Cup]</color> Auto-generating football stadium, pitch, players, ball, and camera in scene...");
                EditorSetupMatch();
            }
        }
    }
#endif

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (autoStartOnPlay)
        {
            BuildCompleteMatchScene();
        }
    }

    [ContextMenu("Build Complete Match Scene")]
    public void BuildCompleteMatchScene()
    {
        Debug.Log("<color=cyan><b>[FIFA World Cup Engine]</b> Initializing Full Match Scene...</color>");

        CleanUpPlaceholderSceneObjects();
        SetupLightingAndAtmosphere();

        // 1. Build authentic pitch, markings, and goal frames
        BuildFullStadiumPitch();

        // 2. Setup Size 5 Football
        var ball = SetupMatchBall();

        // 3. Setup Broadcast TV Camera
        SetupBroadcastCamera(ball.transform);

        // 4. Build TV Broadcast HUD & 2D Minimap Radar
        SetupBroadcastUI();

        // 5. Spawn Team 1 (Argentina - Player 1: Human Controlled)
        var team1Players = SpawnTeam(1, "Argentina", new Color(0.45f, 0.72f, 1.0f), Color.white, new Color(0.95f, 0.9f, 0.1f), FormationType.Formation_4_3_3, true);

        // 6. Spawn Team 2 (France - Dynamic AI Opponent / P2 Local)
        var team2Players = SpawnTeam(2, "France", new Color(0.88f, 0.12f, 0.16f), new Color(0.06f, 0.10f, 0.24f), new Color(0.95f, 0.45f, 0.10f), FormationType.Formation_4_3_3, false);

        // 7. Initialize Match Engine and Audio
        SetupMatchSystems(team1Players, team2Players, ball);

        Debug.Log("<color=green><b>[FIFA World Cup Engine]</b> Match Scene Initialized Successfully!</color>");
    }

    private void CleanUpPlaceholderSceneObjects()
    {
        // 1. Clean up old scene template placeholder primitives
        string[] placeholders = { "Plane", "Cube", "Cube (1)", "Ball" };
        foreach (var name in placeholders)
        {
            var obj = GameObject.Find(name);
            if (obj != null && obj != gameObject)
            {
                DestroyImmediate(obj);
            }
        }

        // 2. Clean up any existing teams, players, markers, pitch, ball, UI, or systems to ensure 0 duplicates
        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in rootObjects)
        {
            if (root == null || root == gameObject) continue;
            string n = root.name;
            if (n.StartsWith("Team_") ||
                n.StartsWith("PlayerOverheadMarker") ||
                n.StartsWith("FIFA_GameSystems") ||
                n.StartsWith("StadiumPitch") ||
                n.StartsWith("MatchBall") ||
                n.StartsWith("BroadcastCanvas") ||
                n.StartsWith("PlayerBlobShadow") ||
                n.StartsWith("WorldCupStadium") ||
                n == "Cube" || n == "Cube (1)" || n == "Plane")
            {
                DestroyImmediate(root);
            }
        }

        // 3. Clean up any legacy Goal components or GameManagers to prevent conflicting/duplicate score counting
        var legacyGoals = FindObjectsByType<Goal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var g in legacyGoals)
        {
            if (g != null && g.gameObject != gameObject) DestroyImmediate(g.gameObject);
        }

        var legacyManagers = FindObjectsByType<GameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var gm in legacyManagers)
        {
            if (gm != null && gm.gameObject != gameObject) DestroyImmediate(gm.gameObject);
        }
    }

    private void SetupLightingAndAtmosphere()
    {
        // 1. Primary Stadium Key Floodlight
        var lightObj = GameObject.Find("Directional Light") ?? GameObject.Find("Stadium_KeyFloodlight");
        Light dirLight = null;
        if (lightObj != null) dirLight = lightObj.GetComponent<Light>();
        if (dirLight == null)
        {
            var newLightObj = new GameObject("Stadium_KeyFloodlight");
            dirLight = newLightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
        }

        dirLight.name = "Stadium_KeyFloodlight";
        dirLight.transform.rotation = Quaternion.Euler(70f, -22f, 0f); // High-angle stadium floodlight with soft pitch shadows
        dirLight.intensity = 1.65f;
        dirLight.color = new Color(1.0f, 0.98f, 0.94f);
        dirLight.shadows = LightShadows.Soft;
        dirLight.shadowStrength = 0.45f;
        dirLight.shadowBias = 0.08f;
        dirLight.shadowNormalBias = 0.02f;

        // 2. Secondary Opposing Stadium Rim Light (creates crisp edge highlights on players' shoulders, hair, and kits)
        var rimObj = GameObject.Find("Stadium_RimLight");
        Light rimLight = null;
        if (rimObj != null) rimLight = rimObj.GetComponent<Light>();
        if (rimLight == null)
        {
            var newRimObj = new GameObject("Stadium_RimLight");
            rimLight = newRimObj.AddComponent<Light>();
            rimLight.type = LightType.Directional;
        }

        rimLight.transform.rotation = Quaternion.Euler(52f, 158f, 0f); // Opposing rear-high angle
        rimLight.intensity = 0.85f;
        rimLight.color = new Color(0.86f, 0.92f, 1.0f); // Crisp stadium cool floodlight rim
        rimLight.shadows = LightShadows.None;

        // 3. Trilight Stadium Ambient Lighting
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.56f, 0.64f, 0.74f);     // Sky fill
        RenderSettings.ambientEquatorColor = new Color(0.46f, 0.52f, 0.60f); // Stands horizon
        RenderSettings.ambientGroundColor = new Color(0.25f, 0.46f, 0.25f);  // Warm grass bounce

        // 4. Global Post-Processing Volume with ACES Tonemapping, Bloom, and Color Grading
        var volume = FindFirstObjectByType<UnityEngine.Rendering.Volume>();
        if (volume == null)
        {
            var volObj = new GameObject("Global_PostProcess_Volume");
            volume = volObj.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 1.0f;
        }

        // Programmatic profile setup so post-processing works identically in both Editor and WebGL build!
        if (volume.sharedProfile == null && volume.profile == null)
        {
            volume.profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
        }

        var activeProfile = volume.profile != null ? volume.profile : volume.sharedProfile;
        if (activeProfile != null)
        {
            if (!activeProfile.Has<UnityEngine.Rendering.Universal.Tonemapping>())
            {
                var tonemap = activeProfile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
                tonemap.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
            }

            if (!activeProfile.Has<UnityEngine.Rendering.Universal.Bloom>())
            {
                var bloom = activeProfile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
                bloom.threshold.Override(0.90f);
                bloom.intensity.Override(0.50f);
                bloom.scatter.Override(0.70f);
            }

            if (!activeProfile.Has<UnityEngine.Rendering.Universal.ColorAdjustments>())
            {
                var colorAdj = activeProfile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
                colorAdj.postExposure.Override(0.12f);
                colorAdj.contrast.Override(14f);
                colorAdj.saturation.Override(16f);
            }

            if (!activeProfile.Has<UnityEngine.Rendering.Universal.Vignette>())
            {
                var vignette = activeProfile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
                vignette.intensity.Override(0.20f);
                vignette.smoothness.Override(0.30f);
            }
        }
    }

    private void BuildFullStadiumPitch()
    {
        var existingPitch = GameObject.Find("StadiumPitch");
        if (existingPitch != null) DestroyImmediate(existingPitch);

        GameObject pitchObj = new GameObject("StadiumPitch");
        var builder = pitchObj.AddComponent<ProceduralPitchBuilder>();

        var turfNormal = ProceduralTextureFactory.CreateTurfNormalMap();
        Vector2 stripeTiling = new Vector2(8f, 2f);

        // High-definition procedural turf with blade normal maps and specular sheen
        Material lightTurf = CreateLitMaterial(new Color(0.20f, 0.56f, 0.24f), 0.26f, turfNormal);
        Material darkTurf = CreateLitMaterial(new Color(0.15f, 0.46f, 0.18f), 0.26f, turfNormal);

        var grassTex1 = ProceduralTextureFactory.CreateTurfGrassTexture(new Color(0.22f, 0.58f, 0.24f), new Color(0.26f, 0.65f, 0.28f));
        var grassTex2 = ProceduralTextureFactory.CreateTurfGrassTexture(new Color(0.16f, 0.48f, 0.18f), new Color(0.19f, 0.53f, 0.21f));

        if (lightTurf.HasProperty("_BaseMap")) { lightTurf.SetTexture("_BaseMap", grassTex1); lightTurf.SetTextureScale("_BaseMap", stripeTiling); }
        else if (lightTurf.HasProperty("_MainTex")) { lightTurf.SetTexture("_MainTex", grassTex1); lightTurf.SetTextureScale("_MainTex", stripeTiling); }
        if (lightTurf.HasProperty("_BumpMap")) lightTurf.SetTextureScale("_BumpMap", stripeTiling);

        if (darkTurf.HasProperty("_BaseMap")) { darkTurf.SetTexture("_BaseMap", grassTex2); darkTurf.SetTextureScale("_BaseMap", stripeTiling); }
        else if (darkTurf.HasProperty("_MainTex")) { darkTurf.SetTexture("_MainTex", grassTex2); darkTurf.SetTextureScale("_MainTex", stripeTiling); }
        if (darkTurf.HasProperty("_BumpMap")) darkTurf.SetTextureScale("_BumpMap", stripeTiling);

        builder.turfMaterialLight = lightTurf;
        builder.turfMaterialDark = darkTurf;
        builder.lineMaterial = CreateLitMaterial(new Color(0.98f, 0.98f, 0.98f), 0.12f);
        builder.goalFrameMaterial = CreateLitMaterial(new Color(0.96f, 0.96f, 0.96f), 0.88f, null, 0.25f);

        builder.BuildCompletePitch();
    }

    private FootballBall SetupMatchBall()
    {
        var existingBall = GameObject.Find("MatchBall");
        if (existingBall != null) DestroyImmediate(existingBall);

        GameObject ballObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballObj.name = "MatchBall";
        ballObj.transform.position = new Vector3(0f, 0.11f, 0f);
        ballObj.transform.localScale = Vector3.one * (0.11f * 2.0f); // 22cm regulation diameter

        // Official World Cup "Al Rihla" procedural texture with normal-mapped aerodynamic seams
        var ballNormal = ProceduralTextureFactory.CreateBallNormalMap();
        Material ballMat = CreateLitMaterial(Color.white, 0.92f, ballNormal);
        var ballTex = ProceduralTextureFactory.CreateAlRihlaBallTexture();
        if (ballMat.HasProperty("_BaseMap")) ballMat.SetTexture("_BaseMap", ballTex);
        else if (ballMat.HasProperty("_MainTex")) ballMat.SetTexture("_MainTex", ballTex);

        var mr = ballObj.GetComponent<MeshRenderer>();
        mr.sharedMaterial = ballMat;

        var ballScript = ballObj.AddComponent<FootballBall>();
        return ballScript;
    }

    private void SetupBroadcastCamera(Transform ballTarget)
    {
        var camObj = Camera.main != null ? Camera.main.gameObject : GameObject.Find("Main Camera");
        if (camObj == null)
        {
            camObj = new GameObject("Main Camera");
            camObj.AddComponent<Camera>();
        }

        var cam = camObj.GetComponent<Camera>();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.20f); // Twilight stadium dusk sky
        }

        var rig = camObj.GetComponent<BroadcastCameraRig>();
        if (rig == null) rig = camObj.AddComponent<BroadcastCameraRig>();

        // Iconic Sideline Broadcast Perspective (locks ball in screen center, strictly horizontal touchlines)
        rig.lockBallInCenter = true;
        rig.distanceToBall = 22.0f;
        rig.sidelineDistance = 22.0f;
        rig.cameraHeight = 14.5f;
        rig.baseFieldOfView = 34f;
        rig.maxFieldOfView = 40f;
        rig.smoothTime = 0.05f;
        rig.SnapToBall();
    }

    private List<FootballPlayerLocomotion> SpawnTeam(int teamId, string teamName, Color primaryColor, Color secondaryColor, Color gkColor, FormationType formation, bool isHumanTeam)
    {
        GameObject teamRoot = new GameObject($"Team_{teamId}_{teamName}");
        var slots = FormationData.GetFormationSlots(formation);
        var playerList = new List<FootballPlayerLocomotion>();
        var aiPlayerList = new List<FootballAIPlayer>();

        TeamPlayerSwitcher switcher = null;
        if (isHumanTeam)
        {
            switcher = teamRoot.AddComponent<TeamPlayerSwitcher>();
            switcher.humanTeamId = teamId;
        }

        // High-definition team national kit materials with fabric normal maps
        var fabricNormal = ProceduralTextureFactory.CreateFabricNormalMap();
        Material outfieldJerseyMat = CreateLitMaterial(Color.white, 0.25f, fabricNormal);
        Texture2D kitTex = (teamId == 1) ? ProceduralTextureFactory.CreateArgentinaKitTexture() : ProceduralTextureFactory.CreateFranceKitTexture();
        if (outfieldJerseyMat.HasProperty("_BaseMap")) outfieldJerseyMat.SetTexture("_BaseMap", kitTex);
        else if (outfieldJerseyMat.HasProperty("_MainTex")) outfieldJerseyMat.SetTexture("_MainTex", kitTex);

        // Shorts & Socks
        Material outfieldShortsMat = CreateLitMaterial(teamId == 1 ? new Color(0.12f, 0.12f, 0.14f) : new Color(0.06f, 0.10f, 0.24f), 0.22f, fabricNormal);
        Material outfieldSocksMat = CreateLitMaterial(teamId == 1 ? new Color(0.96f, 0.96f, 0.96f) : new Color(0.88f, 0.12f, 0.16f), 0.20f, fabricNormal);

        // Goalkeeper kit materials
        Material gkJerseyMat = CreateLitMaterial(Color.white, 0.25f, fabricNormal);
        Texture2D gkTex = ProceduralTextureFactory.CreateGoalkeeperKitTexture(gkColor);
        if (gkJerseyMat.HasProperty("_BaseMap")) gkJerseyMat.SetTexture("_BaseMap", gkTex);
        else if (gkJerseyMat.HasProperty("_MainTex")) gkJerseyMat.SetTexture("_MainTex", gkTex);

        Material gkShortsMat = CreateLitMaterial(gkColor, 0.22f, fabricNormal);
        Material gkSocksMat = CreateLitMaterial(gkColor, 0.20f, fabricNormal);

        PlayerRuntimeState defaultHumanPlayer = null;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            bool isGK = slot.position == PlayerPosition.GK;
            Vector3 worldPos = FormationData.GetWorldPosition(slot, teamId);
            worldPos.y = 0.0f; // Firmly grounded on pitch surface

            GameObject playerObj = new GameObject($"Player_{teamId}_{slot.roleName}");
            playerObj.transform.SetParent(teamRoot.transform, false);
            playerObj.transform.position = worldPos;

            // Look towards opponent goal
            Vector3 targetGoal = PitchConstants.GetTargetGoalCenter(teamId);
            playerObj.transform.rotation = Quaternion.LookRotation((targetGoal - worldPos).normalized, Vector3.up);

            // 1. Collider & Rigidbody
            var col = playerObj.AddComponent<CapsuleCollider>();
            col.height = 1.82f;
            col.radius = 0.38f;
            col.center = new Vector3(0f, 0.91f, 0f);

            var rb = playerObj.AddComponent<Rigidbody>();
            rb.mass = 75f;
            rb.useGravity = false; // Prevents sinking/floating, ground physics is locked to pitch Y
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;

            // Star rosters
            string[] argNames = { "E. Martinez", "M. Acuna", "N. Otamendi", "C. Romero", "N. Molina", "E. Fernandez", "A. Mac Allister", "R. De Paul", "A. Di Maria", "L. Messi", "J. Alvarez" };
            int[] argNumbers = { 23, 8, 19, 13, 26, 24, 20, 7, 11, 10, 9 };

            string[] fraNames = { "H. Lloris", "T. Hernandez", "D. Upamecano", "R. Varane", "J. Kounde", "A. Tchouameni", "A. Rabiot", "A. Griezmann", "K. Mbappe", "O. Giroud", "O. Dembele" };
            int[] fraNumbers = { 1, 22, 18, 4, 5, 8, 14, 7, 10, 9, 11 };

            int jerseyNum = (teamId == 1 && i < argNumbers.Length) ? argNumbers[i] : (teamId == 2 && i < fraNumbers.Length ? fraNumbers[i] : (i + 1));
            string playerName = (teamId == 1 && i < argNames.Length) ? argNames[i] : (teamId == 2 && i < fraNames.Length ? fraNames[i] : $"Player {i + 1}");

            // 2. Runtime State & Attributes
            var runtime = playerObj.AddComponent<PlayerRuntimeState>();
            runtime.teamId = teamId;
            runtime.jerseyNumber = jerseyNum;
            runtime.attributes = RosterGenerator.GeneratePlayer(teamId == 1 ? "CONMEBOL" : "UEFA", slot.position, jerseyNum);
            if (runtime.attributes != null)
            {
                runtime.attributes.playerName = playerName;
                runtime.attributes.jerseyNumber = jerseyNum;
            }

            // 3. Locomotion & Actions
            var locomotion = playerObj.AddComponent<FootballPlayerLocomotion>();
            var actions = playerObj.AddComponent<FootballPlayerActions>();

            // 4. Athletic 3D Visual Mesh with anatomical proportions, star hairstyle, boots, soleplate & studs
            Material jersey = isGK ? gkJerseyMat : outfieldJerseyMat;
            Material shorts = isGK ? gkShortsMat : outfieldShortsMat;
            Material socks = isGK ? gkSocksMat : outfieldSocksMat;

            var profile = GetPlayerVisualProfile(teamId, jerseyNum, playerName);
            BuildPlayerVisuals(playerObj.transform, jersey, shorts, socks, profile, isGK, jerseyNum, teamId);

            playerList.Add(locomotion);

            // 5. Controls: Setup for AI and Dynamic Human Switching
            var ai = playerObj.AddComponent<FootballAIPlayer>();
            ai.formationSlotIndex = i;
            aiPlayerList.Add(ai);

            if (isHumanTeam)
            {
                var inputHandler = playerObj.AddComponent<FootballInputHandler>();
                inputHandler.isHumanControlled = false;
                switcher.RegisterPlayer(runtime);

                if (slot.position == PlayerPosition.ST || defaultHumanPlayer == null)
                {
                    defaultHumanPlayer = runtime;
                }
            }
        }

        if (isHumanTeam && defaultHumanPlayer != null)
        {
            string startName = (teamId == 1) ? "10 MESSI" : "10 MBAPPE";
            Color markerCol = (teamId == 1) ? new Color(0.0f, 1.0f, 0.45f) : new Color(1.0f, 0.35f, 0.1f);
            var marker = CreatePlayerIndicator(defaultHumanPlayer.transform, startName, markerCol);
            switcher.overheadMarker = marker;
            switcher.SwitchToPlayer(defaultHumanPlayer);
        }

        // Attach TeamTacticsController
        var tactics = teamRoot.AddComponent<TeamTacticsController>();
        tactics.teamId = teamId;
        tactics.formation = formation;
        tactics.mentality = TeamMentality.Attacking;
        tactics.difficultySettings = AIDifficultySettings.GetPreset(DifficultyLevel.WorldClass);
        tactics.teamPlayers = aiPlayerList;

        foreach (var ai in aiPlayerList)
        {
            ai.tacticsController = tactics;
        }

        return playerList;
    }

    public enum HairStyle
    {
        ShortBuzz,
        TexturedFade,
        Pompadour,
        BlondeCrop,
        SlickedBack,
        ClassicSidePart,
        CurlyAfroFade
    }

    public struct PlayerVisualProfile
    {
        public string playerName;
        public Color skinColor;
        public Color hairColor;
        public HairStyle hairStyle;
        public bool hasBeard;
        public Color beardColor;
        public bool isCaptain;
        public bool hasWristTape;
        public Color bootBaseColor;
        public Color bootAccentColor;
        public Color bootStudColor;
        public float bodyHeight;
        public float shoulderWidth;
    }

    private PlayerVisualProfile GetPlayerVisualProfile(int teamId, int jerseyNum, string playerName)
    {
        var profile = new PlayerVisualProfile
        {
            playerName = playerName,
            skinColor = new Color(0.88f, 0.72f, 0.58f),
            hairColor = new Color(0.12f, 0.09f, 0.07f),
            hairStyle = HairStyle.TexturedFade,
            hasBeard = false,
            beardColor = new Color(0.12f, 0.09f, 0.07f),
            isCaptain = false,
            hasWristTape = false,
            bootBaseColor = new Color(0.12f, 0.12f, 0.14f),
            bootAccentColor = new Color(0.95f, 0.25f, 0.25f),
            bootStudColor = new Color(0.90f, 0.90f, 0.95f),
            bodyHeight = 1.0f,
            shoulderWidth = 1.0f
        };

        if (teamId == 1) // Argentina
        {
            switch (jerseyNum)
            {
                case 10: // Lionel Messi (Captain, GOAT)
                    profile.skinColor = new Color(0.92f, 0.76f, 0.63f);
                    profile.hairColor = new Color(0.20f, 0.14f, 0.10f);
                    profile.hairStyle = HairStyle.TexturedFade;
                    profile.hasBeard = true;
                    profile.beardColor = new Color(0.28f, 0.18f, 0.12f);
                    profile.isCaptain = true;
                    profile.hasWristTape = true;
                    profile.bootBaseColor = new Color(0.96f, 0.82f, 0.22f); // Adidas X Speedportal Gold
                    profile.bootAccentColor = new Color(0.20f, 0.75f, 0.95f); // Sky blue trim
                    profile.bootStudColor = new Color(1.0f, 0.85f, 0.20f);
                    profile.bodyHeight = 0.97f;
                    profile.shoulderWidth = 0.98f;
                    break;

                case 7: // Rodrigo De Paul (Midfield Engine)
                    profile.skinColor = new Color(0.90f, 0.74f, 0.61f);
                    profile.hairColor = new Color(0.94f, 0.90f, 0.78f); // Bleached platinum blonde
                    profile.hairStyle = HairStyle.BlondeCrop;
                    profile.hasBeard = true;
                    profile.beardColor = new Color(0.18f, 0.13f, 0.10f);
                    profile.hasWristTape = true;
                    profile.bootBaseColor = new Color(0.95f, 0.15f, 0.15f); // Crimson red boots
                    profile.bootAccentColor = Color.white;
                    profile.bodyHeight = 1.01f;
                    profile.shoulderWidth = 1.04f;
                    break;

                case 23: // Emiliano Martinez (Dibu - GK)
                    profile.skinColor = new Color(0.91f, 0.75f, 0.62f);
                    profile.hairColor = new Color(0.16f, 0.11f, 0.08f);
                    profile.hairStyle = HairStyle.ShortBuzz;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.15f, 0.85f, 0.25f); // Volt green
                    profile.bootAccentColor = Color.black;
                    profile.bodyHeight = 1.06f; // Tall keeper
                    profile.shoulderWidth = 1.08f;
                    break;

                case 11: // Angel Di Maria
                    profile.skinColor = new Color(0.89f, 0.73f, 0.60f);
                    profile.hairColor = new Color(0.10f, 0.08f, 0.06f);
                    profile.hairStyle = HairStyle.SlickedBack;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.98f, 0.40f, 0.10f); // Blaze orange
                    profile.bootAccentColor = Color.white;
                    profile.bodyHeight = 1.02f;
                    profile.shoulderWidth = 0.94f;
                    break;

                case 9: // Julian Alvarez
                    profile.skinColor = new Color(0.91f, 0.75f, 0.62f);
                    profile.hairColor = new Color(0.12f, 0.08f, 0.06f);
                    profile.hairStyle = HairStyle.TexturedFade;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.20f, 0.80f, 0.95f);
                    profile.bootAccentColor = Color.white;
                    profile.bodyHeight = 0.99f;
                    break;

                case 19: // Nicolas Otamendi
                    profile.skinColor = new Color(0.87f, 0.70f, 0.57f);
                    profile.hairColor = new Color(0.12f, 0.08f, 0.06f);
                    profile.hairStyle = HairStyle.ClassicSidePart;
                    profile.hasBeard = true;
                    profile.beardColor = new Color(0.14f, 0.10f, 0.07f);
                    profile.bootBaseColor = new Color(0.15f, 0.15f, 0.18f);
                    profile.bootAccentColor = new Color(0.9f, 0.7f, 0.2f);
                    profile.bodyHeight = 1.02f;
                    profile.shoulderWidth = 1.06f;
                    break;

                case 13: // Cristian Romero
                    profile.skinColor = new Color(0.88f, 0.71f, 0.58f);
                    profile.hairColor = new Color(0.10f, 0.07f, 0.05f);
                    profile.hairStyle = HairStyle.TexturedFade;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.85f, 0.15f, 0.25f);
                    profile.bootAccentColor = Color.white;
                    profile.bodyHeight = 1.03f;
                    profile.shoulderWidth = 1.05f;
                    break;

                default:
                    profile.bootBaseColor = new Color(0.15f, 0.15f, 0.18f);
                    profile.bootAccentColor = new Color(0.2f, 0.75f, 0.95f);
                    break;
            }
        }
        else // France
        {
            switch (jerseyNum)
            {
                case 10: // Kylian Mbappe (Captain / Speed Phenomenon)
                    profile.skinColor = new Color(0.44f, 0.30f, 0.20f); // Deep rich complexion
                    profile.hairColor = new Color(0.05f, 0.05f, 0.05f);
                    profile.hairStyle = HairStyle.ShortBuzz;
                    profile.hasBeard = false;
                    profile.hasWristTape = true;
                    profile.bootBaseColor = new Color(0.92f, 0.12f, 0.52f); // Nike Mercurial Hot Pink
                    profile.bootAccentColor = new Color(0.10f, 0.85f, 0.75f);
                    profile.bootStudColor = new Color(0.95f, 0.15f, 0.55f);
                    profile.bodyHeight = 1.00f;
                    profile.shoulderWidth = 1.04f;
                    break;

                case 1: // Hugo Lloris (Captain & Veteran GK)
                    profile.skinColor = new Color(0.91f, 0.75f, 0.62f);
                    profile.hairColor = new Color(0.14f, 0.10f, 0.07f);
                    profile.hairStyle = HairStyle.ClassicSidePart;
                    profile.hasBeard = true;
                    profile.beardColor = new Color(0.18f, 0.14f, 0.10f);
                    profile.isCaptain = true;
                    profile.bootBaseColor = new Color(0.95f, 0.95f, 0.98f);
                    profile.bootAccentColor = new Color(0.1f, 0.3f, 0.8f);
                    profile.bodyHeight = 1.04f;
                    profile.shoulderWidth = 1.04f;
                    break;

                case 9: // Olivier Giroud (Target Man)
                    profile.skinColor = new Color(0.92f, 0.76f, 0.63f);
                    profile.hairColor = new Color(0.18f, 0.13f, 0.09f);
                    profile.hairStyle = HairStyle.Pompadour;
                    profile.hasBeard = true;
                    profile.beardColor = new Color(0.20f, 0.15f, 0.11f);
                    profile.bootBaseColor = new Color(0.15f, 0.15f, 0.20f);
                    profile.bootAccentColor = new Color(0.95f, 0.25f, 0.20f);
                    profile.bodyHeight = 1.06f; // Tall striker
                    profile.shoulderWidth = 1.08f;
                    break;

                case 7: // Antoine Griezmann
                    profile.skinColor = new Color(0.93f, 0.78f, 0.65f);
                    profile.hairColor = new Color(0.95f, 0.85f, 0.60f); // Bright blonde
                    profile.hairStyle = HairStyle.BlondeCrop;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.95f, 0.85f, 0.10f); // Neon yellow
                    profile.bootAccentColor = new Color(0.95f, 0.20f, 0.45f);
                    profile.bodyHeight = 0.98f;
                    profile.shoulderWidth = 0.98f;
                    break;

                case 4: // Raphael Varane
                    profile.skinColor = new Color(0.55f, 0.40f, 0.28f);
                    profile.hairColor = new Color(0.08f, 0.08f, 0.08f);
                    profile.hairStyle = HairStyle.ShortBuzz;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.15f, 0.15f, 0.18f);
                    profile.bootAccentColor = new Color(0.2f, 0.8f, 0.3f);
                    profile.bodyHeight = 1.05f;
                    profile.shoulderWidth = 1.05f;
                    break;

                case 8: // Aurelien Tchouameni
                    profile.skinColor = new Color(0.38f, 0.25f, 0.16f);
                    profile.hairColor = new Color(0.05f, 0.05f, 0.05f);
                    profile.hairStyle = HairStyle.ShortBuzz;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.10f, 0.50f, 0.95f);
                    profile.bootAccentColor = Color.white;
                    profile.bodyHeight = 1.03f;
                    profile.shoulderWidth = 1.06f;
                    break;

                case 18: // Dayot Upamecano
                    profile.skinColor = new Color(0.32f, 0.20f, 0.14f);
                    profile.hairColor = new Color(0.05f, 0.05f, 0.05f);
                    profile.hairStyle = HairStyle.CurlyAfroFade;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.92f, 0.15f, 0.20f);
                    profile.bootAccentColor = Color.white;
                    profile.bodyHeight = 1.04f;
                    profile.shoulderWidth = 1.08f;
                    break;

                case 11: // Ousmane Dembele
                    profile.skinColor = new Color(0.36f, 0.24f, 0.16f);
                    profile.hairColor = new Color(0.05f, 0.05f, 0.05f);
                    profile.hairStyle = HairStyle.ShortBuzz;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.15f, 0.85f, 0.30f); // Neon green
                    profile.bootAccentColor = Color.black;
                    profile.bodyHeight = 1.00f;
                    profile.shoulderWidth = 0.96f;
                    break;

                case 22: // Theo Hernandez
                    profile.skinColor = new Color(0.90f, 0.74f, 0.61f);
                    profile.hairColor = new Color(0.15f, 0.10f, 0.08f);
                    profile.hairStyle = HairStyle.TexturedFade;
                    profile.hasBeard = false;
                    profile.bootBaseColor = new Color(0.95f, 0.45f, 0.10f);
                    profile.bootAccentColor = Color.black;
                    profile.bodyHeight = 1.02f;
                    profile.shoulderWidth = 1.04f;
                    break;

                default:
                    profile.bootBaseColor = new Color(0.15f, 0.15f, 0.18f);
                    profile.bootAccentColor = new Color(0.9f, 0.2f, 0.2f);
                    break;
            }
        }

        return profile;
    }

    private struct PlayerLegJoints
    {
        public Transform hip;
        public Transform knee;
        public Transform ankle;
    }

    private void BuildPlayerVisuals(Transform parent, Material kitMat, Material shortsMat, Material socksMat, PlayerVisualProfile profile, bool isGK, int jerseyNum = 0, int teamId = 1)
    {
        // 1. Materials for individual profile
        Material skinMat = CreateLitMaterial(profile.skinColor, 0.35f);

        Material hairMat = CreateLitMaterial(profile.hairColor, 0.20f);
        Texture2D hairFadeTex = ProceduralTextureFactory.CreateHairFadeTexture(profile.hairColor);
        if (hairMat.HasProperty("_BaseMap")) hairMat.SetTexture("_BaseMap", hairFadeTex);
        else if (hairMat.HasProperty("_MainTex")) hairMat.SetTexture("_MainTex", hairFadeTex);

        Material bootsMat = CreateLitMaterial(profile.bootBaseColor, 0.70f, null, 0.35f);
        Texture2D bootTex = ProceduralTextureFactory.CreateBootTexture(profile.bootBaseColor, profile.bootAccentColor);
        if (bootsMat.HasProperty("_BaseMap")) bootsMat.SetTexture("_BaseMap", bootTex);
        else if (bootsMat.HasProperty("_MainTex")) bootsMat.SetTexture("_MainTex", bootTex);

        Material soleplateMat = CreateLitMaterial(new Color(0.85f, 0.85f, 0.90f), 0.90f, null, 0.85f);
        Texture2D soleplateTex = ProceduralTextureFactory.CreateBootSoleplateTexture(new Color(0.85f, 0.85f, 0.90f), profile.bootStudColor);
        if (soleplateMat.HasProperty("_BaseMap")) soleplateMat.SetTexture("_BaseMap", soleplateTex);
        else if (soleplateMat.HasProperty("_MainTex")) soleplateMat.SetTexture("_MainTex", soleplateTex);

        Material studMat = CreateLitMaterial(profile.bootStudColor, 0.80f, null, 0.50f);

        Material armbandMat = null;
        if (profile.isCaptain)
        {
            armbandMat = CreateLitMaterial(new Color(0.98f, 0.82f, 0.12f), 0.40f);
            Texture2D armbandTex = ProceduralTextureFactory.CreateCaptainArmbandTexture();
            if (armbandMat.HasProperty("_BaseMap")) armbandMat.SetTexture("_BaseMap", armbandTex);
            else if (armbandMat.HasProperty("_MainTex")) armbandMat.SetTexture("_MainTex", armbandTex);
        }

        Material wristTapeMat = null;
        if (profile.hasWristTape)
        {
            wristTapeMat = CreateLitMaterial(new Color(0.96f, 0.96f, 0.98f), 0.15f);
        }

        Material gloveMat = null;
        if (isGK)
        {
            gloveMat = CreateLitMaterial(Color.white, 0.30f);
            Color backCol = (teamId == 1) ? new Color(0.15f, 0.85f, 0.35f) : new Color(0.95f, 0.85f, 0.15f);
            Texture2D gloveTex = ProceduralTextureFactory.CreateGoalkeeperGloveTexture(backCol, new Color(0.95f, 0.95f, 0.95f));
            if (gloveMat.HasProperty("_BaseMap")) gloveMat.SetTexture("_BaseMap", gloveTex);
            else if (gloveMat.HasProperty("_MainTex")) gloveMat.SetTexture("_MainTex", gloveTex);
        }

        float hScale = profile.bodyHeight;
        float wScale = profile.shoulderWidth;

        // 2. Pelvis Root (Anatomical Root Joint at waist level)
        GameObject pelvisRoot = new GameObject("Pelvis");
        pelvisRoot.transform.SetParent(parent, false);
        pelvisRoot.transform.localPosition = new Vector3(0f, 0.84f * hScale, 0f);

        // Shorts waistband (visual mesh on pelvis)
        GameObject hips = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hips.name = "Shorts";
        hips.transform.SetParent(pelvisRoot.transform, false);
        hips.transform.localPosition = Vector3.zero;
        hips.transform.localScale = new Vector3(0.48f * wScale, 0.16f * hScale, 0.36f);
        DestroyImmediate(hips.GetComponent<Collider>());
        hips.GetComponent<MeshRenderer>().sharedMaterial = shortsMat;

        // 3. Torso Joint (Spine & Chest Joint - pivots and articulates upper body)
        GameObject torsoJoint = new GameObject("TorsoJoint");
        torsoJoint.transform.SetParent(pelvisRoot.transform, false);
        torsoJoint.transform.localPosition = new Vector3(0f, 0.08f * hScale, 0f);

        // Upper Chest Jersey Visual Mesh
        GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Torso";
        torso.transform.SetParent(torsoJoint.transform, false);
        torso.transform.localPosition = new Vector3(0f, 0.20f * hScale, 0f);
        torso.transform.localScale = new Vector3(0.52f * wScale, 0.40f * hScale, 0.35f);
        DestroyImmediate(torso.GetComponent<Collider>());
        torso.GetComponent<MeshRenderer>().sharedMaterial = kitMat;

        // Ribbed Collar trim
        GameObject collar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        collar.name = "CollarTrim";
        collar.transform.SetParent(torsoJoint.transform, false);
        collar.transform.localPosition = new Vector3(0f, 0.38f * hScale, 0f);
        collar.transform.localScale = new Vector3(0.32f, 0.04f, 0.32f);
        DestroyImmediate(collar.GetComponent<Collider>());
        Color collarCol = (teamId == 1) ? Color.white : new Color(0.85f, 0.15f, 0.20f);
        Material collarMat = CreateLitMaterial(collarCol, 0.25f);
        collar.GetComponent<MeshRenderer>().sharedMaterial = collarMat;

        // Anatomical Muscular Neck
        GameObject neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        neck.name = "Neck";
        neck.transform.SetParent(torsoJoint.transform, false);
        neck.transform.localPosition = new Vector3(0f, 0.42f * hScale, 0f);
        neck.transform.localScale = new Vector3(0.22f, 0.12f, 0.22f);
        DestroyImmediate(neck.GetComponent<Collider>());
        neck.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        // Squad number on back of jersey
        if (jerseyNum > 0)
        {
            GameObject numObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
            numObj.name = "SquadNumber";
            numObj.transform.SetParent(torso.transform, false);
            numObj.transform.localPosition = new Vector3(0f, 0.04f, -0.52f);
            numObj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            numObj.transform.localScale = new Vector3(0.48f, 0.52f, 1f);
            DestroyImmediate(numObj.GetComponent<Collider>());

            Color numColor = (teamId == 1) ? new Color(0.12f, 0.12f, 0.14f) : Color.white;
            Texture2D numTex = ProceduralTextureFactory.CreateSquadNumberTexture(jerseyNum, numColor);
            Material numMat = CreateLitMaterial(Color.white, 0.3f);
            if (numMat.HasProperty("_BaseMap")) numMat.SetTexture("_BaseMap", numTex);
            else if (numMat.HasProperty("_MainTex")) numMat.SetTexture("_MainTex", numTex);

            if (numMat.HasProperty("_Surface"))
            {
                numMat.SetFloat("_Surface", 1.0f);
                numMat.SetFloat("_Blend", 0.0f);
                numMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                numMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                numMat.SetInt("_ZWrite", 0);
                numMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                numMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            numObj.GetComponent<MeshRenderer>().sharedMaterial = numMat;
        }

        // 4. Head Joint (Child of TorsoJoint: permanently attached to neck and torso)
        GameObject headJoint = new GameObject("HeadJoint");
        headJoint.transform.SetParent(torsoJoint.transform, false);
        headJoint.transform.localPosition = new Vector3(0f, 0.48f * hScale, 0f);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(headJoint.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.12f * hScale, 0f);
        head.transform.localScale = new Vector3(0.28f, 0.30f, 0.28f);
        DestroyImmediate(head.GetComponent<Collider>());
        head.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        // Hair styling tailored to player profile
        GameObject hair = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hair.name = "Hair";
        hair.transform.SetParent(head.transform, false);
        DestroyImmediate(hair.GetComponent<Collider>());
        hair.GetComponent<MeshRenderer>().sharedMaterial = hairMat;

        switch (profile.hairStyle)
        {
            case HairStyle.Pompadour: // Giroud
                hair.transform.localPosition = new Vector3(0f, 0.16f, 0.02f);
                hair.transform.localScale = new Vector3(1.05f, 0.92f, 1.10f);
                break;
            case HairStyle.BlondeCrop: // De Paul, Griezmann
                hair.transform.localPosition = new Vector3(0f, 0.14f, -0.02f);
                hair.transform.localScale = new Vector3(1.04f, 0.82f, 1.05f);
                break;
            case HairStyle.ShortBuzz: // Mbappe, Tchouameni
                hair.transform.localPosition = new Vector3(0f, 0.10f, -0.02f);
                hair.transform.localScale = new Vector3(1.02f, 0.74f, 1.03f);
                break;
            case HairStyle.CurlyAfroFade: // Upamecano
                hair.transform.localPosition = new Vector3(0f, 0.15f, -0.01f);
                hair.transform.localScale = new Vector3(1.06f, 0.90f, 1.06f);
                break;
            default: // TexturedFade (Messi, Alvarez, etc.)
                hair.transform.localPosition = new Vector3(0f, 0.13f, -0.03f);
                hair.transform.localScale = new Vector3(1.04f, 0.80f, 1.05f);
                break;
        }

        // Facial Beard (Messi, Giroud, De Paul, Otamendi, Lloris)
        if (profile.hasBeard)
        {
            GameObject beard = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beard.name = "Beard";
            beard.transform.SetParent(head.transform, false);
            beard.transform.localPosition = new Vector3(0f, -0.14f, 0.08f);
            beard.transform.localScale = new Vector3(0.96f, 0.52f, 0.94f);
            DestroyImmediate(beard.GetComponent<Collider>());

            Material beardMat = CreateLitMaterial(profile.beardColor, 0.20f);
            Texture2D beardTex = ProceduralTextureFactory.CreateBeardTexture(profile.beardColor);
            if (beardMat.HasProperty("_BaseMap")) beardMat.SetTexture("_BaseMap", beardTex);
            else if (beardMat.HasProperty("_MainTex")) beardMat.SetTexture("_MainTex", beardTex);

            beard.GetComponent<MeshRenderer>().sharedMaterial = beardMat;
        }

        // 5. Left & Right Arms (Attached to TorsoJoint shoulders: move in sync with torso)
        float shoulderOffsetX = 0.28f * wScale;
        float shoulderOffsetY = 0.30f * hScale;
        Transform armL = CreateArm(torsoJoint.transform, "Arm_L", new Vector3(-shoulderOffsetX, shoulderOffsetY, 0f), kitMat, skinMat, isGK, profile.isCaptain, armbandMat, profile.hasWristTape, wristTapeMat, gloveMat);
        Transform armR = CreateArm(torsoJoint.transform, "Arm_R", new Vector3(shoulderOffsetX, shoulderOffsetY, 0f), kitMat, skinMat, isGK, false, null, profile.hasWristTape, wristTapeMat, gloveMat);

        // 6. Left & Right Legs with Knee & Ankle Pivot Joints (Attached to Pelvis)
        float hipOffsetX = 0.16f * wScale;
        float hipOffsetY = -0.04f * hScale;
        PlayerLegJoints legL = CreateLegHierarchy(pelvisRoot.transform, "Leg_L", new Vector3(-hipOffsetX, hipOffsetY, 0f), shortsMat, socksMat, bootsMat, soleplateMat, studMat);
        PlayerLegJoints legR = CreateLegHierarchy(pelvisRoot.transform, "Leg_R", new Vector3(hipOffsetX, hipOffsetY, 0f), shortsMat, socksMat, bootsMat, soleplateMat, studMat);

        // 7. Procedural Runner Animator with full anatomical coordination
        var animator = parent.gameObject.AddComponent<ProceduralRunnerAnimator>();
        animator.leftLeg = legL.hip;
        animator.rightLeg = legR.hip;
        animator.leftKnee = legL.knee;
        animator.rightKnee = legR.knee;
        animator.leftAnkle = legL.ankle;
        animator.rightAnkle = legR.ankle;
        animator.leftArm = armL;
        animator.rightArm = armR;
        animator.torso = torsoJoint.transform;
        animator.head = headJoint.transform;

        // 8. Grounded Blob Shadow directly under feet
        GameObject shadowObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadowObj.name = "PlayerBlobShadow";
        shadowObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        shadowObj.transform.localScale = new Vector3(1.35f * wScale, 1.35f * wScale, 1.0f);
        shadowObj.transform.position = new Vector3(parent.position.x, 0.015f, parent.position.z);
        DestroyImmediate(shadowObj.GetComponent<Collider>());

        Material shadowMat = CreateBlobShadowMaterial();
        shadowObj.GetComponent<MeshRenderer>().sharedMaterial = shadowMat;

        var blobShadowScript = shadowObj.AddComponent<BlobShadow>();
        blobShadowScript.playerTransform = parent;
        blobShadowScript.groundY = 0.015f;

        var loco = parent.GetComponent<FootballPlayerLocomotion>();
        if (loco != null)
        {
            loco.blobShadow = shadowObj.transform;
            loco.groundY = 0.015f;
        }
    }

    private Transform CreateArm(Transform parent, string name, Vector3 shoulderPos, Material sleeveMat, Material skinMat, bool isGK, bool isCaptain, Material armbandMat, bool hasWristTape, Material wristTapeMat, Material gloveMat)
    {
        GameObject armRoot = new GameObject(name);
        armRoot.transform.SetParent(parent, false);
        armRoot.transform.localPosition = shoulderPos;

        // Upper arm (sleeve)
        GameObject upperArm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        upperArm.name = "UpperArm";
        upperArm.transform.SetParent(armRoot.transform, false);
        upperArm.transform.localPosition = new Vector3(0f, -0.16f, 0f);
        upperArm.transform.localScale = new Vector3(0.12f, 0.16f, 0.12f);
        DestroyImmediate(upperArm.GetComponent<Collider>());
        upperArm.GetComponent<MeshRenderer>().sharedMaterial = sleeveMat;

        // Official Captain Armband (if captain and left arm)
        if (isCaptain && name.Contains("Arm_L") && armbandMat != null)
        {
            GameObject armband = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            armband.name = "CaptainArmband";
            armband.transform.SetParent(upperArm.transform, false);
            armband.transform.localPosition = new Vector3(0f, -0.10f, 0f);
            armband.transform.localScale = new Vector3(1.10f, 0.42f, 1.10f);
            DestroyImmediate(armband.GetComponent<Collider>());
            armband.GetComponent<MeshRenderer>().sharedMaterial = armbandMat;
        }

        // Forearm (skin) angled slightly forward in runner posture
        GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        forearm.name = "Forearm";
        forearm.transform.SetParent(armRoot.transform, false);
        forearm.transform.localPosition = new Vector3(0f, -0.42f, 0.08f);
        forearm.transform.localScale = new Vector3(0.10f, 0.14f, 0.10f);
        forearm.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        DestroyImmediate(forearm.GetComponent<Collider>());
        forearm.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        // Athletic Wrist Tape
        if (hasWristTape && wristTapeMat != null)
        {
            GameObject wristTape = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wristTape.name = "WristTape";
            wristTape.transform.SetParent(forearm.transform, false);
            wristTape.transform.localPosition = new Vector3(0f, -0.85f, 0f);
            wristTape.transform.localScale = new Vector3(1.12f, 0.20f, 1.12f);
            DestroyImmediate(wristTape.GetComponent<Collider>());
            wristTape.GetComponent<MeshRenderer>().sharedMaterial = wristTapeMat;
        }

        // Hand / Glove
        GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hand.name = isGK ? "GoalkeeperGlove" : "Hand";
        hand.transform.SetParent(forearm.transform, false);
        hand.transform.localPosition = new Vector3(0f, -1.05f, 0f);
        hand.transform.localScale = isGK ? new Vector3(1.35f, 1.45f, 1.1f) : new Vector3(1.1f, 1.2f, 0.8f);
        DestroyImmediate(hand.GetComponent<Collider>());
        hand.GetComponent<MeshRenderer>().sharedMaterial = isGK ? (gloveMat ?? sleeveMat) : skinMat;

        return armRoot.transform;
    }

    private PlayerLegJoints CreateLegHierarchy(Transform parent, string name, Vector3 hipPos, Material shortsMat, Material socksMat, Material bootMat, Material soleplateMat, Material studMat)
    {
        GameObject legRoot = new GameObject(name);
        legRoot.transform.SetParent(parent, false);
        legRoot.transform.localPosition = hipPos;

        // Thigh (swings from hip)
        GameObject thigh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        thigh.name = "Thigh";
        thigh.transform.SetParent(legRoot.transform, false);
        thigh.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        thigh.transform.localScale = new Vector3(0.15f, 0.18f, 0.15f);
        DestroyImmediate(thigh.GetComponent<Collider>());
        thigh.GetComponent<MeshRenderer>().sharedMaterial = shortsMat;

        // Knee Pivot Joint (bends backwards during backswing, straightens on forward plant)
        GameObject kneeJoint = new GameObject("KneeJoint");
        kneeJoint.transform.SetParent(legRoot.transform, false);
        kneeJoint.transform.localPosition = new Vector3(0f, -0.36f, 0f);

        // Shin / Calf with knee-high team socks (child of knee joint)
        GameObject shin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shin.name = "Shin_Sock";
        shin.transform.SetParent(kneeJoint.transform, false);
        shin.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        shin.transform.localScale = new Vector3(0.13f, 0.18f, 0.13f);
        DestroyImmediate(shin.GetComponent<Collider>());
        shin.GetComponent<MeshRenderer>().sharedMaterial = socksMat;

        // Ankle Pivot Joint (for professional foot plantarflexion, swing dorsiflexion, and instep angles)
        GameObject ankleJoint = new GameObject("AnkleJoint");
        ankleJoint.transform.SetParent(kneeJoint.transform, false);
        ankleJoint.transform.localPosition = new Vector3(0f, -0.36f, 0f);

        // Athletic Cleat Boot (child of ankle joint so the boot pitches and rotates dynamically at the ankle)
        GameObject boot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boot.name = "CleatBoot";
        boot.transform.SetParent(ankleJoint.transform, false);
        boot.transform.localPosition = new Vector3(0f, -0.02f, 0.08f);
        boot.transform.localScale = new Vector3(0.13f, 0.09f, 0.27f);
        DestroyImmediate(boot.GetComponent<Collider>());
        boot.GetComponent<MeshRenderer>().sharedMaterial = bootMat;

        // Sleek Chrome Soleplate underneath
        GameObject soleplate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        soleplate.name = "Soleplate";
        soleplate.transform.SetParent(boot.transform, false);
        soleplate.transform.localPosition = new Vector3(0f, -0.52f, 0f);
        soleplate.transform.localScale = new Vector3(0.98f, 0.10f, 0.98f);
        DestroyImmediate(soleplate.GetComponent<Collider>());
        soleplate.GetComponent<MeshRenderer>().sharedMaterial = soleplateMat;

        // 4 Traction Studs under soleplate
        Vector3[] studOffsets = {
            new Vector3(-0.35f, -0.55f, 0.35f),
            new Vector3(0.35f, -0.55f, 0.35f),
            new Vector3(-0.35f, -0.55f, -0.35f),
            new Vector3(0.35f, -0.55f, -0.35f)
        };
        for (int s = 0; s < studOffsets.Length; s++)
        {
            GameObject stud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stud.name = $"Stud_{s}";
            stud.transform.SetParent(soleplate.transform, false);
            stud.transform.localPosition = studOffsets[s];
            stud.transform.localScale = new Vector3(0.22f, 0.45f, 0.22f);
            DestroyImmediate(stud.GetComponent<Collider>());
            stud.GetComponent<MeshRenderer>().sharedMaterial = studMat;
        }

        return new PlayerLegJoints { hip = legRoot.transform, knee = kneeJoint.transform, ankle = ankleJoint.transform };
    }

    private PlayerOverheadMarker CreatePlayerIndicator(Transform parent, string displayName = "10 MESSI", Color? markerColor = null)
    {
        GameObject markerObj = new GameObject("PlayerOverheadMarker");
        var marker = markerObj.AddComponent<PlayerOverheadMarker>();
        marker.Initialize(parent, displayName, markerColor);
        return marker;
    }

    private void SetupBroadcastUI()
    {
        var existingCanvas = GameObject.Find("BroadcastCanvas");
        if (existingCanvas != null) DestroyImmediate(existingCanvas);

        GameObject canvasObj = new GameObject("BroadcastCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 1. Sleek Broadcast Score Bug (Top-Left)
        GameObject scoreBugObj = new GameObject("ScoreBug");
        scoreBugObj.transform.SetParent(canvasObj.transform, false);
        var scoreRect = scoreBugObj.AddComponent<RectTransform>();
        scoreRect.anchorMin = new Vector2(0f, 1f);
        scoreRect.anchorMax = new Vector2(0f, 1f);
        scoreRect.pivot = new Vector2(0f, 1f);
        scoreRect.anchoredPosition = new Vector2(40f, -35f);
        scoreRect.sizeDelta = new Vector2(340f, 48f);

        var bg = scoreBugObj.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.08f, 0.12f, 0.88f);

        var bug = scoreBugObj.AddComponent<BroadcastScoreBug>();

        // Score bug badges
        bug.homeTeamText = CreateTMPText(scoreBugObj.transform, "ARG", new Vector2(-115f, 0f), new Vector2(70f, 36f), 20, TextAlignmentOptions.Center, new Color(0.45f, 0.72f, 1.0f));
        bug.scoreText = CreateTMPText(scoreBugObj.transform, "0 - 0", new Vector2(-40f, 0f), new Vector2(70f, 36f), 22, TextAlignmentOptions.Center, Color.white);
        bug.awayTeamText = CreateTMPText(scoreBugObj.transform, "FRA", new Vector2(35f, 0f), new Vector2(70f, 36f), 20, TextAlignmentOptions.Center, new Color(0.95f, 0.28f, 0.28f));
        bug.clockText = CreateTMPText(scoreBugObj.transform, "00:00", new Vector2(115f, 0f), new Vector2(70f, 36f), 17, TextAlignmentOptions.Center, new Color(0.95f, 0.85f, 0.2f));

        // 2. Compact Control Guide Pill (Top-Right)
        GameObject guideObj = new GameObject("ControlsGuide");
        guideObj.transform.SetParent(canvasObj.transform, false);
        var guideRect = guideObj.AddComponent<RectTransform>();
        guideRect.anchorMin = new Vector2(1f, 1f);
        guideRect.anchorMax = new Vector2(1f, 1f);
        guideRect.pivot = new Vector2(1f, 1f);
        guideRect.anchoredPosition = new Vector2(-40f, -35f);
        guideRect.sizeDelta = new Vector2(250f, 62f);

        var guideBg = guideObj.AddComponent<Image>();
        guideBg.color = new Color(0.06f, 0.08f, 0.12f, 0.80f);

        CreateTMPText(guideObj.transform, "<b>WASD</b>: Move | <b>Shift</b>: Sprint\n<b>Space / L</b>: Shoot/Kick | <b>J</b>: Pass\n<b>K</b>: Cross/Tackle | <b>I</b>: Through", Vector2.zero, new Vector2(230f, 52f), 11, TextAlignmentOptions.Center, Color.white);

        // 3. Modern 2D Minimap Radar (Bottom-Right)
        GameObject radarObj = new GameObject("MinimapRadar");
        radarObj.transform.SetParent(canvasObj.transform, false);
        var radarRect = radarObj.AddComponent<RectTransform>();
        radarRect.anchorMin = new Vector2(1f, 0f);
        radarRect.anchorMax = new Vector2(1f, 0f);
        radarRect.pivot = new Vector2(1f, 0f);
        radarRect.anchoredPosition = new Vector2(-40f, 40f);
        radarRect.sizeDelta = new Vector2(240f, 150f);

        var radarBg = radarObj.AddComponent<Image>();
        radarBg.color = new Color(0.04f, 0.06f, 0.09f, 0.75f);

        var radar = radarObj.AddComponent<MinimapRadar>();
        radar.radarPitchRect = radarRect;

        // Templates for blips
        var homeBlip = CreateBlip(radarObj.transform, "HomeBlipTemplate", new Color(0.45f, 0.72f, 1.0f), 7f);
        var awayBlip = CreateBlip(radarObj.transform, "AwayBlipTemplate", new Color(0.95f, 0.25f, 0.25f), 7f);
        var ballBlip = CreateBlip(radarObj.transform, "BallBlip", Color.white, 9f);

        radar.homeBlipTemplate = homeBlip;
        radar.awayBlipTemplate = awayBlip;
        radar.ballBlip = ballBlip;
    }

    private Image CreateBlip(Transform parent, string name, Color color, float size)
    {
        GameObject blipObj = new GameObject(name);
        blipObj.transform.SetParent(parent, false);
        var rect = blipObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        var img = blipObj.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private TextMeshProUGUI CreateTMPText(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAlignmentOptions alignment, Color color)
    {
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(parent, false);
        var rect = textObj.AddComponent<RectTransform>();
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = color;

        // Safe font loading without calling uninitialized TMP_Settings
        try
        {
            var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (allFonts != null && allFonts.Length > 0)
            {
                tmp.font = allFonts[0];
            }
            else
            {
                var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font != null) tmp.font = font;
            }
        }
        catch { /* Fallback gracefully */ }

        return tmp;
    }

    private void SetupMatchSystems(List<FootballPlayerLocomotion> team1, List<FootballPlayerLocomotion> team2, FootballBall ball)
    {
        GameObject systemsRoot = new GameObject("FIFA_GameSystems");

        // 1. Match Engine
        var engine = systemsRoot.AddComponent<MatchEngine>();
        engine.homeTeamName = "Argentina";
        engine.awayTeamName = "France";
        engine.halfDurationSeconds = 60f; // 1 minute real time per half (60 seconds)

        // 2. Referee System
        systemsRoot.AddComponent<RefereeSystem>();

        // 3. Set Piece Manager
        systemsRoot.AddComponent<SetPieceManager>();

        // 4. Crowd Audio & Commentary
        systemsRoot.AddComponent<CrowdAudioManager>();
        systemsRoot.AddComponent<FootballCommentarySystem>();

        // 5. Penalty Shootout Mode
        systemsRoot.AddComponent<PenaltyShootoutMode>();
    }

    private static Material CreateLitMaterial(Color color, float smoothness = 0.5f, Texture2D normalMap = null, float metallic = 0.0f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

        if (normalMap != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normalMap);
            mat.EnableKeyword("_NORMALMAP");
        }
        return mat;
    }

    private static Material CreateBlobShadowMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        Texture2D tex = ProceduralTextureFactory.CreateBlobShadowTexture();

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1.0f); // Transparent
            mat.SetFloat("_Blend", 0.0f);   // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        return mat;
    }
}
