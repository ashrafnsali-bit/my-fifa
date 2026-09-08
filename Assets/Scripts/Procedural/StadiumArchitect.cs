using UnityEngine;
using Football.Core;

namespace Football.Procedural
{
    /// <summary>
    /// Generates high-fidelity stadium architecture, tiered spectator seating,
    /// dynamic LED perimeter boards, corner floodlight towers, goal nets, and corner flags.
    /// </summary>
    public static class StadiumArchitect
    {
        public static void BuildWorldCupStadium(Transform parent)
        {
            var stadiumRoot = new GameObject("WorldCupStadium_Atmosphere");
            stadiumRoot.transform.SetParent(parent, false);

            // 1. Build Multi-Tier Grandstands & Spectator Seating
            BuildSeatingStands(stadiumRoot.transform);

            // 2. Build 4 Stadium Corner Floodlight Towers
            BuildFloodlightTowers(stadiumRoot.transform);

            // 3. Build Realistic Corner Flags
            BuildCornerFlags(stadiumRoot.transform);

            // 4. Build Stadium Outer Canopy Roof
            BuildCanopyRoof(stadiumRoot.transform);

            // 5. Build Pitch Perimeter Apron & Photographer Zones
            BuildPitchApron(stadiumRoot.transform);

            // 6. Build Sideline Team Dugouts & Benches
            BuildTeamBenches(stadiumRoot.transform);

            // 7. Build Continuous Dynamic Electronic LED Perimeter Ribbon Boards
            BuildLEDPerimeterBoards(stadiumRoot.transform);
        }

        private static void BuildSeatingStands(Transform parent)
        {
            var standsRoot = new GameObject("Grandstands");
            standsRoot.transform.SetParent(parent, false);

            // Grandstand crowd materials using high-density procedural crowd texture
            Material crowdTier1Mat = CreateMaterial(Color.white, 0.25f);
            Material crowdTier2Mat = CreateMaterial(Color.white, 0.25f);
            var crowdTex = ProceduralTextureFactory.CreateCrowdTexture();

            // Set tiling so spectators and seats repeat at authentic human scale
            Vector2 tier1Scale = new Vector2(18f, 3.5f);
            Vector2 tier2Scale = new Vector2(22f, 4.0f);

            if (crowdTier1Mat.HasProperty("_BaseMap")) { crowdTier1Mat.SetTexture("_BaseMap", crowdTex); crowdTier1Mat.SetTextureScale("_BaseMap", tier1Scale); }
            else if (crowdTier1Mat.HasProperty("_MainTex")) { crowdTier1Mat.SetTexture("_MainTex", crowdTex); crowdTier1Mat.SetTextureScale("_MainTex", tier1Scale); }

            if (crowdTier2Mat.HasProperty("_BaseMap")) { crowdTier2Mat.SetTexture("_BaseMap", crowdTex); crowdTier2Mat.SetTextureScale("_BaseMap", tier2Scale); }
            else if (crowdTier2Mat.HasProperty("_MainTex")) { crowdTier2Mat.SetTexture("_MainTex", crowdTex); crowdTier2Mat.SetTextureScale("_MainTex", tier2Scale); }

            Material concreteMat = CreateMaterial(new Color(0.32f, 0.35f, 0.38f), 0.1f);

            // Parapet Banners Material
            Material bannerMat = CreateMaterial(Color.white, 0.4f);
            var bannerTex = ProceduralTextureFactory.CreateBannerTexture();
            if (bannerMat.HasProperty("_BaseMap")) { bannerMat.SetTexture("_BaseMap", bannerTex); bannerMat.SetTextureScale("_BaseMap", new Vector2(8f, 1f)); }
            else if (bannerMat.HasProperty("_MainTex")) { bannerMat.SetTexture("_MainTex", bannerTex); bannerMat.SetTextureScale("_MainTex", new Vector2(8f, 1f)); }

            // VIP Box Glass Material
            Material vipGlassMat = CreateMaterial(new Color(0.12f, 0.20f, 0.28f, 0.85f), 0.95f);

            // National & Tournament Waving Flag Materials
            Material argFlagMat = CreateMaterial(Color.white, 0.35f);
            var argTex = ProceduralTextureFactory.CreateArgentinaFlagTexture();
            if (argFlagMat.HasProperty("_BaseMap")) argFlagMat.SetTexture("_BaseMap", argTex);
            else if (argFlagMat.HasProperty("_MainTex")) argFlagMat.SetTexture("_MainTex", argTex);

            Material fraFlagMat = CreateMaterial(Color.white, 0.35f);
            var fraTex = ProceduralTextureFactory.CreateFranceFlagTexture();
            if (fraFlagMat.HasProperty("_BaseMap")) fraFlagMat.SetTexture("_BaseMap", fraTex);
            else if (fraFlagMat.HasProperty("_MainTex")) fraFlagMat.SetTexture("_MainTex", fraTex);

            float pW = PitchConstants.HalfWidth;
            float pL = PitchConstants.HalfLength;

            // Lateral Sideline Grandstands (East & West)
            BuildStandSection(standsRoot.transform, "Stand_East", new Vector3(pW + 7.5f, 8.5f, 0f), new Vector3(22f, 19f, PitchConstants.PitchLength + 36f), -20f, crowdTier1Mat, crowdTier2Mat, concreteMat, bannerMat, vipGlassMat, argFlagMat, fraFlagMat, phaseOffset: 0.0f);
            BuildStandSection(standsRoot.transform, "Stand_West", new Vector3(-pW - 12.5f, 8.5f, 0f), new Vector3(22f, 19f, PitchConstants.PitchLength + 36f), 20f, crowdTier1Mat, crowdTier2Mat, concreteMat, bannerMat, vipGlassMat, argFlagMat, fraFlagMat, phaseOffset: 1.5f);

            // Endline Behind-the-Goal Stands (North & South)
            BuildStandSection(standsRoot.transform, "Stand_South", new Vector3(0f, 8.5f, -pL - 8.5f), new Vector3(PitchConstants.PitchWidth + 36f, 19f, 22f), 0f, crowdTier1Mat, crowdTier2Mat, concreteMat, bannerMat, vipGlassMat, argFlagMat, fraFlagMat, isEndline: true, facingAngle: 20f, phaseOffset: 0.7f);
            BuildStandSection(standsRoot.transform, "Stand_North", new Vector3(0f, 8.5f, pL + 8.5f), new Vector3(PitchConstants.PitchWidth + 36f, 19f, 22f), 0f, crowdTier1Mat, crowdTier2Mat, concreteMat, bannerMat, vipGlassMat, argFlagMat, fraFlagMat, isEndline: true, facingAngle: -20f, phaseOffset: 2.2f);
        }

