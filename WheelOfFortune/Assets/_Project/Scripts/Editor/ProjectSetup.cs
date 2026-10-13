using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;
using WheelOfFortune.Core;
using WheelOfFortune.UI;

namespace WheelOfFortune.EditorTools
{
    /// <summary>
    /// One-click, repeatable project setup: sprite import settings, sprite atlases, data assets,
    /// prefabs, the main scene and Android player settings. Menu: Tools > Wheel Of Fortune.
    /// </summary>
    public static class ProjectSetup
    {
        private const string Root = "Assets/_Project";
        private const string CommonSprites = Root + "/Art/Sprites/Common";
        private const string RewardSprites = Root + "/Art/Sprites/Rewards";
        private const string ScenePath = Root + "/Scenes/Main.unity";
        private const string ApkPath = "Builds/WheelOfFortune.apk";
        private const int UILayer = 5;
        private const float WheelSize = 600f;
        private const float SliceRadius = WheelSize * 0.295f; // measured from the wheel art: hole centers sit at 29.5% of its width
        private const int ZoneLabelCount = 15;
        private const float ZoneItemWidth = 68f;
        private const float TitleSize = 42f;
        private const float LabelSize = 24f;
        private const float ValueSize = 30f;
        private static readonly Color MutedText = new(0.60f, 0.67f, 0.76f);

        // 9-slice border (pixels) for sprites used on resizable panels and buttons.
        private static readonly Dictionary<string, int> SliceBorders = new()
        {
            { "UI_button_orange_standard", 24 },
            { "UI_button_grey_standard", 24 },
            { "ui_card_panel_zone_bg", 12 },
            { "ui_card_panel_zone_white", 12 },
            { "ui_card_panel_zone_current", 12 },
            { "ui_card_panel_zone_current_white", 12 },
            { "ui_card_panel_zone_coming", 12 },
            { "ui_card_panel_zone_super", 12 },
            { "ui_card_frame_4px_zone", 16 },
            { "ui_card_zone_map_frame", 20 },
            { "ui_card_frame_12px_neutral", 28 },
            { "ui_card_frame_gardient", 24 },
        };

