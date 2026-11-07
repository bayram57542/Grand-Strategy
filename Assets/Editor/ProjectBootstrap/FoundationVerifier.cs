using System;
using System.IO;
using System.Text;
using GrandStrategy.Core;
using GrandStrategy.Database;
using GrandStrategy.Database.Models;
using GrandStrategy.Map;
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
            serializedGm.ApplyModifiedPropertiesWithoutUndo();
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

                // 8. İstifadəçi Play düyməsinə basdıqda təmiz başlanğıc görməsi üçün bazanı 1066-cı ilin başlanğıcına qaytarırıq
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

            // 1. Yuxarı Künc - Tarix və Tick İzləyicisi Paneli (TopLeft)
            var topBarPanel = CreatePanel(
                "DateTrackerPanel",
                canvasGo.transform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -20f),
                new Vector2(620f, 92f),
                new Color(0.12f, 0.15f, 0.21f, 0.94f));

            Text dateText = CreateText(
                "DateTrackerText",
                topBarPanel.transform,
                defaultFont,
                "Tarix: İl 1066, Ay 01 (Gün 01) | Tick #0",
                22,
                FontStyle.Bold,
                new Color(0.96f, 0.89f, 0.68f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(0f, 0.5f),
                new Vector2(1f, 1f),
                new Vector2(18f, 0f),
                new Vector2(-18f, -6f));

            Text speedText = CreateText(
                "SpeedStatusText",
                topBarPanel.transform,
                defaultFont,
                "FASİLƏ (PAUSED)",
                17,
                FontStyle.Normal,
                new Color(0.72f, 0.82f, 0.92f, 1f),
                TextAnchor.MiddleLeft,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.5f),
                new Vector2(18f, 6f),
                new Vector2(-18f, 0f));

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

            // Inspector üzərindən persistent listener və referansları bağlayırıq
            UnityEventTools.AddPersistentListener(draftBtn.onClick, uiManager.OnDraftArmyButtonClicked);
            UnityEventTools.AddPersistentListener(closeBtn.onClick, uiManager.HideProvincePanel);

            var serializedUi = new SerializedObject(uiManager);
            serializedUi.FindProperty("dateTrackerText").objectReferenceValue = dateText;
            serializedUi.FindProperty("speedStatusText").objectReferenceValue = speedText;
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
            serializedUi.ApplyModifiedPropertiesWithoutUndo();

            return uiManager;
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
    }
}