        private static void BuildStandSection(Transform parent, string name, Vector3 pos, Vector3 size, float rotY, Material tier1Mat, Material tier2Mat, Material concreteMat, Material bannerMat, Material vipGlassMat, Material argFlagMat, Material fraFlagMat, bool isEndline = false, float facingAngle = 0f, float phaseOffset = 0f)
        {
            var standObj = new GameObject(name);
            standObj.transform.SetParent(parent, false);
            standObj.transform.position = pos;

            // Tier 1 (Lower seating bowl slope)
            var tier1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tier1.name = "LowerTier";
            tier1.transform.SetParent(standObj.transform, false);
            tier1.transform.localPosition = new Vector3(0f, -1.5f, 0f);
            tier1.transform.localScale = new Vector3(size.x * 0.55f, size.y * 0.45f, size.z * 0.90f);
            if (isEndline) tier1.transform.localRotation = Quaternion.Euler(facingAngle, 0f, 0f);
            else tier1.transform.localRotation = Quaternion.Euler(0f, 0f, rotY);
            Object.DestroyImmediate(tier1.GetComponent<Collider>());
            tier1.GetComponent<MeshRenderer>().sharedMaterial = tier1Mat;

            // Dynamic crowd cheer bobbing
            var cheer1 = tier1.AddComponent<CrowdCheerAnimator>();
            cheer1.wavePhaseOffset = phaseOffset;
            cheer1.verticalBobAmount = 0.05f;

            // Tier 2 (Upper seating bowl)
            var tier2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tier2.name = "UpperTier";
            tier2.transform.SetParent(standObj.transform, false);
            tier2.transform.localPosition = new Vector3(isEndline ? 0f : (pos.x > 0 ? 5.5f : -5.5f), 5.5f, isEndline ? (pos.z > 0 ? 5.5f : -5.5f) : 0f);
            tier2.transform.localScale = new Vector3(size.x * 0.50f, size.y * 0.55f, size.z * 0.98f);
            if (isEndline) tier2.transform.localRotation = Quaternion.Euler(facingAngle * 1.25f, 0f, 0f);
            else tier2.transform.localRotation = Quaternion.Euler(0f, 0f, rotY * 1.25f);
            Object.DestroyImmediate(tier2.GetComponent<Collider>());
            tier2.GetComponent<MeshRenderer>().sharedMaterial = tier2Mat;

            var cheer2 = tier2.AddComponent<CrowdCheerAnimator>();
            cheer2.wavePhaseOffset = phaseOffset + 0.5f;
            cheer2.verticalBobAmount = 0.07f;

            // VIP Executive Suites Row between Tiers
            var vipBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vipBox.name = "VIP_ExecutiveSuites";
            vipBox.transform.SetParent(standObj.transform, false);
            vipBox.transform.localPosition = new Vector3(isEndline ? 0f : (pos.x > 0 ? 2.5f : -2.5f), 2.2f, isEndline ? (pos.z > 0 ? 2.5f : -2.5f) : 0f);
            vipBox.transform.localScale = new Vector3(size.x * 0.40f, 1.8f, size.z * 0.92f);
            if (isEndline) vipBox.transform.localRotation = Quaternion.Euler(facingAngle * 0.5f, 0f, 0f);
            else vipBox.transform.localRotation = Quaternion.Euler(0f, 0f, rotY * 0.5f);
            Object.DestroyImmediate(vipBox.GetComponent<Collider>());
            vipBox.GetComponent<MeshRenderer>().sharedMaterial = vipGlassMat;

            // Stadium Parapet Cheering Banners along front of Tier 1
            var bannerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bannerObj.name = "CrowdBanners";
            bannerObj.transform.SetParent(standObj.transform, false);
            float bannerOffsetDist = isEndline ? (pos.z > 0 ? -size.z * 0.28f : size.z * 0.28f) : (pos.x > 0 ? -size.x * 0.28f : size.x * 0.28f);
            bannerObj.transform.localPosition = new Vector3(isEndline ? 0f : bannerOffsetDist, -2.8f, isEndline ? bannerOffsetDist : 0f);
            bannerObj.transform.localScale = new Vector3(isEndline ? size.x * 0.88f : 0.25f, 1.1f, isEndline ? 0.25f : size.z * 0.88f);
            Object.DestroyImmediate(bannerObj.GetComponent<Collider>());
            bannerObj.GetComponent<MeshRenderer>().sharedMaterial = bannerMat;

            // Dynamic Parapet Animated National & Tournament Waving Flags
            BuildParapetFlags(standObj.transform, pos, size, isEndline, bannerOffsetDist, argFlagMat, fraFlagMat);

            // Concrete stadium rim base
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "ConcreteBase";
            baseObj.transform.SetParent(standObj.transform, false);
            baseObj.transform.localPosition = new Vector3(isEndline ? 0f : (pos.x > 0 ? 8f : -8f), 2f, isEndline ? (pos.z > 0 ? 8f : -8f) : 0f);
            baseObj.transform.localScale = new Vector3(size.x, size.y, size.z);
            Object.DestroyImmediate(baseObj.GetComponent<Collider>());
            baseObj.GetComponent<MeshRenderer>().sharedMaterial = concreteMat;
        }