        [MenuItem("Tools/Wheel Of Fortune/Setup Project")]
        public static void SetupProject()
        {
            ConfigurePlayer();
            ConfigureSprites();
            CreateAtlas("ui_atlas_common", CommonSprites);
            CreateAtlas("ui_atlas_rewards", RewardSprites);
            GameSettings settings = CreateData();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            WheelSliceView slicePrefab = CreateSlicePrefab();
            RewardEntryView entryPrefab = CreateRewardEntryPrefab();
            CreateScene(settings, slicePrefab, entryPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log("Wheel Of Fortune setup complete.");
        }

        [MenuItem("Tools/Wheel Of Fortune/Refresh Presentation")]
        public static void RefreshPresentation()
        {
            // Keep designer-authored wheel contents, amounts and rules intact.
            var rewardIcons = new Dictionary<RewardDefinition, string>();
            foreach (string guid in AssetDatabase.FindAssets("t:RewardDefinition", new[] { Root + "/Data/Rewards" }))
            {
                var reward = AssetDatabase.LoadAssetAtPath<RewardDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                rewardIcons.Add(reward, AssetDatabase.GetAssetPath(reward.Icon));
            }
            ConfigureSprites();
            foreach (var pair in rewardIcons)
            {
                if (!string.IsNullOrEmpty(pair.Value))
                {
                    pair.Key.Init(pair.Key.DisplayName, AssetDatabase.LoadAssetAtPath<Sprite>(pair.Value), pair.Key.IsBomb);
                    EditorUtility.SetDirty(pair.Key);
                }
            }
            CreateAtlas("ui_atlas_common", CommonSprites);
            CreateAtlas("ui_atlas_rewards", RewardSprites);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            WheelSliceView slicePrefab = CreateSlicePrefab();
            RewardEntryView entryPrefab = CreateRewardEntryPrefab();
            // Importing dependent assets can invalidate previously loaded native objects.
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>($"{Root}/Data/game_settings.asset");
            if (settings == null) throw new System.InvalidOperationException("Game settings asset is missing.");
            CreateScene(settings, slicePrefab, entryPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("Wheel Of Fortune presentation refreshed; game data preserved.");
        }

        [MenuItem("Tools/Wheel Of Fortune/Build Android APK")]
        public static void BuildAndroid()
        {
            EditorUserBuildSettings.buildAppBundle = false;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Android build failed: {report.summary.result}");
            Debug.Log($"APK built: {ApkPath}");
        }

        // ---------- Project / assets ----------

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "VertigoDemo";
            PlayerSettings.productName = "Wheel Of Fortune";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.vertigodemo.wheeloffortune");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void ConfigureSprites()
        {
            // Defer dependent atlas imports until all source settings have been written.
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CommonSprites, RewardSprites }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);

                    settings.textureType = TextureImporterType.Sprite;
                    settings.spriteMode = (int)SpriteImportMode.Single;
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteGenerateFallbackPhysicsShape = false;
                    settings.npotScale = TextureImporterNPOTScale.None;
                    settings.mipmapEnabled = false;
                    settings.alphaIsTransparency = true;
                    settings.spriteBorder = SliceBorders.TryGetValue(Path.GetFileNameWithoutExtension(path), out int b)
                        ? new Vector4(b, b, b, b)
                        : Vector4.zero;

                    importer.SetTextureSettings(settings);
                    if (path.StartsWith(RewardSprites + "/")) CropRewardSprite(importer, path);
                    // Compress only the atlas, avoiding a second compression of the source pixels.
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static void CropRewardSprite(TextureImporter importer, string path)
        {
            // Define import bounds only. Supplied PNG files remain untouched.
            var texture = new Texture2D(2, 2);
            texture.LoadImage(File.ReadAllBytes(path));
            Color32[] pixels = texture.GetPixels32();
            int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
            for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a < 16) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            if (maxX >= minX)
            {
                minX = Mathf.Max(0, minX - 4); minY = Mathf.Max(0, minY - 4);
                maxX = Mathf.Min(texture.width - 1, maxX + 4); maxY = Mathf.Min(texture.height - 1, maxY + 4);
                importer.spriteImportMode = SpriteImportMode.Multiple;
#pragma warning disable CS0618
                // Retain the existing sub-sprite name so repeat refreshes keep the file ID.
                SpriteMetaData[] sprites = importer.spritesheet;
                string spriteName = sprites.Length > 0 ? sprites[0].name : Path.GetFileNameWithoutExtension(path);
                importer.spritesheet = new[] { new SpriteMetaData
                {
                    name = spriteName,
                    rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    alignment = (int)SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f)
                } };
#pragma warning restore CS0618
            }
            Object.DestroyImmediate(texture);
        }

