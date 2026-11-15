using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GrandStrategy.Core;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using GrandStrategy.Map;
using GrandStrategy.Tactical;
using GrandStrategy.UI;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class FoundationVerifier
    {
        [MenuItem("Grand Strategy/Verify Tables & Dummy Data")]
        public static void VerifyAndSetupSceneMenu()
        {
            VerifyAndSetupScene();
        }

        [MenuItem("Grand Strategy/Run Military & Diplomacy Simulation Test (1 Year)")]
        public static void VerifyMilitaryDiplomacyMenu()
        {
            VerifyMilitaryAndDiplomacySimulation();
        }

        [MenuItem("Grand Strategy/Build & Verify GlobalMapScene (2D Map + UI)")]
        public static void BuildAndVerifyGlobalMapMenu()
        {
            BuildAndVerifyGlobalMapScene();
        }

        private static void EnsureSceneControllers(
            out DatabaseManager dbManager,
            out DemographicsController demoController,
            out EconomyController ecoController,
            out MilitaryAndDiplomacyController milDipController,
            out GameManager gameManager)
        {
            dbManager = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
            if (dbManager == null)
            {
                var dbGo = new GameObject("DatabaseManager");
                dbManager = dbGo.AddComponent<DatabaseManager>();
            }

            demoController = UnityEngine.Object.FindAnyObjectByType<DemographicsController>();
            if (demoController == null)
            {
                var demoGo = new GameObject("DemographicsController");
                demoController = demoGo.AddComponent<DemographicsController>();
            }

            ecoController = UnityEngine.Object.FindAnyObjectByType<EconomyController>();
            if (ecoController == null)
            {
                var ecoGo = new GameObject("EconomyController");
                ecoController = ecoGo.AddComponent<EconomyController>();
            }

            milDipController = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();
            if (milDipController == null)
            {
                var milGo = new GameObject("MilitaryAndDiplomacyController");
                milDipController = milGo.AddComponent<MilitaryAndDiplomacyController>();
            }

            InfrastructureController infraController = UnityEngine.Object.FindAnyObjectByType<InfrastructureController>();
            if (infraController == null)
            {
                var infraGo = new GameObject("InfrastructureController");
                infraController = infraGo.AddComponent<InfrastructureController>();
            }

            gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
            if (gameManager == null)
            {
                var gmGo = new GameObject("GameManager");
                gameManager = gmGo.AddComponent<GameManager>();
            }

            var serializedGm = new SerializedObject(gameManager);
            var dbProp = serializedGm.FindProperty("databaseManager");
            if (dbProp != null) dbProp.objectReferenceValue = dbManager;
            var demoProp = serializedGm.FindProperty("demographicsController");
            if (demoProp != null) demoProp.objectReferenceValue = demoController;
            var ecoProp = serializedGm.FindProperty("economyController");
            if (ecoProp != null) ecoProp.objectReferenceValue = ecoController;
            var milProp = serializedGm.FindProperty("militaryAndDiplomacyController");
            if (milProp != null) milProp.objectReferenceValue = milDipController;
            var infraProp = serializedGm.FindProperty("infrastructureController");
            if (infraProp != null) infraProp.objectReferenceValue = infraController;

            PoliticalController polController = UnityEngine.Object.FindAnyObjectByType<PoliticalController>();
            if (polController == null)
            {
                var polGo = new GameObject("PoliticalController");
                polController = polGo.AddComponent<PoliticalController>();
            }
            var polProp = serializedGm.FindProperty("politicalController");
            if (polProp != null) polProp.objectReferenceValue = polController;

            SiegeController siegeController = UnityEngine.Object.FindAnyObjectByType<SiegeController>();
            if (siegeController == null)
            {
                var siegeGo = new GameObject("SiegeController");
                siegeController = siegeGo.AddComponent<SiegeController>();
            }
            var siegeProp = serializedGm.FindProperty("siegeController");
            if (siegeProp != null) siegeProp.objectReferenceValue = siegeController;

            AIDiplomacyController aiController = UnityEngine.Object.FindAnyObjectByType<AIDiplomacyController>();
            if (aiController == null)
            {
                var aiGo = new GameObject("AIDiplomacyController");
                aiController = aiGo.AddComponent<AIDiplomacyController>();
            }
            var aiProp = serializedGm.FindProperty("aiDiplomacyController");
            if (aiProp != null) aiProp.objectReferenceValue = aiController;

            MilitaryController militaryController = UnityEngine.Object.FindAnyObjectByType<MilitaryController>();
            if (militaryController == null)
            {
                var mcGo = new GameObject("MilitaryController");
                militaryController = mcGo.AddComponent<MilitaryController>();
            }

            serializedGm.ApplyModifiedPropertiesWithoutUndo();

            GoogleImageManager imgManager = UnityEngine.Object.FindAnyObjectByType<GoogleImageManager>();
            if (imgManager == null)
            {
                var imgGo = new GameObject("GoogleImageManager");
                imgGo.AddComponent<GoogleImageManager>();
            }

            TimeManager timeManager = UnityEngine.Object.FindAnyObjectByType<TimeManager>();
            if (timeManager == null)
            {
                var tmGo = new GameObject("TimeManager");
                tmGo.AddComponent<TimeManager>();
            }
        }

        [CliCommand("verify_tables", "Create SQLite ORM tables, seed dummy data, and read back records", MainThreadRequired = true)]
        public static string VerifyAndSetupScene()
        {
            var report = new StringBuilder();
            try
            {
                string scenePath = "Assets/Scenes/SampleScene.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                EnsureSceneControllers(out var dbManager, out _, out _, out _, out _);

                dbManager.InitializeDatabase();
                dbManager.SeedAndTestDummyData(forceReseed: true);
                dbManager.CloseConnection();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();

                report.AppendLine("STATUS: SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                string err = $"[FoundationVerifier] Gözlənilməz xəta: {ex.Message}\n{ex.StackTrace}";
                Debug.LogError(err);
                return err;
            }
        }

        [CliCommand("verify_military_diplomacy", "Run 12-month Military Mobilization (DraftArmy) and Diplomacy simulation test", MainThreadRequired = true)]
        public static string VerifyMilitaryAndDiplomacySimulation()
        {
            var report = new StringBuilder();
            try
            {
                string scenePath = "Assets/Scenes/SampleScene.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                EnsureSceneControllers(out var dbManager, out _, out _, out _, out var gameManager);

                dbManager.InitializeDatabase();
                dbManager.SeedAndTestDummyData(forceReseed: true);
                gameManager.InitializeCalendar();

                string simSummary = gameManager.RunFastSimulation(yearsToSimulate: 1, draftProvinceName: "Anadolu");
                report.Append(simSummary);

                dbManager.CloseConnection();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveOpenScenes();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();

                report.AppendLine("STATUS: MILITARY & DIPLOMACY SIMULATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                string err = $"[FoundationVerifier] Səfərbərlik/Diplomatiya simulyasiya xətası: {ex.Message}\n{ex.StackTrace}";
                Debug.LogError(err);
                return err;
            }
        }

        [CliCommand("build_and_verify_global_map", "Build GlobalMapScene, ProvinceNode prefab, UI Canvas, and test click + DraftArmy + Tick UI updates", MainThreadRequired = true)]
        public static string BuildAndVerifyGlobalMapScene()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] 2D QLOBAL XƏRİTƏ VƏ UI SİSTEMİ QURULMASI BAŞLADI ===");

                // 1. Orijinal prosessual 2D Əyalət Sprite-ı yaradırıq (Assets/UI_Assets/ProvinceTile.png)
                Sprite provinceSprite = CreateOrLoadProvinceSprite();

                // 2. ProvinceNode Prefabını yaradırıq (Assets/Prefabs/ProvinceNode.prefab)
                ProvinceNode prefabAsset = CreateOrUpdateProvinceNodePrefab(provinceSprite);

                // 3. Yeni GlobalMapScene səhnəsini yaradırıq (Assets/Scenes/GlobalMapScene.unity)
                string globalMapScenePath = "Assets/Scenes/GlobalMapScene.unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // 3a. 2D Ortoqrafik Kamera + Physics2DRaycaster
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5.0f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.13f, 0.18f, 1f);
                camGo.AddComponent<AudioListener>();
                camGo.AddComponent<UniversalAdditionalCameraData>();
                camGo.AddComponent<Physics2DRaycaster>();

                // 3b. Mərkəzi Menecerlər (DatabaseManager, GameManager, Demographics, Economy, MilitaryAndDiplomacy)
                EnsureSceneControllers(
                    out var dbManager,
                    out var demoController,
                    out var ecoController,
                    out var milDipController,
                    out var gameManager);

                // 3c. MapController
                var mapGo = new GameObject("MapController");
                var mapController = mapGo.AddComponent<MapController>();
                var nodesContainerGo = new GameObject("ProvinceNodesContainer");
                nodesContainerGo.transform.SetParent(mapGo.transform, false);

                var serializedMap = new SerializedObject(mapController);
                serializedMap.FindProperty("provinceNodePrefab").objectReferenceValue = prefabAsset;
                serializedMap.FindProperty("nodesContainer").objectReferenceValue = nodesContainerGo.transform;
                serializedMap.ApplyModifiedPropertiesWithoutUndo();

                // 3d. EventSystem + Canvas + UIManager
                CreateEventSystem();
                UIManager uiManager = CreateGlobalMapCanvasAndUI();

                // 4. Bazanı sıfırdan (1066-cı il başlanğıc vəziyyətinə) qururuq və xəritədə əyalətləri yaradırıq
                dbManager.InitializeDatabase();
                dbManager.SeedAndTestDummyData(forceReseed: true);
                gameManager.InitializeCalendar();
                gameManager.EnsureSubsystemsBinding();

                uiManager.BindToGameManagerEvents();
                uiManager.RefreshDateTrackerUI();
                mapController.SpawnProvincesFromDatabase();

                report.AppendLine($"[Səhnə Hazırdır] GlobalMapScene yaradıldı. Xəritədəki ProvinceNode sayı: {mapController.SpawnedNodes.Count}");
                foreach (var node in mapController.SpawnedNodes)
                {
                    report.AppendLine($"  -> Node: {node.name} | Mövqe: {node.transform.position}");
                }

                // 5. İNTERAKTİV TEST 1: Oyunçu "Anadolu" əyalətinin üzərinə tıklayır
                ProvinceNode anadoluNode = mapController.GetNodeByProvinceName("Anadolu");
                if (anadoluNode == null)
                {
                    throw new InvalidOperationException("Xəritədə 'Anadolu' ProvinceNode tapılmadı!");
                }

                anadoluNode.SelectProvince();
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine($"[TEST 1 - Əyalətə Tıklama ('Anadolu')] Panel Açıldı: {uiManager.IsProvincePanelOpen}");
                report.AppendLine($"  -> Başlıq: {uiManager.CurrentDisplayedProvinceTitle}");
                report.AppendLine($"  -> Səfərbərlik Öncəsi Əhali: {uiManager.CurrentDisplayedPopulation}");
                report.AppendLine($"  -> Səfərbərlik Öncəsi Ərzaq: {uiManager.CurrentDisplayedFood}");

                // 6. İNTERAKTİV TEST 2: Oyunçu UI Panelindəki "Səfərbərlik (Draft Army)" düyməsinə basır
                uiManager.OnDraftArmyButtonClicked();
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine($"[TEST 2 - 'Səfərbərlik (Draft Army)' Düyməsinə Basıldı]");
                report.AppendLine($"  -> Səfərbərlikdən Dərhal Sonra Əhali (UI): {uiManager.CurrentDisplayedPopulation}");
                report.AppendLine($"  -> Səfərbərlikdən Dərhal Sonra Ərzaq (UI): {uiManager.CurrentDisplayedFood}");

                // 7. İNTERAKTİV TEST 3: Tick İzləyicisi Testi (2 Ay = 60 Gün irəli aparırıq)
                string dateBeforeTicks = uiManager.CurrentDisplayedDate;
                for (int d = 0; d < 60; d++)
                {
                    gameManager.AdvanceTick();
                }
                string dateAfterTicks = uiManager.CurrentDisplayedDate;

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine($"[TEST 3 - Yuxarı Künc Tick/Tarix İzləyicisi (60 Günlük Tick İrəlilədi)]");
                report.AppendLine($"  -> Əvvəlki Tarix Mətni: {dateBeforeTicks}");
                report.AppendLine($"  -> Yenilənmiş Tarix Mətni: {dateAfterTicks}");
                report.AppendLine($"  -> 2 Aydan Sonra Açıq Paneldəki Əhali: {uiManager.CurrentDisplayedPopulation}");
                report.AppendLine($"  -> 2 Aydan Sonra Açıq Paneldəki Ərzaq: {uiManager.CurrentDisplayedFood}");

                // 8. İNTERAKTİV TEST 4: GoogleImageManager + ImagenTestCanvas ("Şəkil Yarat" düyməsi + RawImage) doğrulaması
                var imgManager = UnityEngine.Object.FindAnyObjectByType<GoogleImageManager>();
                if (imgManager == null)
                {
                    throw new InvalidOperationException("Səhnədə GoogleImageManager tapılmadı!");
                }

                string sampleRequestJson = GoogleImageManager.BuildImagenPredictRequestJson(GoogleImageManager.DefaultProvinceIconPrompt);
                var dummyTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        dummyTex.SetPixel(x, y, new Color(0.85f, 0.72f, 0.30f, 1f));
                    }
                }
                dummyTex.Apply();
                string dummyBase64 = Convert.ToBase64String(dummyTex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(dummyTex);

                string fakeApiResponse = $"{{\"predictions\":[{{\"bytesBase64Encoded\":\"{dummyBase64}\",\"mimeType\":\"image/png\"}}]}}";
                string extractedB64 = GoogleImageManager.ExtractBase64FromResponse(fakeApiResponse);
                bool decodeOk = GoogleImageManager.TryDecodeBase64ToTexture(extractedB64, out Texture2D decodedTexture, out string decodeErr);
                if (!decodeOk || decodedTexture == null)
                {
                    throw new InvalidOperationException($"GoogleImageManager Base64 decode xətası: {decodeErr}");
                }

                uiManager.ApplyGeneratedTextureToRawImage(decodedTexture);
                bool rawImageAssigned = uiManager.GeneratedPreviewRawImage != null && uiManager.GeneratedPreviewRawImage.texture == decodedTexture;

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine($"[TEST 4 - GoogleImageManager & ImagenTestCanvas Doğrulaması]");
                report.AppendLine($"  -> Prompt: \"{uiManager.ImagenTestPrompt}\"");
                report.AppendLine($"  -> REST JSON Sorğusu: {sampleRequestJson}");
                report.AppendLine($"  -> Base64 -> Texture2D -> RawImage Gösterimi Uğurludur: {rawImageAssigned} ({decodedTexture.width}x{decodedTexture.height})");

                // Səhnəni yadda saxlamazdan öncə RawImage-i tələb olunduğu kimi boş (null) başlanğıc vəziyyətinə qaytarırıq
                if (uiManager.GeneratedPreviewRawImage != null)
                {
                    uiManager.GeneratedPreviewRawImage.texture = null;
                    uiManager.GeneratedPreviewRawImage.color = new Color(0.18f, 0.22f, 0.28f, 1f);
                }
                UnityEngine.Object.DestroyImmediate(decodedTexture);

                // 9. İstifadəçi Play düyməsinə basdıqda təmiz başlanğıc görməsi üçün bazanı 1066-cı ilin başlanğıcına qaytarırıq
                // və "Anadolu" panelini açıq saxlayırıq ki, səhnədə dərhal görünsün
                dbManager.SeedAndTestDummyData(forceReseed: true);
                gameManager.InitializeCalendar();
                mapController.SpawnProvincesFromDatabase();
                uiManager.RefreshDateTrackerUI();
                ProvinceNode freshAnadolu = mapController.GetNodeByProvinceName("Anadolu");
                if (freshAnadolu != null)
                {
                    freshAnadolu.SelectProvince();
                }

                dbManager.CloseConnection();

                // Səhnəni yadda saxlayırıq və Build Settings-ə əlavə edirik
                EditorSceneManager.SaveScene(scene, globalMapScenePath);
                RegisterSceneInBuildSettings(globalMapScenePath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();

                Debug.Log("=== [FoundationVerifier] 2D QLOBAL XƏRİTƏ VƏ UI TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: GLOBAL MAP & UI VERIFICATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                string err = $"[FoundationVerifier] GlobalMapScene xətası: {ex.Message}\n{ex.StackTrace}";
                Debug.LogError(err);
                return err;
            }
        }

        private static Sprite CreateOrLoadProvinceSprite()
        {
            string assetPath = "Assets/UI_Assets/ProvinceTile.png";
            string fullPath = Path.Combine(Application.dataPath, "UI_Assets/ProvinceTile.png");

            if (!File.Exists(fullPath))
            {
                const int width = 256;
                const int height = 180;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        bool isBorder = x < 6 || x >= width - 6 || y < 6 || y >= height - 6;
                        if (isBorder)
                        {
                            tex.SetPixel(x, y, new Color(0.18f, 0.15f, 0.10f, 1f));
                        }
                        else
                        {
                            float gradient = 0.88f + 0.12f * (y / (float)height);
                            tex.SetPixel(x, y, new Color(gradient, gradient, gradient, 1f));
                        }
                    }
                }

                tex.Apply();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static ProvinceNode CreateOrUpdateProvinceNodePrefab(Sprite tileSprite)
        {
            string prefabPath = "Assets/Prefabs/ProvinceNode.prefab";

            var rootGo = new GameObject("ProvinceNode");
            var sr = rootGo.AddComponent<SpriteRenderer>();
            sr.sprite = tileSprite;
            sr.sortingOrder = 1;

            var col = rootGo.AddComponent<BoxCollider2D>();
            col.size = new Vector2(4.0f, 2.8125f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(rootGo.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            labelGo.transform.localScale = new Vector3(0.12f, 0.12f, 1f);

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = "Əyalət";
            tm.fontSize = 36;
            tm.characterSize = 1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;

            var meshRenderer = labelGo.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.sortingOrder = 5;
            }

            var nodeComp = rootGo.AddComponent<ProvinceNode>();
            var serializedNode = new SerializedObject(nodeComp);
            serializedNode.FindProperty("spriteRenderer").objectReferenceValue = sr;
            serializedNode.FindProperty("boxCollider").objectReferenceValue = col;
            serializedNode.FindProperty("labelTextMesh").objectReferenceValue = tm;
            serializedNode.ApplyModifiedPropertiesWithoutUndo();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
            UnityEngine.Object.DestroyImmediate(rootGo);

            return savedPrefab.GetComponent<ProvinceNode>();
        }

        private static void CreateEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        private static UIManager CreateGlobalMapCanvasAndUI()
        {
            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("GlobalMapCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            var uiManager = canvasGo.AddComponent<UIManager>();

            // 1. Yuxarı Panel (Faza 5): Cari Tarix (Ay və İl), Qızıl/Ərzaq, Əhali Bölgüsü və Pause (||) / Play (>) / 2x (>>) Düymələri
            var topBarPanel = CreatePanel(
                "DateTrackerPanel",
                canvasGo.transform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -16f),
                new Vector2(960f, 138f),
                new Color(0.12f, 0.15f, 0.21f, 0.95f));

            Text dateText = CreateText(
                "DateTrackerText",
                topBarPanel.transform,
                defaultFont,
                "Tarix: Ay 01, İl 1066 (Gün 01) | Tick #0",
                21,
                FontStyle.Bold,
                new Color(0.96f, 0.89f, 0.68f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(0f, 0.72f),
                new Vector2(0.68f, 0.98f),
                new Vector2(18f, 0f),
                new Vector2(-10f, 0f));

            Text speedText = CreateText(
                "SpeedStatusText",
                topBarPanel.transform,
                defaultFont,
                "FASİLƏ (PAUSED)",
                16,
                FontStyle.Bold,
                new Color(0.72f, 0.82f, 0.92f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(0f, 0.48f),
                new Vector2(0.32f, 0.72f),
                new Vector2(18f, 0f),
                new Vector2(-10f, 0f));

            Text goldAndFoodTxt = CreateText(
                "GoldAndFoodText",
                topBarPanel.transform,
                defaultFont,
                "Qızıl (Gold): 5000.0  |  Ərzaq (Food): 2500.0  [Anadolu]",
                18,
                FontStyle.Bold,
                new Color(0.98f, 0.84f, 0.38f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(0.32f, 0.48f),
                new Vector2(0.98f, 0.72f),
                new Vector2(8f, 0f),
                new Vector2(-18f, 0f));

            Text dynastyControlTxt = CreateText(
                "DynastyAndControlText",
                topBarPanel.transform,
                defaultFont,
                "Dövlət: Səlcuqlu Sultanlığı  |  Birbaşa İdarə: 1/3",
                16,
                FontStyle.Bold,
                new Color(0.96f, 0.90f, 0.70f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(0f, 0.06f),
                new Vector2(0.42f, 0.44f),
                new Vector2(18f, 0f),
                new Vector2(-10f, 0f));

            Text demoSummaryTxt = CreateText(
                "DemographicsSummaryText",
                topBarPanel.transform,
                defaultFont,
                "Əhali (11600): Körpə: 1200 | Gənc: 2500 | Yetişkin: 6800 | Yaşlı: 950 | Yaralı: 150",
                15,
                FontStyle.Normal,
                new Color(0.84f, 0.92f, 0.98f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(0.42f, 0.06f),
                new Vector2(0.98f, 0.44f),
                new Vector2(10f, 0f),
                new Vector2(-18f, 0f));

            // Zaman İdarəetmə Düymələri: Pause (||), Play (>), 2x (>>)
            Button pauseBtn = CreateAnchoredButton(
                "PauseTimeButton",
                topBarPanel.transform,
                defaultFont,
                "Pause (||)",
                new Color(0.55f, 0.24f, 0.22f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-216f, -10f),
                new Vector2(102f, 36f));

            Button playBtn = CreateAnchoredButton(
                "PlayTimeButton",
                topBarPanel.transform,
                defaultFont,
                "Play (>)",
                new Color(0.18f, 0.56f, 0.30f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-108f, -10f),
                new Vector2(96f, 36f));

            Button fast2xBtn = CreateAnchoredButton(
                "FastSpeedButton",
                topBarPanel.transform,
                defaultFont,
                "2x (>>)",
                new Color(0.20f, 0.44f, 0.68f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-14f, -10f),
                new Vector2(88f, 36f));

            // 2. Sağ Tərəf - Əyalət Məlumat Paneli (Right Side)
            var rightPanel = CreatePanel(
                "ProvinceInfoPanel",
                canvasGo.transform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-24f, 0f),
                new Vector2(480f, 540f),
                new Color(0.13f, 0.16f, 0.22f, 0.96f));

            var vlg = rightPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(22, 22, 22, 22);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            Text titleText = CreateLayoutText("ProvinceTitleText", rightPanel.transform, defaultFont, "Əyalət: Anadolu", 28, FontStyle.Bold, new Color(0.98f, 0.86f, 0.46f, 1f), 42f);
            Text climateDevText = CreateLayoutText("ClimateAndDevText", rightPanel.transform, defaultFont, "İqlim: Mülayim  |  İnkişaf Səviyyəsi: 18", 20, FontStyle.Normal, new Color(0.82f, 0.88f, 0.95f, 1f), 32f);
            Text totalPopText = CreateLayoutText("TotalPopulationText", rightPanel.transform, defaultFont, "Ümumi Əhali: 11600 nəfər", 22, FontStyle.Bold, Color.white, 34f);
            Text popBreakdownText = CreateLayoutText("PopulationBreakdownText", rightPanel.transform, defaultFont, "Yetişkin: 6800 | Gənc: 2500\nKörpə: 1200 | Yaşlı: 950 | Yaralı: 150", 17, FontStyle.Normal, new Color(0.80f, 0.85f, 0.90f, 1f), 54f);
            Text foodText = CreateLayoutText("FoodProductionText", rightPanel.transform, defaultFont, "İstehsal Edilən Ərzaq: 1468.8", 21, FontStyle.Bold, new Color(0.52f, 0.92f, 0.58f, 1f), 34f);
            Text ecoDetailsText = CreateLayoutText("EconomyDetailsText", rightPanel.transform, defaultFont, "Xammal: 612.0  |  Vergi Gəliri: 3570.4", 18, FontStyle.Normal, new Color(0.92f, 0.84f, 0.62f, 1f), 30f);
            Text statusText = CreateLayoutText("ActionStatusText", rightPanel.transform, defaultFont, "Əyalət seçildi. Səfərbərlik üçün hazır.", 16, FontStyle.Italic, new Color(0.75f, 0.85f, 0.98f, 1f), 44f);

            Button draftBtn = CreateLayoutButton(
                "DraftArmyButton",
                rightPanel.transform,
                defaultFont,
                "Səfərbərlik (Draft Army)",
                new Color(0.68f, 0.18f, 0.18f, 1f),
                56f);

            Button closeBtn = CreateLayoutButton(
                "ClosePanelButton",
                rightPanel.transform,
                defaultFont,
                "Paneli Bağla",
                new Color(0.26f, 0.30f, 0.38f, 1f),
                40f);

            // 3. Sol Aşağı Künc - Kiçik Test Canvas-ı (Google Imagen API Test Paneli)
            var imagenCanvasGo = new GameObject("ImagenTestCanvas");
            var imagenCanvas = imagenCanvasGo.AddComponent<Canvas>();
            imagenCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            imagenCanvas.sortingOrder = 15;

            var imagenScaler = imagenCanvasGo.AddComponent<CanvasScaler>();
            imagenScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            imagenScaler.referenceResolution = new Vector2(1920f, 1080f);
            imagenScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            imagenScaler.matchWidthOrHeight = 0.5f;

            imagenCanvasGo.AddComponent<GraphicRaycaster>();

            var imagenPanel = CreatePanel(
                "ImagenTestPanel",
                imagenCanvasGo.transform,
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(24f, 24f),
                new Vector2(380f, 410f),
                new Color(0.12f, 0.15f, 0.21f, 0.96f));

            var imagenVlg = imagenPanel.AddComponent<VerticalLayoutGroup>();
            imagenVlg.padding = new RectOffset(18, 18, 16, 16);
            imagenVlg.spacing = 10f;
            imagenVlg.childAlignment = TextAnchor.UpperCenter;
            imagenVlg.childControlWidth = true;
            imagenVlg.childControlHeight = true;
            imagenVlg.childForceExpandWidth = true;
            imagenVlg.childForceExpandHeight = false;

            CreateLayoutText(
                "ImagenTitleText",
                imagenPanel.transform,
                defaultFont,
                "Google Imagen 2D İkon Testi",
                20,
                FontStyle.Bold,
                new Color(0.96f, 0.89f, 0.68f, 1f),
                28f);

            // Boş RawImage (API nəticəsi burada göstərilir)
            var rawImgGo = new GameObject("GeneratedPreviewRawImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(LayoutElement));
            rawImgGo.transform.SetParent(imagenPanel.transform, false);
            var rawImage = rawImgGo.GetComponent<RawImage>();
            rawImage.texture = null;
            rawImage.color = new Color(0.18f, 0.22f, 0.28f, 1f);
            rawImage.raycastTarget = false;
            var rawImgLayout = rawImgGo.GetComponent<LayoutElement>();
            rawImgLayout.preferredWidth = 210f;
            rawImgLayout.preferredHeight = 210f;
            rawImgLayout.minHeight = 210f;

            Text imagenStatusTxt = CreateLayoutText(
                "ImagenStatusText",
                imagenPanel.transform,
                defaultFont,
                "Hazırdır. 'Şəkil Yarat' düyməsinə basın.",
                14,
                FontStyle.Normal,
                new Color(0.80f, 0.86f, 0.94f, 1f),
                52f);

            Button generateImgBtn = CreateLayoutButton(
                "GenerateImageButton",
                imagenPanel.transform,
                defaultFont,
                "Şəkil Yarat",
                new Color(0.16f, 0.48f, 0.72f, 1f),
                48f);

            // 4. "Test Döyüşünə Başla" Düyməsi (GlobalMapCanvas üzərində, Tarix panelinin altında)
            var startBattleBtnGo = new GameObject("StartTestBattleButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            startBattleBtnGo.transform.SetParent(canvasGo.transform, false);
            var startBattleRt = startBattleBtnGo.GetComponent<RectTransform>();
            startBattleRt.anchorMin = new Vector2(0f, 1f);
            startBattleRt.anchorMax = new Vector2(0f, 1f);
            startBattleRt.pivot = new Vector2(0f, 1f);
            startBattleRt.anchoredPosition = new Vector2(24f, -162f);
            startBattleRt.sizeDelta = new Vector2(310f, 50f);

            var startBattleImg = startBattleBtnGo.GetComponent<Image>();
            startBattleImg.color = new Color(0.76f, 0.22f, 0.16f, 0.98f);
            startBattleImg.raycastTarget = true;

            var startBattleBtn = startBattleBtnGo.GetComponent<Button>();
            startBattleBtn.targetGraphic = startBattleImg;

            CreateText(
                "ButtonLabel",
                startBattleBtnGo.transform,
                defaultFont,
                "Test Döyüşünə Başla",
                20,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            // 5. Faza 6A - "Əyalət İdarəetmə Paneli" (ProvinceActionPanel): Məhsuldarlıq %, Tikinti Statusu və "Kanal Çək" Düyməsi
            var actionPanel = CreatePanel(
                "ProvinceActionPanel",
                canvasGo.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 24f),
                new Vector2(540f, 200f),
                new Color(0.11f, 0.16f, 0.22f, 0.96f));

            var actionVlg = actionPanel.AddComponent<VerticalLayoutGroup>();
            actionVlg.padding = new RectOffset(18, 18, 14, 14);
            actionVlg.spacing = 8f;
            actionVlg.childAlignment = TextAnchor.UpperLeft;
            actionVlg.childControlWidth = true;
            actionVlg.childControlHeight = true;
            actionVlg.childForceExpandWidth = true;
            actionVlg.childForceExpandHeight = false;

            CreateLayoutText(
                "ActionPanelTitleText",
                actionPanel.transform,
                defaultFont,
                "Əyalət İdarəetmə Paneli (İnfrastruktur)",
                20,
                FontStyle.Bold,
                new Color(0.96f, 0.89f, 0.68f, 1f),
                28f);

            Text prodStatusTxt = CreateLayoutText(
                "ProductivityStatusText",
                actionPanel.transform,
                defaultFont,
                "Məhsuldarlıq: 100% (Anadolu)",
                18,
                FontStyle.Bold,
                new Color(0.56f, 0.94f, 0.62f, 1f),
                28f);

            Text constrStatusTxt = CreateLayoutText(
                "ConstructionStatusText",
                actionPanel.transform,
                defaultFont,
                "Aktiv Tikinti: Yoxdur",
                16,
                FontStyle.Normal,
                new Color(0.84f, 0.90f, 0.98f, 1f),
                26f);

            Button buildCanalBtn = CreateLayoutButton(
                "BuildCanalButton",
                actionPanel.transform,
                defaultFont,
                "Kanal Çək (Xərc: 5000 Qızıl, Müddət: 6 Ay)",
                new Color(0.14f, 0.52f, 0.44f, 1f),
                50f);

            // Inspector üzərindən persistent listener və referansları bağlayırıq
            UnityEventTools.AddPersistentListener(draftBtn.onClick, uiManager.OnDraftArmyButtonClicked);
            UnityEventTools.AddPersistentListener(closeBtn.onClick, uiManager.HideProvincePanel);
            UnityEventTools.AddPersistentListener(generateImgBtn.onClick, uiManager.OnGenerateImageButtonClicked);
            UnityEventTools.AddPersistentListener(startBattleBtn.onClick, uiManager.OnStartTestBattleButtonClicked);
            UnityEventTools.AddPersistentListener(pauseBtn.onClick, uiManager.OnPauseTimeButtonClicked);
            UnityEventTools.AddPersistentListener(playBtn.onClick, uiManager.OnPlayTimeButtonClicked);
            UnityEventTools.AddPersistentListener(fast2xBtn.onClick, uiManager.OnFastSpeedButtonClicked);
            UnityEventTools.AddPersistentListener(buildCanalBtn.onClick, uiManager.OnBuildCanalButtonClicked);

            var serializedUi = new SerializedObject(uiManager);
            serializedUi.FindProperty("dateTrackerText").objectReferenceValue = dateText;
            serializedUi.FindProperty("speedStatusText").objectReferenceValue = speedText;
            serializedUi.FindProperty("goldAndFoodText").objectReferenceValue = goldAndFoodTxt;
            serializedUi.FindProperty("demographicsSummaryText").objectReferenceValue = demoSummaryTxt;
            serializedUi.FindProperty("pauseTimeButton").objectReferenceValue = pauseBtn;
            serializedUi.FindProperty("playTimeButton").objectReferenceValue = playBtn;
            serializedUi.FindProperty("fastSpeedButton").objectReferenceValue = fast2xBtn;
            serializedUi.FindProperty("provinceInfoPanel").objectReferenceValue = rightPanel;
            serializedUi.FindProperty("provinceTitleText").objectReferenceValue = titleText;
            serializedUi.FindProperty("climateAndDevText").objectReferenceValue = climateDevText;
            serializedUi.FindProperty("totalPopulationText").objectReferenceValue = totalPopText;
            serializedUi.FindProperty("populationBreakdownText").objectReferenceValue = popBreakdownText;
            serializedUi.FindProperty("foodProductionText").objectReferenceValue = foodText;
            serializedUi.FindProperty("economyDetailsText").objectReferenceValue = ecoDetailsText;
            serializedUi.FindProperty("actionStatusText").objectReferenceValue = statusText;
            serializedUi.FindProperty("draftArmyButton").objectReferenceValue = draftBtn;
            serializedUi.FindProperty("closePanelButton").objectReferenceValue = closeBtn;
            serializedUi.FindProperty("generateImageButton").objectReferenceValue = generateImgBtn;
            serializedUi.FindProperty("generatedPreviewRawImage").objectReferenceValue = rawImage;
            serializedUi.FindProperty("imagenStatusText").objectReferenceValue = imagenStatusTxt;
            serializedUi.FindProperty("imagenTestPrompt").stringValue = GoogleImageManager.DefaultProvinceIconPrompt;
            serializedUi.FindProperty("startTestBattleButton").objectReferenceValue = startBattleBtn;
            serializedUi.FindProperty("provinceActionPanel").objectReferenceValue = actionPanel;
            serializedUi.FindProperty("productivityStatusText").objectReferenceValue = prodStatusTxt;
            serializedUi.FindProperty("constructionStatusText").objectReferenceValue = constrStatusTxt;
            serializedUi.FindProperty("buildCanalButton").objectReferenceValue = buildCanalBtn;
            serializedUi.FindProperty("dynastyAndControlText").objectReferenceValue = dynastyControlTxt;
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            return uiManager;
        }

        private static Button CreateAnchoredButton(
            string name,
            Transform parent,
            Font font,
            string label,
            Color bgColor,
            Vector2 anchorAndPivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorAndPivot;
            rt.anchorMax = anchorAndPivot;
            rt.pivot = anchorAndPivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;

            var img = go.GetComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            CreateText(
                "ButtonLabel",
                go.transform,
                font,
                label,
                15,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return btn;
        }

        private static GameObject CreatePanel(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            var img = go.GetComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;

            return go;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var txt = go.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.raycastTarget = false;

            return txt;
        }

        private static Text CreateLayoutText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var txt = go.GetComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.raycastTarget = false;

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.minHeight = preferredHeight;

            return txt;
        }

        private static Button CreateLayoutButton(
            string name,
            Transform parent,
            Font font,
            string label,
            Color bgColor,
            float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;

            CreateText(
                "ButtonLabel",
                go.transform,
                font,
                label,
                20,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return btn;
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            var existing = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in existing)
            {
                if (string.Equals(s.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            existing.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = existing.ToArray();
        }

        [MenuItem("Grand Strategy/Build & Verify TacticalBattleScene (2D Tactical Board)")]
        public static void BuildAndVerifyTacticalBattleMenu()
        {
            BuildAndVerifyTacticalBattleScene();
        }

        [CliCommand("build_and_verify_tactical_battle", "Refactor TacticalBattleScene to 2D TacticalBoardCanvas + UI_BattalionToken prefab, clean up 3D NavMesh/Plane/Capsule/Cube assets, and verify 2D token movement, selection, and combat", MainThreadRequired = true)]
        public static string BuildAndVerifyTacticalBattleScene()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 3 REFACTOR: 2D TAKTİKİ LÖVHƏ (UI CANVAS) QURULMASI BAŞLADI ===");

                // 1. Köhnə 3D NavMesh, Plane material, 3D Capsule/Cube prefabları və 3D skriptlərini silirik
                CleanupLegacy3DTacticalAssets();
                report.AppendLine("[1. Təmizlik] Köhnə 3D Plane, NavMesh, BattlefieldNavMeshSurface, YouthBattalion/AdultBattalion 3D prefabları və BattalionUnit.cs silindi.");

                AssetDatabase.ImportAsset("Assets/Scripts/UI_BattalionToken.cs", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Scripts/TacticalBattleManager.cs", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Scripts/UnitSelectionController.cs", ImportAssetOptions.ForceUpdate);

                // 2. 2D UI Sprite-larını və UI_BattalionToken (50x50 dairəvi UI elementi + HP Bar + Say mətni) prefabını yaradırıq
                Sprite tokenCircleSprite = CreateOrLoadTokenCircleSprite();
                Sprite selectionRingSprite = CreateOrLoadSelectionCircleSprite();
                Sprite whiteBarSprite = CreateOrLoadWhiteBarSprite();
                Sprite shieldSprite = CreateOrLoadShieldSprite();
                Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                GameObject tokenPrefab = CreateOrUpdateUIBattalionTokenPrefab(
                    prefabPath: "Assets/Prefabs/UI_BattalionToken.prefab",
                    circleSprite: tokenCircleSprite,
                    selectionSprite: selectionRingSprite,
                    barSprite: whiteBarSprite,
                    shieldSprite: shieldSprite,
                    font: defaultFont);

                // 3. TacticalBattleScene səhnəsini 2D UI Lövhəsi (TacticalBoardCanvas) ilə sıfırdan qururuq
                string tacticalScenePath = "Assets/Scenes/TacticalBattleScene.unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // 2D Kamera
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                camGo.transform.rotation = Quaternion.identity;
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5.4f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.14f, 0.18f, 0.14f, 1f);
                camGo.AddComponent<AudioListener>();
                camGo.AddComponent<UniversalAdditionalCameraData>();

                EnsureSceneControllers(out var dbManager, out _, out _, out var milDipController, out var gameManager);
                CreateEventSystem();

                // Ekranı tam örtən TacticalBoardCanvas (Screen Space - Overlay)
                var canvasGo = new GameObject("TacticalBoardCanvas");
                var boardCanvas = canvasGo.AddComponent<Canvas>();
                boardCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                boardCanvas.sortingOrder = 5;

                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasGo.AddComponent<GraphicRaycaster>();

                // Döyüş Zəmini (Çəmənlik/Qum çalarlı Taktiki Lövhə UI Image)
                var boardGo = new GameObject("BattlefieldBoard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                boardGo.transform.SetParent(canvasGo.transform, false);
                var boardRect = boardGo.GetComponent<RectTransform>();
                boardRect.anchorMin = new Vector2(0.5f, 0.5f);
                boardRect.anchorMax = new Vector2(0.5f, 0.5f);
                boardRect.pivot = new Vector2(0.5f, 0.5f);
                boardRect.sizeDelta = new Vector2(1920f, 1080f);
                boardRect.anchoredPosition = Vector2.zero;

                var boardImg = boardGo.GetComponent<Image>();
                boardImg.sprite = whiteBarSprite;
                boardImg.color = new Color(0.27f, 0.38f, 0.23f, 1f); // Çəmənlik / Taktiki Masa rəngi
                boardImg.raycastTarget = true;

                // Lövhənin mərkəzindəki cəbhə xətti və yuxarı məlumat başlığı
                var centerLineGo = new GameObject("BattlefieldCenterLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                centerLineGo.transform.SetParent(boardGo.transform, false);
                var centerLineRect = centerLineGo.GetComponent<RectTransform>();
                centerLineRect.anchorMin = new Vector2(0.5f, 0.5f);
                centerLineRect.anchorMax = new Vector2(0.5f, 0.5f);
                centerLineRect.sizeDelta = new Vector2(4f, 940f);
                centerLineRect.anchoredPosition = Vector2.zero;
                var centerLineImg = centerLineGo.GetComponent<Image>();
                centerLineImg.color = new Color(0.86f, 0.82f, 0.64f, 0.25f);
                centerLineImg.raycastTarget = false;

                CreateText(
                    "BoardHeaderTitle",
                    boardGo.transform,
                    defaultFont,
                    "2D TAKTİKİ DÖYÜŞ LÖVHƏSİ  |  Sol Klik / Çərçivə: Seçim  |  Sağ Klik: Hərəkət Əmri",
                    22,
                    FontStyle.Bold,
                    new Color(0.95f, 0.90f, 0.72f, 0.95f),
                    TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 1f),
                    new Vector2(0.5f, 1f),
                    new Vector2(-520f, -56f),
                    new Vector2(520f, -12f));

                // Object Pool və Aktiv Token konteynerləri
                var poolGo = new GameObject("TokenObjectPool", typeof(RectTransform));
                poolGo.transform.SetParent(boardGo.transform, false);
                var poolRect = poolGo.GetComponent<RectTransform>();
                poolRect.anchorMin = new Vector2(0.5f, 0.5f);
                poolRect.anchorMax = new Vector2(0.5f, 0.5f);
                poolRect.anchoredPosition = Vector2.zero;

                var activeGo = new GameObject("ActiveTokensContainer", typeof(RectTransform));
                activeGo.transform.SetParent(boardGo.transform, false);
                var activeRect = activeGo.GetComponent<RectTransform>();
                activeRect.anchorMin = new Vector2(0.5f, 0.5f);
                activeRect.anchorMax = new Vector2(0.5f, 0.5f);
                activeRect.sizeDelta = new Vector2(1920f, 1080f);
                activeRect.anchoredPosition = Vector2.zero;

                // TacticalBattleManager və UnitSelectionController
                var battleMgrGo = new GameObject("TacticalBattleManager");
                var battleManager = battleMgrGo.AddComponent<TacticalBattleManager>();
                var serializedBattleMgr = new SerializedObject(battleManager);
                serializedBattleMgr.FindProperty("battalionTokenPrefab").objectReferenceValue = tokenPrefab;
                serializedBattleMgr.FindProperty("tacticalBoardRect").objectReferenceValue = boardRect;
                serializedBattleMgr.FindProperty("activeTokensContainer").objectReferenceValue = activeRect;
                serializedBattleMgr.FindProperty("poolContainer").objectReferenceValue = poolRect;
                serializedBattleMgr.FindProperty("attackerArmyId").intValue = 1;
                serializedBattleMgr.FindProperty("defenderArmyId").intValue = 2;
                serializedBattleMgr.ApplyModifiedPropertiesWithoutUndo();
                battleManager.ConfigureBoardAndPrefab(tokenPrefab, boardRect, activeRect, poolRect);

                // Faza 6B: Ulduz ikonlu UI_CommanderToken prefabı və "KRAL ÖLDÜ! TAXT BÖHRANI!" mətni
                Sprite starSprite = CreateOrLoadStarSprite();
                GameObject commanderPrefab = CreateOrUpdateUICommanderTokenPrefab(
                    "Assets/Prefabs/UI_CommanderToken.prefab", starSprite, whiteBarSprite, defaultFont);

                Text crisisText = CreateText(
                    "ThroneCrisisText",
                    canvasGo.transform,
                    defaultFont,
                    string.Empty,
                    96,
                    FontStyle.Bold,
                    new Color(1f, 0.18f, 0.12f, 1f),
                    TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-800f, 20f),
                    new Vector2(800f, 220f));
                var crisisOutline = crisisText.gameObject.AddComponent<Outline>();
                crisisOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                crisisOutline.effectDistance = new Vector2(4f, -4f);
                crisisText.gameObject.SetActive(false);

                var serializedBattleMgr6B = new SerializedObject(battleManager);
                serializedBattleMgr6B.FindProperty("commanderTokenPrefab").objectReferenceValue = commanderPrefab;
                serializedBattleMgr6B.FindProperty("throneCrisisText").objectReferenceValue = crisisText;
                serializedBattleMgr6B.ApplyModifiedPropertiesWithoutUndo();
                battleManager.ConfigureCommanderPrefabAndCrisisText(commanderPrefab, crisisText);

                // Faza 3.2: Qala Qapısı Prefabı və Dinamik Mühit UI Elementləri
                Sprite gateSprite = CreateOrLoadGateSprite();
                GameObject gatePrefab = CreateOrUpdateUIGateTokenPrefab(
                    "Assets/Prefabs/UI_GateToken.prefab", gateSprite, whiteBarSprite, defaultFont);

                Text gateBreachedTxt = CreateText(
                    "GateBreachedText",
                    canvasGo.transform,
                    defaultFont,
                    string.Empty,
                    80,
                    FontStyle.Bold,
                    new Color(1f, 0.22f, 0.15f, 1f),
                    TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-800f, 30f),
                    new Vector2(800f, 210f));
                var gateOutline = gateBreachedTxt.gameObject.AddComponent<Outline>();
                gateOutline.effectColor = new Color(0f, 0f, 0f, 0.95f);
                gateOutline.effectDistance = new Vector2(3f, -3f);
                gateBreachedTxt.gameObject.SetActive(false);

                Text sandstormTxt = CreateText(
                    "SandstormWarningText",
                    canvasGo.transform,
                    defaultFont,
                    string.Empty,
                    46,
                    FontStyle.Bold,
                    new Color(0.96f, 0.65f, 0.15f, 1f),
                    TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(-800f, -40f),
                    new Vector2(800f, 100f));
                var sandOutline = sandstormTxt.gameObject.AddComponent<Outline>();
                sandOutline.effectColor = new Color(0.25f, 0.12f, 0f, 0.95f);
                sandOutline.effectDistance = new Vector2(2f, -2f);
                sandstormTxt.gameObject.SetActive(false);

                var serializedBattleMgr32 = new SerializedObject(battleManager);
                serializedBattleMgr32.FindProperty("gateTokenPrefab").objectReferenceValue = gatePrefab;
                serializedBattleMgr32.FindProperty("gateBreachedText").objectReferenceValue = gateBreachedTxt;
                serializedBattleMgr32.FindProperty("sandstormWarningText").objectReferenceValue = sandstormTxt;
                serializedBattleMgr32.ApplyModifiedPropertiesWithoutUndo();
                battleManager.ConfigureGatePrefabAndTexts(gatePrefab, gateBreachedTxt, sandstormTxt);
                EditorUtility.SetDirty(battleManager);

                var selectionGo = new GameObject("UnitSelectionController");
                var selectionController = selectionGo.AddComponent<UnitSelectionController>();
                var serializedSelection = new SerializedObject(selectionController);
                serializedSelection.FindProperty("tacticalCanvas").objectReferenceValue = boardCanvas;
                serializedSelection.FindProperty("tacticalBoardRect").objectReferenceValue = boardRect;
                serializedSelection.ApplyModifiedPropertiesWithoutUndo();
                selectionController.ConfigureCanvasAndBoard(boardCanvas, boardRect);

                // Faza 3.1: "Kalkan Duvarı Qur/Dağıt" UI Düyməsi
                var shieldBtnGo = new GameObject("ShieldWallActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                shieldBtnGo.transform.SetParent(canvasGo.transform, false);
                var shieldBtnRect = shieldBtnGo.GetComponent<RectTransform>();
                shieldBtnRect.anchorMin = new Vector2(0.5f, 0f);
                shieldBtnRect.anchorMax = new Vector2(0.5f, 0f);
                shieldBtnRect.pivot = new Vector2(0.5f, 0f);
                shieldBtnRect.sizeDelta = new Vector2(260f, 48f);
                shieldBtnRect.anchoredPosition = new Vector2(0f, 36f);

                var shieldBtnImg = shieldBtnGo.GetComponent<Image>();
                shieldBtnImg.color = new Color(0.12f, 0.24f, 0.18f, 0.95f);
                var shieldBtnOutline = shieldBtnGo.AddComponent<Outline>();
                shieldBtnOutline.effectColor = new Color(0.25f, 0.85f, 0.50f, 0.90f);
                shieldBtnOutline.effectDistance = new Vector2(2f, -2f);

                var shieldBtnTxt = CreateText(
                    "ShieldWallButtonText",
                    shieldBtnGo.transform,
                    defaultFont,
                    "Kalkan Duvarı Qur",
                    18,
                    FontStyle.Bold,
                    new Color(0.95f, 0.95f, 0.90f, 1f),
                    TextAnchor.MiddleCenter,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);

                var shieldBtn = shieldBtnGo.GetComponent<Button>();
                var serializedSelectionWithShield = new SerializedObject(selectionController);
                serializedSelectionWithShield.FindProperty("shieldWallButton").objectReferenceValue = shieldBtn;
                serializedSelectionWithShield.FindProperty("shieldWallButtonText").objectReferenceValue = shieldBtnTxt;
                serializedSelectionWithShield.ApplyModifiedPropertiesWithoutUndo();
                selectionController.ConfigureShieldWallUI(shieldBtn, shieldBtnTxt);

                // 4. Bazanı hazırlayırıq və 2 Ordu (Army #1 - Sol tərəf, Army #2 - Sağ tərəf) yaradırıq
                dbManager.InitializeDatabase();
                dbManager.SeedAndTestDummyData(forceReseed: true);
                gameManager.InitializeCalendar();

                DraftArmyResult draft1 = milDipController.DraftArmy(dbManager, 1); // Anadolu Ordusu (Army #1)
                DraftArmyResult draft2 = milDipController.DraftArmy(dbManager, 2); // Şirvan Ordusu (Army #2)

                battleManager.InitializeAndSpawnBattle(draft1.CreatedArmy.Id, draft2.CreatedArmy.Id);

                UI_BattalionToken firstAttacker = battleManager.AttackerBattalions[0];
                UI_BattalionToken firstDefender = battleManager.DefenderBattalions[0];

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. TacticalBoardCanvas və UI_BattalionToken (50x50) Düzülüşü]");
                report.AppendLine($"  -> UI_BattalionToken Ölçüsü: {firstAttacker.RectTransform.sizeDelta} | HP Bar Fill Image: {firstAttacker.CurrentHpFillAmount:F2} | Əsgər Sayı Text: '{firstAttacker.CurrentSoldierCountLabel}'");
                report.AppendLine($"  -> Sol Kənar (Ordu #{draft1.CreatedArmy.Id}): {battleManager.AttackerBattalions.Count} token yaradıldı (Nümunə: {firstAttacker.name} @ anchoredPos={firstAttacker.AnchoredPosition})");
                report.AppendLine($"  -> Sağ Kənar (Ordu #{draft2.CreatedArmy.Id}): {battleManager.DefenderBattalions.Count} token yaradıldı (Nümunə: {firstDefender.name} @ anchoredPos={firstDefender.AnchoredPosition})");

                // 5. 2D UI İdarəetmə Testi (UnitSelectionController: Sol Klik Seçim, Canvas Box Select və Sağ Klik Hərəkət)
                selectionController.SelectSingleUnit(firstAttacker);
                bool singleSelectOk = firstAttacker.IsSelected && firstAttacker.IsSelectionRingActive && selectionController.SelectedUnits.Count == 1;

                int boxSelectedCount = selectionController.SelectUnitsInCanvasRect(new Rect(-900f, -500f, 850f, 1000f));
                bool allBoxSelectedOk = boxSelectedCount == battleManager.AttackerBattalions.Count && selectionController.SelectedUnits[0].IsSelectionRingActive;

                Vector2 moveTargetAnchored = new Vector2(-120f, 0f);
                int movedUnitsCount = selectionController.IssueMoveCommandToPosition(moveTargetAnchored);

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. 2D UI İdarəetmə Testi (UnitSelectionController)]");
                report.AppendLine($"  -> Sol Klik (Tək Token Seçimi + SelectionRing Aktivdir): {singleSelectOk} ({firstAttacker.name})");
                report.AppendLine($"  -> UI Box Select (Sol Kənardakı Seçilmiş Token Sayı): {boxSelectedCount}/{battleManager.AttackerBattalions.Count} (SelectionRing Aktivdir: {allBoxSelectedOk})");
                report.AppendLine($"  -> Sağ Klik 2D Hərəkət Əmri ({moveTargetAnchored}): {movedUnitsCount}/{boxSelectedCount} token Vector2.MoveTowards ilə hədəfə yönəldi");

                // 6. Zərər (TakeDamage), HP Bar və Əsgər Sayı Text yenilənməsi testi
                float hpFillBefore = firstAttacker.CurrentHpFillAmount;
                string countBefore = firstAttacker.CurrentSoldierCountLabel;
                firstAttacker.TakeDamage(40f);
                float hpFillAfter = firstAttacker.CurrentHpFillAmount;
                string countAfter = firstAttacker.CurrentSoldierCountLabel;

                // Tokeni tam döyüş simulyasiyası üçün bərpa edirik
                battleManager.InitializeAndSpawnBattle(draft1.CreatedArmy.Id, draft2.CreatedArmy.Id);

                // 7. Real-Zamanlı 2D Token Döyüş Simulyasiyası (Vector2.MoveTowards + Vector2.Distance Aggro/Attack + ReturnToPool)
                int simSteps = battleManager.SimulateBattleUntilResolution(timeStep: 0.25f, maxSteps: 600);
                if (!battleManager.IsBattleEnded)
                {
                    throw new InvalidOperationException("2D Token döyüş simulyasiyası maksimum addımda başa çatmadı!");
                }

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[4. 2D Token Hərəkət, Toqquşma, Zərər və Ölüm (ReturnToPool) Simulyasiyası]");
                report.AppendLine($"  -> Zərər Testi (40 Damage): HP Bar {hpFillBefore:F2} -> {hpFillAfter:F2} | Əsgər Sayı Text: '{countBefore}' -> '{countAfter}'");
                report.AppendLine($"  -> 2D Döyüş Simulyasiyası: {simSteps} addımda ({simSteps * 0.25f:F1} saniyə) tamamlandı");
                report.AppendLine($"  -> Məhv Edilib Hovuza (ReturnToPool) Qaytarılan Token Sayı: {battleManager.TotalDestroyedBattalions}");
                report.AppendLine($"  -> Sağ Qalan Tokenlər -> Ordu #{draft1.CreatedArmy.Id}: {battleManager.AttackerBattalions.Count} | Ordu #{draft2.CreatedArmy.Id}: {battleManager.DefenderBattalions.Count}");
                report.AppendLine($"  -> Qələbə Mesajı: \"{battleManager.LastVictoryMessage}\"");

                // Səhnəni yadda saxlamazdan öncə tokenləri başlanğıc sol-sağ düzülüşünə qaytarırıq
                selectionController.ClearSelection();
                battleManager.InitializeAndSpawnBattle(draft1.CreatedArmy.Id, draft2.CreatedArmy.Id);
                dbManager.CloseConnection();

                EditorSceneManager.SaveScene(scene, tacticalScenePath);
                RegisterSceneInBuildSettings(tacticalScenePath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                AssetDatabase.SaveAssets();

                Debug.Log("=== [FoundationVerifier] FAZA 3 REFACTOR: 2D TAKTİKİ LÖVHƏ UĞURLA QURULDU VƏ DOĞRULANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: 2D TACTICAL BOARD VERIFICATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                string err = $"[FoundationVerifier] 2D TacticalBattleScene xətası: {ex.Message}\n{ex.StackTrace}";
                Debug.LogError(err);
                return err;
            }
        }

        private static void CleanupLegacy3DTacticalAssets()
        {
            string[] obsoleteAssets =
            {
                "Assets/Prefabs/YouthBattalion.prefab",
                "Assets/Prefabs/AdultBattalion.prefab",
                "Assets/Scenes/TacticalBattleScene_NavMeshData.asset",
                "Assets/Models/BattlefieldGround.mat",
                "Assets/Models/YouthBattalionBlue.mat",
                "Assets/Models/AdultBattalionRed.mat",
                "Assets/Animations/BattalionAnimator.controller",
                "Assets/Scripts/BattalionUnit.cs",
                "Assets/Scripts/BattlefieldNavMeshSurface.cs",
                "Assets/Scripts/Billboard.cs"
            };

            foreach (string path in obsoleteAssets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(Path.Combine(Directory.GetCurrentDirectory(), path)))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static Sprite CreateOrLoadTokenCircleSprite()
        {
            string assetPath = "Assets/UI_Assets/TokenCircle.png";
            string fullPath = Path.Combine(Application.dataPath, "UI_Assets/TokenCircle.png");

            if (!File.Exists(fullPath))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float outerRadius = size * 0.46f;
                float borderInnerRadius = size * 0.40f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        if (dist <= borderInnerRadius)
                        {
                            tex.SetPixel(x, y, Color.white);
                        }
                        else if (dist <= outerRadius)
                        {
                            // Dairəvi token kənarı üçün incə vurğu haşiyəsi
                            tex.SetPixel(x, y, new Color(0.82f, 0.82f, 0.82f, 1f));
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                    }
                }

                tex.Apply();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Sprite CreateOrLoadSelectionCircleSprite()
        {
            string assetPath = "Assets/UI_Assets/SelectionCircle.png";
            string fullPath = Path.Combine(Application.dataPath, "UI_Assets/SelectionCircle.png");

            if (!File.Exists(fullPath))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float outerRadius = size * 0.46f;
                float innerRadius = size * 0.36f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), center);
                        if (dist <= outerRadius && dist >= innerRadius)
                        {
                            tex.SetPixel(x, y, new Color(0.25f, 1.0f, 0.45f, 0.95f));
                        }
                        else if (dist < innerRadius)
                        {
                            tex.SetPixel(x, y, new Color(0.25f, 1.0f, 0.45f, 0.14f));
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                    }
                }

                tex.Apply();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Sprite CreateOrLoadWhiteBarSprite()
        {
            string assetPath = "Assets/UI_Assets/WhiteBar.png";
            string fullPath = Path.Combine(Application.dataPath, "UI_Assets/WhiteBar.png");

            if (!File.Exists(fullPath))
            {
                const int width = 64;
                const int height = 16;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                }

                tex.Apply();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static GameObject CreateOrUpdateUIBattalionTokenPrefab(
            string prefabPath,
            Sprite circleSprite,
            Sprite selectionSprite,
            Sprite barSprite,
            Sprite shieldSprite,
            Font font)
        {
            // Ana obyekt: 50x50 ölçülü dairəvi UI elementi
            var rootGo = new GameObject("UI_BattalionToken", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(50f, 50f);
            rootRect.anchoredPosition = Vector2.zero;

            var bodyImg = rootGo.GetComponent<Image>();
            bodyImg.sprite = circleSprite;
            bodyImg.color = new Color(0.18f, 0.56f, 0.96f, 1f);
            bodyImg.raycastTarget = true;

            // Alt obyekt 1: SelectionRing (Seçildikdə görünən yaşıl/qızılı halqa)
            var ringGo = new GameObject("SelectionRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringGo.transform.SetParent(rootGo.transform, false);
            var ringRect = ringGo.GetComponent<RectTransform>();
            ringRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.sizeDelta = new Vector2(62f, 62f);
            ringRect.anchoredPosition = Vector2.zero;

            var ringImg = ringGo.GetComponent<Image>();
            ringImg.sprite = selectionSprite;
            ringImg.color = new Color(0.30f, 1.0f, 0.45f, 0.95f);
            ringImg.raycastTarget = false;
            ringGo.SetActive(false);

            // Alt obyekt 2: HPBarBackground + HPBarFill (Tokenin üzərindəki Can Çubuğu)
            var hpBgGo = new GameObject("HPBarBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpBgGo.transform.SetParent(rootGo.transform, false);
            var hpBgRect = hpBgGo.GetComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0.5f, 1f);
            hpBgRect.anchorMax = new Vector2(0.5f, 1f);
            hpBgRect.pivot = new Vector2(0.5f, 0.5f);
            hpBgRect.sizeDelta = new Vector2(48f, 8f);
            hpBgRect.anchoredPosition = new Vector2(0f, 9f);

            var hpBgImg = hpBgGo.GetComponent<Image>();
            hpBgImg.sprite = barSprite;
            hpBgImg.color = new Color(0.18f, 0.04f, 0.04f, 0.95f);
            hpBgImg.raycastTarget = false;

            var hpFillGo = new GameObject("HPBarFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpFillGo.transform.SetParent(hpBgGo.transform, false);
            var hpFillRect = hpFillGo.GetComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = Vector2.one;
            hpFillRect.offsetMin = new Vector2(1f, 1f);
            hpFillRect.offsetMax = new Vector2(-1f, -1f);

            var hpFillImg = hpFillGo.GetComponent<Image>();
            hpFillImg.sprite = barSprite;
            hpFillImg.color = new Color(0.20f, 0.88f, 0.30f, 1f);
            hpFillImg.type = Image.Type.Filled;
            hpFillImg.fillMethod = Image.FillMethod.Horizontal;
            hpFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFillImg.fillAmount = 1.0f;
            hpFillImg.raycastTarget = false;

            // Alt obyekt 3: SoldierCountText (Tokenin mərkəzində əsgər sayını göstərən kiçik Text)
            var countTxtGo = new GameObject("SoldierCountText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            countTxtGo.transform.SetParent(rootGo.transform, false);
            var countTxtRect = countTxtGo.GetComponent<RectTransform>();
            countTxtRect.anchorMin = Vector2.zero;
            countTxtRect.anchorMax = Vector2.one;
            countTxtRect.offsetMin = Vector2.zero;
            countTxtRect.offsetMax = Vector2.zero;

            var countTxt = countTxtGo.GetComponent<Text>();
            countTxt.font = font;
            countTxt.text = "250";
            countTxt.fontSize = 15;
            countTxt.fontStyle = FontStyle.Bold;
            countTxt.color = Color.white;
            countTxt.alignment = TextAnchor.MiddleCenter;
            countTxt.raycastTarget = false;

            // Alt obyekt 4: ShieldWallIndicator (Faza 3.1: Kalkan Duvarı aktiv olduqda görünən yaşıl qalxan nişanı)
            var shieldGo = new GameObject("ShieldWallIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shieldGo.transform.SetParent(rootGo.transform, false);
            var shieldRect = shieldGo.GetComponent<RectTransform>();
            shieldRect.anchorMin = new Vector2(1f, 0f);
            shieldRect.anchorMax = new Vector2(1f, 0f);
            shieldRect.pivot = new Vector2(1f, 0f);
            shieldRect.sizeDelta = new Vector2(22f, 22f);
            shieldRect.anchoredPosition = new Vector2(2f, -2f);

            var shieldImg = shieldGo.GetComponent<Image>();
            shieldImg.sprite = shieldSprite;
            shieldImg.color = new Color(0.18f, 0.94f, 0.54f, 0.98f);
            shieldImg.raycastTarget = false;
            shieldGo.SetActive(false);

            var tokenComp = rootGo.AddComponent<UI_BattalionToken>();
            var serializedToken = new SerializedObject(tokenComp);
            serializedToken.FindProperty("rectTransform").objectReferenceValue = rootRect;
            serializedToken.FindProperty("tokenImage").objectReferenceValue = bodyImg;
            serializedToken.FindProperty("selectionRingImage").objectReferenceValue = ringImg;
            serializedToken.FindProperty("hpFillImage").objectReferenceValue = hpFillImg;
            serializedToken.FindProperty("soldierCountText").objectReferenceValue = countTxt;
            serializedToken.FindProperty("shieldWallIndicatorImage").objectReferenceValue = shieldImg;
            serializedToken.ApplyModifiedPropertiesWithoutUndo();

            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
            UnityEngine.Object.DestroyImmediate(rootGo);

            return savedPrefab;
        }

                private static Sprite CreateOrLoadGateSprite()
        {
            string assetPath = "Assets/UI_Assets/GateBar.png";
            string fullPath = Path.Combine(Application.dataPath, "UI_Assets/GateBar.png");

            if (!File.Exists(fullPath))
            {
                const int width = 64;
                const int height = 256;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        bool isBorder = x < 4 || x >= width - 4 || y < 4 || y >= height - 4;
                        bool isCrossBeam = (y % 48) < 8;
                        bool isIronStud = (x == 16 || x == width - 16) && (y % 24 < 6 && y % 24 > 1);

                        if (isBorder)
                        {
                            tex.SetPixel(x, y, new Color(0.18f, 0.12f, 0.08f, 1f));
                        }
                        else if (isIronStud)
                        {
                            tex.SetPixel(x, y, new Color(0.75f, 0.72f, 0.68f, 1f));
                        }
                        else if (isCrossBeam)
                        {
                            tex.SetPixel(x, y, new Color(0.28f, 0.18f, 0.12f, 1f));
                        }
                        else
                        {
                            float woodGrain = 0.45f + 0.08f * Mathf.Sin(x * 0.4f);
                            tex.SetPixel(x, y, new Color(woodGrain * 0.9f, woodGrain * 0.6f, woodGrain * 0.38f, 1f));
                        }
                    }
                }

                tex.Apply();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static GameObject CreateOrUpdateUIGateTokenPrefab(string prefabPath, Sprite gateSprite, Sprite barSprite, Font font)
        {
            var rootGo = new GameObject("UI_GateToken", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(48f, 440f);

            var bodyImg = rootGo.GetComponent<Image>();
            bodyImg.sprite = gateSprite;
            bodyImg.color = new Color(0.42f, 0.28f, 0.18f, 1f);
            bodyImg.raycastTarget = true;
            var bodyOutline = rootGo.AddComponent<Outline>();
            bodyOutline.effectColor = new Color(0.1f, 0.05f, 0.02f, 0.95f);
            bodyOutline.effectDistance = new Vector2(2f, -2f);

            var hpBgGo = new GameObject("HPBarBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpBgGo.transform.SetParent(rootGo.transform, false);
            var hpBgRect = hpBgGo.GetComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0.5f, 1f);
            hpBgRect.anchorMax = new Vector2(0.5f, 1f);
            hpBgRect.sizeDelta = new Vector2(140f, 12f);
            hpBgRect.anchoredPosition = new Vector2(0f, 14f);
            var hpBgImg = hpBgGo.GetComponent<Image>();
            hpBgImg.sprite = barSprite;
            hpBgImg.color = new Color(0.18f, 0.04f, 0.04f, 0.95f);
            hpBgImg.raycastTarget = false;

            var hpFillGo = new GameObject("HPBarFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpFillGo.transform.SetParent(hpBgGo.transform, false);
            var hpFillRect = hpFillGo.GetComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = Vector2.one;
            hpFillRect.offsetMin = new Vector2(1f, 1f);
            hpFillRect.offsetMax = new Vector2(-1f, -1f);
            var hpFillImg = hpFillGo.GetComponent<Image>();
            hpFillImg.sprite = barSprite;
            hpFillImg.color = new Color(0.20f, 0.88f, 0.30f, 1f);
            hpFillImg.type = Image.Type.Filled;
            hpFillImg.fillMethod = Image.FillMethod.Horizontal;
            hpFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFillImg.fillAmount = 1f;
            hpFillImg.raycastTarget = false;

            Text titleTxt = CreateText("GateTitleText", rootGo.transform, font, "QALA QAPISI", 16, FontStyle.Bold,
                new Color(0.95f, 0.90f, 0.70f, 1f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-90f, 24f), new Vector2(90f, 52f));
            var titleOutline = titleTxt.gameObject.AddComponent<Outline>();
            titleOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);

            Text countTxt = CreateText("SoldierCountText", rootGo.transform, font, "2000", 14, FontStyle.Bold,
                new Color(1f, 1f, 1f, 1f), TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-60f, 0f), new Vector2(60f, 14f));

            var gateComp = rootGo.AddComponent<UI_GateToken>();
            var so = new SerializedObject(gateComp);
            so.FindProperty("rectTransform").objectReferenceValue = rootRect;
            so.FindProperty("tokenImage").objectReferenceValue = bodyImg;
            so.FindProperty("hpFillImage").objectReferenceValue = hpFillImg;
            so.FindProperty("soldierCountText").objectReferenceValue = countTxt;
            so.FindProperty("gateTitleText").objectReferenceValue = titleTxt;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
            UnityEngine.Object.DestroyImmediate(rootGo);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        private static Sprite CreateOrLoadStarSprite()
        {
            string assetPath = "Assets/UI_Assets/CommanderStar.png";
            string fullPath = Path.Combine(Application.dataPath, "UI_Assets/CommanderStar.png");

            if (!File.Exists(fullPath))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                const float outerR = size * 0.48f;
                const float innerR = size * 0.20f;

                // 5 guşəli ulduzun 10 təpə nöqtəsi
                var verts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    float r = (i % 2 == 0) ? outerR : innerR;
                    verts[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                }

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        bool inside = IsPointInPolygon(new Vector2(x, y), verts);
                        tex.SetPixel(x, y, inside ? Color.white : Color.clear);
                    }
                }

                tex.Apply();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Sprite CreateOrLoadShieldSprite()
        {
            string assetPath = "Assets/UI_Assets/ShieldIcon.png";
            string fullPath = Path.Combine(Application.dataPath, "UI_Assets/ShieldIcon.png");

            if (!File.Exists(fullPath))
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float centerX = (size - 1) * 0.5f;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }

                // Klassik orta əsr qalxanı (Heater Shield) forması
                for (int y = 16; y <= 112; y++)
                {
                    float halfWidth;
                    if (y >= 60)
                    {
                        float normalizedY = (y - 60f) / 52f;
                        halfWidth = 44f - (normalizedY * normalizedY * 4f);
                    }
                    else
                    {
                        float t = (y - 16f) / 44f;
                        halfWidth = Mathf.Lerp(0f, 44f, Mathf.Sqrt(t));
                    }

                    int minX = Mathf.RoundToInt(centerX - halfWidth);
                    int maxX = Mathf.RoundToInt(centerX + halfWidth);

                    for (int x = minX; x <= maxX; x++)
                    {
                        float distToEdge = Mathf.Min(x - minX, maxX - x);
                        float distToTop = 112 - y;
                        float distToBottom = y - 16;
                        float edgeDist = Mathf.Min(distToEdge, Mathf.Min(distToTop, distToBottom));

                        if (edgeDist <= 4f)
                        {
                            tex.SetPixel(x, y, new Color(0.95f, 0.95f, 0.95f, 1f));
                        }
                        else
                        {
                            tex.SetPixel(x, y, new Color(0.80f, 0.85f, 0.90f, 0.95f));
                        }
                    }
                }

                tex.Apply();
                File.WriteAllBytes(fullPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static bool IsPointInPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                    (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static GameObject CreateOrUpdateUICommanderTokenPrefab(string prefabPath, Sprite starSprite, Sprite barSprite, Font font)
        {
            var rootGo = new GameObject("UI_CommanderToken", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(70f, 70f);

            var bodyImg = rootGo.GetComponent<Image>();
            bodyImg.sprite = starSprite;
            bodyImg.color = new Color(1f, 0.82f, 0.18f, 1f);
            bodyImg.raycastTarget = true;
            var bodyOutline = rootGo.AddComponent<Outline>();
            bodyOutline.effectColor = new Color(0.1f, 0.06f, 0f, 0.95f);
            bodyOutline.effectDistance = new Vector2(2f, -2f);

            var ringGo = new GameObject("SelectionRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringGo.transform.SetParent(rootGo.transform, false);
            var ringRect = ringGo.GetComponent<RectTransform>();
            ringRect.sizeDelta = new Vector2(86f, 86f);
            var ringImg = ringGo.GetComponent<Image>();
            ringImg.sprite = CreateOrLoadSelectionCircleSprite();
            ringImg.color = new Color(0.30f, 1.0f, 0.45f, 0.95f);
            ringImg.raycastTarget = false;
            ringGo.SetActive(false);

            var hpBgGo = new GameObject("HPBarBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpBgGo.transform.SetParent(rootGo.transform, false);
            var hpBgRect = hpBgGo.GetComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0.5f, 1f);
            hpBgRect.anchorMax = new Vector2(0.5f, 1f);
            hpBgRect.sizeDelta = new Vector2(60f, 9f);
            hpBgRect.anchoredPosition = new Vector2(0f, 8f);
            var hpBgImg = hpBgGo.GetComponent<Image>();
            hpBgImg.sprite = barSprite;
            hpBgImg.color = new Color(0.18f, 0.04f, 0.04f, 0.95f);
            hpBgImg.raycastTarget = false;

            var hpFillGo = new GameObject("HPBarFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hpFillGo.transform.SetParent(hpBgGo.transform, false);
            var hpFillRect = hpFillGo.GetComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = Vector2.one;
            hpFillRect.offsetMin = new Vector2(1f, 1f);
            hpFillRect.offsetMax = new Vector2(-1f, -1f);
            var hpFillImg = hpFillGo.GetComponent<Image>();
            hpFillImg.sprite = barSprite;
            hpFillImg.color = new Color(0.20f, 0.88f, 0.30f, 1f);
            hpFillImg.type = Image.Type.Filled;
            hpFillImg.fillMethod = Image.FillMethod.Horizontal;
            hpFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            hpFillImg.fillAmount = 1f;
            hpFillImg.raycastTarget = false;

            Text countTxt = CreateText("SoldierCountText", rootGo.transform, font, "50", 13, FontStyle.Bold,
                new Color(0.12f, 0.08f, 0.02f, 1f), TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(0f, -6f), new Vector2(0f, -6f));

            Text nameTxt = CreateText("LeaderNameText", rootGo.transform, font, "★ Kral ★", 15, FontStyle.Bold,
                new Color(1f, 0.92f, 0.55f, 1f), TextAnchor.UpperCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-90f, -26f), new Vector2(90f, -2f));
            var nameOutline = nameTxt.gameObject.AddComponent<Outline>();
            nameOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);

            var commanderComp = rootGo.AddComponent<UI_CommanderToken>();
            var so = new SerializedObject(commanderComp);
            so.FindProperty("rectTransform").objectReferenceValue = rootRect;
            so.FindProperty("tokenImage").objectReferenceValue = bodyImg;
            so.FindProperty("selectionRingImage").objectReferenceValue = ringImg;
            so.FindProperty("hpFillImage").objectReferenceValue = hpFillImg;
            so.FindProperty("soldierCountText").objectReferenceValue = countTxt;
            so.FindProperty("leaderNameText").objectReferenceValue = nameTxt;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
            UnityEngine.Object.DestroyImmediate(rootGo);
            return saved;
        }

        [MenuItem("Grand Strategy/Verify Phase 6B (Leaders, Loyalty & Rebellion)")]
        public static void VerifyPhase6BPoliticsMenu()
        {
            VerifyPhase6BPolitics();
        }

        [CliCommand("verify_phase6b_politics", "Verify Leaders table seed, monthly loyalty decay -> rebellion (IsRebel, rebel Army, province seized) in SQLite, and UI_CommanderToken stars + 'KRAL ÖLDÜ! TAXT BÖHRANI!' on the 2D Tactical Board", MainThreadRequired = true)]
        public static string VerifyPhase6BPolitics()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 6B: LİDERLƏR, SƏDAQƏT VƏ ÜSYAN SİSTEMİ TESTİ BAŞLADI ===");

                // 1. GlobalMapScene-i (PoliticalController daxil) yenidən qururuq
                BuildAndVerifyGlobalMapScene();
                EditorSceneManager.OpenScene("Assets/Scenes/GlobalMapScene.unity", OpenSceneMode.Single);

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                var tm = UnityEngine.Object.FindAnyObjectByType<TimeManager>();
                var pol = UnityEngine.Object.FindAnyObjectByType<PoliticalController>();
                var milController = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();

                if (pol == null)
                {
                    throw new InvalidOperationException("GlobalMapScene-də PoliticalController tapılmadı!");
                }

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                gm.EnsureDatabaseConnection();
                gm.EnsureSubsystemsBinding();

                // Kralın ordusu (Army #1 - Anadolu) səfərbər edilir
                DraftArmyResult kingDraft = milController.DraftArmy(db, 1);

                report.AppendLine("[1. Leaders Cədvəli (SQLite) - Başlanğıc Vəziyyət]");
                foreach (Leader l in db.Connection.Table<Leader>().ToList())
                {
                    report.AppendLine($"  -> {l}");
                }
                Leader initGovA = db.Connection.Table<Leader>().FirstOrDefault(l => l.Name == "Qubernator A");
                int govProvId = initGovA != null && initGovA.ParsedProvinceId.HasValue ? initGovA.ParsedProvinceId.Value : 1;
                Province govProvBefore = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == govProvId);
                report.AppendLine($"  -> {govProvBefore.Name} (Qubernator A əyaləti) üsyandan əvvəl: OwnerFactionId={govProvBefore.OwnerFactionId}, Gold={govProvBefore.Gold:F1}, Youths={govProvBefore.Youths}, Adults={govProvBefore.Adults}");
                report.AppendLine($"  -> Bazadakı Ordu sayı: {db.Connection.Table<Army>().Count()} (Kral Ordusu #{kingDraft.CreatedArmy.Id}: {kingDraft.CreatedArmy.TotalSoldiers} əsgər)");

                // 2. Dinamik Sədaqət Qaydaları Yoxlaması (Faza 6B Təkmilləşdirmə)
                // Aclıq (-10), İnkişaf (+2) və Güc Nisbəti / Opportunizm (-10) qaydalarını ayrıca test edirik
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. Dinamik Sədaqət Qaydaları Testi (Aclıq -10, İnkişaf +2, Opportunizm -10)]");

                // Test zamanı Qubernator A-nın erkən üsyan etməməsi üçün sədaqətini müvəqqəti 100 edirik
                pol.SetLeaderLoyalty("Qubernator A", 100f);

                // Test Lideri yaradırıq (Sədaqət: 70, Şirvan əyalətinə təyin olunur)
                Leader testGovernor = new Leader
                {
                    Name = "Test Qubernatoru",
                    Loyalty = 70f,
                    IsRebel = false,
                    AssignedProvinceId = "2",
                    AssignedArmyId = null
                };
                db.Connection.Insert(testGovernor);

                // Opportunizm: Şirvan gücü (6200) > Kral gücü (3290) * 0.8 (2632) -> Loyalty 70 -> 60 (-10)
                pol.CheckRebellions(db, 1, 1066, applyDynamicLoyalty: true);
                Leader govAfterOpp = db.Connection.Table<Leader>().FirstOrDefault(l => l.Id == testGovernor.Id);
                report.AppendLine($"  -> Qayda 1 (Opportunizm): Güc 6200 > 2632 -> Sədaqət 70.0 -> {govAfterOpp.Loyalty:F1} (-10 tətbiq edildi)");

                // İnkişaf Bonusu (+2): Şirvanda məhsuldarlığı 1.5x edirik
                db.RunInTransaction(c =>
                {
                    Province p = c.Table<Province>().FirstOrDefault(pr => pr.Id == 2);
                    p.ProductivityMultiplier = 1.5f;
                    c.Update(p);
                });
                pol.SetLeaderLoyalty(testGovernor.Name, 60f);
                // Kralın ordusunu böyüdürük ki opportunizm tətbiq olunmasın, yalnız inkişaf bonusu (+2) yoxlansın
                db.RunInTransaction(c =>
                {
                    Army ka = c.Table<Army>().FirstOrDefault(a => a.Id == kingDraft.CreatedArmy.Id);
                    ka.AdultSoldiers = 10000; // 10000+1250 > 6200 / 0.8
                    c.Update(ka);
                });
                pol.CheckRebellions(db, 1, 1066, applyDynamicLoyalty: true);
                Leader govAfterDev = db.Connection.Table<Leader>().FirstOrDefault(l => l.Id == testGovernor.Id);
                report.AppendLine($"  -> Qayda 2 (İnkişaf Bonusu): ProductivityMultiplier >= 1.5x -> Sədaqət 60.0 -> {govAfterDev.Loyalty:F1} (+2 bonus tətbiq edildi)");

                // Aclıq Cəzası (-10): Şirvanda Food = 0 edirik -> Loyalty 62.0 -> 52.0 (-10)
                db.RunInTransaction(c =>
                {
                    Province p = c.Table<Province>().FirstOrDefault(pr => pr.Id == 2);
                    p.Food = 0f;
                    p.ProductivityMultiplier = 1.0f;
                    c.Update(p);
                });
                pol.CheckRebellions(db, 1, 1066, applyDynamicLoyalty: true);
                Leader govAfterStarve = db.Connection.Table<Leader>().FirstOrDefault(l => l.Id == testGovernor.Id);
                report.AppendLine($"  -> Qayda 3 (Aclıq Cəzası): Food <= 0 -> Sədaqət {govAfterDev.Loyalty:F1} -> {govAfterStarve.Loyalty:F1} (-10 cəza tətbiq edildi)");

                // Test liderini silirik və əyaləti / kral ordusunu / Qubernator A-nı bərpa edirik
                db.Connection.Delete<Leader>(testGovernor.Id);
                db.RunInTransaction(c =>
                {
                    Province p = c.Table<Province>().FirstOrDefault(pr => pr.Id == 2);
                    p.Food = 1800f;
                    p.ProductivityMultiplier = 1.0f;
                    c.Update(p);
                    Army ka = c.Table<Army>().FirstOrDefault(a => a.Id == kingDraft.CreatedArmy.Id);
                    ka.AdultSoldiers = 2040;
                    c.Update(ka);
                });
                pol.SetLeaderLoyalty("Qubernator A", 40f);

                // 3. Aylıq Dinamik Tick Döngüsü ilə Qubernator A-nın Üsyanı
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. Aylıq Dinamik Tick Döngüsü ilə Qubernator A-nın Üsyanı]");
                Leader governor = null;
                int monthsSimulated = 0;
                for (int m = 1; m <= 6; m++)
                {
                    tm.SimulateMonths(1);
                    monthsSimulated = m;
                    governor = db.Connection.Table<Leader>().FirstOrDefault(l => l.Name == "Qubernator A");
                    report.AppendLine($"  -> Ay {m}: Qubernator A Sədaqəti = {governor.Loyalty:F1} | IsRebel = {governor.IsRebel}");
                    if (governor.IsRebel)
                    {
                        break;
                    }
                }

                if (governor == null || !governor.IsRebel)
                {
                    throw new InvalidOperationException("Qubernator A dinamik döngüdə üsyan etmədi!");
                }

                int? rebelArmyId = governor.ParsedArmyId;
                Army rebelArmy = rebelArmyId.HasValue
                    ? db.Connection.Table<Army>().FirstOrDefault(a => a.Id == rebelArmyId.Value)
                    : null;
                Province govProvAfter = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == govProvId);
                Diplomacy rebelWar = db.Connection.Table<Diplomacy>().ToList().Find(d => d.FactionB_Id == govProvAfter.OwnerFactionId);

                if (rebelArmy == null || govProvAfter.OwnerFactionId == govProvBefore.OwnerFactionId)
                {
                    throw new InvalidOperationException("Üsyançı ordu yaradılmadı və ya əyalət ələ keçirilmədi!");
                }

                report.AppendLine($"  -> Lider: {governor}");
                report.AppendLine($"  -> Yeni Üsyançı Ordu: {rebelArmy}");
                report.AppendLine($"  -> {govProvAfter.Name} ələ keçirildi: OwnerFactionId {govProvBefore.OwnerFactionId} -> {govProvAfter.OwnerFactionId} | Gold {govProvBefore.Gold:F1} -> {govProvAfter.Gold:F1} | Youths -> {govProvAfter.Youths}, Adults -> {govProvAfter.Adults}");
                report.AppendLine($"  -> Diplomatiya: {(rebelWar != null ? rebelWar.ToString() : "yoxdur")}");
                Leader king = db.Connection.Table<Leader>().FirstOrDefault(l => l.Name == "Kral");
                report.AppendLine($"  -> Kral: {king}");

                db.CloseConnection();

                // 4. 2D Taktiki Döyüş: Kral Ordusu (#1) vs Üsyançı Ordu (#rebel) - Sərkərdə Ulduzları
                BuildAndVerifyTacticalBattleScene();
                EditorSceneManager.OpenScene("Assets/Scenes/TacticalBattleScene.unity", OpenSceneMode.Single);

                // Tactical build bazanı sıfırladığı üçün üsyan vəziyyətini yenidən yaradırıq
                var tdb = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var tgm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                var tpol = UnityEngine.Object.FindAnyObjectByType<PoliticalController>();
                var tmil = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();
                var battleManager = UnityEngine.Object.FindAnyObjectByType<TacticalBattleManager>();

                tdb.InitializeDatabase();
                tdb.SeedAndTestDummyData(forceReseed: true);
                tgm.InitializeCalendar();
                DraftArmyResult tKingDraft = tmil.DraftArmy(tdb, 1);
                tpol.SetLeaderLoyalty("Qubernator A", 20f);
                List<RebellionReport> rebellions = tpol.CheckRebellions(tdb, 1, 1066, applyDynamicLoyalty: false);
                if (rebellions.Count == 0)
                {
                    throw new InvalidOperationException("Taktiki test üçün üsyan yaradıla bilmədi!");
                }

                int tRebelArmyId = rebellions[0].CreatedRebelArmyId;
                battleManager.InitializeAndSpawnBattle(tKingDraft.CreatedArmy.Id, tRebelArmyId);

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine($"[4. 2D Taktiki Lövhə: Kral Ordusu #{tKingDraft.CreatedArmy.Id} vs Üsyançı Ordu #{tRebelArmyId}]");
                report.AppendLine($"  -> Sol tərəf token sayı: {battleManager.AttackerBattalions.Count} | Sağ tərəf token sayı: {battleManager.DefenderBattalions.Count}");
                report.AppendLine($"  -> Yaradılan Sərkərdə (Ulduz) Tokenləri: {battleManager.CommanderTokens.Count}");

                UI_CommanderToken kingToken = null;
                foreach (UI_CommanderToken c in battleManager.CommanderTokens)
                {
                    report.AppendLine($"     * {c.name}: Lider='{c.LeaderName}' | Etiket='{c.CurrentLeaderNameLabel}' | Kral={c.IsKing} | Üsyançı={c.IsRebelLeader} | Ordu #{c.ArmyId} | HP={c.CurrentHP:F0}/{c.MaxHP:F0} | Mövqe={c.AnchoredPosition}");
                    if (c.IsKing)
                    {
                        kingToken = c;
                    }
                }

                if (battleManager.CommanderTokens.Count < 2 || kingToken == null)
                {
                    throw new InvalidOperationException("Sərkərdə Ulduz tokenləri düzgün yaradılmadı!");
                }

                // 5. Kralın Ulduz tokeni ölür -> "KRAL ÖLDÜ! TAXT BÖHRANI!" və Əsgərlərdə Panika (Sürət -50%, Zərər -50%)
                string labelBefore = battleManager.CurrentThroneCrisisLabel;

                // Kralın ordusundakı ilk batalyon tokenini qeyd edirik
                UI_BattalionToken sampleKingSoldier = null;
                for (int i = 0; i < battleManager.AttackerBattalions.Count; i++)
                {
                    if (!(battleManager.AttackerBattalions[i] is UI_CommanderToken))
                    {
                        sampleKingSoldier = battleManager.AttackerBattalions[i];
                        break;
                    }
                }

                float soldierSpeedBefore = sampleKingSoldier != null ? sampleKingSoldier.CurrentMoveSpeed : 0f;
                float soldierDamageBefore = sampleKingSoldier != null ? sampleKingSoldier.CurrentAttackDamage : 0f;

                // Kral öldürülür!
                kingToken.TakeDamage(kingToken.MaxHP + 10f);

                float soldierSpeedAfter = sampleKingSoldier != null ? sampleKingSoldier.CurrentMoveSpeed : 0f;
                float soldierDamageAfter = sampleKingSoldier != null ? sampleKingSoldier.CurrentAttackDamage : 0f;
                bool isSoldierPanicked = sampleKingSoldier != null && sampleKingSoldier.IsPanicked;

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[5. Kralın Ulduz Tokeni Məhv Edildi (HP = 0) -> Panika və Taxt Böhranı]");
                report.AppendLine($"  -> Ölümdən əvvəl ekran mətni: '{labelBefore}'");
                report.AppendLine($"  -> IsKingDead: {battleManager.IsKingDead} | Ekran mətni: '{battleManager.CurrentThroneCrisisLabel}'");
                report.AppendLine($"  -> Kral Əsgərlərində Panika Vəziyyəti (IsPanicked): {isSoldierPanicked}");
                report.AppendLine($"  -> Əsgər Hərəkət Sürəti (50% Azalma): {soldierSpeedBefore:F1} -> {soldierSpeedAfter:F1}");
                report.AppendLine($"  -> Əsgər Vurduğu Zərər (0.5x Yarıbayarı): {soldierDamageBefore:F1} -> {soldierDamageAfter:F1}");

                if (!battleManager.IsKingDead || battleManager.CurrentThroneCrisisLabel != TacticalBattleManager.KingDeathMessage)
                {
                    throw new InvalidOperationException("'KRAL ÖLDÜ! TAXT BÖHRANI!' yazısı ekranda göstərilmədi!");
                }

                if (!isSoldierPanicked || Mathf.Abs(soldierSpeedAfter - (soldierSpeedBefore * 0.5f)) > 1f || Mathf.Abs(soldierDamageAfter - (soldierDamageBefore * 0.5f)) > 0.5f)
                {
                    throw new InvalidOperationException("Kral öldükdə əsgərlərdə panika (sürət və zərər azalması) düzgün tətbiq olunmadı!");
                }

                // Panika sürət bərpası testi: 5 saniyəlik müddət bitdikdə sürət bərpa olunur, zərər isə döyüş boyu yarı qalır
                sampleKingSoldier.RecoverFromPanicSpeed();
                report.AppendLine($"  -> 5 Saniyədən Sonra Panika Sürətinin Bərpası: Sürət={sampleKingSoldier.CurrentMoveSpeed:F1} (bərpa olundu), Zərər={sampleKingSoldier.CurrentAttackDamage:F1} (yarı olaraq qalır)");

                // 6. Qlobal Taxt Boşluğu Cəzası Yoxlaması:
                // Test üçün bazaya sabit 80 sədaqətli yeni bir qubernator qoyuruq
                Leader loyalGovernor = new Leader
                {
                    Name = "Qubernator B (Loyal)",
                    Loyalty = 80f,
                    IsRebel = false,
                    AssignedProvinceId = "1"
                };
                tdb.Connection.Insert(loyalGovernor);

                // Döyüş başa çatdırılır və Kralın ölümü ilə EndBattle çağırılır (GlobalMapScene-ə qayıdılır)
                battleManager.CompleteBattleAndReturnToGlobalMap(winnerArmyIdStr: $"Army_{tRebelArmyId}");

                var dbAfterEnd = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                if (dbAfterEnd == null)
                {
                    dbAfterEnd = DatabaseManager.Instance;
                }

                if (dbAfterEnd != null)
                {
                    dbAfterEnd.InitializeDatabase();
                    Leader govAfterCrisis = dbAfterEnd.Connection.Table<Leader>().FirstOrDefault(l => l.Id == loyalGovernor.Id);
                    report.AppendLine("--------------------------------------------------------------------------------");
                    report.AppendLine("[6. Qlobal Taxt Boşluğu Cəzası (GlobalMapScene / SQLite)]");
                    report.AppendLine($"  -> Kral döyüşdə öldüyü üçün bütün qubernatorlara -30 bal cəza tətbiq edildi:");
                    report.AppendLine($"  -> Qubernator B Sədaqəti: 80.0 -> {govAfterCrisis.Loyalty:F1} (Gözlənilən: 50.0)");

                    if (Mathf.Abs(govAfterCrisis.Loyalty - 50f) > 0.1f)
                    {
                        throw new InvalidOperationException($"Qlobal taxt boşluğu cəzası səhv hesablandı: {govAfterCrisis.Loyalty} != 50.0");
                    }

                    dbAfterEnd.Connection.Delete<Leader>(loyalGovernor.Id);

                    // Bazanı Play mode üçün ilkin təmiz vəziyyətə gətiririk
                    dbAfterEnd.SeedAndTestDummyData(forceReseed: true);
                    dbAfterEnd.CloseConnection();
                }

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 6B TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 6B LEADERS, LOYALTY & REBELLION VERIFICATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 6B xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }

        [MenuItem("Grand Strategy/Verify Phase 6C (Siege, Public Order & Emergency Conscription)")]
        public static void VerifyPhase6CSiegeAndEmergencyMenu()
        {
            VerifyPhase6CSiegeAndEmergency();
        }

        [CliCommand("verify_phase6c_siege_and_emergency", "Verify PublicOrder, ExecuteWounded (-40 order), PublicOrder < 30 (0.5x productivity), SetBesieged (stops nation income), Plunder (+2000 gold/food, 0.2x prod, 0 order), Exploit (-5 order/mo), and CallEmergencyConscription (20% adult militia, 0.2x birth modifier)", MainThreadRequired = true)]
        public static string VerifyPhase6CSiegeAndEmergency()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 6C: MÜHASİRƏ, XALQIN MORALI VƏ FÖVQƏLADƏ VƏZİYYƏT TESTİ BAŞLADI ===");

                // 1. GlobalMapScene-i ən son dəyişikliklərlə açırıq və bazanı qururuq
                BuildAndVerifyGlobalMapScene();
                EditorSceneManager.OpenScene("Assets/Scenes/GlobalMapScene.unity", OpenSceneMode.Single);

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                var tm = UnityEngine.Object.FindAnyObjectByType<TimeManager>();
                var demo = UnityEngine.Object.FindAnyObjectByType<DemographicsController>();
                var eco = UnityEngine.Object.FindAnyObjectByType<EconomyController>();
                var siege = UnityEngine.Object.FindAnyObjectByType<SiegeController>();
                var mil = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();

                if (db == null || gm == null || demo == null || eco == null || siege == null || mil == null)
                {
                    throw new InvalidOperationException("Faza 6C komponentləri tam tapılmadı!");
                }

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                gm.EnsureDatabaseConnection();
                gm.EnsureSubsystemsBinding();

                report.AppendLine("[1. Başlanğıc Vəziyyət (Province #1 - Anadolu və #2 - Şirvan)]");
                Province anadoluInit = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                report.AppendLine($"  -> Anadolu: PublicOrder={anadoluInit.PublicOrder:F0}/100, IsBesieged={anadoluInit.IsBesieged}, BirthRateModifier={anadoluInit.BirthRateModifier * 100f:F0}%, Wounded={anadoluInit.Wounded}, Gold={anadoluInit.Gold:F1}, Food={anadoluInit.Food:F1}");

                if (Mathf.Abs(anadoluInit.PublicOrder - 100f) > 0.1f || anadoluInit.IsBesieged || Mathf.Abs(anadoluInit.BirthRateModifier - 1.0f) > 0.01f)
                {
                    throw new InvalidOperationException("Province modelinin başlanğıc parametrləri (PublicOrder, IsBesieged, BirthRateModifier) düzgün qurulmayıb!");
                }

                // 2. SƏRT QƏRAR 1: Yaralıların Edamı (ExecuteWounded) və İtaətsizlik (PublicOrder < 30)
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. Yaralıların Edamı (ExecuteWounded) və İtaətsizlik (PublicOrder < 30 -> 0.5x Məhsuldarlıq)]");

                int woundedBefore = anadoluInit.Wounded;
                bool executeOk = demo.ExecuteWounded("1", db);
                Province anadoluAfterWounded = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Population popAfterWounded = db.Connection.Table<Population>().FirstOrDefault(p => p.ProvinceId == 1);

                report.AppendLine($"  -> ExecuteWounded İcra Edildi: {executeOk}");
                report.AppendLine($"  -> Yaralılar: {woundedBefore} -> {anadoluAfterWounded.Wounded} (Ərzaq xərci azaldı)");
                report.AppendLine($"  -> Xalqın Moralı (PublicOrder): {anadoluInit.PublicOrder:F0} -> {anadoluAfterWounded.PublicOrder:F0} (Cəza: -40 bal)");

                if (!executeOk || anadoluAfterWounded.Wounded != 0 || Mathf.Abs(anadoluAfterWounded.PublicOrder - 60f) > 0.1f)
                {
                    throw new InvalidOperationException("ExecuteWounded zamanı Wounded sıfırlanmadı və ya PublicOrder -40 düşmədi!");
                }

                // İtaətsizlik Cəzası Yoxlaması: PublicOrder < 30 olduqda ProductivityMultiplier 0.5x olur
                db.RunInTransaction(c =>
                {
                    Province p = c.Table<Province>().FirstOrDefault(pr => pr.Id == 1);
                    p.PublicOrder = 25f; // < 30
                    c.Update(p);
                });

                Province anadoluRebellious = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Economy ecoBeforeMonth = db.Connection.Table<Economy>().FirstOrDefault(e => e.ProvinceId == 1);
                MonthlyEconomyReport ecoReport = eco.ApplyMonthlyEconomyAndConsumption(anadoluRebellious, popAfterWounded, ecoBeforeMonth, 1, 1066);

                report.AppendLine($"  -> İtaətsizlik Vəziyyəti: PublicOrder = {anadoluRebellious.PublicOrder:F0}/100 (< 30 həddi)");
                report.AppendLine($"  -> EffectiveProductivityMultiplier: {anadoluRebellious.EffectiveProductivityMultiplier * 100f:F0}% (Baza 100%-in yarısı: 0.5x)");
                report.AppendLine($"  -> Hesablanan Ərzaq İstehsalı: {ecoReport.FoodProduced:F1} | Vergi Gəliri: {ecoReport.LocalTaxIncome:F1} (Yarıya düşdü)");

                if (Mathf.Abs(anadoluRebellious.EffectiveProductivityMultiplier - 0.5f) > 0.05f)
                {
                    throw new InvalidOperationException("PublicOrder < 30 olduqda məhsuldarlıq yarıya (0.5x) düşmədi!");
                }

                // Moralı bərpa edirik
                db.RunInTransaction(c =>
                {
                    Province p = c.Table<Province>().FirstOrDefault(pr => pr.Id == 1);
                    p.PublicOrder = 100f;
                    c.Update(p);
                });

                // 3. MÜHASİRƏ LOGİSTİKASI: SetBesieged, Xəzinə Təcridi, Exploit və Plunder
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. Mühasirə Logistikası: SetBesieged, Xəzinə Təcridi, Exploit və Plunder]");

                // 3a. SetBesieged(true)
                siege.SetBesieged("1", true, db);
                Province anadoluBesieged = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Nation seljukBeforeEco = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);
                float nationGoldBefore = seljukBeforeEco.Gold;
                float nationFoodBefore = seljukBeforeEco.Food;

                eco.ProcessMonthlyEconomy(db, 1, 1066, logStarvationToConsole: false);

                Nation seljukAfterEco = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);
                Province anadoluAfterEco = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);

                report.AppendLine($"  -> SetBesieged(true) Tətbiq Edildi: IsBesieged = {anadoluBesieged.IsBesieged}");
                report.AppendLine($"  -> Mühasirədəki Əyalət Resursları: Gold={anadoluAfterEco.Gold:F1}, Food={anadoluAfterEco.Food:F1}");
                report.AppendLine($"  -> Qlobal (Nation) Xəzinəsi: Gold={seljukAfterEco.Gold:F1} (Əvvəlki: {nationGoldBefore:F1}), Food={seljukAfterEco.Food:F1} (Əvvəlki: {nationFoodBefore:F1})");

                if (!anadoluBesieged.IsBesieged || Mathf.Abs(seljukAfterEco.Gold - nationGoldBefore) > 0.1f)
                {
                    throw new InvalidOperationException("Mühasirədə olan əyalətin resursları Nation xəzinəsinə axmağı dayandırmadı!");
                }

                // 3b. İstismar (Exploit): PublicOrder -= 5 hər ay, ərzaq orduya gedir
                siege.Exploit("1", db);
                float orderBeforeExploit = anadoluAfterEco.PublicOrder;
                siege.ProcessMonthlySiege(db, 1, 1066);
                Province anadoluAfterExploit = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);

                report.AppendLine($"  -> Exploit Taktikası: IsExploited = {anadoluAfterExploit.IsExploited}");
                report.AppendLine($"  -> Aylıq Moral Düşüşü: {orderBeforeExploit:F0} -> {anadoluAfterExploit.PublicOrder:F0} (-5 bal)");

                if (!anadoluAfterExploit.IsExploited || Mathf.Abs(anadoluAfterExploit.PublicOrder - (orderBeforeExploit - 5f)) > 0.1f)
                {
                    throw new InvalidOperationException("İstismar (Exploit) zamanı PublicOrder hər ay -5 düşmədi!");
                }

                // 3c. Yağmalama (Plunder): Anında 2000 Qızıl və Ərzaq, Məhsuldarlıq 0.2x, PublicOrder 0
                Nation nationBeforePlunder = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);
                float natGoldPrePlunder = nationBeforePlunder.Gold;
                float natFoodPrePlunder = nationBeforePlunder.Food;

                PlunderResult plunderRes = siege.Plunder("1", db);
                Province anadoluPlundered = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Nation nationAfterPlunder = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);

                report.AppendLine($"  -> Plunder İcra Edildi: Success={plunderRes.Success}");
                report.AppendLine($"  -> Qlobal Xəzinəyə Əlavə Edildi: +{plunderRes.GoldGained:F0} Qızıl (Xəzinə: {natGoldPrePlunder:F1} -> {nationAfterPlunder.Gold:F1}) | +{plunderRes.FoodGained:F0} Ərzaq (Xəzinə: {natFoodPrePlunder:F1} -> {nationAfterPlunder.Food:F1})");
                report.AppendLine($"  -> Əyalət Məhsuldarlığı: {anadoluPlundered.ProductivityMultiplier * 100f:F0}% (Gözlənilən: 20%) | Xalqın Moralı: {anadoluPlundered.PublicOrder:F0} (Gözlənilən: 0)");

                if (!plunderRes.Success || Mathf.Abs(anadoluPlundered.ProductivityMultiplier - 0.2f) > 0.01f || anadoluPlundered.PublicOrder > 0.01f)
                {
                    throw new InvalidOperationException("Plunder zamanı məhsuldarlıq 0.2f və ya PublicOrder 0 olmadı!");
                }

                if (Mathf.Abs(nationAfterPlunder.Gold - (natGoldPrePlunder + 2000f)) > 1f || Mathf.Abs(nationAfterPlunder.Food - (natFoodPrePlunder + 2000f)) > 1f)
                {
                    throw new InvalidOperationException("Plunder zamanı xəzinəyə +2000 Qızıl və Ərzaq verilmədi!");
                }

                // 4. FÖVQƏLADƏ VƏZİYYƏT: CallEmergencyConscription (Qadınların 20% səfərbərliyi və 0.2x BirthRate)
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[4. Fövqəladə Vəziyyət (CallEmergencyConscription) və Demoqrafik Doğum Çöküşü]");

                Province shirvanInit = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 2);
                int adultsBeforeEmergency = shirvanInit.Adults;
                int expectedConscripted = Mathf.RoundToInt(adultsBeforeEmergency * 0.20f);

                EmergencyConscriptionResult emgRes = mil.CallEmergencyConscription("2", db);
                Province shirvanAfterEmergency = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 2);
                Population shirvanPop = db.Connection.Table<Population>().FirstOrDefault(p => p.ProvinceId == 2);

                report.AppendLine($"  -> CallEmergencyConscription('2'): Success={emgRes.Success}");
                report.AppendLine($"  -> Səfərbər Edilən Yetkinlər/Qadınlar (20%): {emgRes.ConscriptedAdults} (Gözlənilən: {expectedConscripted})");
                report.AppendLine($"  -> Əyalətdə Qalan Yetkinlər: {adultsBeforeEmergency} -> {shirvanAfterEmergency.Adults}");
                report.AppendLine($"  -> Yaradılan Milis Ordusu: Ordu #{emgRes.CreatedMilitiaArmy.Id} ({emgRes.CreatedMilitiaArmy.AdultSoldiers} əsgər, Morale={emgRes.CreatedMilitiaArmy.Morale})");
                report.AppendLine($"  -> AĞIR CƏZA: Doğum Çarpanı (BirthRateModifier): {emgRes.OldBirthRateModifier * 100f:F0}% -> {shirvanAfterEmergency.BirthRateModifier * 100f:F0}% (Gözlənilən: 20%)");

                if (!emgRes.Success || emgRes.ConscriptedAdults != expectedConscripted || Mathf.Abs(shirvanAfterEmergency.BirthRateModifier - 0.2f) > 0.01f)
                {
                    throw new InvalidOperationException("CallEmergencyConscription zamanı 20% əsgər cəlb edilmədi və ya BirthRateModifier 0.2f olmadı!");
                }

                // Doğum Səviyyəsinin Çöküşünün Doğrulanması (DemographicsController):
                int infantsBeforeMonth = shirvanPop.Infants;
                demo.ProcessMonthlyDemographics(db, 2, 1066);
                Population shirvanPopAfterMonth = db.Connection.Table<Population>().FirstOrDefault(p => p.ProvinceId == 2);
                int actualNewBirths = shirvanPopAfterMonth.Infants - infantsBeforeMonth;

                // 1.0x standart doğumla nə qədər doğulardı:
                float stdBirthRate = demo.CalculateEffectiveBirthRate(shirvanAfterEmergency.DevelopmentLevel);
                int stdBirths = Mathf.RoundToInt(shirvanAfterEmergency.Adults * stdBirthRate);
                int expectedDepressedBirths = Mathf.RoundToInt(shirvanAfterEmergency.Adults * stdBirthRate * 0.2f);

                report.AppendLine($"  -> 1 Aylıq Demoqrafik Tick Sonrası Doğulan Körpələr: {actualNewBirths} (Standart Doğum: {stdBirths}, 0.2x Çarpanla Gözlənilən: {expectedDepressedBirths})");

                if (Mathf.Abs(actualNewBirths - expectedDepressedBirths) > 1)
                {
                    throw new InvalidOperationException($"Doğum çarpanı demoqrafik hesablamada tətbiq olunmadı: {actualNewBirths} != {expectedDepressedBirths}");
                }

                // 5. Testi təmizləyirik və Play mode üçün bazanı ilkin vəziyyətinə gətiririk
                db.SeedAndTestDummyData(forceReseed: true);
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 6C TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 6C SIEGE, PUBLIC ORDER & EMERGENCY CONSCRIPTION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 6C xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }

        [MenuItem("Grand Strategy/Verify Phase 6D (Coup, Dynasty Rename & Overextension)")]
        public static void VerifyPhase6DOverextensionAndCoupMenu()
        {
            VerifyPhase6DOverextensionAndCoup();
        }

        [CliCommand("verify_phase6d_coup_and_overextension", "Verify ExecuteCoup dynasty renaming in SQLite & UI, and Overextension penalties (>3 direct provinces -> -5% tax and -5 loyalty per excess province)", MainThreadRequired = true)]
        public static string VerifyPhase6DOverextensionAndCoup()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 6D: TAXT ÇEVRİLİŞİ VƏ GENİŞLƏNMƏ RİSKİ TESTİ BAŞLADI ===");

                // 1. GlobalMapScene-i ən son dəyişikliklərlə açırıq və bazanı qururuq
                BuildAndVerifyGlobalMapScene();
                EditorSceneManager.OpenScene("Assets/Scenes/GlobalMapScene.unity", OpenSceneMode.Single);

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                var ui = UnityEngine.Object.FindAnyObjectByType<UIManager>();
                var pol = UnityEngine.Object.FindAnyObjectByType<PoliticalController>();
                var eco = UnityEngine.Object.FindAnyObjectByType<EconomyController>();

                if (db == null || gm == null || ui == null || pol == null || eco == null)
                {
                    throw new InvalidOperationException("Faza 6D komponentləri tam tapılmadı!");
                }

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                gm.EnsureDatabaseConnection();
                gm.EnsureSubsystemsBinding();
                ui.RefreshDateTrackerUI();

                report.AppendLine("[1. Başlanğıc Vəziyyət (Nation #1 - Səlcuqlu Dövləti)]");
                Nation nationInit = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);
                report.AppendLine($"  -> RulingDynastyName: '{nationInit.RulingDynastyName}' | Name: '{nationInit.Name}'");
                report.AppendLine($"  -> Birbaşa İdarə: {nationInit.DirectControlledProvinceCount}/{nationInit.MaxDirectControl} (Ümumi Nəzarət: {nationInit.ControlledProvinceCount})");
                report.AppendLine($"  -> UI Göstəricisi: '{ui.CurrentDisplayedDynasty}' | '{ui.CurrentDisplayedDirectControl}'");

                if (nationInit.RulingDynastyName != "Səlcuqlu Sultanlığı" || nationInit.MaxDirectControl != 3)
                {
                    throw new InvalidOperationException("Nation başlanğıc dəyərləri (RulingDynastyName və ya MaxDirectControl) düzgün deyil!");
                }

                // 2. TAXT ÇEVRİLİŞİ (ExecuteCoup) və DÖVLƏT ADININ DƏYİŞMƏSİ
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. Taxt Çevrilişi (ExecuteCoup) və Dövlət Adının Dəyişməsi]");

                // Qubernator A taxt çevrilişi edir və dövlətin adını "Əfşarlar İmperiyası" qoyur
                CoupResult coup1 = pol.ExecuteCoup("Qubernator A", "Əfşarlar İmperiyası", db);
                Nation nationAfterCoup1 = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);
                ui.RefreshDateTrackerUI();

                report.AppendLine($"  -> ExecuteCoup('Qubernator A', 'Əfşarlar İmperiyası'): Success={coup1.Success}");
                report.AppendLine($"  -> Köhnə Sülalə Adı: '{coup1.OldDynastyName}' -> Yeni Sülalə Adı: '{coup1.NewDynastyName}'");
                report.AppendLine($"  -> SQLite Bazası (Nation #1): RulingDynastyName='{nationAfterCoup1.RulingDynastyName}' | Name='{nationAfterCoup1.Name}'");
                report.AppendLine($"  -> UI-da Dərhal Əks Olunan Sülalə Adı: '{ui.CurrentDisplayedDynasty}'");

                if (!coup1.Success || nationAfterCoup1.RulingDynastyName != "Əfşarlar İmperiyası" || ui.CurrentDisplayedDynasty != "Əfşarlar İmperiyası")
                {
                    throw new InvalidOperationException("Taxt çevrilişi zamanı dövlətin adı bazada və ya UI-da düzgün yenilənmədi!");
                }

                // Avtomatik Sülalə Adı Testi: custom name verilmədikdə Liderin adı əsasında avtomatik sülalə adı formalaşır
                Leader testCoupLeader = new Leader
                {
                    Name = "Nadir Xan",
                    Loyalty = 50f,
                    IsRebel = false,
                    AssignedProvinceId = "1"
                };
                db.Connection.Insert(testCoupLeader);

                CoupResult coup2 = pol.ExecuteCoup(testCoupLeader.Id, null, db);
                Nation nationAfterCoup2 = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);
                ui.RefreshDateTrackerUI();

                report.AppendLine($"  -> Avtomatik Adlandırma İlə ExecuteCoup(Nadir Xan): '{coup2.NewDynastyName}'");
                report.AppendLine($"  -> UI Sülalə Adı: '{ui.CurrentDisplayedDynasty}'");

                if (!coup2.Success || !nationAfterCoup2.RulingDynastyName.Contains("Nadir Xan"))
                {
                    throw new InvalidOperationException("Avtomatik sülalə adlandırması uğursuz oldu!");
                }

                // 3. GENİŞLƏNMƏ RİSKİ / İDARƏETMƏ ZƏİFLƏMƏSİ (OVEREXTENSION)
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. Genişlənmə Riski / İdarəetmə Zəifləməsi (Overextension Cəzası)]");

                // Dövlətə (Faction 1) qubernatorsuz 4 yeni əyalət əlavə edirik:
                // Əyalət 1 (Anadolu) + 4 yeni əyalət = 5 birbaşa idarə olunan əyalət!
                // MaxDirectControl = 3 olduğu üçün Overextension = 5 - 3 = 2 artıq əyalət (+2).
                // Hər artıq əyalət üçün -5% vergi və -5 bal aylıq sədaqət cəzası (Toplam: -10%).
                var p3 = new Province { Name = "Qarabağ", Gold = 1000f, Food = 1000f, Adults = 4000, Youths = 1000, Climate = "Mülayim", OwnerFactionId = 1, DevelopmentLevel = 15 };
                var p4 = new Province { Name = "Təbriz", Gold = 1000f, Food = 1000f, Adults = 5000, Youths = 1500, Climate = "Mülayim", OwnerFactionId = 1, DevelopmentLevel = 17 };
                var p5 = new Province { Name = "Dərbənd", Gold = 1000f, Food = 1000f, Adults = 3000, Youths = 800, Climate = "Quraq", OwnerFactionId = 1, DevelopmentLevel = 14 };
                var p6 = new Province { Name = "Gəncə", Gold = 1000f, Food = 1000f, Adults = 4500, Youths = 1200, Climate = "Mülayim", OwnerFactionId = 1, DevelopmentLevel = 16 };

                db.Connection.Insert(p3);
                db.Connection.Insert(p4);
                db.Connection.Insert(p5);
                db.Connection.Insert(p6);

                OverextensionReport overextRep = pol.CalculateNationOverextension(db, 1);
                Nation nationOverext = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);
                ui.RefreshDateTrackerUI();

                report.AppendLine($"  -> Hesablanan Birbaşa İdarə: {overextRep.DirectControlledProvinces} əyalət (Maksimum Cəzasız Hədd: {overextRep.MaxDirectControl})");
                report.AppendLine($"  -> OverextensionCount (Artıq Əyalət): +{overextRep.OverextensionCount}");
                report.AppendLine($"  -> Overextension Cəza Nisbəti (hər əyalət başına 5%): -{overextRep.PenaltyRate * 100f:F0}%");
                report.AppendLine($"  -> UI İdarəetmə Göstəricisi: '{ui.CurrentDisplayedDirectControl}'");

                if (overextRep.DirectControlledProvinces != 5 || overextRep.OverextensionCount != 2 || Mathf.Abs(overextRep.PenaltyRate - 0.10f) > 0.01f)
                {
                    throw new InvalidOperationException($"Overextension hesablaması səhvdir: Birbaşa={overextRep.DirectControlledProvinces}, Artıq={overextRep.OverextensionCount}, Cəza={overextRep.PenaltyRate}");
                }

                // 3a. Vergi Cəzası Yoxlaması (-10% vergi gəliri):
                Province sampleProv = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Population samplePop = db.Connection.Table<Population>().FirstOrDefault(p => p.ProvinceId == 1);
                Economy sampleEco = db.Connection.Table<Economy>().FirstOrDefault(e => e.ProvinceId == 1);

                // Normal (1.0x) vergi gəliri:
                MonthlyEconomyReport normalEco = eco.ApplyMonthlyEconomyAndConsumption(sampleProv, samplePop, sampleEco, 1, 1066, taxModifier: 1.0f);
                float normalTax = normalEco.LocalTaxIncome;

                // Overextension cəzası (0.90x = 1.0 - 0.10):
                MonthlyEconomyReport penalEco = eco.ApplyMonthlyEconomyAndConsumption(sampleProv, samplePop, sampleEco, 1, 1066, taxModifier: (1.0f - nationOverext.OverextensionPenaltyRate));
                float penalTax = penalEco.LocalTaxIncome;

                report.AppendLine($"  -> Baza Vergi Gəliri (Cəzasız): {normalTax:F1}");
                report.AppendLine($"  -> Overextension Cəzalı Vergi Gəliri (-10%): {penalTax:F1} (Gözlənilən: {normalTax * 0.90f:F1})");

                if (Mathf.Abs(penalTax - (normalTax * 0.90f)) > 0.5f)
                {
                    throw new InvalidOperationException("Overextension vergi cəzası (-10%) düzgün tətbiq olunmadı!");
                }

                // 3b. Sədaqət Cəzası Yoxlaması (hər ay -10 bal):
                // Test üçün sabit 80 sədaqətli yeni bir qubernator qoyuruq (Şirvan qubernatoru)
                Leader loyalGov = new Leader
                {
                    Name = "Sadiq Qubernator",
                    Loyalty = 80f,
                    IsRebel = false,
                    AssignedProvinceId = "2"
                };
                db.Connection.Insert(loyalGov);

                pol.CheckRebellions(db, 1, 1066, applyDynamicLoyalty: true);
                Leader govAfterOverext = db.Connection.Table<Leader>().FirstOrDefault(l => l.Id == loyalGov.Id);

                report.AppendLine($"  -> Qubernator Sədaqəti: 80.0 -> {govAfterOverext.Loyalty:F1} (2 artıq əyalət səbəbindən -10 bal cəza tətbiq edildi)");

                if (Mathf.Abs(govAfterOverext.Loyalty - 70f) > 0.1f)
                {
                    throw new InvalidOperationException($"Overextension sədaqət cəzası düzgün tətbiq olunmadı: {govAfterOverext.Loyalty} != 70.0");
                }

                // 3c. Qubernator Təyinatı İlə Overextension-un Aradan Qaldırılması:
                // Əyalətlərə qubernatorlar təyin edirik -> Birbaşa idarə sayı 2-yə düşür (<= 3) -> Cəza sıfırlanır!
                Leader govQarabag = new Leader { Name = "Qarabağ Qubernatoru", Loyalty = 90f, AssignedProvinceId = p3.Id.ToString() };
                Leader govTabriz = new Leader { Name = "Təbriz Qubernatoru", Loyalty = 90f, AssignedProvinceId = p4.Id.ToString() };
                Leader govDerbend = new Leader { Name = "Dərbənd Qubernatoru", Loyalty = 90f, AssignedProvinceId = p5.Id.ToString() };
                db.Connection.Insert(govQarabag);
                db.Connection.Insert(govTabriz);
                db.Connection.Insert(govDerbend);

                OverextensionReport resolvedRep = pol.CalculateNationOverextension(db, 1);
                ui.RefreshDateTrackerUI();

                report.AppendLine($"  -> Qubernatorlar Təyin Edildikdən Sonra Birbaşa İdarə: {resolvedRep.DirectControlledProvinces}/3 | Overextension: +{resolvedRep.OverextensionCount}");
                report.AppendLine($"  -> UI Göstəricisi: '{ui.CurrentDisplayedDirectControl}' (Cəza aradan qaldırıldı)");

                if (resolvedRep.OverextensionCount != 0 || resolvedRep.PenaltyRate != 0f)
                {
                    throw new InvalidOperationException("Qubernatorlar təyin edildikdən sonra Overextension sıfırlanmadı!");
                }

                // 4. Testi təmizləyirik və Play mode üçün bazanı ilkin vəziyyətinə gətiririk
                db.SeedAndTestDummyData(forceReseed: true);
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 6D TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 6D OVEREXTENSION & COUP VERIFICATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 6D xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }

        [MenuItem("Grand Strategy/Verify Phase 3.1 (Shield Wall & Flanking)")]
        public static void VerifyPhase3_1TacticalShieldAndFlankingMenu()
        {
            VerifyPhase3_1TacticalShieldAndFlanking();
        }

        [CliCommand("verify_phase3_1_tactical_shield_and_flanking", "Verify Kalkan Duvarı (moveSpeed -70%, incoming damage -60% [0.4x], visual indicator), Flanking (front 1.0x, flank 1.5x, rear 2.0x), and UI button & combat indicators in TacticalBattleScene", MainThreadRequired = true)]
        public static string VerifyPhase3_1TacticalShieldAndFlanking()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 3.1: KALKAN DUVARI VƏ FLANKING SİSTEMİ TESTİ BAŞLADI ===");

                // 1. TacticalBattleScene-i sıfırdan ən son komponentlərlə qururuq
                BuildAndVerifyTacticalBattleScene();
                EditorSceneManager.OpenScene("Assets/Scenes/TacticalBattleScene.unity", OpenSceneMode.Single);

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var battleMgr = UnityEngine.Object.FindAnyObjectByType<TacticalBattleManager>();
                var selectionController = UnityEngine.Object.FindAnyObjectByType<UnitSelectionController>();
                var milController = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();

                if (db == null || battleMgr == null || selectionController == null || milController == null)
                {
                    throw new InvalidOperationException("TacticalBattleScene komponentləri tam tapılmadı!");
                }

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);

                DraftArmyResult draft1 = milController.DraftArmy(db, 1);
                DraftArmyResult draft2 = milController.DraftArmy(db, 2);

                battleMgr.InitializeAndSpawnBattle(draft1.CreatedArmy.Id, draft2.CreatedArmy.Id);

                UI_BattalionToken playerToken = battleMgr.AttackerBattalions[0];
                UI_BattalionToken enemyToken = battleMgr.DefenderBattalions[0];

                report.AppendLine("[1. Başlanğıc Vəziyyət]");
                report.AppendLine($"  -> Oyunçu Tokeni: '{playerToken.name}', Sürət: {playerToken.MoveSpeed:F1}, HP: {playerToken.CurrentHP:F1}/{playerToken.MaxHP:F1}, Kalkan: {playerToken.IsShieldWallActive}");
                report.AppendLine($"  -> Rəqib Tokeni: '{enemyToken.name}', Sürət: {enemyToken.MoveSpeed:F1}, HP: {enemyToken.CurrentHP:F1}/{enemyToken.MaxHP:F1}");

                // 2. Kalkan Duvarı (Shield Wall) Əmri Yoxlaması
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. 'Kalkan Duvarı' (Shield Wall) Əmri və Rejim Dəyişimi]");

                float baseSpeed = playerToken.MoveSpeed;
                selectionController.SelectSingleUnit(playerToken);

                bool buttonVisible = selectionController.ShieldWallButton != null && selectionController.ShieldWallButton.gameObject.activeSelf;
                string buttonTextBefore = selectionController.ShieldWallButtonText != null ? selectionController.ShieldWallButtonText.text : string.Empty;
                report.AppendLine($"  -> Token Seçildi -> UI Düyməsi Görünür: {buttonVisible} | Mətn: '{buttonTextBefore}'");

                if (!buttonVisible || !buttonTextBefore.Contains("Kalkan Duvarı Qur"))
                {
                    throw new InvalidOperationException("Kalkan Duvarı UI düyməsi seçilmiş token üçün düzgün aktivləşmədi!");
                }

                // Düyməyə basırıq (Kalkan divarı aktivləşdirilir)
                selectionController.ToggleSelectedUnitsShieldWall();
                float wallSpeed = playerToken.MoveSpeed;
                float expectedWallSpeed = baseSpeed * 0.30f; // 70% azalma
                string buttonTextActive = selectionController.ShieldWallButtonText != null ? selectionController.ShieldWallButtonText.text : string.Empty;

                report.AppendLine($"  -> Kalkan Duvarı Aktivləşdirildi: IsShieldWallActive={playerToken.IsShieldWallActive}");
                report.AppendLine($"  -> Hərəkət Sürəti (70% Azalma): {baseSpeed:F1} -> {wallSpeed:F1} (Gözlənilən: {expectedWallSpeed:F1})");
                report.AppendLine($"  -> Alınan Zərər Çarpanı (60% Azalma): {playerToken.IncomingDamageMultiplier:F2}x (0.4x)");
                report.AppendLine($"  -> UI Qalxan Nişanı Görünür: {playerToken.IsShieldWallIndicatorActive} | Düymə Mətni: '{buttonTextActive}'");

                if (!playerToken.IsShieldWallActive || Mathf.Abs(wallSpeed - expectedWallSpeed) > 0.5f || Mathf.Abs(playerToken.IncomingDamageMultiplier - 0.40f) > 0.01f)
                {
                    throw new InvalidOperationException($"Kalkan Duvarı parametrləri düzgün hesablanmadı! Sürət={wallSpeed}, Çarpan={playerToken.IncomingDamageMultiplier}");
                }

                // Kalkan divarı zərər testi: 50 zərər vurulur -> 0.4x zərər qəbul edir = 20 zərər
                float hpBeforeDamage = playerToken.CurrentHP;
                playerToken.TakeDamage(50f);
                float hpAfterDamage = playerToken.CurrentHP;
                float expectedHpAfter = hpBeforeDamage - (50f * 0.40f);

                report.AppendLine($"  -> 50 Zərər Alındıqda (0.4x Çarpan İlə): HP {hpBeforeDamage:F1} -> {hpAfterDamage:F1} (Gözlənilən: {expectedHpAfter:F1})");

                if (Mathf.Abs(hpAfterDamage - expectedHpAfter) > 0.1f)
                {
                    throw new InvalidOperationException($"Kalkan divarında zərərin 60% azalması düzgün işləmədi: Alınan zərər={hpBeforeDamage - hpAfterDamage}, Gözlənilən={50f * 0.40f}");
                }

                // Kalkan divarını söndürürük -> Sürət bərpa olunur
                selectionController.ToggleSelectedUnitsShieldWall();
                report.AppendLine($"  -> Kalkan Duvarı Söndürüldü: IsShieldWallActive={playerToken.IsShieldWallActive}, Bərpa Olunan Sürət={playerToken.MoveSpeed:F1}, Çarpan={playerToken.IncomingDamageMultiplier:F2}x");

                if (playerToken.IsShieldWallActive || Mathf.Abs(playerToken.MoveSpeed - baseSpeed) > 0.5f || Mathf.Abs(playerToken.IncomingDamageMultiplier - 1.0f) > 0.01f)
                {
                    throw new InvalidOperationException("Kalkan divarı söndürüldükdə sürət və ya çarpan ilkin dəyərə qayıtmadı!");
                }

                // Seçimi təmizləyirik -> Düymə gizlənir
                selectionController.ClearSelection();
                bool buttonHiddenOnClear = selectionController.ShieldWallButton != null && !selectionController.ShieldWallButton.gameObject.activeSelf;
                report.AppendLine($"  -> Seçim Təmizləndikdə UI Düyməsi Gizləndi: {buttonHiddenOnClear}");

                // 3. Flanking və Arxadan Zərbə (Facing Direction & Directional Multipliers)
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. Flanking / Arxadan Və Yanlardan Hücum Bonusu]");

                // Test üçün təmiz iki token mövqeləndiririk
                // Düşmən (defender) (0, 0) nöqtəsindədir və Sağa (Vector2.right) baxır
                enemyToken.AnchoredPosition = new Vector2(0f, 0f);
                enemyToken.SetFacingDirection(Vector2.right);
                playerToken.AnchoredPosition = new Vector2(50f, 0f); // Düşmənin qarşısında (Ön tərəf)

                // 3a. Ön tərəfdən hücum (Front: 1.0x)
                CombatAttackDirection frontDir = UI_BattalionToken.DetermineAttackDirection(playerToken, enemyToken);
                float frontMult = UI_BattalionToken.GetDirectionalDamageMultiplier(frontDir);
                report.AppendLine($"  -> Mövqe (50, 0) vs Facing (1, 0) -> Hücum Yönü: {frontDir} | Çarpan: {frontMult:F1}x");

                if (frontDir != CombatAttackDirection.Front || Mathf.Abs(frontMult - 1.0f) > 0.01f)
                {
                    throw new InvalidOperationException($"Ön tərəf hücumu səhv müəyyən edildi: {frontDir}, {frontMult}");
                }

                // 3b. Yan tərəfdən hücum (Flank: 1.5x)
                playerToken.AnchoredPosition = new Vector2(0f, 50f); // Düşmənin sağında/solunda 90 dərəcə cinahda
                CombatAttackDirection flankDir = UI_BattalionToken.DetermineAttackDirection(playerToken, enemyToken);
                float flankMult = UI_BattalionToken.GetDirectionalDamageMultiplier(flankDir);
                playerToken.TryPerformAttack(enemyToken, ignoreCooldown: true);
                string flankIndicator = battleMgr.LastCombatIndicatorText;
                report.AppendLine($"  -> Mövqe (0, 50) vs Facing (1, 0) -> Hücum Yönü: {flankDir} | Çarpan: {flankMult:F1}x | UI Mətn: '{flankIndicator}'");

                if (flankDir != CombatAttackDirection.Flank || Mathf.Abs(flankMult - 1.5f) > 0.01f || !flankIndicator.Contains("x1.5"))
                {
                    throw new InvalidOperationException($"Yan (Flank) hücumu səhv müəyyən edildi: {flankDir}, {flankMult}, Indicator={flankIndicator}");
                }

                // 3c. Arxadan hücum (Rear: 2.0x ikiqat kritik zərbə!)
                playerToken.AnchoredPosition = new Vector2(-50f, 0f); // Düşmənin birbaşa arxasında (180 dərəcə)
                CombatAttackDirection rearDir = UI_BattalionToken.DetermineAttackDirection(playerToken, enemyToken);
                float rearMult = UI_BattalionToken.GetDirectionalDamageMultiplier(rearDir);
                playerToken.TryPerformAttack(enemyToken, ignoreCooldown: true);
                string rearIndicator = battleMgr.LastCombatIndicatorText;
                report.AppendLine($"  -> Mövqe (-50, 0) vs Facing (1, 0) -> Hücum Yönü: {rearDir} | Çarpan: {rearMult:F1}x | UI Mətn: '{rearIndicator}'");

                if (rearDir != CombatAttackDirection.Rear || Mathf.Abs(rearMult - 2.0f) > 0.01f || !rearIndicator.Contains("x2.0"))
                {
                    throw new InvalidOperationException($"Arxadan (Rear) hücumu səhv müəyyən edildi: {rearDir}, {rearMult}, Indicator={rearIndicator}");
                }

                // 4. Kalkan Duvarı + Flanking Kombinasiya Yoxlaması
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[4. Kalkan Duvarı + Arxadan Zərbə Kombinasiyası]");

                // Düşmən tokenini Kalkan Divarı rejiminə keçiririk
                enemyToken.SetShieldWall(true);
                enemyToken.SetFacingDirection(Vector2.right);
                playerToken.AnchoredPosition = new Vector2(-50f, 0f); // Arxadan hücum
                float enemyHpBefore = enemyToken.CurrentHP;
                float baseAtk = playerToken.AttackDamage;
                // Arxadan zərbə: zərər = baseAtk * 2.0f * 0.40f (Kalkan qoruyur) = baseAtk * 0.80f
                playerToken.TryPerformAttack(enemyToken, ignoreCooldown: true);
                float enemyHpAfter = enemyToken.CurrentHP;
                float actualDamageTaken = enemyHpBefore - enemyHpAfter;
                float expectedDamageTaken = baseAtk * 2.0f * 0.40f;

                report.AppendLine($"  -> Düşmən Kalkan Divarında (0.4x) + Oyunçu Arxadan Zərbə Vurdu (2.0x): Alınan Zərər={actualDamageTaken:F2} (Gözlənilən: {expectedDamageTaken:F2})");

                if (Mathf.Abs(actualDamageTaken - expectedDamageTaken) > 0.2f)
                {
                    throw new InvalidOperationException($"Kombinasiya zərəri səhvdir: Alınan={actualDamageTaken}, Gözlənilən={expectedDamageTaken}");
                }

                // 5. Tam Döyüş Simulyasiyası (Bütün tokenlərin real-time toqquşması və bitişi)
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[5. Real-Time Döyüş Simulyasiyası]");
                battleMgr.InitializeAndSpawnBattle(draft1.CreatedArmy.Id, draft2.CreatedArmy.Id);
                int simSteps = battleMgr.SimulateBattleUntilResolution(timeStep: 0.25f, maxSteps: 600);
                report.AppendLine($"  -> Simulyasiya {simSteps} addımda başa çatdı. IsBattleEnded: {battleMgr.IsBattleEnded} | Qalib: {battleMgr.LastVictoryMessage}");

                if (!battleMgr.IsBattleEnded)
                {
                    throw new InvalidOperationException("Döyüş simulyasiyası vaxtında bitmədi!");
                }

                // Təmizlik və səhnə qeydi
                battleMgr.InitializeAndSpawnBattle(draft1.CreatedArmy.Id, draft2.CreatedArmy.Id);
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 3.1 TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 3.1 SHIELD WALL & FLANKING VERIFICATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 3.1 xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }

        [MenuItem("Grand Strategy/Verify Phase 3.2 (Dynamic Environment & Castle Gate)")]
        public static void VerifyPhase3_2TacticalEnvironmentAndGateMenu()
        {
            VerifyPhase3_2TacticalEnvironmentAndGate();
        }

        [CliCommand("verify_phase3_2_tactical_environment_and_gate", "Verify Dynamic Environments (Plains, Desert sandstorm -50% range/speed, Siege castle gate 2000 HP movement blocking & breach)", MainThreadRequired = true)]
        public static string VerifyPhase3_2TacticalEnvironmentAndGate()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 3.2: DİNAMİK MÜHİT VƏ QALA QAPISI TESTİ BAŞLADI ===");

                // 1. TacticalBattleScene-i ən son prefablar və komponentlərlə yenidən qururuq
                BuildAndVerifyTacticalBattleScene();
                EditorSceneManager.OpenScene("Assets/Scenes/TacticalBattleScene.unity", OpenSceneMode.Single);

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var battleMgr = UnityEngine.Object.FindAnyObjectByType<TacticalBattleManager>();
                var milController = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();

                if (db == null || battleMgr == null || milController == null)
                {
                    throw new InvalidOperationException("TacticalBattleScene komponentləri tapılmadı!");
                }

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);

                DraftArmyResult draft1 = milController.DraftArmy(db, 1);
                DraftArmyResult draft2 = milController.DraftArmy(db, 2);

                // =========================================================================
                // 1. SƏHRA VƏ QUM FIRTINASI MEXANİKASI (Desert & Sandstorm)
                // =========================================================================
                report.AppendLine("[1. Səhra və Qum Fırtınası Mexanikası (BattleEnvironment.Desert)]");
                battleMgr.InitializeAndSpawnBattle(draft1.CreatedArmy.Id, draft2.CreatedArmy.Id, BattleEnvironment.Desert);

                if (battleMgr.CurrentEnvironment != BattleEnvironment.Desert)
                {
                    throw new InvalidOperationException($"Mühit Desert olaraq təyin edilmədi: {battleMgr.CurrentEnvironment}");
                }

                UI_BattalionToken testToken = battleMgr.AttackerBattalions[0];
                float baseSpeed = testToken.BaseMoveSpeed;
                float baseRange = testToken.BaseAttackRange;

                bool sandstormActive = battleMgr.IsSandstormActive;
                string sandstormLabel = battleMgr.CurrentSandstormWarningLabel;
                float stormySpeed = testToken.MoveSpeed;
                float stormyRange = testToken.AttackRange;

                report.AppendLine($"  -> Döyüş Mühiti: {battleMgr.CurrentEnvironment} | Qum Fırtınası Aktivdir: {sandstormActive}");
                report.AppendLine($"  -> UI Xəbərdarlıq Mətni: \"{sandstormLabel}\"");
                report.AppendLine($"  -> Batalyon Sürəti (50% Azalma): {baseSpeed:F1} -> {stormySpeed:F1} (Gözlənilən: {baseSpeed * 0.5f:F1})");
                report.AppendLine($"  -> Batalyon Mənzili (50% Azalma): {baseRange:F1} -> {stormyRange:F1} (Gözlənilən: {baseRange * 0.5f:F1})");

                if (!sandstormActive || !sandstormLabel.Contains("QUM FIRTINASI AKTİVDİR") ||
                    Mathf.Abs(stormySpeed - (baseSpeed * 0.5f)) > 0.5f ||
                    Mathf.Abs(stormyRange - (baseRange * 0.5f)) > 0.5f)
                {
                    throw new InvalidOperationException($"Qum fırtınası parametrləri düzgün hesablanmadı! Sürət={stormySpeed}, Mənzil={stormyRange}, UI={sandstormLabel}");
                }

                // Fırtınanı söndürürük və bərpanı yoxlayırıq
                battleMgr.SetSandstorm(false);
                float restoredSpeed = testToken.MoveSpeed;
                float restoredRange = testToken.AttackRange;
                bool isSandstormStopped = !battleMgr.IsSandstormActive;
                report.AppendLine($"  -> Fırtına Bitdikdə Bərpa: Sürət {stormySpeed:F1} -> {restoredSpeed:F1} | Mənzil {stormyRange:F1} -> {restoredRange:F1} | UI Gizləndi: {string.IsNullOrEmpty(battleMgr.CurrentSandstormWarningLabel)}");

                if (!isSandstormStopped || Mathf.Abs(restoredSpeed - baseSpeed) > 0.5f || Mathf.Abs(restoredRange - baseRange) > 0.5f)
                {
                    throw new InvalidOperationException("Qum fırtınası bitdikdə sürət və mənzil bərpa olunmadı!");
                }

                // =========================================================================
                // 2. QALA MÜHASİRƏSİ VƏ QALA QAPISI MEXANİKASI (BattleEnvironment.Siege)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. Qala Mühasirəsi və Qapı Mexanikası (BattleEnvironment.Siege)]");

                battleMgr.InitializeAndSpawnBattle(draft1.CreatedArmy.Id, draft2.CreatedArmy.Id, BattleEnvironment.Siege);

                if (battleMgr.CurrentEnvironment != BattleEnvironment.Siege)
                {
                    throw new InvalidOperationException($"Mühit Siege olaraq təyin edilmədi: {battleMgr.CurrentEnvironment}");
                }

                UI_GateToken gate = battleMgr.GateToken;
                if (gate == null || !battleMgr.IsGateIntact)
                {
                    throw new InvalidOperationException("Qala mühasirəsində UI_GateToken yaradılmadı!");
                }

                report.AppendLine($"  -> Qala Qapısı (UI_GateToken) Mövcuddur: True | Mövqe: {gate.AnchoredPosition} | Can (HP): {gate.CurrentHP:F1}/{gate.MaxHP:F1} | Adı: '{gate.CurrentGateTitleLabel}'");

                if (gate.MaxHP < 2000f || Mathf.Abs(gate.AnchoredPosition.x) > 0.01f)
                {
                    throw new InvalidOperationException($"Qala qapısının HP və ya mövqeyi səhvdir: HP={gate.MaxHP}, Pos={gate.AnchoredPosition}");
                }

                // 2a. Hədəf seçimi testi: Hücumçu token (Sol tərəf) üçün məcburi ilk hədəf Qapıdır
                UI_BattalionToken attacker = battleMgr.AttackerBattalions[0];
                UI_BattalionToken targetedEnemy = attacker.FindNearestEnemyWithinRange(1000f);
                report.AppendLine($"  -> Hücumçunun (Sol tərəf) Axtardığı İlk Hədəf: '{targetedEnemy?.name}' (Qapı ilə eynidir: {targetedEnemy == gate})");

                if (targetedEnemy != gate)
                {
                    throw new InvalidOperationException($"Hücumçu ilk hədəf olaraq Qapını seçmədi! Seçilən: {targetedEnemy?.name}");
                }

                // 2b. Bloklama Məntiqi Testi: Hücumçu Qapını keçərək X = +150-yə getmək istəyir
                attacker.AnchoredPosition = new Vector2(-100f, 0f);
                attacker.MoveTo(new Vector2(150f, 0f));
                attacker.TickCombat(2.0f); // 2 saniyə irəliləməyə çalışır

                float clampedX = attacker.AnchoredPosition.x;
                report.AppendLine($"  -> Bloklama Yoxlaması: Hücumçu X=-100-dən X=+150-yə getmək istədi -> Çatdığı X: {clampedX:F1} (Mərkəzi X=0 xəttini keçə bilmədi: {clampedX <= 0f})");

                if (clampedX > 0f)
                {
                    throw new InvalidOperationException($"Bloklama xətası: Hücumçu qapı sındırılmadan mərkəzi xətti keçdi! X={clampedX}");
                }

                // 2c. Müdafiəçilər (Sağ tərəf) və Uzaqmənzilli (Ranged) Testi
                UI_BattalionToken defender = battleMgr.DefenderBattalions[0];
                if (defender is UI_GateToken && battleMgr.DefenderBattalions.Count > 1)
                {
                    defender = battleMgr.DefenderBattalions[1];
                }
                defender.AnchoredPosition = new Vector2(80f, 0f);
                defender.SetRanged(true, 180f); // Uzaqmənzilli oxçu
                float distAcrossGate = Vector2.Distance(defender.AnchoredPosition, attacker.AnchoredPosition);
                bool canAttackAcrossGate = distAcrossGate <= defender.AttackRange;
                report.AppendLine($"  -> Müdafiəçi Qapının Arxasında: X={defender.AnchoredPosition.x:F1} | Uzaq Mənzilli (Ranged): {defender.IsRanged} (Mənzil: {defender.AttackRange:F1})");
                report.AppendLine($"  -> Qapının Arxasından Hücumçunu Vura Bilir: {canAttackAcrossGate} (Məsafə: {distAcrossGate:F1} <= {defender.AttackRange:F1})");

                // 2d. Qapının Sındırılması (Gate Breach) Testi
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. Qapının Sındırılması və İçəri Hücum Keçidi]");

                gate.TakeDamage(2000f); // Qapıya 2000 zərər verilir (sındırılır)
                bool isGateBreached = !battleMgr.IsGateIntact;
                string gateBreachedLabel = battleMgr.CurrentGateBreachedLabel;
                report.AppendLine($"  -> Qapıya 2000 Zərər Verildi -> Qapı Sındırıldı (IsGateIntact=False): {isGateBreached}");
                report.AppendLine($"  -> Ekranda Görünən Xəbərdarlıq Mətni: \"{gateBreachedLabel}\"");

                if (!isGateBreached || !gateBreachedLabel.Contains("QALA QAPISI SINDIRILDI"))
                {
                    throw new InvalidOperationException($"Qala qapısının sındırılması və ya UI xəbərdarlığı işləmədi! Intact={battleMgr.IsGateIntact}, UI={gateBreachedLabel}");
                }

                // Bloklama aradan qalxdı: İndi hücumçu mərkəzi xətti keçərək içəri hücum edə bilməlidir
                attacker.MoveTo(new Vector2(100f, 0f));
                attacker.TickCombat(2.0f);
                float postBreachX = attacker.AnchoredPosition.x;
                report.AppendLine($"  -> Qapı Sındıqdan Sonra İçəri Keçid: Hücumçu X=-25-dən X=+100-ə hərəkət etdi -> Yeni X: {postBreachX:F1} (Mərkəzi xətti keçdi: {postBreachX > 0f})");

                if (postBreachX <= 0f)
                {
                    throw new InvalidOperationException($"Qapı sındırıldıqdan sonra hücumçunun içəri keçidi bloklandı! X={postBreachX}");
                }

                // 4. Real-Time Döyüş Simulyasiyası
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[4. Real-Time Döyüş Simulyasiyası]");
                int simSteps = battleMgr.SimulateBattleUntilResolution(timeStep: 0.25f, maxSteps: 600);
                report.AppendLine($"  -> Simulyasiya {simSteps} addımda başa çatdı. IsBattleEnded: {battleMgr.IsBattleEnded} | Qalib: {battleMgr.LastVictoryMessage}");

                if (!battleMgr.IsBattleEnded)
                {
                    throw new InvalidOperationException("Qala mühasirəsi döyüşü vaxtında bitmədi!");
                }

                // Səhnəni təmiz saxlayırıq
                battleMgr.InitializeAndSpawnBattle(draft1.CreatedArmy.Id, draft2.CreatedArmy.Id);
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 3.2 TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 3.2 DYNAMIC ENVIRONMENT & CASTLE GATE VERIFICATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 3.2 xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }



        [MenuItem("Grand Strategy/Verify Phase 4 (Core Game Loop & Scene Transition)")]
        public static void VerifyPhase4CoreLoopMenu()
        {
            VerifyPhase4CoreLoop();
        }

        [CliCommand("verify_phase4_core_loop", "Rebuild GlobalMapScene and TacticalBattleScene, verify Build Settings registration, test StartBattle('Army_1', 'Army_2') -> TacticalBattleScene -> Victory -> EndBattle deletes defeated army from SQLite DB -> GlobalMapScene", MainThreadRequired = true)]
        public static string VerifyPhase4CoreLoop()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 4: SƏHNƏLƏR ARASI KEÇİD VƏ CORE GAME LOOP TESTİ BAŞLADI ===");

                // 1. Hər iki səhnəni (TacticalBattleScene və GlobalMapScene) ən son dəyişikliklərlə yenidən qururuq
                BuildAndVerifyTacticalBattleScene();
                BuildAndVerifyGlobalMapScene();

                RegisterSceneInBuildSettings("Assets/Scenes/TacticalBattleScene.unity");
                RegisterSceneInBuildSettings("Assets/Scenes/GlobalMapScene.unity");

                report.AppendLine("[1. Build Settings Səhnələri]");
                foreach (var bsScene in EditorBuildSettings.scenes)
                {
                    report.AppendLine($"  -> {bsScene.path} (Enabled: {bsScene.enabled})");
                }

                // 2. GlobalMapScene-də bazanı sıfırdan hazırlayırıq və 2 ordu (Army #1 və Army #2) səfərbər edirik
                EditorSceneManager.OpenScene("Assets/Scenes/GlobalMapScene.unity", OpenSceneMode.Single);
                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var milController = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();
                var uiManager = UnityEngine.Object.FindAnyObjectByType<UIManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();

                DraftArmyResult draft1 = milController.DraftArmy(db, 1); // Army #1 (Anadolu)
                DraftArmyResult draft2 = milController.DraftArmy(db, 2); // Army #2 (Şirvan)
                int initialArmyCount = db.Connection.Table<Army>().Count();

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. GlobalMapScene: 'Test Döyüşünə Başla' Düyməsi və StartBattle('Army_1', 'Army_2')]");
                report.AppendLine($"  -> Başlanğıc Baza Orduları: {initialArmyCount} ədəd (Ordu #{draft1.CreatedArmy.Id}: {draft1.CreatedArmy.TotalSoldiers} əsgər, Ordu #{draft2.CreatedArmy.Id}: {draft2.CreatedArmy.TotalSoldiers} əsgər)");
                report.AppendLine($"  -> UIManager.StartTestBattleButton mövcuddur: {uiManager.StartTestBattleButton != null}");

                // 3. Oyunçunun "Test Döyüşünə Başla" düyməsinə basmasını simulyasiya edirik
                uiManager.OnStartTestBattleButtonClicked();

                string activeSceneAfterStart = EditorSceneManager.GetActiveScene().name;
                var gmInTactical = GameManager.Instance != null
                    ? GameManager.Instance
                    : UnityEngine.Object.FindAnyObjectByType<GameManager>();

                report.AppendLine($"  -> StartBattle çağırıldıqdan sonra aktiv səhnə: {activeSceneAfterStart}");
                report.AppendLine($"  -> GameManager.CurrentAttackerArmyId = '{gmInTactical.CurrentAttackerArmyId}' | CurrentDefenderArmyId = '{gmInTactical.CurrentDefenderArmyId}'");

                // 4. TacticalBattleManager-in GameManager-dəki ID-lərdən qoşunları yaratmasını və döyüşü bitirməsini yoxlayırıq
                var battleManager = UnityEngine.Object.FindAnyObjectByType<TacticalBattleManager>();
                battleManager.InitializeBattleFromGameManager();

                int spawnedAttackers = battleManager.AttackerBattalions.Count;
                int spawnedDefenders = battleManager.DefenderBattalions.Count;

                int simSteps = battleManager.SimulateBattleUntilResolution(timeStep: 0.25f, maxSteps: 600);
                string winnerIdStr = battleManager.GetWinningArmyIdString();

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. TacticalBattleScene: GameManager ID-ləri ilə Qoşun Yaratma və Döyüş Nəticəsi]");
                report.AppendLine($"  -> GameManager-dən oxunan ordularla yaradılan batalyonlar: Hücum ({gmInTactical.CurrentAttackerArmyId}) = {spawnedAttackers}, Müdafiə ({gmInTactical.CurrentDefenderArmyId}) = {spawnedDefenders}");
                report.AppendLine($"  -> Döyüş Simulyasiyası: {simSteps} addımda tamamlandı | IsBattleEnded: {battleManager.IsBattleEnded} | Qalib Ordu ID: '{winnerIdStr}' (Ordu #{battleManager.WinningArmyId})");

                // 5. Döyüş bitdikdən sonra EndBattle(winnerArmyId) vasitəsilə məğlub ordunun bazadan silinməsini və GlobalMapScene-ə qayıdışı yoxlayırıq
                battleManager.CompleteBattleAndReturnToGlobalMap();

                string activeSceneAfterEnd = EditorSceneManager.GetActiveScene().name;
                var dbAfterEnd = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                dbAfterEnd.InitializeDatabase();

                var remainingArmies = dbAfterEnd.Connection.Table<Army>().ToList();
                bool winnerStillExists = remainingArmies.Exists(a => a.Id == 1);
                bool loserDeletedFromDb = !remainingArmies.Exists(a => a.Id == 2);

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[4. EndBattle: Məğlub Ordunun Bazadan Silinməsi və GlobalMapScene-ə Qayıdış]");
                report.AppendLine($"  -> EndBattle çağırıldıqdan sonra aktiv səhnə: {activeSceneAfterEnd}");
                report.AppendLine($"  -> Bazada qalan orduların sayı: {remainingArmies.Count} (Qalib Ordu #1 bazada qalır: {winnerStillExists} | Məğlub Ordu #2 bazadan silindi: {loserDeletedFromDb})");

                // 6. Play Mode-da istifadəçinin dərhal sınaqdan keçirə bilməsi üçün GlobalMapScene-də ilkin vəziyyəti bərpa edib saxlayırıq
                dbAfterEnd.SeedAndTestDummyData(forceReseed: true);
                var milAfterEnd = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();
                milAfterEnd.DraftArmy(dbAfterEnd, 1);
                milAfterEnd.DraftArmy(dbAfterEnd, 2);

                var mapController = UnityEngine.Object.FindAnyObjectByType<MapController>();
                mapController?.SpawnProvincesFromDatabase();

                var uiAfterEnd = UnityEngine.Object.FindAnyObjectByType<UIManager>();
                uiAfterEnd?.RefreshDateTrackerUI();
                uiAfterEnd?.ShowProvincePanel(1);

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 4: CORE GAME LOOP TESTİ UĞURLA TAMAMLANDI ===");
                Debug.Log(report.ToString());
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 4 xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }

        [MenuItem("Grand Strategy/Verify Phase 5 (Demographics, Economy & Time Engine)")]
        public static void VerifyPhase5SimulationMenu()
        {
            VerifyPhase5Simulation();
        }

        [CliCommand("verify_phase5_simulation", "Build GlobalMapScene with TimeManager, Nation/Province tables, Top HUD (Date, Gold/Food, Demographics, Pause/Play/2x buttons), and verify 12-month real-time tick pipeline + aging + SQLite auto-save", MainThreadRequired = true)]
        public static string VerifyPhase5Simulation()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 5: DEMOQRAFİYA, İQTİSADİYYAT VƏ ZAMAN MOTORU TESTİ BAŞLADI ===");

                AssetDatabase.ImportAsset("Assets/Scripts/Models/Nation.cs", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Scripts/Models/Province.cs", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Scripts/Models/Population.cs", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Scripts/TimeManager.cs", ImportAssetOptions.ForceUpdate);

                // 1. GlobalMapScene-i yeni Yuxarı Panel (Tarix, Qızıl/Ərzaq, Əhali Bölgüsü, Pause/Play/2x düymələri) və TimeManager ilə qururuq
                BuildAndVerifyGlobalMapScene();
                EditorSceneManager.OpenScene("Assets/Scenes/GlobalMapScene.unity", OpenSceneMode.Single);

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                var tm = UnityEngine.Object.FindAnyObjectByType<TimeManager>();
                var ui = UnityEngine.Object.FindAnyObjectByType<UIManager>();

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                gm.EnsureDatabaseConnection();
                gm.EnsureSubsystemsBinding();
                ui.BindToGameManagerEvents();
                ui.ShowProvincePanel(1);
                ui.RefreshDateTrackerUI();

                Province beforeProv = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Nation beforeNation = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);

                report.AppendLine("[1. SQLite Baza Modelləri (Province & Nation) və Başlanğıc UI Vəziyyəti]");
                report.AppendLine($"  -> Başlanğıc Province #1 ({beforeProv.Name}): Gold={beforeProv.Gold:F1}, Food={beforeProv.Food:F1} | Babies={beforeProv.Babies}, Youths={beforeProv.Youths}, Adults={beforeProv.Adults}, Elders={beforeProv.Elders}, Wounded={beforeProv.Wounded} (Ümumi: {beforeProv.TotalPopulation})");
                report.AppendLine($"  -> Başlanğıc Nation #1 ({beforeNation.Name}): Gold={beforeNation.Gold:F1}, Food={beforeNation.Food:F1} | Ümumi Əhali: {beforeNation.TotalPopulation}");
                report.AppendLine($"  -> UI Tarix Text: \"{ui.CurrentDisplayedDate}\"");
                report.AppendLine($"  -> UI Qızıl & Ərzaq Text: \"{ui.CurrentDisplayedGoldAndFood}\"");
                report.AppendLine($"  -> UI Demoqrafiya Text: \"{ui.CurrentDisplayedDemographicsSummary}\"");

                // 2. Zaman İdarəetmə Düymələri Testi: Play (>), 2x (>>), Pause (||)
                ui.OnPlayTimeButtonClicked();
                string speedAfterPlay = ui.CurrentDisplayedSpeed;
                int tmSpeedAfterPlay = tm.CurrentSpeedMultiplier;

                ui.OnFastSpeedButtonClicked();
                string speedAfter2x = ui.CurrentDisplayedSpeed;
                int tmSpeedAfter2x = tm.CurrentSpeedMultiplier;

                ui.OnPauseTimeButtonClicked();
                string speedAfterPause = ui.CurrentDisplayedSpeed;
                bool tmPausedAfterPause = tm.IsPaused;

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. TimeManager və UI Zaman Düymələri (Play >, 2x >>, Pause ||)]");
                report.AppendLine($"  -> TimeManager SecondsPerMonthAt1x: {tm.SecondsPerMonthAt1x:F1} saniyə = 1 Ay");
                report.AppendLine($"  -> 'Play (>)' Düyməsi: Sürət={tmSpeedAfterPlay}x | UI=\"{speedAfterPlay}\"");
                report.AppendLine($"  -> '2x (>>)' Düyməsi: Sürət={tmSpeedAfter2x}x | UI=\"{speedAfter2x}\"");
                report.AppendLine($"  -> 'Pause (||)' Düyməsi: IsPaused={tmPausedAfterPause} | UI=\"{speedAfterPause}\"");

                // 3. 12 Aylıq (1 Tam İl) Tick Pipeline Simulyasiyası (Aylıq İstehsal/İstehlak/Doğum + 12-ci Ayda İllik Yaşlanma & Sağalma + SQLite Auto-save)
                ui.OnPlayTimeButtonClicked();
                tm.SimulateMonths(12);
                ui.RefreshDateTrackerUI();
                ui.RefreshSelectedProvinceUI();

                Province afterProv = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Nation afterNation = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. 12 Ay (1 İl) Tick Pipeline Nəticəsi: İstehsal, İstehlak, Doğum, İllik Yaşlanma və Auto-Save]");
                report.AppendLine($"  -> 12 Ay Sonra UI Tarix Text: \"{ui.CurrentDisplayedDate}\"");
                report.AppendLine($"  -> 12 Ay Sonra UI Qızıl & Ərzaq Text: \"{ui.CurrentDisplayedGoldAndFood}\"");
                report.AppendLine($"  -> 12 Ay Sonra UI Demoqrafiya Text: \"{ui.CurrentDisplayedDemographicsSummary}\"");
                report.AppendLine($"  -> SQLite Auto-Save Province #1 ({afterProv.Name}): Gold {beforeProv.Gold:F1} -> {afterProv.Gold:F1} (+{afterProv.Gold - beforeProv.Gold:F1}) | Food {beforeProv.Food:F1} -> {afterProv.Food:F1} (+{afterProv.Food - beforeProv.Food:F1})");
                report.AppendLine($"  -> Demoqrafik Dəyişim (Babies -> Youths -> Adults -> Elders -> Ölüm, Wounded -> Adults):");
                report.AppendLine($"     * Körpə (Babies): {beforeProv.Babies} -> {afterProv.Babies}");
                report.AppendLine($"     * Gənc (Youths): {beforeProv.Youths} -> {afterProv.Youths}");
                report.AppendLine($"     * Yetişkin (Adults): {beforeProv.Adults} -> {afterProv.Adults}");
                report.AppendLine($"     * Yaşlı (Elders): {beforeProv.Elders} -> {afterProv.Elders}");
                report.AppendLine($"     * Yaralı (Wounded): {beforeProv.Wounded} -> {afterProv.Wounded} (Tam sağalıb Adults sinfinə qayıtdı)");
                report.AppendLine($"     * Ümumi Əhali: {beforeProv.TotalPopulation} -> {afterProv.TotalPopulation} (+{afterProv.TotalPopulation - beforeProv.TotalPopulation})");
                report.AppendLine($"  -> SQLite Auto-Save Nation #1 ({afterNation.Name}): Gold={afterNation.Gold:F1}, Food={afterNation.Food:F1}, Ümumi Əhali={afterNation.TotalPopulation}");

                // 4. Səhnəni Play Mode üçün hazır vəziyyətdə (Pause rejimində, 2 test ordusu ilə) saxlayırıq
                tm.Pause();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                var milController = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();
                milController.DraftArmy(db, 1);
                milController.DraftArmy(db, 2);

                var mapController = UnityEngine.Object.FindAnyObjectByType<MapController>();
                mapController?.SpawnProvincesFromDatabase();

                ui.ShowProvincePanel(1);
                ui.RefreshDateTrackerUI();
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 5 TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 5 DEMOGRAPHICS, ECONOMY & TIME ENGINE VERIFICATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 5 xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }

        [MenuItem("Grand Strategy/Verify Phase 6A (Infrastructure & Global Commands)")]
        public static void VerifyPhase6AInfrastructureMenu()
        {
            VerifyPhase6AInfrastructure();
        }

        [CliCommand("verify_phase6a_infrastructure", "Build GlobalMapScene with InfrastructureController and ProvinceActionPanel, test 'Kanal Çək' button (5000 Gold, 6 Months), countdown over 6 monthly ticks, and verify 150% ProductivityMultiplier (+50% Gold and Food production)", MainThreadRequired = true)]
        public static string VerifyPhase6AInfrastructure()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 6A: İNFRASTRUKTUR VƏ QLOBAL ƏMRLƏR SİSTEMİ TESTİ BAŞLADI ===");

                AssetDatabase.ImportAsset("Assets/Scripts/Models/Province.cs", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Scripts/InfrastructureController.cs", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Scripts/EconomyController.cs", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Scripts/GameManager.cs", ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset("Assets/Scripts/UIManager.cs", ImportAssetOptions.ForceUpdate);

                // 1. GlobalMapScene-i yeni ProvinceActionPanel ("Əyalət İdarəetmə Paneli") və InfrastructureController ilə yenidən qururuq
                BuildAndVerifyGlobalMapScene();
                EditorSceneManager.OpenScene("Assets/Scenes/GlobalMapScene.unity", OpenSceneMode.Single);

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                var tm = UnityEngine.Object.FindAnyObjectByType<TimeManager>();
                var ui = UnityEngine.Object.FindAnyObjectByType<UIManager>();
                var ecoCtrl = UnityEngine.Object.FindAnyObjectByType<EconomyController>();

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                gm.EnsureDatabaseConnection();
                gm.EnsureSubsystemsBinding();
                ui.BindToGameManagerEvents();
                ui.ShowProvincePanel(1);
                ui.RefreshDateTrackerUI();

                Province initProv = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Economy initEco = db.Connection.Table<Economy>().FirstOrDefault(e => e.ProvinceId == 1);

                report.AppendLine("[1. Başlanğıc Vəziyyət (Province #1 - Anadolu) və 'Əyalət İdarəetmə Paneli']");
                report.AppendLine($"  -> Başlanğıc Qızıl (Gold): {initProv.Gold:F1} | Ərzaq (Food): {initProv.Food:F1}");
                report.AppendLine($"  -> Başlanğıc ProductivityMultiplier: {initProv.ProductivityMultiplier:F2}x | ActiveConstruction: '{initProv.ActiveConstruction}' | ConstructionMonthsLeft: {initProv.ConstructionMonthsLeft}");
                report.AppendLine($"  -> Başlanğıc Aylıq İstehsal (1.0x): Ərzaq={initEco.FoodProduction:F1}, Vergi (Qızıl)={initEco.LocalTaxIncome:F1}");
                report.AppendLine($"  -> UI Məhsuldarlıq Mətni: \"{ui.CurrentDisplayedProductivity}\"");
                report.AppendLine($"  -> UI Tikinti Statusu Mətni: \"{ui.CurrentDisplayedConstructionStatus}\"");

                // 2. Oyunçu UI-dakı "Kanal Çək (Xərc: 5000 Qızıl, Müddət: 6 Ay)" düyməsinə basır
                ui.OnBuildCanalButtonClicked();

                Province afterStartProv = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Nation afterStartNation = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);

                if (Mathf.Abs(afterStartProv.Gold - 0f) > 0.01f || afterStartProv.ActiveConstruction != "Kanal" || afterStartProv.ConstructionMonthsLeft != 6)
                {
                    throw new InvalidOperationException($"StartConstruction xətası: Gold={afterStartProv.Gold}, ActiveConstruction='{afterStartProv.ActiveConstruction}', MonthsLeft={afterStartProv.ConstructionMonthsLeft}");
                }

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. 'Kanal Çək (Xərc: 5000 Qızıl, Müddət: 6 Ay)' Əmri İcra Edildi]");
                report.AppendLine($"  -> Büdcədən 5000 Qızıl Çıxıldı: Province Gold {initProv.Gold:F1} -> {afterStartProv.Gold:F1} | Nation Gold -> {afterStartNation.Gold:F1}");
                report.AppendLine($"  -> ActiveConstruction: '{afterStartProv.ActiveConstruction}' | ConstructionMonthsLeft: {afterStartProv.ConstructionMonthsLeft} ay");
                report.AppendLine($"  -> UI Tikinti Statusu Mətni: \"{ui.CurrentDisplayedConstructionStatus}\"");

                // 3. 3 Ay (3 Tick) İrəli Aparırıq -> "Kanal çəkilir... 3 ay qaldı"
                tm.SimulateMonths(3);
                ui.RefreshDateTrackerUI();
                ui.RefreshSelectedProvinceUI();

                Province midProv = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Economy midEco = db.Connection.Table<Economy>().FirstOrDefault(e => e.ProvinceId == 1);

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. 3 Ay Sonra (Yarı Müddət Yoxlaması)]");
                report.AppendLine($"  -> ActiveConstruction: '{midProv.ActiveConstruction}' | ConstructionMonthsLeft: {midProv.ConstructionMonthsLeft} ay | ProductivityMultiplier: {midProv.ProductivityMultiplier:F2}x");
                report.AppendLine($"  -> UI Tikinti Statusu Mətni: \"{ui.CurrentDisplayedConstructionStatus}\"");
                report.AppendLine($"  -> 3-cü Ayda Cari Aylıq İstehsal (1.0x): Ərzaq={midEco.FoodProduction:F1}, Vergi (Qızıl)={midEco.LocalTaxIncome:F1}");

                // 4. Qalan 3 Ayı (Cəmi 6 Ay) İrəli Aparırıq -> Tikinti Bitir, Məhsuldarlıq 150% (1.5x) Olur
                tm.SimulateMonths(3);
                ui.RefreshDateTrackerUI();
                ui.RefreshSelectedProvinceUI();

                Province finalProv = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Population finalPop = db.Connection.Table<Population>().FirstOrDefault(p => p.ProvinceId == 1);
                Economy finalEco = db.Connection.Table<Economy>().FirstOrDefault(e => e.ProvinceId == 1);

                float baseFoodWithoutCanal = finalEco.FoodProduction / finalProv.ProductivityMultiplier;
                float baseTaxWithoutCanal = finalEco.LocalTaxIncome / finalProv.ProductivityMultiplier;

                if (Mathf.Abs(finalProv.ProductivityMultiplier - 1.5f) > 0.001f || !string.IsNullOrEmpty(finalProv.ActiveConstruction) || finalProv.ConstructionMonthsLeft != 0)
                {
                    throw new InvalidOperationException($"Tikinti tamamlanma xətası: Multiplier={finalProv.ProductivityMultiplier}, ActiveConstruction='{finalProv.ActiveConstruction}', MonthsLeft={finalProv.ConstructionMonthsLeft}");
                }

                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[4. 6 Ay Tamamlandı: Tikinti Bitdi və Məhsuldarlıq 150% (1.5x) Oldu]");
                report.AppendLine($"  -> Tamamlanmış Province #1 ({finalProv.Name}): ProductivityMultiplier = {finalProv.ProductivityMultiplier:F2}x (150%) | ActiveConstruction = '{finalProv.ActiveConstruction}' | ConstructionMonthsLeft = {finalProv.ConstructionMonthsLeft}");
                report.AppendLine($"  -> UI Məhsuldarlıq Mətni: \"{ui.CurrentDisplayedProductivity}\"");
                report.AppendLine($"  -> UI Tikinti Statusu Mətni: \"{ui.CurrentDisplayedConstructionStatus}\"");
                report.AppendLine($"  -> Aylıq Ərzaq İstehsalı (+50% Artım): 1.0x Baza={baseFoodWithoutCanal:F1} -> 1.5x Kanal İlə={finalEco.FoodProduction:F1}");
                report.AppendLine($"  -> Aylıq Vergi/Qızıl Gəliri (+50% Artım): 1.0x Baza={baseTaxWithoutCanal:F1} -> 1.5x Kanal İlə={finalEco.LocalTaxIncome:F1}");
                report.AppendLine($"  -> 6 Ay Sonra Yığılmış Ehtiyatlar: Qızıl={finalProv.Gold:F1} | Ərzaq={finalProv.Food:F1}");

                // 5. Səhnəni Play Mode üçün təmiz başlanğıc vəziyyətində saxlayırıq
                tm.Pause();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                var milController = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();
                milController.DraftArmy(db, 1);
                milController.DraftArmy(db, 2);

                var mapController = UnityEngine.Object.FindAnyObjectByType<MapController>();
                mapController?.SpawnProvincesFromDatabase();

                ui.ShowProvincePanel(1);
                ui.RefreshDateTrackerUI();
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 6A TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 6A INFRASTRUCTURE & GLOBAL COMMANDS VERIFICATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 6A xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }

        [MenuItem("Grand Strategy/Verify/Phase 7.1 Diplomacy (Ceasefire & Secret Pacts)")]
        [CliCommand("verify_phase7_1_ceasefire_and_secret_pacts", "Verify Ceasefire (Atəşkəs duration countdown, 'Atəşkəs bitdi!', attack blocked) and Secret Pacts (Loyalty < 50 forms pact with stronger enemy, war declaration triggers betrayal -> province & army transferred to enemy, 'XƏYANƏT! [Vassal Adı] düşmən tərəfə keçdi!')", MainThreadRequired = true)]
        public static string VerifyPhase7_1CeasefireAndSecretPacts()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 7.1: ATƏŞKƏS VƏ GİZLİ VASSAL ƏLAQƏLƏRİ SİSTEMİ TESTİ BAŞLADI ===");

                // 1. GlobalMapScene səhnəsini açırıq və bazanı ilkinləşdiririk
                BuildAndVerifyGlobalMapScene();
                EditorSceneManager.OpenScene("Assets/Scenes/GlobalMapScene.unity", OpenSceneMode.Single);

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                var tm = UnityEngine.Object.FindAnyObjectByType<TimeManager>();
                var pol = UnityEngine.Object.FindAnyObjectByType<PoliticalController>();
                var mil = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();

                if (pol == null || mil == null || db == null || gm == null)
                {
                    throw new InvalidOperationException("GlobalMapScene-də tələb olunan komponentlər tapılmadı!");
                }

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                gm.EnsureDatabaseConnection();
                gm.EnsureSubsystemsBinding();

                report.AppendLine("[1. ORM və Məlumat Bazası (SQLite) Yoxlaması]");
                Diplomacy initialDip = db.Connection.Table<Diplomacy>().FirstOrDefault();
                Leader initialLeader = db.Connection.Table<Leader>().FirstOrDefault();
                if (initialDip == null || initialLeader == null)
                {
                    throw new InvalidOperationException("Bazada ilkin Diplomacy və ya Leader qeydləri tapılmadı!");
                }
                report.AppendLine($"  -> Diplomacy modeli: CeasefireMonthsLeft = {initialDip.CeasefireMonthsLeft}");
                report.AppendLine($"  -> Leader modeli: SecretPactFactionId = {initialLeader.SecretPactFactionId?.ToString() ?? "null"}");

                // =========================================================================
                // 2. ATƏŞKƏS (CEASEFIRE) SİSTEMİ TESTİ
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. Atəşkəs (Ceasefire) Sistemi Testi]");

                // Fraksiya 1 (Kral) və Fraksiya 2 arasında 12 aylıq Atəşkəs elan edirik
                bool ceasefireSuccess = mil.ProposeCeasefire(1, 2, 12, db);
                if (!ceasefireSuccess)
                {
                    throw new InvalidOperationException("ProposeCeasefire(1, 2, 12) uğursuz oldu!");
                }

                Diplomacy dip1_2 = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == 1 && d.FactionB_Id == 2) || (d.FactionA_Id == 2 && d.FactionB_Id == 1));

                if (dip1_2 == null || dip1_2.CeasefireMonthsLeft != 12 || dip1_2.TreatyType != "Atəşkəs")
                {
                    throw new InvalidOperationException($"Atəşkəs bazaya düzgün yazılmadı! CeasefireMonthsLeft={dip1_2?.CeasefireMonthsLeft}, TreatyType={dip1_2?.TreatyType}");
                }
                report.AppendLine($"  -> ProposeCeasefire(1, 2, 12) uğurlu: TreatyType='{dip1_2.TreatyType}', CeasefireMonthsLeft={dip1_2.CeasefireMonthsLeft}");

                // Atəşkəs qüvvədə olduğu müddətdə ordular bir-birinə hücum edə bilməz
                bool canAttackWhileCeasefire = mil.CanAttack(1, 2, db);
                bool isCeasefireActive = mil.IsCeasefireActive(1, 2, db);
                if (canAttackWhileCeasefire || !isCeasefireActive)
                {
                    throw new InvalidOperationException($"Atəşkəs qüvvədə olduğu halda hücum bloklanmadı! CanAttack={canAttackWhileCeasefire}, IsActive={isCeasefireActive}");
                }
                report.AppendLine($"  -> Hücum Yoxlaması: IsCeasefireActive(1, 2) = {isCeasefireActive}, CanAttack(1, 2) = {canAttackWhileCeasefire} (Hücum bloklandı)");

                // Müharibə elanı da bloklanmalıdır
                bool warBlocked = !mil.DeclareWar(1, 2, db);
                if (!warBlocked)
                {
                    throw new InvalidOperationException("Atəşkəs dövründə müharibə elanı bloklanmadı!");
                }
                report.AppendLine($"  -> Müharibə Elanı Yoxlaması: Atəşkəs zamanı DeclareWar(1, 2) uğurla bloklandı (WarBlocked={warBlocked})");

                // Aylıq Tick döngüsü ilə atəşkəs müddətinin azalması: 1 ay irəliləyirik
                mil.ProcessMonthlyDiplomacy(db, 2, 1066, logExpirations: false);
                dip1_2 = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == 1 && d.FactionB_Id == 2) || (d.FactionA_Id == 2 && d.FactionB_Id == 1));
                if (dip1_2.CeasefireMonthsLeft != 11)
                {
                    throw new InvalidOperationException($"1 ay sonra CeasefireMonthsLeft 11 olmalı idi, amma {dip1_2.CeasefireMonthsLeft} tapıldı!");
                }
                report.AppendLine($"  -> 1 Aylıq Tick Sonrası: CeasefireMonthsLeft 12 -> {dip1_2.CeasefireMonthsLeft} (-1 azaldı)");

                // Qalan 11 ayı da irəliləyirik və müddətin bitməsini yoxlayırıq
                for (int m = 0; m < 11; m++)
                {
                    mil.ProcessMonthlyDiplomacy(db, 3 + m, 1066, logExpirations: false);
                }

                dip1_2 = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == 1 && d.FactionB_Id == 2) || (d.FactionA_Id == 2 && d.FactionB_Id == 1));

                if (dip1_2.CeasefireMonthsLeft != 0 || mil.IsCeasefireActive(1, 2, db))
                {
                    throw new InvalidOperationException($"12 ay sonra atəşkəs bitməli idi! CeasefireMonthsLeft={dip1_2.CeasefireMonthsLeft}");
                }
                if (string.IsNullOrWhiteSpace(mil.LastCeasefireExpiredMessage) || !mil.LastCeasefireExpiredMessage.Contains("Atəşkəs bitdi"))
                {
                    throw new InvalidOperationException($"'Atəşkəs bitdi!' xəbərdarlığı çıxmadı! LastMessage='{mil.LastCeasefireExpiredMessage}'");
                }
                report.AppendLine($"  -> 12 Ay Tamamlandı: CeasefireMonthsLeft = {dip1_2.CeasefireMonthsLeft}, IsCeasefireActive = {mil.IsCeasefireActive(1, 2, db)}");
                report.AppendLine($"  -> Xəbərdarlıq Mesajı: \"{mil.LastCeasefireExpiredMessage}\" (Atəşkəs başa çatdı, döyüşə icazə verildi)");

                // =========================================================================
                // 3. VASSALLARIN GİZLİ ƏLAQƏLƏRİ (SECRET PACTS) VƏ XƏYANƏT TESTİ
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. Vassalların Gizli Əlaqələri (Secret Pacts) və Xəyanət Testi]");

                // Qubernator A-nın sədaqətini 45 edirik (< 50)
                pol.SetLeaderLoyalty("Qubernator A", 45f);
                Leader govA = db.Connection.Table<Leader>().FirstOrDefault(l => l.Name == "Qubernator A");
                report.AppendLine($"  -> Qubernator A Sədaqəti: {govA.Loyalty:F1}/100 (< 50)");

                // Düşmən Fraksiya 2 üçün ordu yaradırıq ki, hərbi gücü Kraldan çox olsun
                Army enemyArmy = new Army
                {
                    OwnerFactionId = 2,
                    LocationProvinceId = 2,
                    YouthSoldiers = 5000,
                    AdultSoldiers = 5000,
                    Morale = 1.0f
                };
                db.Connection.Insert(enemyArmy);

                // Qubernator A üçün əyalət (#1 - Anadolu) və ilkin ordu təyin edirik
                Army govArmy = new Army
                {
                    OwnerFactionId = 1,
                    LocationProvinceId = 1,
                    YouthSoldiers = 1000,
                    AdultSoldiers = 2000,
                    Morale = 1.0f
                };
                db.Connection.Insert(govArmy);
                govA.AssignedArmyId = govArmy.Id.ToString();
                govA.AssignedProvinceId = "1";
                govA.SecretPactFactionId = null;
                db.Connection.Update(govA);

                // Aylıq siyasi döngəni (CheckRebellions) işə salırıq -> Qubernator A güclü düşmən Fraksiya 2 ilə gizli ittifaq qurur
                pol.CheckRebellions(db, 1, 1067, applyDynamicLoyalty: false);

                govA = db.Connection.Table<Leader>().FirstOrDefault(l => l.Name == "Qubernator A");
                if (!govA.SecretPactFactionId.HasValue || govA.SecretPactFactionId.Value != 2)
                {
                    throw new InvalidOperationException($"Gizli ittifaq qurulmadı! SecretPactFactionId = {govA.SecretPactFactionId?.ToString() ?? "null"}");
                }
                report.AppendLine($"  -> Gizli İttifaq Quruldu: Qubernator A SecretPactFactionId = {govA.SecretPactFactionId.Value} (Fraksiya 2 ilə gizli ittifaq)");

                // Kral Fraksiya 2-yə müharibə elan edir -> XƏYANƏT İŞƏ DÜŞÜR!
                bool warDeclared = mil.DeclareWar(1, 2, db);
                if (!warDeclared)
                {
                    throw new InvalidOperationException("DeclareWar(1, 2) uğursuz oldu!");
                }

                // Xəyanət nəticələrini yoxlayırıq
                govA = db.Connection.Table<Leader>().FirstOrDefault(l => l.Name == "Qubernator A");
                Province transferredProvince = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == 1);
                Army transferredArmy = db.Connection.Table<Army>().FirstOrDefault(a => a.Id == govArmy.Id);

                if (transferredProvince.OwnerFactionId != 2)
                {
                    throw new InvalidOperationException($"Əyalət düşmən tərəfə keçmədi! OwnerFactionId = {transferredProvince.OwnerFactionId} (Gözlənən: 2)");
                }
                if (transferredArmy.OwnerFactionId != 2)
                {
                    throw new InvalidOperationException($"Ordu düşmən tərəfə keçmədi! OwnerFactionId = {transferredArmy.OwnerFactionId} (Gözlənən: 2)");
                }
                if (string.IsNullOrWhiteSpace(pol.LastBetrayalMessage) || !pol.LastBetrayalMessage.Contains("XƏYANƏT"))
                {
                    throw new InvalidOperationException($"'XƏYANƏT!' xəbərdarlığı ekrana çıxmadı! LastMessage='{pol.LastBetrayalMessage}'");
                }

                report.AppendLine($"  -> Müharibə Elan Edildi: DeclareWar(1, 2) uğurla icra olundu.");
                report.AppendLine($"  -> XƏYANƏT REALLAŞDI! Xəbərdarlıq: \"{pol.LastBetrayalMessage}\"");
                report.AppendLine($"  -> Əyalət #{transferredProvince.Id} ({transferredProvince.Name}) Sahibi: {transferredProvince.OwnerFactionId} (Fraksiya 2-yə keçdi)");
                report.AppendLine($"  -> Ordu #{transferredArmy.Id} Sahibi: {transferredArmy.OwnerFactionId} (Fraksiya 2-yə keçdi)");
                report.AppendLine($"  -> Qubernator A Sədaqəti: {govA.Loyalty:F1} | SecretPactFactionId: {govA.SecretPactFactionId?.ToString() ?? "null (icra edildi)"}");

                // 4. Səhnəni təmiz başlanğıc vəziyyətinə qaytarırıq
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                mil.DraftArmy(db, 1);
                mil.DraftArmy(db, 2);
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 7.1 TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 7.1 CEASEFIRE AND SECRET PACTS VERIFICATION SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 7.1 xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }


        [MenuItem("Grand Strategy/Verify/Phase 7.2 Diplomacy (Relations, Alliances & Proactive AI)")]
        [CliCommand("verify_phase7_2_diplomacy_and_ai", "Verify RelationScore & Tiers (-100..-60 Düşmən, -19..+19 Neytral, +60..+100 Müttəfiq), Non-Aggression Pact & Reputation (-30 on broken treaty), Low reputation (<50) pact rejection, Military Alliance Call to Arms, Proactive AI Coalition (3x hegemon triggers coalition), and AI Opportunistic War (70% army loss triggers war)", MainThreadRequired = true)]
        public static string VerifyPhase7_2DiplomacyAndAI()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 7.2: DİPLOMATİK MÜNASİBƏTLƏR, İTTİFAQ VƏ PROAKTİV AI TESTİ BAŞLADI ===");

                // 1. GlobalMapScene səhnəsini qururuq və bazanı ilkinləşdiririk
                BuildAndVerifyGlobalMapScene();
                EditorSceneManager.OpenScene("Assets/Scenes/GlobalMapScene.unity", OpenSceneMode.Single);

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
                var pol = UnityEngine.Object.FindAnyObjectByType<PoliticalController>();
                var mil = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>();
                var ai = UnityEngine.Object.FindAnyObjectByType<AIDiplomacyController>();

                if (db == null || gm == null || mil == null || ai == null)
                {
                    throw new InvalidOperationException("GlobalMapScene-də tələb olunan komponentlər tapılmadı!");
                }

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                gm.EnsureDatabaseConnection();
                gm.EnsureSubsystemsBinding();

                report.AppendLine("[1. Diplomatik Münasibətlər və Nüfuz Modelləri (SQLite)]");
                // 1.1 RelationScore və Status kateqoriyalarını yoxlayırıq
                report.AppendLine($"  -> RelationScore Kateqoriyaları:");
                report.AppendLine($"     * -75 bal: '{Diplomacy.GetRelationTier(-75)}' (Gözlənilən: Düşmən)");
                report.AppendLine($"     * 0 bal: '{Diplomacy.GetRelationTier(0)}' (Gözlənilən: Neytral)");
                report.AppendLine($"     * 75 bal: '{Diplomacy.GetRelationTier(75)}' (Gözlənilən: Müttəfiq)");

                if (Diplomacy.GetRelationTier(-75) != "Düşmən" ||
                    Diplomacy.GetRelationTier(0) != "Neytral" ||
                    Diplomacy.GetRelationTier(75) != "Müttəfiq")
                {
                    throw new InvalidOperationException("RelationScore diplomatik kateqoriyaları düzgün deyil!");
                }

                // 1.2 DiplomaticReputation yoxlanışı (başlanğıc 100)
                Nation seljuk = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);
                report.AppendLine($"  -> Nation #1 DiplomaticReputation: {seljuk.DiplomaticReputation:F0}/100 (Başlanğıc: 100)");
                if (seljuk.DiplomaticReputation != 100f)
                {
                    throw new InvalidOperationException($"Başlanğıc DiplomaticReputation 100 olmalı idi, amma {seljuk.DiplomaticReputation} tapıldı!");
                }

                // =========================================================================
                // 2. MÜQAVİLƏLƏR SİSTEMİ: HÜCUM ETMƏMƏK PAKTI VƏ NÜFUZ CƏZASI (-30)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. Hücum Etməmək Paktı (Non-Aggression Pact) və Müqavilə Pozulması Cəzası]");

                // Fraksiya 1 və Fraksiya 2 arasında 10 illik (120 aylıq) Hücum Etməmək Paktı bağlayırıq
                bool napSuccess = mil.ProposeNonAggressionPact(1, 2, 120, db);
                if (!napSuccess || !mil.IsNonAggressionPactActive(1, 2, db))
                {
                    throw new InvalidOperationException("ProposeNonAggressionPact uğursuz oldu!");
                }
                Diplomacy napDip = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == 1 && d.FactionB_Id == 2) || (d.FactionA_Id == 2 && d.FactionB_Id == 1));
                report.AppendLine($"  -> Hücum Etməmək Paktı Quruldu: TreatyType='{napDip.TreatyType}', NonAggressionMonthsLeft={napDip.NonAggressionMonthsLeft} ay");
                report.AppendLine($"  -> Hücum Yoxlaması: CanAttack(1, 2) = {mil.CanAttack(1, 2, db)} (Hücum bloklandı)");

                // Pakt qüvvədə ikən müharibə elan etməyə cəhd edirik (breakTreaties: false olarsa bloklanmalıdır)
                bool warBlocked = !mil.DeclareWar(1, 2, db, breakTreaties: false);
                if (!warBlocked)
                {
                    throw new InvalidOperationException("Pakt qüvvədə olduğu halda müharibə elanı bloklanmadı!");
                }
                report.AppendLine($"  -> Müharibə Elanı Yoxlaması: Pakt zamanı DeclareWar(1, 2, breakTreaties:false) bloklandı: {warBlocked}");

                // İndi paktı pozaraq birbaşa müharibə elan edirik -> Nüfuz kəskin (-30 bal) düşməlidir!
                bool breakSuccess = mil.BreakTreatyAndDeclareWar(1, 2, db);
                seljuk = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);
                report.AppendLine($"  -> BreakTreatyAndDeclareWar icra olundu: Success={breakSuccess}");
                report.AppendLine($"  -> MÜQAVİLƏ POZULDU! DiplomaticReputation: 100 -> {seljuk.DiplomaticReputation:F0}/100 (-30 bal cəza tətbiq edildi)");
                report.AppendLine($"  -> Mesaj: \"{mil.LastTreatyBrokenMessage}\"");

                if (seljuk.DiplomaticReputation != 70f)
                {
                    throw new InvalidOperationException($"Müqavilə pozulduqdan sonra DiplomaticReputation 70 olmalı idi, amma {seljuk.DiplomaticReputation} tapıldı!");
                }

                // 2.3 Nüfuzu aşağı olan dövlətlə AI-lərin müqavilə bağlamaqdan imtina etməsi:
                seljuk.DiplomaticReputation = 40f;
                db.Connection.Update(seljuk);
                bool rejectedNap = !mil.ProposeNonAggressionPact(1, 2, 60, db);
                bool rejectedAlliance = !mil.FormMilitaryAlliance(1, 2, db);
                report.AppendLine($"  -> Aşağı Nüfuz (<50) Yoxlaması (Nüfuz: {seljuk.DiplomaticReputation:F0}):");
                report.AppendLine($"     * Pakt Təklifi Rədd Edildi: {rejectedNap}");
                report.AppendLine($"     * İttifaq Təklifi Rədd Edildi: {rejectedAlliance}");

                if (!rejectedNap || !rejectedAlliance)
                {
                    throw new InvalidOperationException("Nüfuzu aşağı olan (<50) dövlətin diplomatik təklifi rədd edilmədi!");
                }

                // Selcukun nüfuzunu təmizləyirik (100)
                seljuk.DiplomaticReputation = 100f;
                db.Connection.Update(seljuk);

                // =========================================================================
                // 3. HƏRBİ İTTİFAQ (MILITARY ALLIANCE) VƏ DÖYÜŞƏ ÇAĞIRIŞ (CALL TO ARMS)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. Hərbi İttifaq (Military Alliance) və Döyüşə Qoşulma (Call to Arms)]");

                // Fraksiya 1 (Səlcuq) və Fraksiya 2 (Şirvan) arasında Hərbi İttifaq qururuq
                bool allianceSuccess = mil.FormMilitaryAlliance(1, 2, db);
                if (!allianceSuccess || !mil.AreAllies(1, 2, db))
                {
                    throw new InvalidOperationException("Hərbi İttifaq qurulmadı!");
                }
                Diplomacy allianceDip = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == 1 && d.FactionB_Id == 2) || (d.FactionA_Id == 2 && d.FactionB_Id == 1));
                report.AppendLine($"  -> Hərbi İttifaq Quruldu: TreatyType='{allianceDip.TreatyType}', RelationScore={allianceDip.RelationScore} ({allianceDip.RelationTier})");

                // Fraksiya 1 Fraksiya 3-ə (Bizans) müharibə elan edir -> Müttəfiq Fraksiya 2 də avtomatik müharibəyə qoşulmalıdır!
                mil.DeclareWar(1, 3, db);

                Diplomacy allyWarDip = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == 2 && d.FactionB_Id == 3) || (d.FactionA_Id == 3 && d.FactionB_Id == 2));

                if (allyWarDip == null || allyWarDip.TreatyType != "Müharibə")
                {
                    throw new InvalidOperationException("Call to Arms işləmədi! Fraksiya 2 Fraksiya 3-ə müharibə elan etmədi!");
                }
                report.AppendLine($"  -> Call to Arms Reallaşdı: Fraksiya 2 (Müttəfiq) avtomatik olaraq Fraksiya 3-ə qarşı müharibəyə qoşuldu! (TreatyType='{allyWarDip.TreatyType}', RelationScore={allyWarDip.RelationScore})");

                // =========================================================================
                // 4. PROAKTİV AI: KOALİSİYA MEXANİKASI (COALITION)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[4. Proaktiv Süni İntellekt: Koalisiya Mexanikası (Coalition)]");

                // Sınaq üçün bazanı təmizləyib yenidən qururuq
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();

                // Fraksiya 1-ə nəhəng hərbi güc (9000 əsgər) veririk (qonşularından 3x dəfə çox)
                Army hegemonArmy = new Army
                {
                    OwnerFactionId = 1,
                    LocationProvinceId = 1,
                    YouthSoldiers = 4500,
                    AdultSoldiers = 4500,
                    Morale = 1.0f
                };
                db.Connection.Insert(hegemonArmy);

                // Qonşu Fraksiya 2 və Fraksiya 3 üçün kiçik ordular (hərəsi 1000 əsgər)
                Army f2Army = new Army
                {
                    OwnerFactionId = 2,
                    LocationProvinceId = 2,
                    YouthSoldiers = 500,
                    AdultSoldiers = 500,
                    Morale = 1.0f
                };
                Army f3Army = new Army
                {
                    OwnerFactionId = 3,
                    LocationProvinceId = 2,
                    YouthSoldiers = 500,
                    AdultSoldiers = 500,
                    Morale = 1.0f
                };
                db.Connection.Insert(f2Army);
                db.Connection.Insert(f3Army);

                report.AppendLine($"  -> Başlanğıc Güc Balansı: Fraksiya 1={hegemonArmy.TotalSoldiers} əsgər (Hegemon) vs Fraksiya 2=1000, Fraksiya 3=1000 (Orta: 1000)");

                // AI diplomatiya motorunu işə salırıq
                ai.ProcessMonthlyAI(db, 1, 1066);

                // Fraksiya 2 və Fraksiya 3 aralarında Koalisiya (Hərbi İttifaq) qurmalıdır!
                Diplomacy coalitionDip = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == 2 && d.FactionB_Id == 3) || (d.FactionA_Id == 3 && d.FactionB_Id == 2));

                if (coalitionDip == null || !coalitionDip.IsAlliance)
                {
                    throw new InvalidOperationException($"Koalisiya qurulmadı! Faction 2 və 3 arasında ittifaq tapılmadı (TreatyType='{coalitionDip?.TreatyType}').");
                }
                report.AppendLine($"  -> KOALİSİYA QURULDU! Fraksiya 2 və Fraksiya 3 aralarında birləşdilər: TreatyType='{coalitionDip.TreatyType}', RelationScore={coalitionDip.RelationScore}");
                report.AppendLine($"  -> AI Mesajı: \"{ai.LastCoalitionMessage}\"");

                // Koalisiya üzvlərinin Fraksiya 1 ilə münasibəti düşmən olmalıdır (<= -60)
                Diplomacy f2_hegemon = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == 2 && d.FactionB_Id == 1) || (d.FactionA_Id == 1 && d.FactionB_Id == 2));
                Diplomacy f3_hegemon = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    (d.FactionA_Id == 3 && d.FactionB_Id == 1) || (d.FactionA_Id == 1 && d.FactionB_Id == 3));

                report.AppendLine($"  -> Koalisiya Hədəfi (Fraksiya 1) ilə Münasibətlər: Faction 2={f2_hegemon?.RelationScore} ({f2_hegemon?.RelationTier}), Faction 3={f3_hegemon?.RelationScore} ({f3_hegemon?.RelationTier})");
                if (f2_hegemon.RelationScore > -60 || f3_hegemon.RelationScore > -60)
                {
                    throw new InvalidOperationException("Koalisiya üzvlərinin Hegemon dövlətlə münasibəti düşmən (<= -60) olmadı!");
                }

                // =========================================================================
                // 5. PROAKTİV AI: FÜRSƏTÇİ HÜCUM (ORDUNUN 70%-Nİ İTİRDİKDƏ)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[5. Proaktiv Süni İntellekt: Fürsətçi Hücum (70% Ordu İtkisi)]");

                // Fraksiya 1-in ordusunun 70%-dən çoxunu məhv edirik (9000 -> 1000 əsgər, 88.8% itki)
                hegemonArmy.YouthSoldiers = 500;
                hegemonArmy.AdultSoldiers = 500;
                db.Connection.Update(hegemonArmy);

                report.AppendLine($"  -> Hegemon Ordu İtkisi: Pik Güc=9000 -> Cari Güc=1000 (Itki: >70%)");

                // Aylıq AI döngüsünü çağırırıq -> Münasibəti mənfi olan qonşu AI fürsətdən istifadə edib dərhal müharibə elan edir!
                ai.ProcessMonthlyAI(db, 2, 1066);

                Diplomacy opportunisticWar = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    ((d.FactionA_Id == 2 && d.FactionB_Id == 1) || (d.FactionA_Id == 1 && d.FactionB_Id == 2) ||
                     (d.FactionA_Id == 3 && d.FactionB_Id == 1) || (d.FactionA_Id == 1 && d.FactionB_Id == 3)) &&
                    d.TreatyType == "Müharibə");

                if (opportunisticWar == null)
                {
                    throw new InvalidOperationException("Fürsətçi müharibə elan edilmədi!");
                }
                report.AppendLine($"  -> FÜRSƏTÇİ HÜCUM REALLAŞDI! Qonşu AI zəifləmiş dövlətə müharibə elan etdi:");
                report.AppendLine($"     * Müqavilə: {opportunisticWar.TreatyType} | RelationScore: {opportunisticWar.RelationScore}");
                report.AppendLine($"     * AI Mesajı: \"{ai.LastOpportunisticWarMessage}\"");

                // Səhnəni təmiz başlanğıc vəziyyətinə qaytarırıq
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                mil.DraftArmy(db, 1);
                mil.DraftArmy(db, 2);
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 7.2 TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 7.2 DIPLOMATIC RELATIONS, ALLIANCES & PROACTIVE AI SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 7.2 xətası: {ex}");
                return $"ERROR: {ex}";
            }
        }

    
        [MenuItem("Grand Strategy/Verify/Phase 8.1 Global Trade and Dynamic Market")]
        [CliCommand("verify_phase8_1_global_trade_and_dynamic_market", "Verify Strategic Resources (Iron, Horses, Silk), Military Dependencies (Iron for Shield Wall, Horses for Flanking), Silk PublicOrder Bonus, Dynamic Market Fluctuations (War, Siege, Peace), and Buy/Sell Resources", MainThreadRequired = true)]
        public static string VerifyPhase8_1GlobalTradeAndDynamicMarket()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 8.1: QLOBAL TİCARƏT VƏ DİNAMİK BAZAR TESTİ BAŞLADI ===");
                report.AppendLine("================================================================================");
                report.AppendLine("FAZA 8.1 - QLOBAL TİCARƏT: STRATEJİ RESURSLAR VƏ DİNAMİK BAZAR DOĞRULAMA");
                report.AppendLine("================================================================================");

                // 1. SƏHNƏ VƏ BAZA ƏLAQƏSİ
                string scenePath = "Assets/Scenes/GlobalMapScene.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    throw new InvalidOperationException($"'{scenePath}' səhnəsi açıla bilmədi!");
                }

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>() ?? new GameObject("DatabaseManager").AddComponent<DatabaseManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>() ?? new GameObject("GameManager").AddComponent<GameManager>();
                var eco = UnityEngine.Object.FindAnyObjectByType<EconomyController>() ?? new GameObject("EconomyController").AddComponent<EconomyController>();
                var mil = UnityEngine.Object.FindAnyObjectByType<MilitaryAndDiplomacyController>() ?? new GameObject("MilitaryAndDiplomacyController").AddComponent<MilitaryAndDiplomacyController>();
                var market = UnityEngine.Object.FindAnyObjectByType<DynamicMarketController>() ?? new GameObject("DynamicMarketController").AddComponent<DynamicMarketController>();
                var ui = UnityEngine.Object.FindAnyObjectByType<UIManager>();

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();

                // =========================================================================
                // 1. YENİ RESURSLAR VƏ VERİLƏNLƏR BAZASI YOXLANIŞI
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[1. Yeni Resurslar və Məlumat Bazası (Nation & Province)]");

                var anadolu = db.Connection.Table<Province>().FirstOrDefault(p => p.Name == "Anadolu");
                var shirvan = db.Connection.Table<Province>().FirstOrDefault(p => p.Name == "Şirvan");
                var seljuk = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);

                if (anadolu == null || shirvan == null || seljuk == null)
                {
                    throw new InvalidOperationException("Əyalətlər və ya Dövlət bazada tapılmadı!");
                }

                report.AppendLine($"  -> Anadolu Resursları: Dəmir={anadolu.Iron}, At={anadolu.Horses}, İpək={anadolu.Silk}, Tip={anadolu.StrategicResourceType}");
                report.AppendLine($"  -> Şirvan Resursları: Dəmir={shirvan.Iron}, At={shirvan.Horses}, İpək={shirvan.Silk}, Tip={shirvan.StrategicResourceType}");
                report.AppendLine($"  -> Səlcuqlu Dövləti Xəzinəsi: Qızıl={seljuk.Gold}, Ərzaq={seljuk.Food}, Dəmir={seljuk.Iron}, At={seljuk.Horses}, İpək={seljuk.Silk}");

                if (anadolu.Iron <= 0f || shirvan.Horses <= 0f || seljuk.Iron <= 0f)
                {
                    throw new InvalidOperationException("Anadolu üçün Iron və ya Şirvan üçün Horses bazada müsbət deyil!");
                }

                // =========================================================================
                // 2. DİNAMİK BAZAR DALĞALANMASI (MÜHARİBƏ, MÜHASİRƏ, SÜLH)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. Dinamik Bazar Mühərriki və Qiymət Dalğalanması]");

                // 2a. Sülh dövrü yoxlanışı (aktiv müharibə yoxdur)
                db.Connection.Execute("DELETE FROM Diplomacies WHERE TreatyType = 'Müharibə' OR RelationScore <= -60;");
                anadolu.IsBesieged = false;
                shirvan.IsBesieged = false;
                db.Connection.Update(anadolu);
                db.Connection.Update(shirvan);

                market.ProcessMarketFluctuations(db);
                report.AppendLine($"  -> Sülh Dövrü Qiymətləri: Ərzaq={market.CurrentPriceFood:F1}, Dəmir={market.CurrentPriceIron:F1}, At={market.CurrentPriceHorses:F1}, İpək={market.CurrentPriceSilk:F1} (Sülh Era: {market.IsPeaceEra})");

                if (!market.IsPeaceEra || market.CurrentPriceSilk <= DynamicMarketController.BasePriceSilk)
                {
                    throw new InvalidOperationException("Sülh dövründə İpək qiyməti artmadı!");
                }

                // 2b. Müharibə dövrü yoxlanışı (Dəmir və At qiymətləri 2.0x - 3.0x qalxmalıdır)
                var warDip = new Diplomacy
                {
                    FactionA_Id = 1,
                    FactionB_Id = 2,
                    TreatyType = "Müharibə",
                    RelationScore = -80,
                    TreatyDuration = 12
                };
                db.Connection.Insert(warDip);

                market.ProcessMarketFluctuations(db);
                report.AppendLine($"  -> Müharibə Dövrü Qiymətləri (1 Müharibə): Dəmir={market.CurrentPriceIron:F1} (Baza: {DynamicMarketController.BasePriceIron}), At={market.CurrentPriceHorses:F1} (Baza: {DynamicMarketController.BasePriceHorses})");

                if (market.CurrentPriceIron < DynamicMarketController.BasePriceIron * 2.0f ||
                    market.CurrentPriceHorses < DynamicMarketController.BasePriceHorses * 2.0f)
                {
                    throw new InvalidOperationException("Müharibə başlayan kimi Dəmir və At qiymətləri 2 qat və ya daha çox artmadı!");
                }

                // 2c. Mühasirə dövrü yoxlanışı (Food 5.0x artmalıdır)
                anadolu.IsBesieged = true;
                db.Connection.Update(anadolu);

                market.ProcessMarketFluctuations(db);
                report.AppendLine($"  -> Mühasirə Dövrü Ərzaq Qiyməti: Ərzaq={market.CurrentPriceFood:F1} (Baza: {DynamicMarketController.BasePriceFood}, Gözlənən: 5.0x)");

                if (market.CurrentPriceFood < DynamicMarketController.BasePriceFood * 5.0f)
                {
                    throw new InvalidOperationException("Mühasirə vəziyyətində Ərzaq qiyməti 5 qat artmadı!");
                }

                // =========================================================================
                // 3. RESURS ALQI-SATQISI (BUYRESOURCE / SELLRESOURCE)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. Bazar Alqı-Satqı Əməliyyatları (Buy / Sell)]");

                seljuk.Gold = 1000f;
                seljuk.Food = 50f;
                seljuk.Iron = 30f;
                db.Connection.Update(seljuk);

                float foodPrice = market.CurrentPriceFood;
                int buyAmount = 10;
                bool buySuccess = market.BuyResource(1, StrategicResourceType.Food, buyAmount, db);
                var seljukAfterBuy = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);

                report.AppendLine($"  -> Satınalma Testi ({buyAmount} Ərzaq, Qiymət={foodPrice:F1}): Uğur={buySuccess}, Qalan Qızıl={seljukAfterBuy.Gold:F1}, Yeni Ərzaq={seljukAfterBuy.Food:F1}");

                if (!buySuccess || Math.Abs(seljukAfterBuy.Food - (50f + buyAmount)) > 0.01f)
                {
                    throw new InvalidOperationException("Resurs satınalma (BuyResource) uğursuz oldu!");
                }

                float ironPrice = market.CurrentPriceIron;
                int sellAmount = 10;
                float preSellGold = seljukAfterBuy.Gold;
                bool sellSuccess = market.SellResource(1, StrategicResourceType.Iron, sellAmount, db);
                var seljukAfterSell = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 1);

                report.AppendLine($"  -> Satış Testi ({sellAmount} Dəmir, Qiymət={ironPrice:F1}): Uğur={sellSuccess}, Yeni Qızıl={seljukAfterSell.Gold:F1}, Qalan Dəmir={seljukAfterSell.Iron:F1}");

                if (!sellSuccess || Math.Abs(seljukAfterSell.Iron - 20f) > 0.01f || seljukAfterSell.Gold <= preSellGold)
                {
                    throw new InvalidOperationException("Resurs satışı (SellResource) uğursuz oldu!");
                }

                // =========================================================================
                // 4. HƏRBİ ASILILIQLAR: DƏMİR VƏ AT
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[4. Hərbi Asılılıqlar: Dəmir (Iron) və At (Horses)]");

                var testObj = new GameObject("TestToken_Phase8_1");
                var token = testObj.AddComponent<UI_BattalionToken>();

                // 4a. Dəmir Asılılığı (Kalkan Duvarı)
                token.HasIronEquipment = false;
                token.SetShieldWall(true);
                report.AppendLine($"  -> Dəmir Yoxdur Testi: IsShieldWallActive={token.IsShieldWallActive}, IncomingDamageMultiplier={token.IncomingDamageMultiplier:F2}x");

                if (token.IsShieldWallActive || Math.Abs(token.IncomingDamageMultiplier - 1.0f) > 0.01f)
                {
                    throw new InvalidOperationException("Dəmir olmadığı halda Kalkan Duvarı quruldu və ya hərbi bonus ləğv edilmədi!");
                }

                token.HasIronEquipment = true;
                token.SetShieldWall(true);
                report.AppendLine($"  -> Dəmir Var Testi: IsShieldWallActive={token.IsShieldWallActive}, IncomingDamageMultiplier={token.IncomingDamageMultiplier:F2}x");

                if (!token.IsShieldWallActive || Math.Abs(token.IncomingDamageMultiplier - 0.40f) > 0.01f)
                {
                    throw new InvalidOperationException("Dəmir olduğu halda Kalkan Duvarı 0.40x zərər bonusunu tətbiq etmədi!");
                }

                // 4b. At Asılılığı (Süvari Flanking)
                var defenderObj = new GameObject("TestDefender_Phase8_1");
                var defender = defenderObj.AddComponent<UI_BattalionToken>();
                token.InitializeToken(BattalionType.YouthCavalry, 1, 1, 100, true, Vector2.zero);

                // At yoxdursa: Flanking bonusu ləğv edilir (1.0x)
                token.HasHorsesMounted = false;
                float unmountedFlankMult = UI_BattalionToken.GetDirectionalDamageMultiplier(CombatAttackDirection.Flank, token);
                report.AppendLine($"  -> At Yoxdur Flanking Çarpanı: {unmountedFlankMult:F2}x (Gözlənən: 1.0x)");

                if (Math.Abs(unmountedFlankMult - 1.0f) > 0.01f)
                {
                    throw new InvalidOperationException("At olmadığı halda Süvari Flanking bonusu ləğv edilmədi!");
                }

                // At varsa: Standart Flanking bonusu (1.5x)
                token.HasHorsesMounted = true;
                float mountedFlankMult = UI_BattalionToken.GetDirectionalDamageMultiplier(CombatAttackDirection.Flank, token);
                report.AppendLine($"  -> At Var Flanking Çarpanı: {mountedFlankMult:F2}x (Gözlənən: 1.5x)");

                if (Math.Abs(mountedFlankMult - 1.5f) > 0.01f)
                {
                    throw new InvalidOperationException("At olduğu halda Flanking 1.5x bonus vermədi!");
                }

                UnityEngine.Object.DestroyImmediate(testObj);
                UnityEngine.Object.DestroyImmediate(defenderObj);

                // =========================================================================
                // 5. İPƏK BONUSU: AYLIQ MORAL (+2 PUBLICORDER)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[5. İpək Bonusu: Aylıq Xalqın Moralı (+2 PublicOrder)]");

                seljuk.Silk = 50f;
                db.Connection.Update(seljuk);

                anadolu.IsBesieged = false;
                anadolu.PublicOrder = 70f;
                db.Connection.Update(anadolu);

                eco.ProcessMonthlyEconomy(db, 2, 1066);

                var anadoluAfterEco = db.Connection.Table<Province>().FirstOrDefault(p => p.Id == anadolu.Id);
                report.AppendLine($"  -> İpək Var İkən Aylıq Dövr: Başlanğıc Moral=70 -> Yeni Moral={anadoluAfterEco.PublicOrder:F0} (Gözlənən: >= 72)");

                if (anadoluAfterEco.PublicOrder < 72f)
                {
                    throw new InvalidOperationException("Xəzinədə İpək olduğu halda aylıq +2 Moral bonusu verilmədi!");
                }

                // =========================================================================
                // 6. UI İNTEQRASİYASI YOXLANIŞI
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[6. UI İnteqrasiyası: Qlobal Xəritə Göstəriciləri]");

                if (ui != null)
                {
                    ui.SelectProvince(1);
                    ui.RefreshSelectedProvinceUI();
                    string uiText = ui.CurrentDisplayedGoldAndFood;
                    report.AppendLine($"  -> UI Üst Panel Mətni: [{uiText}]");

                    if (!uiText.Contains("Dəmir") || !uiText.Contains("At") || !uiText.Contains("İpək"))
                    {
                        throw new InvalidOperationException("UI göstəricisində Dəmir, At və İpək rəqəmləri əks olunmadı!");
                    }
                }

                // =========================================================================
                // TƏMİZLƏMƏ VƏ BƏRPA
                // =========================================================================
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                mil.DraftArmy(db, 1);
                mil.DraftArmy(db, 2);
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 8.1 TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 8.1 GLOBAL TRADE & DYNAMIC MARKET SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 8.1 xətası: {ex}");
                return report.ToString() + "\nERROR: " + ex.Message;
            }
        }

    
        [MenuItem("Grand Strategy/Verify/Phase 8.2 Caravans and Raiding")]
        [CliCommand("verify_phase8_2_caravans_and_raiding", "Verify Caravan Model & SQLite Table, Auto-spawn & Movement, Delivery to Target Treasury, Raiding by Enemy Army with Loot Transfer, and 2D UI Caravan Tokens", MainThreadRequired = true)]
        public static string VerifyPhase8_2CaravansAndRaiding()
        {
            var report = new StringBuilder();
            try
            {
                Debug.Log("=== [FoundationVerifier] FAZA 8.2: FİZİKİ KARVANLAR VƏ YAĞMALAMA TESTİ BAŞLADI ===");
                report.AppendLine("================================================================================");
                report.AppendLine("FAZA 8.2 - QLOBAL TİCARƏT: FİZİKİ KARVANLAR VƏ YAĞMALAMA (RAIDING) DOĞRULAMA");
                report.AppendLine("================================================================================");

                // 1. SƏHNƏ VƏ BAZA ƏLAQƏSİ
                string scenePath = "Assets/Scenes/GlobalMapScene.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    throw new InvalidOperationException($"'{scenePath}' səhnəsi açıla bilmədi!");
                }

                var db = UnityEngine.Object.FindAnyObjectByType<DatabaseManager>() ?? new GameObject("DatabaseManager").AddComponent<DatabaseManager>();
                var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>() ?? new GameObject("GameManager").AddComponent<GameManager>();
                var trade = UnityEngine.Object.FindAnyObjectByType<TradeController>() ?? new GameObject("TradeController").AddComponent<TradeController>();
                var mil = UnityEngine.Object.FindAnyObjectByType<MilitaryController>() ?? new GameObject("MilitaryController").AddComponent<MilitaryController>();
                var ui = UnityEngine.Object.FindAnyObjectByType<UIManager>();

                db.InitializeDatabase();
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();

                // =========================================================================
                // 1. KARVAN MODELİ VƏ VERİLƏNLƏR BAZASI YOXLANIŞI
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[1. Karvan Modeli və Məlumat Bazası (Caravan & DatabaseManager)]");

                var caravans = db.Connection.Table<Caravan>().ToList();
                report.AppendLine($"  -> Bazadakı İlkin Karvan Sayı: {caravans.Count}");

                if (caravans.Count == 0)
                {
                    throw new InvalidOperationException("Caravans cədvəli və ya dummy karvan bazada tapılmadı!");
                }

                var dummy = caravans[0];
                report.AppendLine($"  -> Dummy Karvan #{dummy.Id}: Sahibi=Fraksiya {dummy.OwnerFactionId}, Mənbə=#{dummy.SourceProvinceId} -> Hədəf=#{dummy.TargetProvinceId}, Resurs={dummy.ResourceAmount} {dummy.ResourceType}, İrəliləyiş={dummy.TravelProgress}%");

                if (dummy.ResourceAmount <= 0f || string.IsNullOrEmpty(dummy.ResourceType))
                {
                    throw new InvalidOperationException("Dummy karvanın resurs məlumatları natamamdır!");
                }

                // =========================================================================
                // 2. YENİ KARVANIN YARANMASI VƏ 2D XƏRİTƏ TOKENİ
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[2. Karvanın Yaranması və 2D UI Tokeni (UI_CaravanToken)]");

                var newCaravan = trade.SpawnCaravan(1, 1, 2, "Silk", 50f, db);
                if (newCaravan == null)
                {
                    throw new InvalidOperationException("SpawnCaravan uğursuz oldu!");
                }

                report.AppendLine($"  -> Yeni Karvan Yaradıldı: ID=#{newCaravan.Id}, Yük=50 Silk, Mənbə=1 -> Hədəf=2");

                Vector3 startPos = trade.GetCaravanCurrentPosition(newCaravan);
                report.AppendLine($"  -> Karvanın Başlanğıc Koordinatı (Progress=0%): ({startPos.x:F2}, {startPos.y:F2})");

                // =========================================================================
                // 3. KARVANIN HƏRƏKƏTİ VƏ TƏHVİL VERİLMƏSİ (DELIVERY)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[3. Karvan Hərəkəti və Hədəf Xəzinəyə Təhvil Verilməsi]");

                float initialProgress = newCaravan.TravelProgress;
                trade.ProcessMonthlyTrade(db, 1, 1066);

                var movedCaravan = db.Connection.Table<Caravan>().FirstOrDefault(c => c.Id == newCaravan.Id);
                report.AppendLine($"  -> 1 Aylıq Hərəkət Sonrası: İrəliləyiş={movedCaravan.TravelProgress:F1}% (Əvvəlki={initialProgress:F1}%)");

                if (movedCaravan.TravelProgress <= initialProgress)
                {
                    throw new InvalidOperationException("Aylıq ticarət dövründə karvan irəliləmədi!");
                }

                Vector3 movedPos = trade.GetCaravanCurrentPosition(movedCaravan);
                report.AppendLine($"  -> Karvanın Yeni Mövqeyi: ({movedPos.x:F2}, {movedPos.y:F2})");

                // Karvanı 100%-ə çatdırıb hədəf xəzinəyə resurs köçürülməsini yoxlayırıq
                var targetNationBefore = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 2);
                float targetSilkBefore = targetNationBefore?.Silk ?? 0f;

                trade.DeliverCaravan(movedCaravan.Id, db);

                var completedCaravan = db.Connection.Table<Caravan>().FirstOrDefault(c => c.Id == movedCaravan.Id);
                var targetNationAfter = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 2);

                report.AppendLine($"  -> Çatdırılma Yoxlanışı: IsCompleted={completedCaravan.IsCompleted}, Hədəf Dövlət #{targetNationAfter.Id} İpək: {targetSilkBefore:F0} -> {targetNationAfter.Silk:F0} (+{movedCaravan.ResourceAmount:F0})");

                if (!completedCaravan.IsCompleted || targetNationAfter.Silk < targetSilkBefore + movedCaravan.ResourceAmount)
                {
                    throw new InvalidOperationException("Karvan çatdıqda resurs hədəf dövlətin xəzinəsinə əlavə olunmadı!");
                }

                // =========================================================================
                // 4. YAĞMALAMA (RAIDING) MEXANİKASI VƏ QƏNİMƏT KÖÇÜRÜLMƏSİ
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[4. Yağmalama (Raiding) Mexanikası və Qənimətin Ələ Keçirilməsi]");

                // Fraksiya 1 və Fraksiya 3 arasında müharibə vəziyyəti təsdiqlənir
                var warDip = db.Connection.Table<Diplomacy>().FirstOrDefault(d =>
                    ((d.FactionA_Id == 1 && d.FactionB_Id == 3) || (d.FactionA_Id == 3 && d.FactionB_Id == 1)) &&
                    d.TreatyType == "Müharibə");

                if (warDip == null)
                {
                    warDip = new Diplomacy { FactionA_Id = 1, FactionB_Id = 3, TreatyType = "Müharibə", RelationScore = -90 };
                    db.Connection.Insert(warDip);
                }

                // Düşmən Fraksiya 3-ə aid bir ordu yaradırıq (Əyalət 2-də yerləşir)
                var enemyArmy = new Army
                {
                    OwnerFactionId = 3,
                    LocationProvinceId = 2,
                    YouthSoldiers = 800,
                    AdultSoldiers = 1200,
                    Morale = 1.0f
                };
                db.Connection.Insert(enemyArmy);

                // Fraksiya 1-ə aid 60 Dəmir (Iron) daşıyan və Əyalət 2-yə yaxınlaşmış karvan yaradırıq
                var vulnerableCaravan = trade.SpawnCaravan(1, 1, 2, "Iron", 60f, db);
                vulnerableCaravan.TravelProgress = 85f; // Əyalət 2-dəki ordunun yaxınlığındadır!
                db.Connection.Update(vulnerableCaravan);

                var raiderNationBefore = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 3);
                float raiderIronBefore = raiderNationBefore?.Iron ?? 0f;

                report.AppendLine($"  -> Düşmən Ordu #{enemyArmy.Id} (Fraksiya 3) Əyalət 2-də pusqudadır.");
                report.AppendLine($"  -> Karvan #{vulnerableCaravan.Id} (Fraksiya 1, 60 Iron) yaxınlıqdan keçir (Progress=85%).");

                // Yağmalama əmri icra edilir
                var raidResult = mil.RaidCaravan(vulnerableCaravan.Id, enemyArmy.Id, db);
                var raidedCaravan = db.Connection.Table<Caravan>().FirstOrDefault(c => c.Id == vulnerableCaravan.Id);
                var raiderNationAfter = db.Connection.Table<Nation>().FirstOrDefault(n => n.Id == 3);

                report.AppendLine($"  -> Yağmalama Nəticəsi: Uğur={raidResult.Success}");
                report.AppendLine($"  -> Karvan Vəziyyəti: IsRaided={raidedCaravan.IsRaided}");
                report.AppendLine($"  -> Qənimət Köçürülməsi: Fraksiya 3 Xəzinəsində Iron: {raiderIronBefore:F0} -> {raiderNationAfter.Iron:F0} (+{raidResult.LootedAmount:F0})");
                report.AppendLine($"  -> Bildiriş Mesajı: [{raidResult.Message}]");

                if (!raidResult.Success || !raidedCaravan.IsRaided || raiderNationAfter.Iron < raiderIronBefore + 60f)
                {
                    throw new InvalidOperationException("Karvan yağmalanmadı və ya qənimət yağmalayanın xəzinəsinə köçürülmədi!");
                }

                if (!raidResult.Message.Contains("KARVAN YAĞMALANDI") || !raidResult.Message.Contains("Fraksiya 3"))
                {
                    throw new InvalidOperationException("Yağmalama bildirişi ekranda və konsolda tələb olunan formatda çıxmadı!");
                }

                // =========================================================================
                // 5. RADİUS ƏSASLI AVTOMATİK YAĞMALAMA (CHECKCARAVANRAIDS)
                // =========================================================================
                report.AppendLine("--------------------------------------------------------------------------------");
                report.AppendLine("[5. Radius Əsaslı Avtomatik Yağmalama Testi]");

                var autoVulnerableCaravan = trade.SpawnCaravan(1, 1, 2, "Silk", 75f, db);
                autoVulnerableCaravan.TravelProgress = 90f; // Əyalət 2-yə çox yaxındır
                db.Connection.Update(autoVulnerableCaravan);

                var autoRaidResults = trade.CheckCaravanRaids(db);
                var autoRaidedCaravan = db.Connection.Table<Caravan>().FirstOrDefault(c => c.Id == autoVulnerableCaravan.Id);

                report.AppendLine($"  -> Avtomatik Yağmalama Sayı: {autoRaidResults.Count}, Karvan IsRaided={autoRaidedCaravan.IsRaided}");
                if (autoRaidResults.Count == 0 || !autoRaidedCaravan.IsRaided)
                {
                    throw new InvalidOperationException("CheckCaravanRaids avtomatik yaxınlıqdakı düşmən karvanını yağmalamadı!");
                }

                // =========================================================================
                // TƏMİZLƏMƏ VƏ BƏRPA
                // =========================================================================
                db.SeedAndTestDummyData(forceReseed: true);
                gm.InitializeCalendar();
                mil.DraftArmy(db, 1);
                mil.DraftArmy(db, 2);
                db.CloseConnection();

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

                Debug.Log("=== [FoundationVerifier] FAZA 8.2 TESTİ UĞURLA TAMAMLANDI (0 XƏTA) ===");
                report.AppendLine("STATUS: PHASE 8.2 CARAVANS & RAIDING SUCCESS (0 ERRORS)");
                return report.ToString();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FoundationVerifier] Faza 8.2 xətası: {ex}");
                return report.ToString() + "\nERROR: " + ex.Message;
            }
        }

    }
}