        private static void BuildLEDPerimeterBoards(Transform parent)
        {
            var ledRoot = new GameObject("LED_PerimeterBoards");
            ledRoot.transform.SetParent(parent, false);

            var ledTex = ProceduralTextureFactory.CreateLEDAdTexture();
            Material ledMat = CreateMaterial(Color.white, 0.9f);
            if (ledMat.HasProperty("_BaseMap")) ledMat.SetTexture("_BaseMap", ledTex);
            else if (ledMat.HasProperty("_MainTex")) ledMat.SetTexture("_MainTex", ledTex);

            if (ledMat.HasProperty("_EmissionColor"))
            {
                ledMat.EnableKeyword("_EMISSION");
                ledMat.SetTexture("_EmissionMap", ledTex);
                ledMat.SetColor("_EmissionColor", Color.white * 1.5f);
            }

            float pW = PitchConstants.HalfWidth + 1.8f;
            float pL = PitchConstants.HalfLength + 2.4f;
            float boardHeight = 0.95f;
            float boardThick = 0.18f;

            // Sidelines (East & West) - Continuous ribbons
            CreateSingleLEDBoard(ledRoot.transform, "LED_East", new Vector3(pW, boardHeight * 0.5f, 0f), new Vector3(boardThick, boardHeight, PitchConstants.PitchLength + 4.0f), ledMat, new Vector2(14f, 1f));
            CreateSingleLEDBoard(ledRoot.transform, "LED_West", new Vector3(-pW, boardHeight * 0.5f, 0f), new Vector3(boardThick, boardHeight, PitchConstants.PitchLength + 4.0f), ledMat, new Vector2(14f, 1f));

            // Endlines (South & North) - Split left and right leaving goal mouth open for depth
            float halfWidthOpen = 8.5f;
            float sideWidth = (PitchConstants.PitchWidth - halfWidthOpen * 2f) * 0.5f;
            float posX = halfWidthOpen + sideWidth * 0.5f;

            // South Behind Goal
            CreateSingleLEDBoard(ledRoot.transform, "LED_South_Left", new Vector3(-posX, boardHeight * 0.5f, -pL), new Vector3(sideWidth, boardHeight, boardThick), ledMat, new Vector2(4f, 1f));
            CreateSingleLEDBoard(ledRoot.transform, "LED_South_Right", new Vector3(posX, boardHeight * 0.5f, -pL), new Vector3(sideWidth, boardHeight, boardThick), ledMat, new Vector2(4f, 1f));

            // North Behind Goal
            CreateSingleLEDBoard(ledRoot.transform, "LED_North_Left", new Vector3(-posX, boardHeight * 0.5f, pL), new Vector3(sideWidth, boardHeight, boardThick), ledMat, new Vector2(4f, 1f));
            CreateSingleLEDBoard(ledRoot.transform, "LED_North_Right", new Vector3(posX, boardHeight * 0.5f, pL), new Vector3(sideWidth, boardHeight, boardThick), ledMat, new Vector2(4f, 1f));
        }

        private static void CreateSingleLEDBoard(Transform parent, string name, Vector3 pos, Vector3 scale, Material baseMat, Vector2 uvTiling)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = name;
            board.transform.SetParent(parent, false);
            board.transform.position = pos;
            board.transform.localScale = scale;
            Object.DestroyImmediate(board.GetComponent<Collider>());

            // Material instance with custom tiling
            Material matInstance = new Material(baseMat);
            if (matInstance.HasProperty("_BaseMap")) matInstance.SetTextureScale("_BaseMap", uvTiling);
            else if (matInstance.HasProperty("_MainTex")) matInstance.SetTextureScale("_MainTex", uvTiling);

