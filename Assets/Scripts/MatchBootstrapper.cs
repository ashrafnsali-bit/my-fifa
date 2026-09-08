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

        // 5. Spawn Team 1 (Argentina - Human Controlled)
        var team1Players = SpawnTeam(1, "Argentina", new Color(0.45f, 0.72f, 1.0f), Color.white, new Color(0.95f, 0.9f, 0.1f), FormationType.Formation_4_3_3, true);

        // 6. Spawn Team 2 (France - AI Opponent)
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
                n.StartsWith("WorldCupStadium"))
            {
                DestroyImmediate(root);
            }
        }
    }

    private void SetupLightingAndAtmosphere()
    {
        var lightObj = GameObject.Find("Directional Light");
        Light dirLight = null;
        if (lightObj != null) dirLight = lightObj.GetComponent<Light>();
        if (dirLight == null)
        {
            var newLightObj = new GameObject("Stadium_Floodlight");
            dirLight = newLightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
        }

        dirLight.transform.rotation = Quaternion.Euler(56f, -38f, 0f);
        dirLight.intensity = 1.45f;
        dirLight.color = new Color(1.0f, 0.98f, 0.93f);
        dirLight.shadows = LightShadows.Soft;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.48f, 0.54f, 0.65f);

        // Global Post-Processing Volume for broadcast television glow and ACES tonemapping
        var volume = FindFirstObjectByType<UnityEngine.Rendering.Volume>();
        if (volume == null)
        {
            var volObj = new GameObject("Global_PostProcess_Volume");
            volume = volObj.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            volume.priority = 1.0f;
        }

#if UNITY_EDITOR
        if (volume.sharedProfile == null)
        {
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile != null) volume.sharedProfile = profile;
        }
