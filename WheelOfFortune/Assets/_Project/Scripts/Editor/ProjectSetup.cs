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
        private const int ZoneVisibleCount = 9;
        private const float ZoneItemWidth = 68f;
        // Every column shares the stage's vertical axis: side panels sit at +/- ColumnX, the wheel at 0.
        private const float ColumnX = 495f;
        private const float ColumnWidth = 330f;
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string HeadingMaterialPath = Root + "/Art/Fonts/LiberationSans SDF - Heading.mat";
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
            ReceiptEntryView receiptEntryPrefab = CreateReceiptEntryPrefab();
            CreateScene(settings, slicePrefab, entryPrefab, receiptEntryPrefab);

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
            ReceiptEntryView receiptEntryPrefab = CreateReceiptEntryPrefab();
            // Importing dependent assets can invalidate previously loaded native objects.
            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>($"{Root}/Data/game_settings.asset");
            if (settings == null) throw new System.InvalidOperationException("Game settings asset is missing.");
            CreateScene(settings, slicePrefab, entryPrefab, receiptEntryPrefab);
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

        /// <summary>Font material with a soft drop shadow, shared by titles, numbers and button labels.</summary>
        private static Material HeadingMaterial()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(HeadingMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HeadingMaterialPath)!);
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, HeadingMaterialPath);
            }
            material.CopyPropertiesFromMaterial(font.material);
            material.shaderKeywords = font.material.shaderKeywords;
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.65f));
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.9f);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ---------- Prefabs ----------

        private static WheelSliceView CreateSlicePrefab()
        {
            RectTransform root = Rect("ui_wheel_slice", null, Vector2.zero, new Vector2(110f, 110f));
            var view = root.gameObject.AddComponent<WheelSliceView>();
            Image icon = Img(Rect("ui_image_slice_icon_value", root, new Vector2(0f, 12f), new Vector2(54f, 54f)), null);
            TMP_Text amount = Text(Rect("ui_text_slice_amount_value", root, new Vector2(0f, -28f), new Vector2(84f, 28f)),
                "x1", 21f, Color.white);
            Heading(amount);
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

        private static ReceiptEntryView CreateReceiptEntryPrefab()
        {
            RectTransform root = Rect("ui_receipt_entry", null, Vector2.zero, new Vector2(480f, 84f));
            Img(Stretch("ui_image_receipt_entry_bg", root), Common("ui_card_panel_zone_coming"), sliced: true, maskable: true)
                .color = new Color(0.13f, 0.17f, 0.23f, 0.85f);
            var view = root.gameObject.AddComponent<ReceiptEntryView>();
            Vector2 left = new(0f, 0.5f), right = new(1f, 0.5f);
            Image icon = Img(Rect("ui_image_receipt_icon_value", root, new Vector2(14f, 0f), new Vector2(86f, 64f), left, left),
                null, maskable: true);
            TMP_Text rewardName = Text(Rect("ui_text_receipt_name_value", root, new Vector2(112f, 0f), new Vector2(220f, 60f), left, left),
                string.Empty, LabelSize, MutedText, TextAlignmentOptions.Left, maskable: true);
            rewardName.enableAutoSizing = true;
            rewardName.fontSizeMin = 16f;
            rewardName.fontSizeMax = LabelSize;
            TMP_Text amount = Text(Rect("ui_text_receipt_amount_value", root, new Vector2(-16f, 0f), new Vector2(124f, 60f), right, right),
                string.Empty, ValueSize, Color.white, TextAlignmentOptions.Right, maskable: true);
            amount.fontStyle = FontStyles.Bold;
            Set(view, ("_icon", icon), ("_nameText", rewardName), ("_amountText", amount));
            return SavePrefab(root, "ui_receipt_entry").GetComponent<ReceiptEntryView>();
        }

        private static GameObject SavePrefab(RectTransform root, string prefabName)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, $"{Root}/Prefabs/{prefabName}.prefab");
            Object.DestroyImmediate(root.gameObject);
            return prefab;
        }

        // ---------- Scene ----------

        private static void CreateScene(GameSettings settings, WheelSliceView slicePrefab, RewardEntryView entryPrefab,
            ReceiptEntryView receiptEntryPrefab)
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
            // Wide, faint light pooled behind the whole stage ties the three columns together.
            Image atmosphere = Img(Rect("ui_image_atmosphere", stage, new Vector2(0f, -60f), new Vector2(1900f, 1300f)),
                Common("star_glow_alpha"));
            atmosphere.preserveAspect = false;
            atmosphere.color = new Color(0.22f, 0.36f, 0.55f, 0.16f);

            ZoneBarView zoneBar = CreateZoneBar(stage);
            RewardPanelView rewardPanel = CreateRewardPanel(stage, entryPrefab);
            CashOutPanelView cashOutPanel = CreateCashOutPanel(stage);
            WheelView wheel = CreateWheel(stage, slicePrefab);

            Image flyingIcon = Img(Rect("ui_image_flying_icon_value", canvas, Vector2.zero, new Vector2(110f, 110f)), null);
            flyingIcon.gameObject.SetActive(false);
            Set(rewardPanel, ("_flyingIcon", flyingIcon));

            ResultPopupView popup = CreatePopup(canvas);
            ResultPopupView receiptPopup = CreateReceiptPopup(canvas, receiptEntryPrefab);

            var controller = canvasGo.AddComponent<GameController>();
            Set(controller,
                ("_settings", settings), ("_wheel", wheel), ("_zoneBar", zoneBar),
                ("_rewardPanel", rewardPanel), ("_cashOutPanel", cashOutPanel),
                ("_popup", popup), ("_receiptPopup", receiptPopup),
                ("_bombPopup.Title", "BOMB EXPLODED!\nALL REWARDS ARE LOST"),
                ("_bombPopup.Icon", LoadSprite(RewardSprites, "ui_card_icon_death")),
                ("_bombPopup.ButtonLabel", "GIVE UP"),
                ("_collectedPopup.Title", "REWARDS COLLECTED!"),
                ("_collectedPopup.Icon", LoadSprite(RewardSprites, "UI_icon_chest_big_nolight")),
                ("_collectedPopup.ButtonLabel", "PLAY AGAIN"));
            RunOnValidate(controller);
            RunOnValidate(popup);
            RunOnValidate(receiptPopup);

            // Editor preview: show zone 1 so the scene is not full of empty placeholders. Play mode overwrites this.
            var preview = new WheelSession(settings, _ => 0);
            wheel.Show(preview);
            zoneBar.Show(preview.Zone, settings);
            cashOutPanel.Show(preview.CanLeave, GameSettings.NextZone(preview.Zone, settings.SafeZoneInterval));
            foreach (Graphic graphic in wheel.GetComponentsInChildren<Graphic>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
            foreach (RectTransform transform in wheel.GetComponentsInChildren<RectTransform>(true))
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        }

        private static ZoneBarView CreateZoneBar(RectTransform stage)
        {
            Vector2 top = new(0.5f, 1f);
            RectTransform bar = Rect("ui_zone_bar", stage, new Vector2(0f, -32f), new Vector2(ZoneItemWidth * ZoneVisibleCount + 32f, 76f), top, top);
            var view = bar.gameObject.AddComponent<ZoneBarView>();
            Img(Stretch("ui_image_zone_bar_bg", bar), Common("ui_card_panel_zone_coming"), sliced: true)
                .color = new Color(0.065f, 0.085f, 0.12f, 0.95f);

            RectTransform viewport = Stretch("ui_zone_bar_viewport", bar, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();
            // Flat tinted tile for the current zone; ZoneBarView colors it by zone type.
            Image highlight = Img(Rect("ui_image_zone_current_value", viewport, Vector2.zero, new Vector2(ZoneItemWidth - 8f, 56f)),
                Common("ui_card_panel_zone_coming"), sliced: true, maskable: true);

            RectTransform content = Rect("ui_zone_bar_content", viewport, Vector2.zero, new Vector2(ZoneItemWidth * ZoneLabelCount, 64f));
            var labels = new TMP_Text[ZoneLabelCount];
            for (int i = 0; i < ZoneLabelCount; i++)
            {
                float x = (i - ZoneLabelCount / 2) * ZoneItemWidth;
                labels[i] = Heading(Text(Rect($"ui_text_zone_{i}_value", content, new Vector2(x, 0f), new Vector2(ZoneItemWidth, 64f)),
                    string.Empty, ValueSize, Color.white, maskable: true));
            }

            Image frame = Img(Rect("ui_image_zone_current_frame_value", viewport, Vector2.zero, new Vector2(ZoneItemWidth + 2f, 70f)),
                Common("ui_card_zone_map_frame"), sliced: true, maskable: true);
            Img(Stretch("ui_image_zone_bar_frame", bar), Common("ui_card_frame_4px_zone"), sliced: true).color = new Color(0.4f, 0.5f, 0.65f, 0.35f);

            // Upcoming milestones flank the strip and line up with the side columns below.
            float y = bar.anchoredPosition.y - 38f;
            TMP_Text nextSafe = ZoneInfoBox(stage, "ui_zone_info_safe", "NEXT SAFE ZONE", new Vector2(-ColumnX, y),
                new Color(0.55f, 0.9f, 0.3f));
            TMP_Text nextSuper = ZoneInfoBox(stage, "ui_zone_info_super", "NEXT SUPER ZONE", new Vector2(ColumnX, y),
                new Color(1f, 0.76f, 0.25f));

            Set(view, ("_content", content), ("_zoneTexts", labels), ("_currentHighlight", highlight),
                ("_currentFrame", (RectTransform)frame.transform),
                ("_nextSafeText", nextSafe), ("_nextSuperText", nextSuper), ("_itemWidth", ZoneItemWidth));
            return view;
        }

        /// <summary>Informational legend: dark flat card with an accent outline, never a filled button shape.</summary>
        private static TMP_Text ZoneInfoBox(RectTransform stage, string boxName, string label, Vector2 position, Color accent)
        {
            Vector2 top = new(0.5f, 1f), left = new(0f, 0.5f), right = new(1f, 0.5f);
            RectTransform box = Rect(boxName, stage, position, new Vector2(ColumnWidth, 64f), top, new Vector2(0.5f, 0.5f));
            Img(Stretch($"ui_image_{boxName}_bg", box), Common("ui_card_panel_zone_coming"), sliced: true)
                .color = new Color(0.065f, 0.085f, 0.12f, 0.95f);
            Img(Stretch($"ui_image_{boxName}_frame", box), Common("ui_card_frame_4px_zone"), sliced: true)
                .color = new Color(accent.r, accent.g, accent.b, 0.55f);
            Img(Rect($"ui_image_{boxName}_accent", box, new Vector2(10f, 0f), new Vector2(6f, 36f), left, left),
                Common("ui_card_panel_zone_coming"), sliced: true).color = accent;
            Text(Rect($"ui_text_{boxName}_label", box, new Vector2(28f, 0f), new Vector2(200f, 54f), left, left),
                label, 20f, MutedText, TextAlignmentOptions.Left).characterSpacing = 1f;
            var value = Heading(Text(Rect($"ui_text_{boxName}_value", box, new Vector2(-18f, 0f), new Vector2(80f, 54f), right, right),
                "0", ValueSize, accent, TextAlignmentOptions.Right));
            value.enableAutoSizing = true;
            value.fontSizeMin = 18f;
            value.fontSizeMax = ValueSize;
            return value;
        }

        /// <summary>Shared dark card used by both side columns.</summary>
        private static RectTransform SidePanel(RectTransform stage, string panelName, float x, string title)
        {
            RectTransform panel = Rect(panelName, stage, new Vector2(x, -88f), new Vector2(ColumnWidth, 600f));
            Img(Stretch($"ui_image_{panelName}_bg", panel), Common("ui_card_panel_zone_coming"), sliced: true)
                .color = new Color(0.065f, 0.085f, 0.12f, 0.95f);
            Img(Stretch($"ui_image_{panelName}_frame", panel), Common("ui_card_frame_4px_zone"), sliced: true)
                .color = new Color(0.4f, 0.5f, 0.65f, 0.22f);
            Vector2 top = new(0.5f, 1f);
            Text(Rect($"ui_text_{panelName}_title", panel, new Vector2(0f, -22f), new Vector2(290f, 42f), top, top),
                title, LabelSize, Color.white).characterSpacing = 1f;
            Img(Rect($"ui_image_{panelName}_divider", panel, new Vector2(0f, -76f), new Vector2(ColumnWidth - 52f, 2f), top, top), null)
                .color = new Color(0.4f, 0.5f, 0.65f, 0.25f);
            return panel;
        }

        private static RewardPanelView CreateRewardPanel(RectTransform stage, RewardEntryView entryPrefab)
        {
            RectTransform panel = SidePanel(stage, "ui_reward_panel", -ColumnX, "COLLECTED REWARDS");
            var view = panel.gameObject.AddComponent<RewardPanelView>();
            Vector2 top = new(0.5f, 1f);

            RectTransform scroll = Stretch("ui_reward_scroll", panel);
            scroll.offsetMin = new Vector2(20f, 20f);
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

            var empty = Rect("ui_reward_empty_state_value", panel, new Vector2(0f, -30f), new Vector2(270f, 200f));
            Img(Rect("ui_image_reward_empty_icon", empty, new Vector2(0f, 48f), new Vector2(130f, 74f)),
                LoadSprite(RewardSprites, "UI_icon_chest_small_noligt")).color = new Color(1f, 1f, 1f, 0.45f);
            Text(Rect("ui_text_reward_empty_title", empty, new Vector2(0f, -16f), new Vector2(270f, 36f)),
                "YOUR RUN STARTS HERE", 22f, MutedText);
            var emptyHint = Text(Rect("ui_text_reward_empty_hint", empty, new Vector2(0f, -62f), new Vector2(270f, 56f)),
                "Spin the wheel to\nbuild your rewards", 21f, MutedText);
            emptyHint.textWrappingMode = TextWrappingModes.Normal;
            Set(view, ("_entryPrefab", entryPrefab), ("_content", content), ("_emptyState", empty.gameObject));
            return view;
        }

        /// <summary>Right column mirrors the rewards list: whether leaving is possible, and the button that does it.</summary>
        private static CashOutPanelView CreateCashOutPanel(RectTransform stage)
        {
            RectTransform panel = SidePanel(stage, "ui_cash_out_panel", ColumnX, "CASH OUT");
            var view = panel.gameObject.AddComponent<CashOutPanelView>();

            Image glow = Img(Rect("ui_image_cash_out_glow", panel, new Vector2(0f, 90f), new Vector2(360f, 260f)), Common("star_glow_alpha"));
            glow.preserveAspect = false;
            glow.color = new Color(1f, 0.7f, 0.3f, 0.18f);
            Image icon = Img(Rect("ui_image_cash_out_icon_value", panel, new Vector2(0f, 90f), new Vector2(230f, 110f)),
                LoadSprite(RewardSprites, "UI_icon_chest_big_nolight"));
            TMP_Text status = Text(Rect("ui_text_cash_out_status_value", panel, new Vector2(0f, -60f), new Vector2(290f, 80f)),
                string.Empty, 22f, MutedText);
            status.textWrappingMode = TextWrappingModes.Normal;
            status.lineSpacing = 8f;

            Vector2 bottom = new(0.5f, 0f);
            Button exit = CreateButton(Rect("ui_button_exit", panel, new Vector2(0f, 24f), new Vector2(ColumnWidth - 48f, 80f), bottom, bottom),
                Common("UI_button_orange_standard"), "COLLECT", "ui_text_exit_value");
            exit.transition = Selectable.Transition.SpriteSwap;
            exit.spriteState = new SpriteState { disabledSprite = Common("UI_button_grey_standard") };
            AddShadow(exit.gameObject, 4f);

            Set(view, ("_buttonImage", exit.targetGraphic), ("_buttonText", exit.GetComponentInChildren<TMP_Text>()),
                ("_statusText", status), ("_icon", icon));
            return view;
        }

        private static WheelView CreateWheel(RectTransform stage, WheelSliceView slicePrefab)
        {
            RectTransform wheel = Rect("ui_wheel", stage, new Vector2(0f, -88f), new Vector2(WheelSize, WheelSize));
            var view = wheel.gameObject.AddComponent<WheelView>();

            // Tinted per tier by WheelView, so bronze/silver/golden each light the stage differently.
            Image glow = Img(Rect("ui_image_wheel_glow_value", wheel, Vector2.zero, new Vector2(980f, 980f)), Common("star_glow_alpha"));
            Image backdrop = Img(Rect("ui_image_wheel_backdrop", wheel, new Vector2(0f, -10f), new Vector2(WheelSize * 1.12f, WheelSize * 1.12f)),
                Common("star_glow_alpha"));
            backdrop.color = new Color(0f, 0f, 0f, 0.55f); // soft contact shadow lifting the wheel off the background

            TMP_Text title = Heading(Text(Rect("ui_text_wheel_title_value", wheel, new Vector2(0f, WheelSize * 0.5f + 70f), new Vector2(WheelSize, 60f)),
                "BRONZE SPIN", TitleSize, Color.white), 5f);
            TMP_Text hint = Text(Rect("ui_text_wheel_zone_hint_value", wheel, new Vector2(0f, -WheelSize * 0.5f - 34f), new Vector2(650f, 40f)),
                string.Empty, 22f, MutedText);
            hint.characterSpacing = 2f;

            RectTransform rotator = Stretch("ui_wheel_rotator", wheel);
            Image baseImage = Img(Stretch("ui_image_wheel_base_value", rotator), Common("ui_spin_bronze_base"));

            var slices = new WheelSliceView[WheelDefinition.SliceCount];
            for (int i = 0; i < slices.Length; i++)
            {
                // Slice i is placed i * 45 degrees clockwise from the top. WheelView keeps its content upright while spinning.
                float angle = i * 360f / slices.Length;
                var slice = (WheelSliceView)PrefabUtility.InstantiatePrefab(slicePrefab, rotator);
                slice.name = $"ui_wheel_slice_{i}";
                var rt = (RectTransform)slice.transform;
                rt.anchoredPosition = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad)) * SliceRadius;
                rt.localEulerAngles = Vector3.zero;
                slices[i] = slice;
            }

            // Drawn at the same scale as the wheel base art (600 / 485) so the pointer stays as sharp as the base.
            float artScale = WheelSize / 485f;
            Image indicator = Img(Rect("ui_image_wheel_indicator_value", wheel, new Vector2(0f, WheelSize * 0.47f),
                new Vector2(43f * artScale, 59f * artScale)), Common("ui_spin_bronze_indicator"));
            AddShadow(indicator.gameObject, 5f);

            RectTransform pulse = Rect("ui_spin_pulse", wheel, Vector2.zero, new Vector2(134f * artScale, 134f * artScale));
            Image halo = Img(Rect("ui_image_spin_halo", pulse, Vector2.zero, new Vector2(300f, 300f)), Common("star_glow_alpha"));
            halo.color = new Color(0.45f, 0.65f, 1f, 0.55f);
            Button spin = CreateButton(Stretch("ui_button_spin", pulse), Common("ui_spin_generic_button"), null, null);
            spin.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = Color.white,
                pressedColor = new Color(0.75f, 0.78f, 0.85f),
                selectedColor = Color.white,
                disabledColor = new Color(0.6f, 0.6f, 0.65f, 0.75f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            AddShadow(spin.gameObject, 6f);

            Set(view, ("_rotator", rotator), ("_glow", glow), ("_spinPulse", pulse), ("_baseImage", baseImage),
                ("_indicatorImage", indicator), ("_titleText", title), ("_zoneHintText", hint), ("_slices", slices));
            return view;
        }

        private static void AddShadow(GameObject target, float distance)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(0f, -distance);
        }

        private static ResultPopupView CreatePopup(RectTransform canvas)
        {
            (RectTransform root, RectTransform panel) = PopupShell(canvas, "ui_popup_result", new Vector2(760f, 720f));

            Vector2 top = new(0.5f, 1f), bottom = new(0.5f, 0f);
            TMP_Text title = Text(Rect("ui_text_popup_title_value", panel, new Vector2(0f, -40f), new Vector2(700f, 130f), top, top),
                string.Empty, TitleSize, Color.white);
            Heading(title);
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

        /// <summary>Cash-out popup: a larger panel whose middle is a scrollable two-column list of everything collected.</summary>
        private static ResultPopupView CreateReceiptPopup(RectTransform canvas, ReceiptEntryView entryPrefab)
        {
            (RectTransform root, RectTransform panel) = PopupShell(canvas, "ui_popup_receipt", new Vector2(1080f, 900f));

            Vector2 top = new(0.5f, 1f), bottom = new(0.5f, 0f);
            TMP_Text title = Text(Rect("ui_text_popup_title_value", panel, new Vector2(0f, -40f), new Vector2(1000f, 70f), top, top),
                string.Empty, TitleSize, Color.white);
            Heading(title);
            Image icon = Img(Rect("ui_image_popup_icon_value", panel, new Vector2(0f, -118f), new Vector2(240f, 160f), top, top), null);

            RectTransform scroll = Stretch("ui_receipt_scroll", panel);
            scroll.offsetMin = new Vector2(40f, 170f);
            scroll.offsetMax = new Vector2(-40f, -300f);
            Img(scroll, Common("ui_card_panel_zone_coming"), sliced: true, raycast: true) // raycast surface needed for dragging the list
                .color = new Color(0.04f, 0.055f, 0.08f, 0.6f);
            scroll.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect("ui_receipt_content", scroll, Vector2.zero, Vector2.zero, top, top);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(480f, 84f);
            grid.spacing = new Vector2(16f, 12f);
            grid.padding = new RectOffset(0, 0, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = content;
            scrollRect.viewport = scroll;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            Button close = CreateButton(Rect("ui_button_popup_close", panel, new Vector2(0f, 40f), new Vector2(340f, 100f), bottom, bottom),
                Common("UI_button_orange_standard"), string.Empty, "ui_text_popup_button_value");

            var view = root.gameObject.AddComponent<ResultPopupView>(); // added last so OnValidate finds the button
            Set(view, ("_panel", panel), ("_titleText", title), ("_icon", icon),
                ("_buttonText", close.GetComponentInChildren<TMP_Text>()),
                ("_receiptEntryPrefab", entryPrefab), ("_receiptContent", content));
            root.gameObject.SetActive(false);
            return view;
        }

        /// <summary>Full-screen input blocker plus a framed panel of the given size.</summary>
        private static (RectTransform root, RectTransform panel) PopupShell(RectTransform canvas, string objectName, Vector2 size)
        {
            RectTransform root = Stretch(objectName, canvas);
            Img(Stretch("ui_image_popup_blocker", root), null, raycast: true).color = new Color(0f, 0f, 0f, 0.8f); // blocks input behind

            RectTransform panel = Rect("ui_popup_panel", root, Vector2.zero, size);
            Img(Stretch("ui_image_popup_bg", panel), Common("ui_card_panel_zone_bg"), sliced: true);
            Img(Stretch("ui_image_popup_frame", panel), Common("ui_card_frame_12px_neutral"), sliced: true);
            return (root, panel);
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

        private static Material _headingMaterial;

        private static TMP_Text Text(RectTransform rt, string text, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, bool maskable = false)
        {
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            label.fontSize = size;
            label.fontStyle = FontStyles.Normal;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.maskable = maskable;
            return label;
        }

        /// <summary>Bold display style with the shared drop-shadow material.</summary>
        private static TMP_Text Heading(TMP_Text label, float spacing = 0f)
        {
            if (_headingMaterial == null) _headingMaterial = HeadingMaterial();
            label.fontSharedMaterial = _headingMaterial;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = spacing;
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
                Heading(Text(Stretch(labelName, rt), label, ValueSize, Color.white), 2f);
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