            var mr = board.GetComponent<MeshRenderer>();
            mr.sharedMaterial = matInstance;

            // Attach dynamic electronic scroller
            var scroller = board.AddComponent<ProceduralLEDScroller>();
            scroller.scrollSpeed = 0.035f;
        }

        private static void BuildFloodlightTowers(Transform parent)
        {
            var towersRoot = new GameObject("FloodlightTowers");
            towersRoot.transform.SetParent(parent, false);

            Material trussMat = CreateMaterial(new Color(0.25f, 0.28f, 0.32f), 0.7f);
            Material bulbMat = CreateEmissiveMaterial(new Color(1f, 0.98f, 0.9f), 4.5f);
            Material beamMat = CreateVolumetricBeamMaterial(new Color(1f, 0.98f, 0.88f, 0.05f));
            Mesh beamMesh = CreateVolumetricBeamMesh(2.2f, 25.0f, 65.0f, 16);

            float cornerX = PitchConstants.HalfWidth + 24.0f;
            float cornerZ = PitchConstants.HalfLength + 24.0f;
            float towerHeight = 36.0f;

            Vector3[] towerPositions = {
                new Vector3(-cornerX, 0f, -cornerZ),
                new Vector3(cornerX, 0f, -cornerZ),
                new Vector3(-cornerX, 0f, cornerZ),
                new Vector3(cornerX, 0f, cornerZ)
            };

            for (int i = 0; i < towerPositions.Length; i++)
            {
                var pos = towerPositions[i];
                var tower = new GameObject($"FloodlightTower_{i + 1}");
                tower.transform.SetParent(towersRoot.transform, false);
                tower.transform.position = pos;

                // Main Pylon Mast
                var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mast.name = "Mast";
                mast.transform.SetParent(tower.transform, false);
                mast.transform.localPosition = new Vector3(0f, towerHeight * 0.5f, 0f);
                mast.transform.localScale = new Vector3(1.2f, towerHeight * 0.5f, 1.2f);
                Object.DestroyImmediate(mast.GetComponent<Collider>());
                mast.GetComponent<MeshRenderer>().sharedMaterial = trussMat;

                // Light Panel Gantry Head
                var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                head.name = "GantryHead";
                head.transform.SetParent(tower.transform, false);
                head.transform.localPosition = new Vector3(0f, towerHeight, 0f);
                head.transform.localScale = new Vector3(6f, 4f, 1.5f);
                head.transform.rotation = Quaternion.LookRotation(Vector3.zero - (pos + Vector3.up * towerHeight), Vector3.up);
                Object.DestroyImmediate(head.GetComponent<Collider>());
                head.GetComponent<MeshRenderer>().sharedMaterial = bulbMat;

                // Spot Light casting atmospheric stadium beam
                var spotObj = new GameObject("FloodlightBeam");
                spotObj.transform.SetParent(head.transform, false);
                spotObj.transform.localPosition = Vector3.forward * 0.5f;
                spotObj.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

                var light = spotObj.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 95f;
                light.spotAngle = 65f;
                light.innerSpotAngle = 45f;
                light.intensity = 8.5f;
                light.color = new Color(1.0f, 0.98f, 0.92f);
                light.shadows = LightShadows.None; // Save performance

                // Atmospheric Volumetric Light Shaft Cone
                var shaftObj = new GameObject("VolumetricLightShaft");
                shaftObj.transform.SetParent(head.transform, false);
                shaftObj.transform.localPosition = Vector3.forward * 1.0f;
                shaftObj.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
                var mf = shaftObj.AddComponent<MeshFilter>();
                var mr = shaftObj.AddComponent<MeshRenderer>();
                mf.sharedMesh = beamMesh;
                mr.sharedMaterial = beamMat;
            }
        }

        private static void BuildCornerFlags(Transform parent)
        {
            var flagsRoot = new GameObject("CornerFlags");
            flagsRoot.transform.SetParent(parent, false);

            Material poleMat = CreateMaterial(Color.white, 0.6f);
            Material flagClothMat = CreateMaterial(new Color(0.95f, 0.85f, 0.05f), 0.1f); // Vibrant yellow cloth

            Vector3[] cornerSpots = {
                new Vector3(-PitchConstants.HalfWidth, 0f, -PitchConstants.HalfLength),
                new Vector3(PitchConstants.HalfWidth, 0f, -PitchConstants.HalfLength),
                new Vector3(-PitchConstants.HalfWidth, 0f, PitchConstants.HalfLength),
                new Vector3(PitchConstants.HalfWidth, 0f, PitchConstants.HalfLength)
            };

            for (int i = 0; i < cornerSpots.Length; i++)
            {
                var flagObj = new GameObject($"CornerFlag_{i + 1}");
                flagObj.transform.SetParent(flagsRoot.transform, false);
                flagObj.transform.position = cornerSpots[i];

                // Flexible pole (1.5m regulation)
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(flagObj.transform, false);
                pole.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                pole.transform.localScale = new Vector3(0.05f, 0.75f, 0.05f);
                Object.DestroyImmediate(pole.GetComponent<Collider>());
                pole.GetComponent<MeshRenderer>().sharedMaterial = poleMat;

                // Triangular cloth pennant
                var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cloth.name = "Cloth";
                cloth.transform.SetParent(flagObj.transform, false);
                cloth.transform.localPosition = new Vector3(0.18f, 1.35f, 0f);
                cloth.transform.localScale = new Vector3(0.36f, 0.26f, 0.02f);
                Object.DestroyImmediate(cloth.GetComponent<Collider>());
                cloth.GetComponent<MeshRenderer>().sharedMaterial = flagClothMat;
            }
        }