#endif
    }

    private void BuildFullStadiumPitch()
    {
        var existingPitch = GameObject.Find("StadiumPitch");
        if (existingPitch != null) DestroyImmediate(existingPitch);

        GameObject pitchObj = new GameObject("StadiumPitch");
        var builder = pitchObj.AddComponent<ProceduralPitchBuilder>();

        // High-definition procedural turf with blade noise
        Material lightTurf = CreateLitMaterial(new Color(0.22f, 0.58f, 0.24f), 0.30f);
        Material darkTurf = CreateLitMaterial(new Color(0.16f, 0.48f, 0.18f), 0.30f);

        var grassTex1 = ProceduralTextureFactory.CreateTurfGrassTexture(new Color(0.22f, 0.58f, 0.24f), new Color(0.26f, 0.65f, 0.28f));
        var grassTex2 = ProceduralTextureFactory.CreateTurfGrassTexture(new Color(0.16f, 0.48f, 0.18f), new Color(0.19f, 0.53f, 0.21f));

        if (lightTurf.HasProperty("_BaseMap")) lightTurf.SetTexture("_BaseMap", grassTex1);
        else if (lightTurf.HasProperty("_MainTex")) lightTurf.SetTexture("_MainTex", grassTex1);

        if (darkTurf.HasProperty("_BaseMap")) darkTurf.SetTexture("_BaseMap", grassTex2);
        else if (darkTurf.HasProperty("_MainTex")) darkTurf.SetTexture("_MainTex", grassTex2);

        builder.turfMaterialLight = lightTurf;
        builder.turfMaterialDark = darkTurf;
        builder.lineMaterial = CreateLitMaterial(new Color(0.98f, 0.98f, 0.98f), 0.15f);
        builder.goalFrameMaterial = CreateLitMaterial(new Color(0.96f, 0.96f, 0.96f), 0.85f);

        builder.BuildCompletePitch();

        // Build stadium perimeter LED advertising hoardings
        BuildPerimeterBoards(pitchObj.transform);
    }

    private void BuildPerimeterBoards(Transform parent)
    {
        GameObject boardsRoot = new GameObject("AdBoards");
        boardsRoot.transform.SetParent(parent, false);

        Material boardMat = CreateLitMaterial(new Color(0.08f, 0.10f, 0.16f), 0.85f);
        var ledTex = ProceduralTextureFactory.CreateLEDAdTexture();
        if (boardMat.HasProperty("_BaseMap")) boardMat.SetTexture("_BaseMap", ledTex);
        else if (boardMat.HasProperty("_MainTex")) boardMat.SetTexture("_MainTex", ledTex);

        if (boardMat.HasProperty("_EmissionColor"))
        {
            boardMat.EnableKeyword("_EMISSION");
            boardMat.SetColor("_EmissionColor", Color.white * 1.5f);
            boardMat.SetTexture("_EmissionMap", ledTex);
        }

        float w = PitchConstants.HalfWidth + 3.0f;
        float l = PitchConstants.HalfLength + 3.0f;
        float h = 0.95f;

        // Sideline boards
        CreateBoardQuad(boardsRoot.transform, new Vector3(-w, h * 0.5f, 0f), new Vector3(0.2f, h, PitchConstants.PitchLength + 8f), boardMat);
        CreateBoardQuad(boardsRoot.transform, new Vector3(w, h * 0.5f, 0f), new Vector3(0.2f, h, PitchConstants.PitchLength + 8f), boardMat);

        // Endline boards
        float goalHalfGap = PitchConstants.GoalWidth * 0.5f + 2.5f;
        float cornerW = (PitchConstants.PitchWidth - goalHalfGap * 2f) * 0.5f;

        CreateBoardQuad(boardsRoot.transform, new Vector3(-w + cornerW * 0.5f, h * 0.5f, -l), new Vector3(cornerW, h, 0.2f), boardMat);
        CreateBoardQuad(boardsRoot.transform, new Vector3(w - cornerW * 0.5f, h * 0.5f, -l), new Vector3(cornerW, h, 0.2f), boardMat);
        CreateBoardQuad(boardsRoot.transform, new Vector3(-w + cornerW * 0.5f, h * 0.5f, l), new Vector3(cornerW, h, 0.2f), boardMat);
        CreateBoardQuad(boardsRoot.transform, new Vector3(w - cornerW * 0.5f, h * 0.5f, l), new Vector3(cornerW, h, 0.2f), boardMat);
    }

    private void CreateBoardQuad(Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "AdBoard";
        cube.transform.SetParent(parent, false);
        cube.transform.position = pos;
        cube.transform.localScale = scale;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private FootballBall SetupMatchBall()
    {
        var existingBall = GameObject.Find("MatchBall");
        if (existingBall != null) DestroyImmediate(existingBall);

        GameObject ballObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ballObj.name = "MatchBall";
        ballObj.transform.position = new Vector3(0f, 0.11f, 0f);
        ballObj.transform.localScale = Vector3.one * (0.11f * 2.0f); // 22cm regulation diameter

        // Official World Cup "Al Rihla" procedural texture mapping
        Material ballMat = CreateLitMaterial(Color.white, 0.90f);
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

        // Intimate Broadcast Perspective (close to the player with the ball, strictly horizontal touchlines)
        rig.sidelineDistance = 33.0f;
        rig.cameraHeight = 10.8f;
        rig.baseFieldOfView = 28f;
        rig.maxFieldOfView = 38f;
        rig.smoothTime = 0.18f;
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

        // High-definition team national kit materials
        Material outfieldJerseyMat = CreateLitMaterial(Color.white, 0.35f);
        Texture2D kitTex = (teamId == 1) ? ProceduralTextureFactory.CreateArgentinaKitTexture() : ProceduralTextureFactory.CreateFranceKitTexture();
        if (outfieldJerseyMat.HasProperty("_BaseMap")) outfieldJerseyMat.SetTexture("_BaseMap", kitTex);
        else if (outfieldJerseyMat.HasProperty("_MainTex")) outfieldJerseyMat.SetTexture("_MainTex", kitTex);

        // Shorts & Socks
        Material outfieldShortsMat = CreateLitMaterial(teamId == 1 ? new Color(0.12f, 0.12f, 0.14f) : new Color(0.06f, 0.10f, 0.24f), 0.35f);
        Material outfieldSocksMat = CreateLitMaterial(teamId == 1 ? new Color(0.96f, 0.96f, 0.96f) : new Color(0.88f, 0.12f, 0.16f), 0.25f);

        // Goalkeeper kit materials
        Material gkJerseyMat = CreateLitMaterial(Color.white, 0.45f);
        Texture2D gkTex = ProceduralTextureFactory.CreateGoalkeeperKitTexture(gkColor);
        if (gkJerseyMat.HasProperty("_BaseMap")) gkJerseyMat.SetTexture("_BaseMap", gkTex);
        else if (gkJerseyMat.HasProperty("_MainTex")) gkJerseyMat.SetTexture("_MainTex", gkTex);

        Material gkShortsMat = CreateLitMaterial(gkColor, 0.35f);
        Material gkSocksMat = CreateLitMaterial(gkColor, 0.25f);

        // Anatomy & Equipment materials
        Material skinMat = CreateLitMaterial(new Color(0.88f, 0.72f, 0.60f), 0.15f);
        Material bootsMat = CreateLitMaterial(new Color(0.12f, 0.12f, 0.14f), 0.70f);
        Material hairMat = CreateLitMaterial(new Color(0.14f, 0.10f, 0.08f), 0.15f);

        PlayerRuntimeState defaultHumanPlayer = null;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            bool isGK = slot.position == PlayerPosition.GK;
            Vector3 worldPos = FormationData.GetWorldPosition(slot, teamId);
            worldPos.y = 1.0f;

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
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

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

            // 4. Athletic 3D Visual Mesh with limbs, shorts, socks, and styled hair
            Material jersey = isGK ? gkJerseyMat : outfieldJerseyMat;
            Material shorts = isGK ? gkShortsMat : outfieldShortsMat;
            Material socks = isGK ? gkSocksMat : outfieldSocksMat;

            BuildPlayerVisuals(playerObj.transform, jersey, shorts, socks, skinMat, bootsMat, hairMat, isGK);

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
            var marker = CreatePlayerIndicator(defaultHumanPlayer.transform, "10 MESSI");
            switcher.overheadMarker = marker;
            switcher.SwitchToPlayer(defaultHumanPlayer);
        }

        // Attach TeamTacticsController
        var tactics = teamRoot.AddComponent<TeamTacticsController>();
        tactics.teamId = teamId;
        tactics.formation = formation;
        tactics.difficultySettings = AIDifficultySettings.GetPreset(DifficultyLevel.Professional);
        tactics.teamPlayers = aiPlayerList;

        foreach (var ai in aiPlayerList)
        {
            ai.tacticsController = tactics;
        }

        return playerList;
    }

    private struct PlayerLegJoints
    {
        public Transform hip;
        public Transform knee;
    }

    private void BuildPlayerVisuals(Transform parent, Material kitMat, Material shortsMat, Material socksMat, Material skinMat, Material bootsMat, Material hairMat, bool isGK)
    {
        // 1. Torso / Jersey
        GameObject torso = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        torso.name = "Torso";
        torso.transform.SetParent(parent, false);
        torso.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        torso.transform.localScale = new Vector3(0.56f, 0.48f, 0.38f);
        DestroyImmediate(torso.GetComponent<Collider>());
        torso.GetComponent<MeshRenderer>().sharedMaterial = kitMat;

        // 2. Shorts / Hips
        GameObject hips = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hips.name = "Shorts";
        hips.transform.SetParent(parent, false);
        hips.transform.localPosition = new Vector3(0f, 0.82f, 0f);
        hips.transform.localScale = new Vector3(0.48f, 0.18f, 0.38f);
        DestroyImmediate(hips.GetComponent<Collider>());
        hips.GetComponent<MeshRenderer>().sharedMaterial = shortsMat;

        // 3. Head
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 1.62f, 0f);
        head.transform.localScale = new Vector3(0.32f, 0.35f, 0.32f);
        DestroyImmediate(head.GetComponent<Collider>());
        head.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        // Hair styling cap
        GameObject hair = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hair.name = "Hair";
        hair.transform.SetParent(head.transform, false);
        hair.transform.localPosition = new Vector3(0f, 0.14f, -0.04f);
        hair.transform.localScale = new Vector3(1.04f, 0.82f, 1.05f);
        DestroyImmediate(hair.GetComponent<Collider>());
        hair.GetComponent<MeshRenderer>().sharedMaterial = hairMat;

        // 4. Left & Right Arms (Pivoting at shoulders for natural athletic swing)
        Transform armL = CreateArm(parent, "Arm_L", new Vector3(-0.32f, 1.25f, 0f), kitMat, skinMat, isGK);
        Transform armR = CreateArm(parent, "Arm_R", new Vector3(0.32f, 1.25f, 0f), kitMat, skinMat, isGK);

        // 5. Left & Right Legs with Knee Pivot Joints (for fluid human running locomotion)
        PlayerLegJoints legL = CreateLegHierarchy(parent, "Leg_L", new Vector3(-0.16f, 0.78f, 0f), shortsMat, socksMat, bootsMat);
        PlayerLegJoints legR = CreateLegHierarchy(parent, "Leg_R", new Vector3(0.16f, 0.78f, 0f), shortsMat, socksMat, bootsMat);

        // 6. Procedural Runner Animator with full anatomical coordination
        var animator = parent.gameObject.AddComponent<ProceduralRunnerAnimator>();
        animator.leftLeg = legL.hip;
        animator.rightLeg = legR.hip;
        animator.leftKnee = legL.knee;
        animator.rightKnee = legR.knee;
        animator.leftArm = armL;
        animator.rightArm = armR;
        animator.torso = torso.transform;
        animator.head = head.transform;
    }

    private Transform CreateArm(Transform parent, string name, Vector3 shoulderPos, Material sleeveMat, Material skinMat, bool isGK)
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

        // Forearm (skin) angled slightly forward in runner posture
        GameObject forearm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        forearm.name = "Forearm";
        forearm.transform.SetParent(armRoot.transform, false);
        forearm.transform.localPosition = new Vector3(0f, -0.42f, 0.08f);
        forearm.transform.localScale = new Vector3(0.10f, 0.14f, 0.10f);
        forearm.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        DestroyImmediate(forearm.GetComponent<Collider>());
        forearm.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

        // Hand / Glove
        GameObject hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hand.name = "Hand";
        hand.transform.SetParent(forearm.transform, false);
        hand.transform.localPosition = new Vector3(0f, -1.0f, 0f);
        hand.transform.localScale = new Vector3(1.1f, 1.2f, 0.8f);
        DestroyImmediate(hand.GetComponent<Collider>());
        hand.GetComponent<MeshRenderer>().sharedMaterial = isGK ? sleeveMat : skinMat;

        return armRoot.transform;
    }

    private PlayerLegJoints CreateLegHierarchy(Transform parent, string name, Vector3 hipPos, Material shortsMat, Material socksMat, Material bootMat)
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

        // Athletic Cleat Boot (child of knee joint below shin)
        GameObject boot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boot.name = "CleatBoot";
        boot.transform.SetParent(kneeJoint.transform, false);
        boot.transform.localPosition = new Vector3(0f, -0.36f, 0.08f);
        boot.transform.localScale = new Vector3(0.14f, 0.10f, 0.28f);
        DestroyImmediate(boot.GetComponent<Collider>());
        boot.GetComponent<MeshRenderer>().sharedMaterial = bootMat;

        return new PlayerLegJoints { hip = legRoot.transform, knee = kneeJoint.transform };
    }

    private PlayerOverheadMarker CreatePlayerIndicator(Transform parent, string displayName = "10 MESSI")
    {
        GameObject markerObj = new GameObject("PlayerOverheadMarker");
        var marker = markerObj.AddComponent<PlayerOverheadMarker>();
        marker.Initialize(parent, displayName);
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

        CreateTMPText(guideObj.transform, "<b>WASD</b>: Move | <b>Shift</b>: Sprint\n<b>J / Space</b>: Pass | <b>L</b>: Shoot\n<b>K</b>: Cross/Tackle | <b>I</b>: Through", Vector2.zero, new Vector2(230f, 52f), 11, TextAlignmentOptions.Center, Color.white);

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
        engine.halfDurationSeconds = 180f; // 3 mins real time per half

        // 2. Referee System
        systemsRoot.AddComponent<RefereeSystem>();

        // 3. Set Piece Manager
        systemsRoot.AddComponent<SetPieceManager>();

        // 4. Crowd Audio & Commentary
        systemsRoot.AddComponent<CrowdAudioManager>();
        systemsRoot.AddComponent<FootballCommentarySystem>();
    }

    private static Material CreateLitMaterial(Color color, float smoothness = 0.5f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }
}