        private static void CreateAtlas(string atlasName, string folder)
        {
            string path = $"{Root}/Art/Atlases/{atlasName}.spriteatlasv2";
            var atlas = new SpriteAtlasAsset();
            atlas.Add(new[] { AssetDatabase.LoadAssetAtPath<Object>(folder) });
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path);

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                enableRotation = false,     // UI Images do not support rotated or tight packed sprites
                enableTightPacking = false,
                padding = 8                 // extra space so smaller mip levels do not bleed into neighbours
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                generateMipMaps = true,     // icons are drawn much smaller than their source; without mips they alias
                sRGB = true,
                filterMode = FilterMode.Trilinear
            };
            // ASTC 4x4 = highest quality block compression on Android; default settings smear colour on small icons.
            importer.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 2048,
                format = TextureImporterFormat.ASTC_4x4
            });
            importer.SaveAndReimport();
        }

        private static GameSettings CreateData()
        {
            RewardDefinition bomb = Reward("bomb", "Bomb", "ui_card_icon_death", isBomb: true);
            RewardDefinition gold = Reward("gold", "Gold", "UI_icon_gold");
            RewardDefinition cash = Reward("cash", "Cash", "UI_icon_cash");
            RewardDefinition chestSmall = Reward("chest_small", "Small Chest", "UI_icon_chest_small_noligt");
            RewardDefinition chestSilver = Reward("chest_silver", "Silver Chest", "UI_icon_chest_silver_nolight");
            RewardDefinition chestGold = Reward("chest_gold", "Gold Chest", "UI_icon_chest_gold_nolight");
            RewardDefinition chestSuper = Reward("chest_super", "Super Chest", "UI_icon_chest_super_nolight");
            RewardDefinition grenade = Reward("grenade_m26", "M26 Grenade", "ui_icon_render_cons_grenade_m26");
            RewardDefinition molotov = Reward("molotov", "Molotov", "ui_icon_render_t_cons_molotov");
            RewardDefinition cap = Reward("baseball_cap", "Baseball Cap", "ui_icon_baseball_cap_easter");
            RewardDefinition helmet = Reward("helmet_pumpkin", "Pumpkin Helmet", "ui_icon_helmet_pumpkin");
            RewardDefinition pistolPoints = Reward("pistol_points", "Pistol Points", "UI_Icons_Pistol_Points");
            RewardDefinition riflePoints = Reward("rifle_points", "Rifle Points", "UI_Icons_Rifle_Points");
            RewardDefinition armorPoints = Reward("armor_points", "Armor Points", "UI_Icons_Armor_Points");
            RewardDefinition smg = Reward("smg_tier3", "SMG", "UI_Icon_Renders_tier3_smg");
            RewardDefinition sniper = Reward("sniper_tier3", "Sniper", "UI_Icon_Renders_tier3_sniper");
            RewardDefinition shotgun = Reward("shotgun_tier3", "Shotgun", "UI_Icon_Renders_tier3_shotgun");
            RewardDefinition bayonet = Reward("bayonet_summer", "Bayonet Summer Vice", "ui_icon_mle_bayonet_summer_vice");
            RewardDefinition glasses = Reward("aviator_glasses", "Aviator Glasses", "ui_icon_aviator_glasses_easter");

            WheelDefinition bronze = Wheel("wheel_bronze", "BRONZE SPIN", new Color(0.96f, 0.62f, 0.28f), "bronze",
                Slice(bomb, 1), Slice(cash, 100), Slice(gold, 5), Slice(chestSmall, 1),
                Slice(grenade, 1), Slice(pistolPoints, 10), Slice(cap, 1), Slice(riflePoints, 10));
            WheelDefinition silver = Wheel("wheel_silver", "SILVER SPIN", new Color(0.86f, 0.9f, 0.95f), "silver",
                Slice(cash, 500), Slice(gold, 25), Slice(chestSilver, 1), Slice(smg, 1),
                Slice(armorPoints, 20), Slice(molotov, 2), Slice(helmet, 1), Slice(riflePoints, 30));
            WheelDefinition golden = Wheel("wheel_golden", "GOLDEN SPIN", new Color(1f, 0.8f, 0.25f), "golden",
                Slice(gold, 100), Slice(cash, 5000), Slice(chestGold, 1), Slice(sniper, 1),
                Slice(chestSuper, 1), Slice(shotgun, 1), Slice(bayonet, 1), Slice(glasses, 1));

            var settings = LoadOrCreate<GameSettings>($"{Root}/Data/game_settings.asset");
            settings.Init(bronze, silver, golden);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static RewardDefinition Reward(string id, string displayName, string spriteName, bool isBomb = false)
        {
            var reward = LoadOrCreate<RewardDefinition>($"{Root}/Data/Rewards/reward_{id}.asset");
            reward.Init(displayName, LoadSprite(RewardSprites, spriteName), isBomb);
            EditorUtility.SetDirty(reward);
            return reward;
        }

        private static WheelDefinition Wheel(string id, string title, Color color, string art, params WheelSlice[] slices)
        {
            var wheel = LoadOrCreate<WheelDefinition>($"{Root}/Data/Wheels/{id}.asset");
            wheel.Init(title, color,
                LoadSprite(CommonSprites, $"ui_spin_{art}_base"),
                LoadSprite(CommonSprites, $"ui_spin_{art}_indicator"),
                slices);
            EditorUtility.SetDirty(wheel);
            return wheel;
        }

        private static WheelSlice Slice(RewardDefinition reward, int amount) =>
            new() { Reward = reward, BaseAmount = amount };

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Sprite LoadSprite(string folder, string spriteName) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{spriteName}.png")
            ?? throw new FileNotFoundException($"Sprite not found: {folder}/{spriteName}.png");

        private static Sprite Common(string spriteName) => LoadSprite(CommonSprites, spriteName);

        // ---------- Prefabs ----------

        private static WheelSliceView CreateSlicePrefab()
        {
            RectTransform root = Rect("ui_wheel_slice", null, Vector2.zero, new Vector2(110f, 110f));
            var view = root.gameObject.AddComponent<WheelSliceView>();
            Image icon = Img(Rect("ui_image_slice_icon_value", root, new Vector2(0f, 12f), new Vector2(54f, 54f)), null);
            TMP_Text amount = Text(Rect("ui_text_slice_amount_value", root, new Vector2(0f, -28f), new Vector2(84f, 28f)),
                "x1", 21f, Color.white);
            amount.fontStyle = FontStyles.Bold;
            Set(view, ("_icon", icon), ("_amountText", amount));
            return SavePrefab(root, "ui_wheel_slice").GetComponent<WheelSliceView>();
        }

        private static RewardEntryView CreateRewardEntryPrefab()
        {
            RectTransform root = Rect("ui_reward_entry", null, Vector2.zero, new Vector2(280f, 80f));
            var element = root.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = 80f;
            Img(Stretch("ui_image_reward_entry_bg", root), Common("ui_card_panel_zone_coming"), sliced: true, maskable: true)
                .color = new Color(0.13f, 0.17f, 0.23f, 0.7f);
            var view = root.gameObject.AddComponent<RewardEntryView>();
            Image icon = Img(Rect("ui_image_reward_icon_value", root, new Vector2(14f, 0f), new Vector2(86f, 60f),
                anchor: new Vector2(0f, 0.5f), pivot: new Vector2(0f, 0.5f)), null, maskable: true);
            TMP_Text amount = Text(Rect("ui_text_reward_amount_value", root, new Vector2(-14f, 0f), new Vector2(152f, 60f),
                anchor: new Vector2(1f, 0.5f), pivot: new Vector2(1f, 0.5f)), string.Empty, ValueSize, Color.white,
                TextAlignmentOptions.Right, maskable: true);
            amount.fontStyle = FontStyles.Bold;
            Set(view, ("_icon", icon), ("_amountText", amount));
            return SavePrefab(root, "ui_reward_entry").GetComponent<RewardEntryView>();
        }

        private static GameObject SavePrefab(RectTransform root, string prefabName)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, $"{Root}/Prefabs/{prefabName}.prefab");
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        // ---------- Scene ----------

        private static void CreateScene(GameSettings settings, WheelSliceView slicePrefab, RewardEntryView entryPrefab)
        {
            var cameraGo = new GameObject("main_camera", typeof(Camera));
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0; // UI is an overlay canvas; the camera only clears the screen

            new GameObject("event_system", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("ui_canvas_main", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = UILayer;
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var canvas = (RectTransform)canvasGo.transform;

            Img(Stretch("ui_image_background", canvas), null).color = new Color(0.035f, 0.05f, 0.075f);
            // A fixed-size composition stays together as Expand adds space at the edges.
            RectTransform stage = Rect("ui_stage", canvas, Vector2.zero, new Vector2(1320f, 940f));
            Image atmosphere = Img(Rect("ui_image_atmosphere", stage, new Vector2(160f, -40f), new Vector2(1380f, 1000f)),
                Common("star_glow_alpha"));
            atmosphere.color = new Color(0.18f, 0.34f, 0.52f, 0.14f);
            Img(Rect("ui_image_header_divider", stage, new Vector2(0f, 268f), new Vector2(1220f, 2f)), null)
                .color = new Color(0.35f, 0.46f, 0.60f, 0.2f);

            ZoneBarView zoneBar = CreateZoneBar(stage);
            RewardPanelView rewardPanel = CreateRewardPanel(stage, entryPrefab);
            WheelView wheel = CreateWheel(stage, slicePrefab);

            Image flyingIcon = Img(Rect("ui_image_flying_icon_value", canvas, Vector2.zero, new Vector2(110f, 110f)), null);
            flyingIcon.gameObject.SetActive(false);
            Set(rewardPanel, ("_flyingIcon", flyingIcon));

            ResultPopupView popup = CreatePopup(canvas);

            var controller = canvasGo.AddComponent<GameController>();
            Set(controller,
                ("_settings", settings), ("_wheel", wheel), ("_zoneBar", zoneBar),
                ("_rewardPanel", rewardPanel), ("_popup", popup),
                ("_bombPopup.Title", "BOMB EXPLODED!\nALL REWARDS ARE LOST"),
                ("_bombPopup.Icon", LoadSprite(RewardSprites, "ui_card_icon_death")),
                ("_bombPopup.ButtonLabel", "GIVE UP"),
                ("_collectedPopup.Title", "REWARDS COLLECTED!"),
                ("_collectedPopup.Icon", LoadSprite(RewardSprites, "UI_icon_chest_big_nolight")),
                ("_collectedPopup.ButtonLabel", "PLAY AGAIN"));
            RunOnValidate(controller);
            RunOnValidate(popup);

            // Editor preview: show zone 1 so the scene is not full of empty placeholders. Play mode overwrites this.
            var preview = new WheelSession(settings, _ => 0);
            wheel.Show(preview);
            zoneBar.Show(preview.Zone, settings);
            foreach (Graphic graphic in wheel.GetComponentsInChildren<Graphic>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
            foreach (RectTransform transform in wheel.GetComponentsInChildren<RectTransform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        }

        private static ZoneBarView CreateZoneBar(RectTransform canvas)
        {
            Vector2 top = new(0.5f, 1f);
            RectTransform bar = Rect("ui_zone_bar", canvas, new Vector2(-120f, -32f), new Vector2(ZoneItemWidth * 11 + 32f, 76f), top, top);
            var view = bar.gameObject.AddComponent<ZoneBarView>();
            Img(Stretch("ui_image_zone_bar_bg", bar), Common("ui_card_panel_zone_bg"), sliced: true);

            RectTransform viewport = Stretch("ui_zone_bar_viewport", bar, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image highlight = Img(Rect("ui_image_zone_current_value", viewport, Vector2.zero, new Vector2(ZoneItemWidth - 6f, 64f)),
                Common("ui_card_panel_zone_current_white"), sliced: true, maskable: true);

            RectTransform content = Rect("ui_zone_bar_content", viewport, Vector2.zero, new Vector2(ZoneItemWidth * ZoneLabelCount, 64f));
            var labels = new TMP_Text[ZoneLabelCount];
            for (int i = 0; i < ZoneLabelCount; i++)
            {
                float x = (i - ZoneLabelCount / 2) * ZoneItemWidth;
                labels[i] = Text(Rect($"ui_text_zone_{i}_value", content, new Vector2(x, 0f), new Vector2(ZoneItemWidth, 64f)),
                    string.Empty, ValueSize, Color.white, maskable: true);
                labels[i].fontStyle = FontStyles.Bold;
            }

            Img(Rect("ui_image_zone_current_frame", viewport, Vector2.zero, new Vector2(ZoneItemWidth + 4f, 74f)),
                Common("ui_card_zone_map_frame"), sliced: true, maskable: true);
            Img(Stretch("ui_image_zone_bar_frame", bar), Common("ui_card_frame_4px_zone"), sliced: true).color = new Color(1f, 1f, 1f, 0.35f);

            // Next safe / super zone boxes (top right corner)
            Vector2 topRight = new(1f, 1f);
            TMP_Text nextSuper = ZoneInfoBox(canvas, "ui_zone_info_super", "SUPER ZONE", new Vector2(-24f, -32f),
                Common("ui_card_panel_zone_current_white"), new Color(1f, 0.76f, 0.25f), new Color(0.2f, 0.12f, 0f), topRight);
            TMP_Text nextSafe = ZoneInfoBox(canvas, "ui_zone_info_safe", "SAFE ZONE", new Vector2(-24f, -108f),
                Common("ui_card_panel_zone_super"), Color.white, Color.white, topRight);

            Set(view, ("_content", content), ("_zoneTexts", labels), ("_currentHighlight", highlight),
                ("_nextSafeText", nextSafe), ("_nextSuperText", nextSuper), ("_itemWidth", ZoneItemWidth));
            return view;
        }

        private static TMP_Text ZoneInfoBox(RectTransform canvas, string boxName, string label, Vector2 position,
            Sprite sprite, Color tint, Color textColor, Vector2 anchor)
        {
            RectTransform box = Rect(boxName, canvas, position, new Vector2(260f, 64f), anchor, anchor);
            Img(Stretch($"ui_image_{boxName}_bg", box), sprite, sliced: true).color = tint;
            Text(Rect($"ui_text_{boxName}_label", box, new Vector2(16f, 0f), new Vector2(166f, 54f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f)),
                label, 22f, textColor, TextAlignmentOptions.Left);
            var value = Text(Rect($"ui_text_{boxName}_value", box, new Vector2(-16f, 0f), new Vector2(64f, 54f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f)),
                "0", ValueSize, textColor, TextAlignmentOptions.Right);
            value.fontStyle = FontStyles.Bold;
            value.enableAutoSizing = true;
            value.fontSizeMin = 18f;
            value.fontSizeMax = ValueSize;
            return value;
        }

        private static RewardPanelView CreateRewardPanel(RectTransform canvas, RewardEntryView entryPrefab)
        {
            RectTransform panel = Rect("ui_reward_panel", canvas, new Vector2(-415f, -70f), new Vector2(330f, 640f));
            var view = panel.gameObject.AddComponent<RewardPanelView>();
            Img(Stretch("ui_image_reward_panel_bg", panel), Common("ui_card_panel_zone_coming"), sliced: true)
                .color = new Color(0.065f, 0.085f, 0.12f, 0.95f);
            Img(Stretch("ui_image_reward_panel_frame", panel), Common("ui_card_frame_4px_zone"), sliced: true)
                .color = new Color(0.4f, 0.5f, 0.65f, 0.22f);

            Vector2 top = new(0.5f, 1f);
            Text(Rect("ui_text_reward_panel_title", panel, new Vector2(0f, -22f), new Vector2(290f, 42f), top, top),
                "COLLECTED REWARDS", LabelSize, Color.white);
            Img(Rect("ui_image_reward_panel_divider", panel, new Vector2(0f, -76f), new Vector2(278f, 2f), top, top), null)
                .color = new Color(0.4f, 0.5f, 0.65f, 0.25f);
            Vector2 bottom = new(0.5f, 0f);
            Button exit = CreateButton(Rect("ui_button_exit", panel, new Vector2(0f, 22f), new Vector2(282f, 72f), bottom, bottom),
                Common("UI_button_orange_standard"), "EXIT", "ui_text_exit");
            exit.transition = Selectable.Transition.SpriteSwap;
            exit.spriteState = new SpriteState { disabledSprite = Common("UI_button_grey_standard") };

            RectTransform scroll = Stretch("ui_reward_scroll", panel);
            scroll.offsetMin = new Vector2(20f, 158f);
            scroll.offsetMax = new Vector2(-20f, -94f);
            Img(scroll, null, raycast: true).color = Color.clear; // raycast surface needed for dragging the list
            scroll.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect("ui_reward_content", scroll, Vector2.zero, Vector2.zero, top, top);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = content;
            scrollRect.viewport = scroll;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            var empty = Rect("ui_reward_empty_state_value", panel, new Vector2(0f, 20f), new Vector2(270f, 200f));
            Img(Rect("ui_image_reward_empty_icon", empty, new Vector2(0f, 48f), new Vector2(130f, 74f)),
                LoadSprite(RewardSprites, "UI_icon_chest_small_noligt")).color = new Color(1f, 1f, 1f, 0.45f);
            Text(Rect("ui_text_reward_empty_title", empty, new Vector2(0f, -16f), new Vector2(270f, 36f)),
                "YOUR RUN STARTS HERE", 22f, MutedText);
            var emptyHint = Text(Rect("ui_text_reward_empty_hint", empty, new Vector2(0f, -62f), new Vector2(270f, 56f)),
                "Spin the wheel to\nbuild your rewards", 21f, MutedText);
            emptyHint.textWrappingMode = TextWrappingModes.Normal;
            var collectHint = Text(Rect("ui_text_reward_collect_hint", panel, new Vector2(0f, 102f), new Vector2(288f, 56f), bottom, bottom),
                "Collect in a safe\nor super zone", 20f, MutedText);
            collectHint.textWrappingMode = TextWrappingModes.Normal;
            Set(view, ("_entryPrefab", entryPrefab), ("_content", content), ("_emptyState", empty.gameObject));
            return view;
        }

        private static WheelView CreateWheel(RectTransform canvas, WheelSliceView slicePrefab)
        {
            RectTransform wheel = Rect("ui_wheel", canvas, new Vector2(170f, -65f), new Vector2(WheelSize, WheelSize));
            var view = wheel.gameObject.AddComponent<WheelView>();

            Image glow = Img(Rect("ui_image_wheel_glow", wheel, Vector2.zero, new Vector2(900f, 900f)), Common("star_glow_alpha"));
            glow.color = new Color(0.5f, 0.7f, 1f, 0.10f);

            TMP_Text title = Text(Rect("ui_text_wheel_title_value", wheel, new Vector2(0f, WheelSize * 0.5f + 74f), new Vector2(WheelSize, 60f)),
                "BRONZE SPIN", TitleSize, Color.white);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 5f;
            TMP_Text hint = Text(Rect("ui_text_wheel_zone_hint_value", wheel, new Vector2(0f, -WheelSize * 0.5f - 55f), new Vector2(650f, 40f)),
                string.Empty, 22f, MutedText);

            RectTransform rotator = Stretch("ui_wheel_rotator", wheel);
            Image baseImage = Img(Stretch("ui_image_wheel_base_value", rotator), Common("ui_spin_bronze_base"));

            var slices = new WheelSliceView[WheelDefinition.SliceCount];
            for (int i = 0; i < slices.Length; i++)
            {
                // Slice i is placed i * 45 degrees clockwise from the top and turned to face outward.
                float angle = i * 360f / slices.Length;
                var slice = (WheelSliceView)PrefabUtility.InstantiatePrefab(slicePrefab, rotator);
                slice.name = $"ui_wheel_slice_{i}";
                var rt = (RectTransform)slice.transform;
                rt.anchoredPosition = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * SliceRadius;
                rt.localEulerAngles = new Vector3(0f, 0f, -angle);
                slices[i] = slice;
            }

            Image indicator = Img(Rect("ui_image_wheel_indicator_value", wheel, new Vector2(0f, WheelSize * 0.45f), new Vector2(70f, 96f)),
                Common("ui_spin_bronze_indicator"));
            CreateButton(Rect("ui_button_spin", wheel, Vector2.zero, new Vector2(170f, 170f)), Common("ui_spin_generic_button"), null, null);

            Set(view, ("_rotator", rotator), ("_glow", (RectTransform)glow.transform), ("_baseImage", baseImage),
                ("_indicatorImage", indicator), ("_titleText", title), ("_zoneHintText", hint), ("_slices", slices));
            return view;
        }

        private static ResultPopupView CreatePopup(RectTransform canvas)
        {
            RectTransform root = Stretch("ui_popup_result", canvas);
            Img(Stretch("ui_image_popup_blocker", root), null, raycast: true).color = new Color(0f, 0f, 0f, 0.8f); // blocks input behind

            RectTransform panel = Rect("ui_popup_panel", root, Vector2.zero, new Vector2(760f, 720f));
            Img(Stretch("ui_image_popup_bg", panel), Common("ui_card_panel_zone_bg"), sliced: true);
            Img(Stretch("ui_image_popup_frame", panel), Common("ui_card_frame_12px_neutral"), sliced: true);

            Vector2 top = new(0.5f, 1f), bottom = new(0.5f, 0f);
            TMP_Text title = Text(Rect("ui_text_popup_title_value", panel, new Vector2(0f, -40f), new Vector2(700f, 130f), top, top),
                string.Empty, TitleSize, Color.white);
            title.fontStyle = FontStyles.Bold;
            title.textWrappingMode = TextWrappingModes.Normal;
            Image icon = Img(Rect("ui_image_popup_icon_value", panel, new Vector2(0f, 10f), new Vector2(340f, 340f)), null);
            Button close = CreateButton(Rect("ui_button_popup_close", panel, new Vector2(0f, 40f), new Vector2(340f, 100f), bottom, bottom),
                Common("UI_button_orange_standard"), string.Empty, "ui_text_popup_button_value");

            var view = root.gameObject.AddComponent<ResultPopupView>(); // added last so OnValidate finds the button
            Set(view, ("_panel", panel), ("_titleText", title), ("_icon", icon),
                ("_buttonText", close.GetComponentInChildren<TMP_Text>()));
            root.gameObject.SetActive(false);
            return view;
        }

        // ---------- Small UI helpers ----------

        private static RectTransform Rect(string objectName, Transform parent, Vector2 position, Vector2 size,
            Vector2? anchor = null, Vector2? pivot = null)
        {
            var go = new GameObject(objectName, typeof(RectTransform)) { layer = UILayer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor ?? new Vector2(0.5f, 0.5f);
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform Stretch(string objectName, Transform parent, float inset = 0f)
        {
            RectTransform rt = Rect(objectName, parent, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        private static Image Img(RectTransform rt, Sprite sprite, bool sliced = false, bool raycast = false, bool maskable = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !sliced; // never stretch icons
            image.raycastTarget = raycast;
            image.maskable = maskable;
            return image;
        }

        private static TMP_Text Text(RectTransform rt, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool maskable = false)
        {
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            label.fontSize = size;
            label.fontStyle = FontStyles.Normal;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.maskable = maskable;
            return label;
        }

        private static Button CreateButton(RectTransform rt, Sprite sprite, string label, string labelName)
        {
            Image image = Img(rt, sprite, sliced: sprite.border != Vector4.zero, raycast: true);
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (label != null)
            {
                var text = Text(Stretch(labelName, rt), label, ValueSize, Color.white);
                text.fontStyle = FontStyles.Bold;
                text.characterSpacing = 2f;
            }
            return button;
        }

        /// <summary>Writes private [SerializeField] fields, the same way the Inspector does.</summary>
        private static void Set(Object target, params (string field, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach ((string field, object value) in values)
            {
                SerializedProperty property = so.FindProperty(field)
                    ?? throw new System.ArgumentException($"{target.GetType().Name}.{field} not found");
                switch (value)
                {
                    case Object obj: property.objectReferenceValue = obj; break;
                    case string text: property.stringValue = text; break;
                    case float number: property.floatValue = number; break;
                    case Object[] array:
                        property.arraySize = array.Length;
                        for (int i = 0; i < array.Length; i++)
                            property.GetArrayElementAtIndex(i).objectReferenceValue = array[i];
                        break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RunOnValidate(Object target)
        {
            target.GetType().GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);
            EditorUtility.SetDirty(target);
        }
    }
}