        private static void BuildCanopyRoof(Transform parent)
        {
            var roofObj = new GameObject("StadiumCanopyRoof");
            roofObj.transform.SetParent(parent, false);
            roofObj.transform.position = new Vector3(0f, 22f, 0f);

            Material canopyMat = CreateMaterial(new Color(0.94f, 0.95f, 0.96f), 0.4f);

            float pW = PitchConstants.HalfWidth + 24.0f;
            float pL = PitchConstants.HalfLength + 24.0f;
            float roofThickness = 0.8f;
            float roofDepth = 22.0f;

            // East/West Overhangs
            CreateRoofPanel(roofObj.transform, new Vector3(-pW - 2f, 0f, 0f), new Vector3(roofDepth, roofThickness, PitchConstants.PitchLength + 36f), 12f, canopyMat);
            CreateRoofPanel(roofObj.transform, new Vector3(pW + 2f, 0f, 0f), new Vector3(roofDepth, roofThickness, PitchConstants.PitchLength + 36f), -12f, canopyMat);

            // North/South Overhangs
            CreateRoofPanel(roofObj.transform, new Vector3(0f, 0f, -pL - 2f), new Vector3(PitchConstants.PitchWidth + 36f, roofThickness, roofDepth), 0f, canopyMat, pitchAngle: 12f);
            CreateRoofPanel(roofObj.transform, new Vector3(0f, 0f, pL + 2f), new Vector3(PitchConstants.PitchWidth + 36f, roofThickness, roofDepth), 0f, canopyMat, pitchAngle: -12f);
        }

        private static void CreateRoofPanel(Transform parent, Vector3 pos, Vector3 scale, float rollAngle, Material mat, float pitchAngle = 0f)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "CanopyPanel";
            panel.transform.SetParent(parent, false);
            panel.transform.position = pos;
            panel.transform.localScale = scale;
            panel.transform.rotation = Quaternion.Euler(pitchAngle, 0f, rollAngle);
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void BuildPitchApron(Transform parent)
        {
            var apronObj = new GameObject("PitchApron");
            apronObj.transform.SetParent(parent, false);

            Material apronTurf = CreateMaterial(new Color(0.14f, 0.42f, 0.16f), 0.2f);
            float apronW = PitchConstants.PitchWidth + 12f;
            float apronL = PitchConstants.PitchLength + 14f;

            // Artificial turf border surrounding the pitch
            var apronPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apronPlane.name = "ApronGround";
            apronPlane.transform.SetParent(apronObj.transform, false);
            apronPlane.transform.position = new Vector3(0f, -0.05f, 0f);
            apronPlane.transform.localScale = new Vector3(apronW, 0.1f, apronL);
            Object.DestroyImmediate(apronPlane.GetComponent<Collider>());
            apronPlane.GetComponent<MeshRenderer>().sharedMaterial = apronTurf;

            // Media & Photographer materials
            Material stoolMat = CreateMaterial(new Color(0.15f, 0.15f, 0.18f), 0.5f);
            Material vestGreenMat = CreateMaterial(new Color(0.10f, 0.88f, 0.15f), 0.3f); // Neon lime media vest
            Material vestOrangeMat = CreateMaterial(new Color(1.0f, 0.42f, 0.05f), 0.3f); // Safety orange media vest
            Material cameraLensMat = CreateMaterial(new Color(0.92f, 0.92f, 0.94f), 0.8f); // Off-white telephoto lens
            Material cameraBodyMat = CreateMaterial(new Color(0.10f, 0.10f, 0.12f), 0.6f); // Dark camera body
            Material skinMat = CreateMaterial(new Color(0.85f, 0.68f, 0.54f), 0.4f);

            float goalBackZ = PitchConstants.HalfLength + 3.8f;

            // Professional Photographers seated behind both goals
            for (int i = -4; i <= 4; i++)
            {
                if (Mathf.Abs(i) <= 1) continue; // Leave central goal mouth open for depth
                Material vestMat = (i % 2 == 0) ? vestGreenMat : vestOrangeMat;
                CreatePhotographerUnit(apronObj.transform, new Vector3(i * 4.5f, 0f, -goalBackZ), 0f, stoolMat, vestMat, cameraLensMat, cameraBodyMat, skinMat);
                CreatePhotographerUnit(apronObj.transform, new Vector3(i * 4.5f, 0f, goalBackZ), 180f, stoolMat, vestMat, cameraLensMat, cameraBodyMat, skinMat);
            }

            // Sideline TV Broadcast Jib/Crane along the western touchline
            BuildBroadcastTVCamera(apronObj.transform, new Vector3(-PitchConstants.HalfWidth - 3.2f, 0f, 3.5f), cameraBodyMat, cameraLensMat, stoolMat);
        }

        private static void CreatePhotographerUnit(Transform parent, Vector3 pos, float rotY, Material stoolMat, Material vestMat, Material lensMat, Material bodyMat, Material skinMat)
        {
            var unit = new GameObject("PhotographerUnit");
            unit.transform.SetParent(parent, false);
            unit.transform.position = pos;
            unit.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            // Stool
            var stool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stool.name = "Stool";
            stool.transform.SetParent(unit.transform, false);
            stool.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            stool.transform.localScale = new Vector3(0.42f, 0.18f, 0.42f);
            Object.DestroyImmediate(stool.GetComponent<Collider>());
            stool.GetComponent<MeshRenderer>().sharedMaterial = stoolMat;

            // Seated photographer body (legs)
            var legs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            legs.name = "Pants";
            legs.transform.SetParent(unit.transform, false);
            legs.transform.localPosition = new Vector3(0f, 0.35f, 0.12f);
            legs.transform.localScale = new Vector3(0.38f, 0.32f, 0.34f);
            Object.DestroyImmediate(legs.GetComponent<Collider>());
            legs.GetComponent<MeshRenderer>().sharedMaterial = stoolMat;

            // Torso wearing neon media bib
            var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            torso.name = "MediaVest";
            torso.transform.SetParent(unit.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            torso.transform.localScale = new Vector3(0.40f, 0.45f, 0.28f);
            Object.DestroyImmediate(torso.GetComponent<Collider>());
            torso.GetComponent<MeshRenderer>().sharedMaterial = vestMat;

            // Head with cap
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(unit.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            head.transform.localScale = new Vector3(0.24f, 0.26f, 0.24f);
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

            // Monopod pole
            var monopod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            monopod.name = "Monopod";
            monopod.transform.SetParent(unit.transform, false);
            monopod.transform.localPosition = new Vector3(0f, 0.55f, 0.42f);
            monopod.transform.localScale = new Vector3(0.035f, 0.55f, 0.035f);
            Object.DestroyImmediate(monopod.GetComponent<Collider>());
            monopod.GetComponent<MeshRenderer>().sharedMaterial = stoolMat;

            // Camera Body
            var cam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cam.name = "CameraBody";
            cam.transform.SetParent(unit.transform, false);
            cam.transform.localPosition = new Vector3(0f, 0.95f, 0.35f);
            cam.transform.localScale = new Vector3(0.18f, 0.14f, 0.14f);
            Object.DestroyImmediate(cam.GetComponent<Collider>());
            cam.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // Telephoto Zoom Lens Barrel
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lens.name = "TelephotoLens";
            lens.transform.SetParent(unit.transform, false);
            lens.transform.localPosition = new Vector3(0f, 0.95f, 0.62f);
            lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lens.transform.localScale = new Vector3(0.11f, 0.22f, 0.11f);
            Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.GetComponent<MeshRenderer>().sharedMaterial = lensMat;
        }

        private static void BuildBroadcastTVCamera(Transform parent, Vector3 pos, Material bodyMat, Material lensMat, Material tripodMat)
        {
            var crane = new GameObject("TouchlineTVCrane");
            crane.transform.SetParent(parent, false);
            crane.transform.position = pos;
            crane.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            // Heavy Tripod Base
            var baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObj.name = "Tripod";
            baseObj.transform.SetParent(crane.transform, false);
            baseObj.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            baseObj.transform.localScale = new Vector3(0.8f, 0.65f, 0.8f);
            Object.DestroyImmediate(baseObj.GetComponent<Collider>());
            baseObj.GetComponent<MeshRenderer>().sharedMaterial = tripodMat;

            // Main Broadcast Studio Camera Box
            var camBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            camBox.name = "BroadcastCamera";
            camBox.transform.SetParent(crane.transform, false);
            camBox.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            camBox.transform.localScale = new Vector3(0.35f, 0.30f, 0.65f);
            Object.DestroyImmediate(camBox.GetComponent<Collider>());
            camBox.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            // Massive Optical Zoom Lens with Hood
            var bigLens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bigLens.name = "BroadcastLens";
            bigLens.transform.SetParent(camBox.transform, false);
            bigLens.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            bigLens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bigLens.transform.localScale = new Vector3(0.24f, 0.35f, 0.24f);
            Object.DestroyImmediate(bigLens.GetComponent<Collider>());
            bigLens.GetComponent<MeshRenderer>().sharedMaterial = lensMat;

            // Red Tally Broadcast Light
            var tally = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tally.name = "TallyLight";
            tally.transform.SetParent(camBox.transform, false);
            tally.transform.localPosition = new Vector3(0f, 0.20f, 0f);
            tally.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
            Object.DestroyImmediate(tally.GetComponent<Collider>());
            tally.GetComponent<MeshRenderer>().sharedMaterial = CreateEmissiveMaterial(Color.red, 3.0f);
        }

        private static void BuildTeamBenches(Transform parent)
        {
            var benchesRoot = new GameObject("TeamDugouts");
            benchesRoot.transform.SetParent(parent, false);

            float dugoutX = -PitchConstants.HalfWidth - 4.5f; // Along lateral touchline
            Material frameMat = CreateMaterial(new Color(0.18f, 0.20f, 0.24f), 0.7f);
            Material glassMat = CreateMaterial(new Color(0.7f, 0.85f, 0.95f, 0.35f), 0.9f);
            Material seatArg = CreateMaterial(new Color(0.44f, 0.72f, 0.98f), 0.4f);
            Material seatFra = CreateMaterial(new Color(0.06f, 0.10f, 0.28f), 0.4f);
            Material skinMat = CreateMaterial(new Color(0.85f, 0.68f, 0.54f), 0.4f);
            Material coachSuitMat = CreateMaterial(new Color(0.12f, 0.13f, 0.16f), 0.6f);

            // Home Dugout (Argentina) with seated squad and head coach Lionel Scaloni
            BuildSingleDugout(benchesRoot.transform, "Dugout_Argentina", new Vector3(dugoutX, 0f, -14f), frameMat, glassMat, seatArg, seatArg, skinMat, coachSuitMat);
            // Away Dugout (France) with seated squad and head coach Didier Deschamps
            BuildSingleDugout(benchesRoot.transform, "Dugout_France", new Vector3(dugoutX, 0f, 14f), frameMat, glassMat, seatFra, seatFra, skinMat, coachSuitMat);
        }

        private static void BuildSingleDugout(Transform parent, string name, Vector3 pos, Material frameMat, Material glassMat, Material seatMat, Material tracksuitMat, Material skinMat, Material coachSuitMat)
        {
            var dugout = new GameObject(name);
            dugout.transform.SetParent(parent, false);
            dugout.transform.position = pos;

            // Curved shelter canopy roof
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "ShelterRoof";
            roof.transform.SetParent(dugout.transform, false);
            roof.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            roof.transform.localScale = new Vector3(3.2f, 0.1f, 8.5f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
            Object.DestroyImmediate(roof.GetComponent<Collider>());
            roof.GetComponent<MeshRenderer>().sharedMaterial = glassMat;

            // Dugout bench platform
            var bench = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bench.name = "BenchSeats";
            bench.transform.SetParent(dugout.transform, false);
            bench.transform.localPosition = new Vector3(-0.4f, 0.55f, 0f);
            bench.transform.localScale = new Vector3(1.1f, 0.45f, 7.8f);
            Object.DestroyImmediate(bench.GetComponent<Collider>());
            bench.GetComponent<MeshRenderer>().sharedMaterial = seatMat;

            // Seated substitute players along the bench
            for (int i = 0; i < 6; i++)
            {
                float zOff = -2.8f + i * 1.12f;
                var subPlayer = new GameObject($"SubPlayer_{i + 1}");
                subPlayer.transform.SetParent(dugout.transform, false);
                subPlayer.transform.localPosition = new Vector3(-0.35f, 0.75f, zOff);

                // Seated legs
                var legs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                legs.name = "Legs";
                legs.transform.SetParent(subPlayer.transform, false);
                legs.transform.localPosition = new Vector3(0.2f, -0.15f, 0f);
                legs.transform.localScale = new Vector3(0.42f, 0.32f, 0.35f);
                Object.DestroyImmediate(legs.GetComponent<Collider>());
                legs.GetComponent<MeshRenderer>().sharedMaterial = tracksuitMat;

                // Torso in team tracksuit
                var torso = GameObject.CreatePrimitive(PrimitiveType.Cube);
                torso.name = "Torso";
                torso.transform.SetParent(subPlayer.transform, false);
                torso.transform.localPosition = new Vector3(0f, 0.22f, 0f);
                torso.transform.localScale = new Vector3(0.38f, 0.45f, 0.38f);
                Object.DestroyImmediate(torso.GetComponent<Collider>());
                torso.GetComponent<MeshRenderer>().sharedMaterial = tracksuitMat;

                // Head
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(subPlayer.transform, false);
                head.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                head.transform.localScale = new Vector3(0.24f, 0.26f, 0.24f);
                Object.DestroyImmediate(head.GetComponent<Collider>());
                head.GetComponent<MeshRenderer>().sharedMaterial = skinMat;
            }

            // Head Coach / Manager standing in the technical area
            var manager = new GameObject("HeadCoach");
            manager.transform.SetParent(dugout.transform, false);
            manager.transform.localPosition = new Vector3(2.5f, 0f, 0f);

            var mgrLegs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mgrLegs.name = "Pants";
            mgrLegs.transform.SetParent(manager.transform, false);
            mgrLegs.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            mgrLegs.transform.localScale = new Vector3(0.35f, 0.85f, 0.28f);
            Object.DestroyImmediate(mgrLegs.GetComponent<Collider>());
            mgrLegs.GetComponent<MeshRenderer>().sharedMaterial = coachSuitMat;

            var mgrTorso = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mgrTorso.name = "Jacket";
            mgrTorso.transform.SetParent(manager.transform, false);
            mgrTorso.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            mgrTorso.transform.localScale = new Vector3(0.48f, 0.60f, 0.32f);
            Object.DestroyImmediate(mgrTorso.GetComponent<Collider>());
            mgrTorso.GetComponent<MeshRenderer>().sharedMaterial = coachSuitMat;

            var mgrHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mgrHead.name = "Head";
            mgrHead.transform.SetParent(manager.transform, false);
            mgrHead.transform.localPosition = new Vector3(0f, 1.58f, 0f);
            mgrHead.transform.localScale = new Vector3(0.26f, 0.30f, 0.26f);
            Object.DestroyImmediate(mgrHead.GetComponent<Collider>());
            mgrHead.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

            // Technical Area white outline markings on the turf ground
            Material lineMat = CreateMaterial(new Color(0.92f, 0.92f, 0.94f), 0.2f);
            var techBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            techBox.name = "TechnicalAreaLine";
            techBox.transform.SetParent(dugout.transform, false);
            techBox.transform.localPosition = new Vector3(2.2f, 0.01f, 0f);
            techBox.transform.localScale = new Vector3(3.2f, 0.02f, 8.2f);
            Object.DestroyImmediate(techBox.GetComponent<Collider>());
            techBox.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
        }

        private static void BuildParapetFlags(Transform parent, Vector3 pos, Vector3 size, bool isEndline, float bannerOffsetDist, Material argMat, Material fraMat)
        {
            var flagsRoot = new GameObject("ParapetFlags");
            flagsRoot.transform.SetParent(parent, false);

            Material poleMat = CreateMaterial(new Color(0.85f, 0.88f, 0.92f), 0.8f);
            int flagCount = isEndline ? 6 : 8;
            float totalSpan = isEndline ? size.x * 0.82f : size.z * 0.82f;
            float spacing = totalSpan / (flagCount - 1);

            for (int i = 0; i < flagCount; i++)
            {
                float offset = -totalSpan * 0.5f + i * spacing;
                Vector3 flagPos = isEndline 
                    ? new Vector3(offset, -1.8f, bannerOffsetDist)
                    : new Vector3(bannerOffsetDist, -1.8f, offset);

                var flagpole = new GameObject($"FlagPole_{i + 1}");
                flagpole.transform.SetParent(flagsRoot.transform, false);
                flagpole.transform.localPosition = flagPos;

                // Base Pole
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Pole";
                pole.transform.SetParent(flagpole.transform, false);
                pole.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                pole.transform.localScale = new Vector3(0.06f, 1.4f, 0.06f);
                Object.DestroyImmediate(pole.GetComponent<Collider>());
                pole.GetComponent<MeshRenderer>().sharedMaterial = poleMat;

                // Flag Cloth
                var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cloth.name = "FlagCloth";
                cloth.transform.SetParent(flagpole.transform, false);

                Material flagMat = (i % 2 == 0) ? argMat : fraMat;
                cloth.GetComponent<MeshRenderer>().sharedMaterial = flagMat;
                Object.DestroyImmediate(cloth.GetComponent<Collider>());

                float outwardSign = isEndline ? (pos.z > 0 ? -1f : 1f) : (pos.x > 0 ? -1f : 1f);
                if (isEndline)
                {
                    cloth.transform.localPosition = new Vector3(0.7f, 2.3f, 0.1f * outwardSign);
                    cloth.transform.localScale = new Vector3(1.35f, 0.85f, 0.02f);
                }
                else
                {
                    cloth.transform.localPosition = new Vector3(0.1f * outwardSign, 2.3f, 0.7f);
                    cloth.transform.localScale = new Vector3(0.02f, 0.85f, 1.35f);
                }

                // Dynamic waving animation in stadium wind
                var waving = cloth.AddComponent<WavingFlag>();
                waving.waveSpeed = 3.8f;
                waving.waveAngle = 14.0f;
                waving.phaseOffset = i * 0.85f;
            }
        }

        public static Material CreateMaterial(Color color, float smoothness = 0.5f)
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

        public static Material CreateEmissiveMaterial(Color color, float emissionIntensity = 2.0f)
        {
            Material mat = CreateMaterial(color, 0.9f);
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emissionIntensity);
            }
            return mat;
        }

        public static Material CreateVolumetricBeamMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);

            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1.0f);
                mat.SetFloat("_Blend", 1.0f); // Additive
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return mat;
        }

        private static Mesh CreateVolumetricBeamMesh(float topRadius, float bottomRadius, float length, int segments = 16)
        {
            Mesh mesh = new Mesh();
            mesh.name = "VolumetricBeamCone";

            int numVerts = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[numVerts];
            Color[] colors = new Color[numVerts];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i] = new Vector3(cos * topRadius, sin * topRadius, 0f);
                colors[i] = new Color(1f, 1f, 1f, 0.22f);

                vertices[segments + 1 + i] = new Vector3(cos * bottomRadius, sin * bottomRadius, length);
                colors[segments + 1 + i] = new Color(1f, 1f, 1f, 0.0f);
            }

            int triIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int t0 = i;
                int t1 = i + 1;
                int b0 = segments + 1 + i;
                int b1 = segments + 1 + i + 1;

                triangles[triIndex++] = t0;
                triangles[triIndex++] = b0;
                triangles[triIndex++] = t1;

                triangles[triIndex++] = t1;
                triangles[triIndex++] = b0;
                triangles[triIndex++] = b1;
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
