using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.EventSystems;
using HarmonyLib;
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MadIslandKCM
{
    [BepInPlugin("com.kydra.madisland.kcm", "Mad Island Kydra Cheat Menu", "1.2")]
    public class KydraCheatMenu : BaseUnityPlugin
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
        private const byte VK_RETURN = 0x0D;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private static readonly string _signature = "0JDQstGC0L7RgCDQvtGA0LjQs9C40L3QsNC70YzQvdC+0LPQviDQvNC+0LTQsCBLeWRyYSBGcm9zYSAtIGh0dHBzOi8vc3RlYW1jb21tdW5pdHkuY29tL2lkL19hcmRpa18vINCb0Y7QsdC+0LUg0LjQt9C80LXQvdC10L3QuNC1INC4INC/0LXRgNC10L/Rg9Cx0LvQuNC60LDRhtC40Y8g0LzQvtC00LAg0YDQsNC30YDQtdGI0LXQvdGLINGC0L7Qu9GM0LrQviDRgSDRg9C60LDQt9Cw0L3QuNC10Lwg0L7RgNC40LPQuNC90LDQu9CwINC4INGB0YHRi9C70LrQuCDQvdCwINCw0LLRgtC+0YDQsC4=";

        public static bool IsUIOpen = false;
        public static bool isExecutingCommandSequence = false;
        public static KeyCode CurrentToggleKey = KeyCode.F3;

        public static bool isGodMode = false;
        public static bool isNoclip = false;
        public static float flySpeed = 12f;

        private string hpInput = "10000";
        private bool showUI = false;
        private Rect windowRect = new Rect(80, 60, 720, 830);
        private int activeTab = 0;

        private ConfigEntry<string> configLanguage;
        private ConfigEntry<string> configToggleKey;
        private ConfigEntry<string> configFavorites;

        private KeyCode toggleKey = KeyCode.F3;
        private bool isRebindingKey = false;
        private static HashSet<string> favoriteItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool IsRussian => configLanguage != null && configLanguage.Value.ToLower() == "ru";

        private string T(string en, string ru)
        {
            return IsRussian ? ru : en;
        }

        [Serializable]
        public class CategoryDefinition
        {
            public string nameRU;
            public string nameEN;
            public List<string> defaultPrefixes = new List<string>();
            public HashSet<string> itemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public static List<CategoryDefinition> categoryList = new List<CategoryDefinition>();
        private string[] categoryDisplayNamesRU;
        private string[] categoryDisplayNamesEN;

        private bool showCategoryEditor = false;
        private Vector2 scrollPositionCategoryEditor;
        private string activeCategoryPickerItemId = "";

        private string newCatNameRU = "Новая категория";
        private string newCatNameEN = "New Category";

        public class ItemInfo
        {
            public string rawId;
            public string cleanId;
            public Sprite iconSprite;
        }

        private List<ItemInfo> itemsList = new List<ItemInfo>();
        private List<ItemInfo> filteredItems = new List<ItemInfo>();
        private bool isLoaded = false;
        private string debugStatus = "";
        private string searchText = "";
        private Vector2 scrollPositionItems;
        private Vector2 scrollPositionNpcs;
        private Vector2 scrollPositionNpcCheats;

        private int selectedCategory = 0;
        private int giveAmount = 1;
        private string customAmountText = "1";
        private int currentSortType = 0;
        private int currentPage = 0;
        private const int itemsPerPage = 25;

        public class NpcSpawnData
        {
            public string nameRU;
            public string nameEN;
            public string command;
            public string iconKey;
            public int categoryIndex;
            public Sprite cachedSprite;
        }

        private List<NpcSpawnData> npcDatabase = new List<NpcSpawnData>();
        private List<NpcSpawnData> filteredNpcs = new List<NpcSpawnData>();
        private int selectedNpcCategory = 0;
        private string searchNpcText = "";
        private int currentNpcPage = 0;
        private const int npcsPerPage = 20;

        private string[] npcCatEN = new string[] { "All", "Natives (M)", "Natives (F)", "NPCs", "Bosses", "Animals (Friendly)", "Animals (Hostile)", "Monsters", "Ruins/Hell/Lab" };
        private string[] npcCatRU = new string[] { "Все", "Туземцы (М)", "Туземцы (Ж)", "NPC и Связанные", "Боссы", "Животные (Дружелюбные)", "Животные (Враждебные)", "Монстры", "Руины/Ад/Лаборатория" };

        private string expInput = "1000";
        private string pointsInput = "10";
        private string skillPointsInput = "10";
        private string atkInput = "50";
        private string runSpeedInput = "10";
        private string followCapInput = "5";
        private string tpInput = "1";

        private string npcIdInput = "10";
        private string patrolInput = "5";
        private string deadTimeInput = "60";
        private string moralInput = "100";

        private float targetTimeScale = 1.0f;
        private float lastAppliedTimeScale = -1.0f;

        void Awake()
        {
            configLanguage = Config.Bind("General", "Language", "en", "Language selection (en/ru)");
            configToggleKey = Config.Bind("General", "ToggleKey", "F3", "Key to open/close cheat menu");
            configFavorites = Config.Bind("General", "Favorites", "", "Comma-separated favorite item IDs");

            if (Enum.TryParse<KeyCode>(configToggleKey.Value, out KeyCode parsedKey))
            {
                toggleKey = parsedKey;
            }
            CurrentToggleKey = toggleKey;

            LoadFavoritesFromConfig();
            InitDefaultCategories();
            ImportCategoriesFromFile();

            try
            {
                var harmony = new Harmony("com.kydra.madisland.kcm");
                harmony.PatchAll();
            }
            catch (Exception) { }

            InitNpcDatabase();
            debugStatus = T("Press 'Refresh' to load items", "Нажмите 'Обновить' для загрузки");
        }

        void Update()
        {
            IsUIOpen = showUI;
            CurrentToggleKey = toggleKey;

            if (isGodMode)
            {
                DirectGameDataModifier.SetPlayerHPDirect(999999f, DirectGameDataModifier.HPSetType.CurrentOnly, false);
            }

            CameraController.UpdateCamera();

            if (isNoclip)
            {
                UpdateNoclipMovement();
            }

            if (isRebindingKey)
            {
                foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                {
                    if (UnityEngine.Input.GetKeyDown(k))
                    {
                        if (k != KeyCode.Escape)
                        {
                            toggleKey = k;
                            CurrentToggleKey = toggleKey;
                            configToggleKey.Value = toggleKey.ToString();
                            Config.Save();
                            debugStatus = T($"Toggle key changed to: {toggleKey}", $"Клавиша вызова изменена на: {toggleKey}");
                        }
                        isRebindingKey = false;
                        break;
                    }
                }
                return;
            }

            if (UnityEngine.Input.GetKeyDown(toggleKey))
            {
                showUI = !showUI;
                IsUIOpen = showUI;
                if (showUI && !isLoaded)
                {
                    ScanGameItems();
                }
            }

            bool modifierPressed = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift) ||
                                   UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
            if (modifierPressed)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Comma) || UnityEngine.Input.GetKeyDown(KeyCode.Less))
                {
                    targetTimeScale = Mathf.Max(0.0f, targetTimeScale - 1.0f);
                }
                if (UnityEngine.Input.GetKeyDown(KeyCode.Period) || UnityEngine.Input.GetKeyDown(KeyCode.Greater))
                {
                    targetTimeScale = Mathf.Min(50.0f, targetTimeScale + 1.0f);
                }
            }

            if (Time.timeScale == 0f && lastAppliedTimeScale > 0f) { }
            else if (Mathf.Abs(Time.timeScale - targetTimeScale) > 0.01f || lastAppliedTimeScale != targetTimeScale)
            {
                Time.timeScale = targetTimeScale;
                lastAppliedTimeScale = targetTimeScale;
            }
        }

        private void UpdateNoclipMovement()
        {
            GameObject player = DirectGameDataModifier.GetPlayerObject();
            if (player == null) return;

            Vector3 move = Vector3.zero;
            Transform camTransform = Camera.main != null ? Camera.main.transform : player.transform;

            if (UnityEngine.Input.GetKey(KeyCode.W)) move += camTransform.forward;
            if (UnityEngine.Input.GetKey(KeyCode.S)) move -= camTransform.forward;
            if (UnityEngine.Input.GetKey(KeyCode.D)) move += camTransform.right;
            if (UnityEngine.Input.GetKey(KeyCode.A)) move -= camTransform.right;
            if (UnityEngine.Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.C)) move -= Vector3.up;

            player.transform.position += move * flySpeed * Time.unscaledDeltaTime;
        }

        #region ИЗБРАННОЕ
        private void LoadFavoritesFromConfig()
        {
            favoriteItemIds.Clear();
            if (configFavorites != null && !string.IsNullOrEmpty(configFavorites.Value))
            {
                string[] parts = configFavorites.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var p in parts)
                {
                    favoriteItemIds.Add(p.Trim());
                }
            }
        }

        private void SaveFavoritesToConfig()
        {
            if (configFavorites != null)
            {
                configFavorites.Value = string.Join(",", favoriteItemIds);
                Config.Save();
            }
        }

        private void ToggleFavorite(string cleanId)
        {
            if (string.IsNullOrEmpty(cleanId)) return;
            if (favoriteItemIds.Contains(cleanId))
            {
                favoriteItemIds.Remove(cleanId);
            }
            else
            {
                favoriteItemIds.Add(cleanId);
            }
            SaveFavoritesToConfig();
            ApplySortAndFilter();
        }
        #endregion

        #region УПРАВЛЕНИЕ КАТЕГОРИЯМИ
        private void InitDefaultCategories()
        {
            categoryList.Clear();

            AddCategoryDef("⚔️ Оружие", "⚔️ Weapons", "wp_,sword_,shield_,knife_,bow_,spear_,axe_",
                "arrow_00,arrow_01,arrow_02,arrow_chaos_01,arrow_glass_01,arrow_pretty_01,arrow_spider_01,arrow_wing_01,torch_01,torch_chaos_01");

            AddCategoryDef("🛡️ Броня", "🛡️ Armor", "cloth_,armor_,hat_,pants_,top_,shoes_",
                "acce_00,acce_01,acce_02,acce_03,acce_04,acce_05,acce_06,acce_07,acce_08,acce_09,acce_10,acce_102,acce_104,acce_105,acce_106,acce_118,acce_12,acce_124,acce_125,acce_127,acce_13,acce_131,acce_14,acce_15,acce_152,acce_16,acce_17,acce_18,acce_19,acce_22,acce_23,acce_24,acce_25,acce_27,acce_28,acce_30,acce_32,acce_33,acce_34,acce_36,acce_37,acce_38,acce_40,acce_46,acce_48,acce_57,acce_59,acce_63,acce_67,acce_68,acce_71,acce_74,acce_76,acce_77,acce_78,acce_79,acce_80,acce_88,acce_90,acce_91,acce_92,acce_93,acce_96,acce_97,acce_99,acce_s_00,acce_s_01,acce_s_02,acce_s_03,acce_s_04,acce_s_06,acce_s_death_01,acce_s_wisp_01,acce_s_wisp_02,clothu_00,clothu_01,clothu_03,clothu_04,clothu_05,clothu_06,clothu_08,clothu_10,clothu_100,clothu_102,clothu_103,clothu_104,clothu_105,clothu_106,clothu_114,clothu_115,clothu_117,clothu_118,clothu_119,clothu_120,clothu_121,clothu_126,clothu_127,clothu_128,clothu_131,clothu_132,clothu_133,clothu_134,clothu_135,clothu_136,clothu_137,clothu_138,clothu_14,clothu_152,clothu_153,clothu_154,clothu_155,clothu_16,clothu_17,clothu_18,clothu_19,clothu_21,clothu_22,clothu_23,clothu_24,clothu_25,clothu_26,clothu_27,clothu_28,clothu_30,clothu_31,clothu_32,clothu_33,clothu_34,clothu_35,clothu_36,clothu_38,clothu_39,clothu_40,clothu_41,clothu_42,clothu_43,clothu_44,clothu_45,clothu_47,clothu_48,clothu_49,clothu_50,clothu_51,clothu_53,clothu_54,clothu_55,clothu_56,clothu_57,clothu_58,clothu_59,clothu_60,clothu_61,clothu_63,clothu_64,clothu_65,clothu_67,clothu_68,clothu_69,clothu_70,clothu_71,clothu_72,clothu_73,clothu_74,clothu_91,clothu_92,clothu_93,clothu_94,clothu_95,clothu_96,clothu_97,clothu_98,clothu_99,l_cloth_00,l_cloth_01,l_clothu_00,l_clothu_01,l_clothu_02,m_cloth_00,m_cloth_05,clothu_75,clothu_76,clothu_77,clothu_78,clothu_79,clothu_80,clothu_81,clothu_82,clothu_83,clothu_84,clothu_85,clothu_86,clothu_87,clothu_88,clothu_89,clothu_90,m_cloth_06,m_cloth_07,m_clothu_00,m_clothu_01,m_clothu_02,m_clothu_03,m_clothu_05,m_clothu_06,m_clothu_07,hip_107,hip_31,hip_32,hip_33,hip_38,hip_69,m_shoes_00");

            AddCategoryDef("🍎 Еда", "🍎 Food", "food_,cook_,seed_,berry_,herb_,mushroom_,meat_",
                "fish_angler_01,fish_ass_01,fish_babyface_01,fish_bigot_01,fish_blind_01,fish_clione_01,fish_dragon_01,fish_egg_01,fish_eyeball_01,fish_firog_01,fish_fishoil_01,fish_glow_01,fish_goldfish_01,fish_goldfish_02,fish_jellyfish_01,fish_killer_01,fish_left_01,fish_liquid_01,fish_meat_01,fish_meat_02,fish_melty_01,fish_moguro_01,fish_monkish_01,fish_mutantoctopus_01,fish_paiot_01,fish_peach_01,fish_red_01,fish_rock_01,fish_sandfugu_01,fish_shrimp_01,fish_tentacle_01");

            AddCategoryDef("🪵 Ресурсы", "🪵 Resources", "wood_,iron_,stone_,bone_,leather_,ore_,repairkit_",
                "acorn_01,ev_king_stone_01,brain,branch_01,cactus_01,charcoal_01,claw_01,clay_01,cobweb_01,collect_note_takumi,collect_transceiver,def_jar_cursed_01,diamond_01,elecmuscle_01,ev_king_branch_01,ev_king_fiber_01,ev_king_sap_01,ev_king_vine_01,ev_king2_bar_01,ev_king2_grip_01,ev_king2_head_01,ev_king3_hammer_01,explosionbag_01,fabric_01,feather_01,fiber_01,glass_01,gold_01,hide_01,hive_01,leaf_01,liquid_01,orb_core_00,orb_core_01,orb_curse_01,orb_dead_01,orb_earth_01,orb_fighter_01,orb_life_01,orb_mad_01,orb_soul_01,orb_soul_02,parts_gengiant_01_oppai,parts_genunder_01_oppai,parts_mummy_01_oppai,poop_01,potion_air_01,recipe_corrupt,recipe_death,recipe_man,recipe_meat,recipe_plant,recipe_pretty,recipe_sand,recipe_spider,recipe_train,recipe_wing,rope_01,sand_01,sap_01,shell_01,shell_turtle_01,skin_gorilla_01,skin_snake_01,skin_spider_01,skin_whitetiger_01,soil_01,titanium_01,token_kill_01,token_talk_01,vine_01,vine_02,wool_01,worm_01,worm_02,xmas_box_santa_01");

            AddCategoryDef("👥 NPC", "👥 NPCs", "slave_,npc_,friend_", "");

            AddCategoryDef("📦 Прочее", "📦 Misc", "*", "");

            AddCategoryDef("16.08.2026 unsupport", "08.16.2026 unsupport", "",
                "wp_boomerang_01,wp_bow_meat_01,wp_chainsaw_01,wp_club_iron_01,wp_paddle_01,berry_chaos_01,cook_manju_manko_01,cook_manju_tinko_01,cook_onigiri_01,cook_sandwich_pork _01,food_back,food_carrot,food_corn,food_egg,food_meat_rotten_01,food_potato,food_soybean,food_watermelon_cut_01,food_watermelon_cut_02,herb_02_02,herb_02_03,mushroom_01g,seed_carrot,seed_corn,seed_piment,seed_potato,seed_soybean,seed_wheat,wood_01_ground,wood_01_wood,wood_01_wood2,wood_01_wood3,wood_01_wood4,stone_01_ground,stone_01_stone,stone_02,stone_03,stone_cobble_01,stone_small_01,stone_small_01_fx,2d_uki,2dcube,2dcube_100,2dcube_alpha,2dcube_grad,angry_01,animal_boss_gorilla_01,animal_boss_gorilla_02,fence_animal_01_1,fence_animal_01_2,fence_animal_01_3,fence_animal_02_1,fence_animal_02_2,fence_animal_02_3,props_02_animalremover_01,arrow_spear_01,arrow_test_0,arrow_test_1,dropdownarrow,ruins_prop_01_arrow_01,sign_arrow_01,props_07_vib_01,background,bathtub_01_back,bed_snake_01_back,bench_cook_01_backfoot_l,bench_cook_01_backfoot_r,bench_cook_02_foot_back,bench_corrupt_back,deco_summer_chair_01_back,gen_house_01_back,gen_house_02_back,guillotine_01_back,guillotine_01_foot_back,guillotine_01_neck_backbottom,guillotine_01_neck_backtop,inputfieldbackground,poultryfarming_01_back,props_01_stall_01_back,props_06_chair_01_back,props_06_prison_back,props_07_fireplace_01_back,props_07_hanger_01_rope_back,props_07_pictureframe_back,props_07_woodhorse_baseback,props_08_torch_chaos_01_back,props_death_01_candle_01_back,props_labo_01_light_work_01_back,props_raider_01_metal_01_back,props_xmas_02_back_body,props_xmas_02_back_foot,props_xmas_02_body_back,trap_01_back,trap_02_back,trap_fish_01_back,water_back,well_01_base_back,xmas_snowdome_back,bed_02_base,bed_02_foot_l,bed_02_foot_r,bed_03_base,bed_03_curtain_l,bed_03_curtain_r,bed_03_pillar,bed_03_roof,bed_hammock_01,bed_snake_01_front,arch_leaf_01,arch_rose_01,attack_01,attention_01,baby,bag_01,barrel_01,bathtub_01_front,bathtub_01_water,bench_cook_01_base,bench_cook_01_prop_01,bench_cook_01_prop_02,bench_cook_01_prop_03,bench_cook_01_prop_04,bench_cook_02_base,bench_cook_02_base_l,bench_cook_02_base_r,bench_cook_02_floor,bench_cook_02_foot_front,bench_cook_02_prop_01,bench_cook_02_prop_02,bench_cook_02_prop_03,bench_cook_02_prop_04,bench_corrupt_base,bench_corrupt_candle_01,props_01_cook_chocolate_banana_02,props_01_cook_candied_apple_01,props_01_cook_chocolate_banana_01,props_01_cook_shavedice_01,props_01_cook_takoyaki_01,props_xmas_03_cookie_01,props_xmas_03_cookie_02,props_xmas_03_cookie_03,bench_workshop_base,bench_workshop_l_foot,bench_workshop_r_foot,props_hallo_01_bench_01,props_xmas_bench_01,bloodstone_01,deco_flagstone_iron_01,deco_flagstone_mushroom_01,deco_flagstone_plastic_01,props_05_table_stone_01_foot,props_05_table_stone_01_top,props_06_stone_02,props_06_stone_02a,props_06_stone_03,props_06_stone_03a,props_07_hanger_01_stone,props_07_stone_01,props_07_stone_02,props_11_stone_00,props_11_stone_01,props_11_stone_02,props_11_stone_03,wall_stone_01,boat_01_0,boat_01_1,bodyhook_01_base,bodyhook_01_hook_01,bodyhook_01_hook_02,bodyhook_01_top,boss_hunter_01_l_arm,boss_hunter_01_r_leg,boss_hunter_02_body,boss_hunter_02_l_foot,boss_hunter_02_r_arm,bridge_01_acce,bridge_01_base,bridge_01_height,bridge_01_side,bridge_01_width,build_broken,build_broken_02,burrow_bear_01,burrow_rabbit_01,burrow_wolf_01,candle_01,carpet_01,carpet_02,carpet_03,cave,chair_01,checkmark,checkmarkwhite_01,chest_03,chest_04,chitin_01,props_05_table_cloth_01_front,props_05_table_cloth_01_top,cobweb_02,cobweb_03,cobweb_04,coconut_01,collection_failed,crabshell,daruma_machine_01_window,daruma_machine_01_body,daruma_machine_01_door,dead_01,deco_coins_01,deco_jar_cursed_01,deco_jar_skeleton_01,deco_platform_01_base,deco_platform_01_side,deco_skeleton_pin_01,deco_summer_ball_beach_01,deco_summer_ball_beach_01_suika,deco_summer_ball_holder,deco_summer_chair_01_front,deco_summer_parasol_01_bar,deco_summer_parasol_01_colorful,deco_summer_uchiwa_01,props_01_deco_griddle_01,props_01_deco_griddle_02,props_07_deco_chair_zabuton_01,props_death_01_bookshelf_01_deco_l,props_death_01_bookshelf_01_deco_r,direction_01,door_01,dropitem_01,drug_01,drugbag_01,dryrack_01,entrance_01,exp_bg,eye_fade_01,failure_03_oppai,faint_bg,farm_01_fence,farm_01_fence2,farm_01_ground,farm_herbpot_01,farm_herbpot_02,fence_01,fence_02,fence_03,fence_rock_01,fence_rock_01_h,fence_rock_01_w,fence_spike_01,fish,fish_bigeye,fish_cathead,fish_catseye,fish_fish,fish_penguinfish,fish_rainbowsalmon,fish_salmon,fishing_area,fishing_area_bg,fishing_progress,fishing_progress_bg,flag_fasttravel_01,flag_fasttravel_02,flower_mutantlotus,fnickers_01,furnace_01_0,furnace_01_1,fx_grenade_freeze_01,g_l_arm_g,g_l_leg_g,g_r_arm_g,g_r_leg_g,g2_l_arm_g,g2_l_leg_g,g2_r_arm_g,g2_r_leg_g,gen_arm_light,gen_chest_01,gen_hand_01,gen_head_lamp,gen_house_01_front,gen_house_02_front,gen_tinkopot,gengiant_01_oppai,genunder_01_oppai,goldingot,grass_mandora_01,ground,ground_mask_01,guillotine_01_base,guillotine_01_edge,guillotine_01_foot_front,guillotine_01_front,guillotine_01_neck_frontbottom,guillotine_01_neck_fronttop,guillotine_01_top,hammer_king_01,heart_01,home_01,house_gen_01,house_gen_02,house_wall_01,house_wall_02,human_girl_01,human_girl_02,human_girl_03,human_girl_04,human_man_01,hummer_01,info,ironball,ironingot,juice_green,juice_red,juice_yellow,junk_01,keyboard_key_01,knob,l_arm,l_leg,labo_door,life_bg,props_hallo_01_chest_01,light_skeleton_lantern_01,light_skeleton_lantern_02,light_skeleton_lantern_02_light,light_spider_lantern_01,light_spider_lantern_01_light,light_wing_lantern_01,lizard_tail_01,loadscreen_00,logo,m_l_arm_g,m_l_leg_g,m_r_arm_g,m_r_leg_g,m_tinko_g,mapimage,mummy_01_oppai,mush_big_green_01,mutantlotus_01,noimage,oppai,oppai_00,oppai_01,oppai_02,oppai_03,parts_gen_head,parts_gen_l_arm,parts_gen_l_leg,parts_gen_r_arm,parts_gen_r_leg,parts_gen_tinko,parts_gengirl_head,parts_gengirl_l_arm,parts_gengirl_l_leg,parts_gengirl_oppai,parts_gengirl_r_arm,parts_gengirl_r_leg,pause,pedestal_01,pillar_01,plant_cabbage,plant_corn,plant_garlic,plant_onion,plant_pimento,plant_poteto,plant_soil_01,plant_soybean,plant_sprout,plant_tomato,play,pole_head_01,pole_head_02,pole_head_03,pole_head_04,pot_01,pot_healing_01,pot_libido_01,potion_air_01_bg,poultryfarming_01_frame_00,poultryfarming_01_frame_01,poultryfarming_01_frame_02,poultryfarming_01_front,poultryfarming_01_pillar,poultryfarming_01_top,pretty_01_carpet_01,pretty_01_chair_01,pretty_01_cushion_01,pretty_01_doll_01,pretty_01_doll_02,pretty_01_mirror_01,prop_07_signboard_01,prop_07_signboard_02,prop_07_signboard_03,prop_07_signboard_04,prop_07_tomb_01,prop_well_01,props_01_fork_01,props_01_grilledcorn_01,props_01_sake_01,props_01_sake_02,props_01_sake_03,props_01_skeleton_01,props_01_skeleton_02,props_01_stall_01_apron,props_01_stall_01_l_pole,props_01_stall_01_r_pole,props_01_stall_01_roof,props_01_stall_01_stand,props_01_sunflower_01,props_01_sunflower_01_top,props_01_watermelon_01,props_01_watermelon_01_break,props_01_watermelon_01_cut_01,props_01_watermelon_01_cut_02,props_01_yoyo_01_blue,props_01_yoyo_01_brown,props_01_yoyo_01_goldfish,props_01_yoyo_01_heart,props_01_yoyo_01_pink,props_01_yoyo_01_purple,props_01_yoyo_01_rainbow,props_01_yoyo_01_red,props_01_yoyo_01_sky,props_01_yoyo_01_yellow,props_01_yoyo_gom_01,props_04_light_01,props_04_light_02,props_04_light_03,props_04_light_moon_01_base,props_04_light_moon_01_yellow,props_05_dish_01,props_05_dish_02,props_05_table_dark_01_foot,props_05_table_dark_01_top,props_05_table_foot_01,props_05_table_foot_02_l,props_05_table_foot_02_r,props_05_table_foot_03,props_05_table_glass_01_foot,props_05_table_glass_01_front,props_05_table_glass_01_top,props_05_table_heart_01_l_foot,props_05_table_heart_01_r_foot,props_05_table_heart_01_top,props_05_table_sheet_01,props_05_table_sheetfront_01,props_05_table_top_01,props_05_table_top_02,props_05_table_top_03,props_05_table_topfront_01,props_05_table_topfront_02,props_06_basket_01,props_06_chair_01,props_06_chair_01_l_foot,props_06_chair_01_r_foot,props_06_flower_00,props_06_flower_01,props_06_flower_02,props_06_gen_rack_01,props_06_pole_01,props_06_prison_front,props_06_prison_horizon_01,props_06_prison_horizon_02,props_06_prison_vert_01,props_06_prison_vert_02,props_06_rock_00,props_06_rock_01,props_06_rock_02,props_06_rock_03,props_06_rock_04,props_06_rock_05,props_06_rockline_00,props_06_rockline_00_cut,props_06_rockline_00_loop,props_06_rockline_01,props_06_teruteru_01,props_06_teruteru_01_rope,props_06_teruteru_02,props_06_teruteru_02_rope,props_06_teruteru_03,props_06_teruteru_03_rope,props_07_01_stepladder_01,props_07_barrel_01,props_07_beam_01,props_07_beam_02,props_07_beehive_01,props_07_candle_01,props_07_carpet_01,props_07_chain_01,props_07_chain_02,props_07_chain_root,props_07_chair_01,props_07_chair_01_l_foot,props_07_chair_01_r_foot,props_07_chair_02,props_07_chair_02_foot_01,props_07_chair_02_foot_02,props_07_chair_kana_01,props_07_chair_kana_01_foot,props_07_closet_pretty,props_07_closet_pretty_top_a,props_07_closet_pretty_top_b,props_07_closet_pretty_top_c,props_07_closet_spider,props_07_closet_spider_foot_l,props_07_closet_spider_foot_r,props_07_closet_spider_top_l,props_07_closet_spider_top_r,props_07_doll_01,props_07_doll_02,props_07_fireplace_01,props_07_gear_01,props_07_hanger_01_base,props_07_hanger_01_rope_top,props_07_hanger_01_rope_front,props_07_hanger_01_top,props_07_herb_01,props_07_herb_02,props_07_herbpot_01,props_07_herbpot_02,props_07_house_01,props_07_house_01_floor,props_07_house_01_sheet,props_07_house_01_wall,props_07_ladder_01,props_07_mush_wood_01,props_07_pictureframe_01,props_07_pictureframe_02,props_07_pillar_01,props_07_pillar_01_gold,props_07_rope_01,props_07_rope_02,props_07_sign_help,props_07_toilet_01,props_07_toilet_gold_01,props_07_vine_01,props_07_wall_small_01,props_07_woodhorse_bar_horizon,props_07_woodhorse_bar_vert_01,props_07_woodhorse_bar_vert_02,props_07_woodhorse_base,props_07_woodhorse_head,props_08_bush_01,props_08_bush_02,props_08_bush_light_01,props_08_chair_01,props_08_chair_foot_l_01,props_08_chair_foot_r_01,props_08_mothman_cocoon_01,props_08_mothman_cocoon_02,props_08_raft_bottom,props_08_raft_floor,props_08_raft_frame,props_08_raft_paddle,props_08_raft_pole_01,props_08_raft_pole_02,props_08_raft_pole_base,props_08_raft_pole_bottom,props_08_raft_sail,props_08_root_01,props_08_root_02,props_08_root_03,props_08_seed_02a,props_08_seed_02b,props_08_stand_01,props_08_torch_chaos_01,props_08_tree_base,props_08_tree_branch,props_08_tree_root,props_08_vine_01,props_08_vine_02,props_08_vine_03,props_08_vine_04,props_09_roots_01,props_09_roots_02,props_09_roots_03,props_09_roots_04,props_09_roots_05,props_09_roots_base_00,props_09_roots_base_01,props_09_roots_base_core,props_10_orb_core_01,props_10_orb_curse_01,props_10_orb_dead_01,props_10_orb_earth_01,props_10_orb_fighter_01,props_10_orb_life_01,props_10_orb_mad_01,props_10_stand_blue,props_10_stand_green,props_10_stand_purple,props_10_stand_yellow,props_11_bark_00,props_11_bark_01,props_11_bark_02,props_11_bark_bridge_01,props_death_01_book_01_b,props_death_01_book_01_br,props_death_01_book_01_g,props_death_01_book_01_k,props_death_01_book_01_p,props_death_01_book_01_r,props_death_01_book_01_w,props_death_01_book_02_b,props_death_01_book_02_br,props_death_01_book_02_g,props_death_01_book_02_k,props_death_01_book_02_p,props_death_01_book_02_r,props_death_01_book_02_w,props_death_01_book_03_b,props_death_01_book_03_br,props_death_01_book_03_g,props_death_01_book_03_k,props_death_01_book_03_p,props_death_01_book_03_r,props_death_01_book_03_w,props_death_01_bookshelf_01,props_death_01_bookshelf_01_foot_l,props_death_01_bookshelf_01_foot_r,props_death_01_bookshelf_01_top,props_death_01_bookshelf_01_w,props_death_01_candle_01,props_death_01_candle_02_a,props_death_01_candle_02_b,props_death_01_candle_02_c,props_death_01_curtain_01,props_death_01_curtain_01_pole,props_death_01_curtain_01_roof,props_death_01_door_01,props_death_01_fireplace_01,props_death_01_fireplace_01_b,props_death_01_fireplace_01_b_in,props_death_01_fireplace_01_in,props_death_01_goldcross,props_death_01_masonry_01,props_death_01_masonry_01_top,props_death_01_mat_01,props_death_01_walllight_01,props_death_01_walllight_01_base,props_fancy_01_balloon_01,props_fancy_01_balloon_02,props_fancy_01_balloon_03,props_fancy_01_melody_01,props_hallo_01_acce_01,props_hallo_01_acce_02,props_hallo_01_cake_01,props_hallo_01_candle_01,props_hallo_01_candle_02,props_hallo_01_candy_01,props_hallo_01_candy_02,props_hallo_01_chair_01,props_hallo_01_chair_02,props_hallo_01_cobweb_01,props_hallo_01_coffin_01_base,props_hallo_01_coffin_01_top,props_hallo_01_cupcake_01,props_hallo_01_cupcake_02,props_hallo_01_donut_01,props_hallo_01_donut_02,props_hallo_01_donut_03,props_hallo_01_hamburger_01,props_hallo_01_hand_01,props_hallo_01_hand_02,props_hallo_01_lamp_01,props_hallo_01_pumpkin_00,props_hallo_01_pumpkin_01,props_hallo_01_pumpkin_02,props_hallo_01_pumpkin_03,props_hallo_01_pumpkintower_01,props_hallo_01_skewer_01,props_hallo_01_skewer_02,props_hallo_01_skewer_03,props_hallo_01_stand_01,props_hallo_01_stand_02,props_hallo_01_stand_03,props_hallo_01_table_01_foot,props_hallo_01_table_01_top,props_hallo_01_tomb_01,props_hallo_01_treat_01,props_kunai_01,props_labo_01_cushion_01,props_labo_01_cushion_02,props_labo_01_door_01_door,props_labo_01_door_01_frame_l,props_labo_01_door_01_frame_r,props_labo_01_door_01_frame_top,props_labo_01_fan_frame_01,props_labo_01_fan_frame_02,props_labo_01_fan_frame_03,props_labo_01_fan_propeller_01,props_labo_01_frame_01,props_labo_01_frame_02,props_labo_01_hatch_01,props_labo_01_light_01,props_labo_01_light_02,props_labo_01_light_work_01_front,props_labo_01_light_work_01_stand,props_labo_01_locker_01,props_labo_01_locker_02,props_labo_01_monitor_01,props_labo_01_pipe_01,props_labo_01_potion_01,props_labo_01_potion_02,props_labo_01_potion_03,props_labo_01_potion_04,props_labo_01_potionpipe_01,props_labo_01_stand_01,props_labo_02_bag_01,props_labo_02_broken_01,props_labo_02_broken_02,props_labo_02_button_01,props_labo_02_chair_01,props_labo_02_coffee_01,props_labo_02_controler_01,props_labo_02_controler_01_glass,props_labo_02_drain_01,props_labo_02_drip_01,props_labo_02_fence_01,props_labo_02_hatch,props_labo_02_keyboard_01,props_labo_02_light_01,props_labo_02_med_01,props_labo_02_med_02,props_labo_02_paper_01,props_labo_02_parts_01,props_labo_02_parts_02,props_labo_02_parts_03,props_labo_02_parts_04,props_labo_02_parts_05,props_labo_02_parts_06,props_labo_02_pc_01,props_labo_02_plate_01,props_labo_02_potion_01,props_labo_02_scissors_01,props_labo_02_scissors_02,props_labo_02_syringe_01,props_labo_02_syringe_02,props_labo_02_table_01_base,props_labo_02_table_01_foot,props_labo_02_trashbox_01,props_raider_01_drum_01,props_raider_01_metal_01_edge,props_raider_01_tire_01,props_shuriken_01,props_spring_01_ambrella_01_bar,props_spring_01_ambrella_01_top,props_spring_01_bonbori_01,props_spring_01_chair_01,props_spring_01_chair_01_foot,props_spring_01_dango_01,props_spring_01_lantern_01,props_spring_01_lantern_01_paper_01,props_spring_01_lantern_01_paper_02,props_spring_01_lantern_02,props_spring_01_lantern_03,props_spring_01_sakuramochi_01,props_spring_01_sheet_01,props_spring_01_sheet_02,props_xmas_01_light_01,props_xmas_02_body,props_xmas_02_chair,props_xmas_02_front_body,props_xmas_02_front_foot,props_xmas_02_head,props_xmas_02_tail,props_xmas_03_base_01,props_xmas_03_bell_01,props_xmas_03_cake_01,props_xmas_03_cake_02,props_xmas_03_capcake_01,props_xmas_03_capcake_02,props_xmas_03_chair_01,props_xmas_03_chair_01_foot_01,props_xmas_03_chair_01_foot_02,props_xmas_03_chimney_01,props_xmas_03_chimney_snow_01,props_xmas_03_chimney_snow_02,props_xmas_03_dome_01,props_xmas_03_glassball_01,props_xmas_03_glassball_02,props_xmas_03_presentbox_01,props_xmas_03_ribbon_01,props_xmas_03_ring_01,props_xmas_03_snowman_01,props_xmas_03_tree_01,props_xmas_03_yona_01,props_xmas_bell_01,props_xmas_berry_01,props_xmas_box_01,props_xmas_box_02,props_xmas_box_03,props_xmas_box_04,props_xmas_box_05,props_xmas_glassball_01_b,props_xmas_glassball_01_r,props_xmas_glassball_01_w,props_xmas_glassball_01_y,props_xmas_glassball_02_b,props_xmas_glassball_02_r,props_xmas_glassball_02_y,props_xmas_snowman_01,props_xmas_socks_01,props_xmas_star_01,props_xmas_tree_01,props_xmas_wreath_01,ruins_prop_01_firehole_01,ruins_prop_01_ironball_01,ruins_prop_01_lantern_01_gold,ruins_prop_01_lantern_01_iron,ruins_prop_01_pendulum_01_bar,ruins_prop_01_pendulum_01_edge,ruins_prop_01_rail_01,ruins_prop_01_spike_01,ruins_prop_01_switch_01_off,ruins_prop_01_switch_01_on,ruins_prop_02_cage,ruins_prop_02_cagebar,ruins_prop_02_cagechain,ruins_prop_02_candle_01,ruins_prop_02_candle_02,ruins_prop_02_coffin,ruins_prop_02_coffindoor,ruins_prop_02_skeleton_01,ruins_prop_02_skeleton_02,ruins_prop_02_wallflag,punch_01,questicon_01,r_arm,r_leg,raincollector_01_bucket,raincollector_01_tent,raincollector_01_water,reticle_01,ring_01,rock,rocktower_01,root_01,ruins_arch_01,ruins_arch_02,ruins_rubble_01,ruins_rubble_02,ruins_wall_01,ruins_wall_02,ruins_wall_03,sand,shower_01_bar,shower_01_base,shower_01_head,shower_01_leaf,shower_01_platform_01,shower_01_tub,signboard_01,skull_00,skull_01,star_01,state_ui,statue_01,statue_01_tinko,statuebase_01,sweets_chocofondue_01,sweets_pillar_01,sweets_pillar_02,sweets_present_01,sweets_strawberry_01,sweets_tile_01,sweets_wall_01,sweets_wall_02,sweets_wall_03,sweets_whip_01,sweets_whip_02,sweets_whip_03,sweets_whip_04,talk,tent_01_in,tent_01_out,tent_gen_01,tinko,tomb_01,totem,trap_01_close,trap_01_front,trap_01_open,trap_02_base,trap_02_front,trap_02_open,trap_fish_01_front,uimask,uisprite,wall_iron_01,wall_wood_01,water_01,weed_01,weed_03,weed_04,well_01_base,well_01_frame,whip_01,white_circle,white_circle2,woodbox_01");

            AddCategoryDef("Животные", "Animals", "",
                "animal_bat_01,animal_angler_01,animal_ape_01,animal_bear_01,animal_bee_01,animal_bigfoot_01,animal_bird_01,animal_buffalo_01,animal_butterfly_01,animal_chicken_01,animal_chicken_02,animal_chicken_03,animal_crab_01,animal_crayfish_01,animal_crocodile_01,animal_darkgoat_01,animal_death_01,animal_death_02,animal_deer_01,animal_eater_01,animal_eleceel_01,animal_ent_01,animal_firefly_01,animal_frog_01,animal_hallo_jack_01,animal_leech_01,animal_lizard_01,animal_mandora_01,animal_milkcow_01,animal_milkcow_02,animal_mole_01,animal_mouse_01,animal_necksbaby_01,animal_ostrich_01,animal_pig_01,animal_pig_02,animal_rabbit_01,animal_ruck_01,animal_ruck_02,animal_ruck_03,animal_sandcat_01,animal_scorpion_01,animal_sheep_01,animal_skeleton_01,animal_snail_01,animal_snail_02,animal_spider_01,animal_spider_02,animal_turtle_01,animal_werewolf_01,animal_whitetiger_01,animal_wing_01,animal_wolf_01,animal_wooper_01");

            AddCategoryDef("Постройки", "Buildings", "",
                "animalremover_01,fence_animal_01,fence_animal_02,fence_animal_03,att_vib_01,att_vib_02,att_vib_03,att_vib_05,bed_01,bed_02,bed_03,bed_ope_01,bathtub_01,bench_chaos,bench_cook_01,bench_cook_02,bench_drink,bench_dye,bench_hand,bench_halloween,bench_iron,bench_meat,bench_mens,bench_plant,bench_pretty,bench_sand,bench_spider,bench_spring,bench_summer,bench_wing,bench_wood,bench_workshop,bench_xmas,deco_stone_bench_01,block_stairs_01,block_stairs_02,block_stairs_03,block_stairs_04,block_water_01,block_water_blood_01,block_water_corrupt_01,block_water_lake_01,block_water_onsen_01,block_water_swamp_01,deco_flagstone_01,deco_flagstone_cactus_01,deco_flagstone_meat_01,deco_flagstone_mush_01,deco_flagstone_sweets_01,deco_flagstone_wool_01,deco_stone_01,deco_stone_02,factory_stone_01,stonemill_01,table_stone_01,campfire_01,campfire_02,capsule_01,chest_01,chest_02,chest_cactus_01,chest_plant_01,chest_pretty_01,chest_rainbow_01,chest_spider_01,chest_wing_01,chest_wool_01,closet_pretty,closet_spider,table_cloth_01,cocoon_01,cocoon_02,coral_blue_01,coral_green_01,coral_rainbow_01,coral_red_01,curtain_01,curtain_01_b,curtain_01_k,deco_arch_leaf_01,deco_arch_rose_01,deco_arch_ruins_01,deco_bag_01,deco_ball_beach_01,deco_ball_beach_01_suika,deco_barrel_01,deco_basket_01,deco_beam_wood_01,deco_beam_wood_02,deco_boneless_01,deco_bush_01,deco_bush_mush_01,deco_cactus_01,deco_cactus_02,deco_carpet_01,deco_carpet_02,deco_carpet_03,deco_carpet_kana_01,deco_chain_01,deco_chair_01,deco_chair_02,deco_chair_chaos_01,deco_chair_kana_01,deco_chair_zabuton_01,deco_claypot_01,deco_death_arch_01,deco_death_bookshelf_01,deco_death_bookshelf_01_w,deco_death_bookshelf_02,deco_death_carpet_01,deco_death_fence_01,deco_death_fireplace_01,deco_death_fireplace_01_b,deco_death_goldcross_01,deco_death_masonry_01,deco_death_masonry_02,deco_dish_01,deco_dish_02,deco_fancy_balloon_01,deco_fancy_balloon_02,deco_fancy_balloon_03,deco_fancy_melody_01,deco_figure_cassie_01,deco_figure_cassie_02,deco_figure_dancingcat_01,deco_figure_giant_01,deco_figure_giant_02,deco_figure_keigo_01,deco_figure_keigo_02,deco_figure_man_01,deco_figure_man_02,deco_figure_mermaid_01,deco_figure_mermaid_02,deco_figure_merry_01,deco_figure_merry_02,deco_figure_nami_01,deco_figure_nami_02,deco_figure_reika_01,deco_figure_reika_02,deco_figure_sally_01,deco_figure_sally_02,deco_figure_santadeer_01,deco_figure_shino_01,deco_figure_shino_02,deco_figure_takumi_01,deco_figure_takumi_02,deco_figure_trader_01,deco_figure_yona_01,deco_figure_yona_02,deco_fire_01,deco_fire_02,deco_fireplace_gold_01,deco_fishbowl_01,deco_glasscase_01,deco_glasscase_02,deco_glasstank_01,deco_glasstank_02,deco_griddle_01,deco_griddle_02,deco_haniwa_01,deco_labo_chair_01,deco_labo_cushion_01,deco_labo_cushion_02,deco_labo_drip_01,deco_mat_01,deco_mat_snake_01,deco_pictureframe_01,deco_pictureframe_02,deco_pillar_01,deco_pillar_gold_01,deco_pillar_wood_01,deco_pole_bone_01,deco_pool_01,deco_pool_02,deco_pool_sweets_02,deco_pretty_carpet_01,deco_pretty_carpet_02,deco_pretty_chair_01,deco_pretty_cushion_01,deco_pretty_doll_01,deco_pretty_doll_02,deco_pretty_mirror_01,deco_rock_01,deco_rock_02,deco_rope_01,deco_sign_blood_01,deco_signboard_01,deco_signboard_02,deco_signboard_03,deco_signboard_04,deco_spring_ambrella_01,deco_spring_chair_01,deco_spring_sheet_01,deco_spring_sheet_02,deco_stall_01,deco_stand_orb_01_blue,deco_stand_orb_01_green,deco_stand_orb_01_purple,deco_stand_orb_01_yellow,deco_statue_snake_gold_01,deco_summer_beachmats_01,deco_summer_beachmats_02,deco_summer_bubblemachine_01,deco_summer_bubblemachine_01_l,deco_summer_bubblemachine_01_r,deco_summer_chair_01_blue,deco_summer_chair_01_green,deco_summer_chair_01_pink,deco_summer_confetti_01,deco_summer_firework_01_blue,deco_summer_firework_01_green,deco_summer_firework_01_red,deco_summer_parasol_01_blue,deco_summer_parasol_01_color,deco_summer_parasol_01_red,deco_summer_yoyo_01_blue,deco_summer_yoyo_01_brown,deco_summer_yoyo_01_goldfish,deco_summer_yoyo_01_heart,deco_summer_yoyo_01_pink,deco_summer_yoyo_01_purple,deco_summer_yoyo_01_rainbow,deco_summer_yoyo_01_red,deco_summer_yoyo_01_sky,deco_summer_yoyo_01_yellow,deco_sweets_chocofondue_01,deco_sweets_pillar_01,deco_sweets_pillar_02,deco_sweets_pillar_03,deco_sweets_present_01,deco_sweets_strawberry_01,deco_sweets_wall_01,deco_sweets_wall_03,deco_sweets_whip_01,deco_sweets_whip_02,deco_sweets_whip_03,deco_sweets_whip_04,deco_teruteru_01,deco_teruteru_02,deco_teruteru_03,deco_toilet_01,deco_toilet_gold_01,deco_tomb_01,deco_tree_01,deco_tree_02,deco_tree_03,deco_tree_04,deco_tree_corrupt_01,deco_tree_mush_01,deco_tree_palm_01_left,deco_tree_palm_01_right,deco_tree_sakura_01,deco_tree_sakura_02,deco_ukiwa_01_blue,deco_ukiwa_01_donut,deco_ukiwa_01_flamingo,deco_ukiwa_01_pink,deco_ukiwa_01_suika,deco_vine_01,deco_waterflow_01,deco_waterflow_01_blood,deco_waterflow_01_choco,deco_weed_01,deco_weed_03,deco_weed_04,deco_woodbox_01,def_ass_angrypot_01,def_ass_asstotem_01,def_ass_beacon_01,def_ass_beacon_02,def_ass_dummy_01,def_ass_dummy_02,def_ass_dummy_03,def_ass_dummy_04,def_ass_flowertotem_01,def_ass_helltotem_01,def_ass_helltotem_02,def_ass_meathead_01,def_needle_01,def_needle_02,def_needle_03,def_pot_libido_01,def_raider_rotarysaw_01,factory_breed_01,factory_dig_01,factory_harvest_01,factory_wood_01,farm_beehive_01,farm_breed_01,farm_mushroom_01,farm_poultry_01,fence_gen_01,fence_gen_02,flag_01,flag_02,fleshcollector_01,flower_ajisai_blue_01,flower_ajisai_red_01,flower_blue_01,flower_red_01,flower_yellow_01,foodstorage_01,foodstorage_02,foodstorage_spider_01,furnace_01,gen_asswall_00,gen_asswall_01,gen_asswall_02,gen_guillotine_01,gen_hanger_01,gen_hook_01,gen_horse_01,gen_house_01,gen_house_02,gen_milk_01,gen_pole_01,gen_restrain_01,gen_restrain_02,gen_restrain_03,gen_restrain_04,gen_restrain_05,gen_sandbag_01,gen_stand_01,gen_stand_02,gen_stand_03,gen_stand_04,gen_toilet_01,hallo_acce_01,hallo_acce_02,hallo_candle_01,hallo_candle_02,hallo_chair_01,hallo_chair_02,hallo_chest_01,hallo_cobweb_01,hallo_coffin_01,hallo_hand_01,hallo_hand_02,hallo_lamp_01,hallo_pumpkin_01,hallo_pumpkin_02,hallo_pumpkin_03,hallo_pumptower_01,hallo_stand_01,hallo_stand_02,hallo_stand_03,hallo_table_01,hallo_tomb_01,house_01,house_02,house_hatch_01,house_kana_01,light_candle_01,light_candle_02,light_candle_03,light_candle_04,light_candle_death_01,light_candle_death_02,light_candle_death_03,light_candle_prison_01,light_chaos_01,light_chaos_02,light_labo_01,light_labo_01b,light_labo_01b_b,light_labo_01b_g,light_labo_02,light_labo_work_01,light_labo_work_01b,light_lantern_01,light_lantern_02,light_lantern_bone_01,light_lantern_spider_01,light_lantern_wing_01,light_moon_01_custom,light_pool_01,light_pool_02,light_pool_03,light_spring_bonbori_01,light_spring_lantern_01,light_spring_lantern_02,light_spring_lantern_03,machine_build_01,machine_core_01,machine_daruma_01,machine_recycle_01,planter_01,planter_02,platform_01,platform_02,platform_03,platform_04,platform_05,platform_06,raft_01,raincollector_01,sheet_01,sheet_02,sheet_03,shower_01,spike_01,table_01,table_01_02,table_02,table_03,table_04,table_dark_01,table_glass_01,table_heart_01,tent_01,tent_chaos,tent_spider,tent_wing,torch_02,torch_bone_01,torch_chaos_02,trap_01,trap_02,trap_fish_01,wall_cactus_01,wall_glass_01,wall_gold_01,wall_leaf_01,wall_man_01,wall_meat_01,wall_mushroom_01,wall_raider_drum_01,wall_raider_tire_01,wall_rock_01,wall_sand_01,wall_spider_01,wall_sweets_01,wall_wood_02,wall_wool_01,well_02,xmas_bell_01,xmas_berry_01,xmas_box_01,xmas_box_02,xmas_box_03,xmas_box_04,xmas_box_05,xmas_candy_01_l_b,xmas_candy_01_l_g,xmas_candy_01_l_r,xmas_candy_01_r_b,xmas_candy_01_r_g,xmas_candy_01_r_r,xmas_chair_01,xmas_chimney_01,xmas_chimney_snow_01,xmas_chimney_snow_02,xmas_glassball_01_b,xmas_glassball_01_r,xmas_glassball_01_w,xmas_glassball_01_y,xmas_glassball_02_b,xmas_glassball_02_r,xmas_glassball_02_y,xmas_light_01_b,xmas_light_01_g,xmas_light_01_r,xmas_light_stand_01,xmas_ring_01,xmas_ring_02,xmas_santadeer_meat_01,xmas_sleigh_01,xmas_snowdome,xmas_snowdome_01,xmas_snowdome_02,xmas_snowdome_03,xmas_snowdome_icon,xmas_snowman_01,xmas_socks_01,xmas_star_01,xmas_starsmall_01,xmas_tree_01,xmas_wreath_01");

            AddCategoryDef("Расходники", "Supplies", "",
                "def_ass_awake_01,def_ass_awake_10,def_ass_awake_44,drone_01,pocket_01,potion_abortion_01,potion_age_01,potion_age_02,potion_faint_02,potion_grow_01,potion_life_01,potion_life_02,potion_love_01,potion_perfume_01,potion_preg_01,sleeppowder_01,waterbag_00,waterbag_01,waterbag_02,woodbowl_00,woodbowl_01,woodbowl_02,xmas_box_figure_01");

            RebuildCategoryDisplayNames();
        }

        private void AddCategoryDef(string ru, string en, string prefixesStr, string assignedIdsStr)
        {
            var cat = new CategoryDefinition
            {
                nameRU = ru,
                nameEN = en,
                defaultPrefixes = prefixesStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
            };

            if (!string.IsNullOrEmpty(assignedIdsStr))
            {
                string[] ids = assignedIdsStr.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var id in ids)
                {
                    cat.itemIds.Add(id.Trim());
                }
            }

            categoryList.Add(cat);
        }

        private void RebuildCategoryDisplayNames()
        {
            List<string> ru = new List<string> { "Все", "⭐ Избранное" };
            List<string> en = new List<string> { "All", "⭐ Favorites" };

            foreach (var c in categoryList)
            {
                ru.Add(c.nameRU);
                en.Add(c.nameEN);
            }

            categoryDisplayNamesRU = ru.ToArray();
            categoryDisplayNamesEN = en.ToArray();
        }

        private static string GetConfigFilePath()
        {
            return Path.Combine(Paths.ConfigPath, "MadIslandKCM_Categories.txt");
        }

        public static void ExportCategoriesToFile()
        {
            try
            {
                string path = GetConfigFilePath();
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# MadIslandKCM Category Item Mapping");
                sb.AppendLine("# Format: CATEGORY|NameRU|NameEN|DefaultPrefixes|AssignedItemIDs");
                sb.AppendLine("# AssignedItemIDs are comma-separated exact item IDs.");

                foreach (var cat in categoryList)
                {
                    string prefixesStr = string.Join(",", cat.defaultPrefixes);
                    string itemsStr = string.Join(",", cat.itemIds);
                    sb.AppendLine($"CATEGORY|{cat.nameRU}|{cat.nameEN}|{prefixesStr}|{itemsStr}");
                }

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception) { }
        }

        public bool ImportCategoriesFromFile()
        {
            try
            {
                string path = GetConfigFilePath();
                if (!File.Exists(path))
                {
                    ExportCategoriesToFile();
                    return false;
                }

                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                List<CategoryDefinition> newCats = new List<CategoryDefinition>();

                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                    string[] parts = line.Split('|');
                    if (parts.Length >= 3 && parts[0] == "CATEGORY")
                    {
                        var cat = new CategoryDefinition
                        {
                            nameRU = parts[1],
                            nameEN = parts[2]
                        };

                        if (parts.Length >= 4 && !string.IsNullOrEmpty(parts[3]))
                        {
                            cat.defaultPrefixes = parts[3].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
                        }

                        if (parts.Length >= 5 && !string.IsNullOrEmpty(parts[4]))
                        {
                            string[] ids = parts[4].Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var id in ids)
                            {
                                cat.itemIds.Add(id.Trim());
                            }
                        }

                        newCats.Add(cat);
                    }
                }

                if (newCats.Count > 0)
                {
                    categoryList = newCats;
                    RebuildCategoryDisplayNames();
                    ApplySortAndFilter();
                    debugStatus = T($"Imported {categoryList.Count} categories from .txt!", $"Загружены категории из .txt ({categoryList.Count})");
                    return true;
                }
            }
            catch (Exception ex)
            {
                debugStatus = T($"Import error: {ex.Message}", $"Ошибка импорта: {ex.Message}");
            }
            return false;
        }

        public static void AssignItemToCategory(string cleanId, int categoryIndex)
        {
            if (string.IsNullOrEmpty(cleanId) || categoryIndex <= 1 || categoryIndex > categoryList.Count + 1) return;

            int targetCatIdx = categoryIndex - 2;
            if (targetCatIdx < 0 || targetCatIdx >= categoryList.Count) return;

            string idLower = cleanId.ToLower();

            foreach (var cat in categoryList)
            {
                cat.itemIds.Remove(idLower);
            }

            var targetCat = categoryList[targetCatIdx];
            targetCat.itemIds.Add(idLower);

            ExportCategoriesToFile();
        }

        public static int GetItemCategoryIndex(string cleanId)
        {
            if (string.IsNullOrEmpty(cleanId)) return 0;
            string idLower = cleanId.ToLower();

            for (int i = 0; i < categoryList.Count; i++)
            {
                if (categoryList[i].itemIds.Contains(idLower))
                {
                    return i + 2;
                }
            }

            for (int i = 0; i < categoryList.Count; i++)
            {
                var cat = categoryList[i];
                foreach (var pref in cat.defaultPrefixes)
                {
                    if (pref == "*" || (!string.IsNullOrEmpty(pref) && idLower.StartsWith(pref.ToLower())))
                    {
                        return i + 2;
                    }
                }
            }

            return categoryList.Count + 1;
        }
        #endregion

        #region БАЗА ДАННЫХ NPC
        private void InitNpcDatabase()
        {
            npcDatabase.Clear();

            AddNpc("10", "Туземец", "Native Man", "/npc 10", 1);
            AddNpc("11", "Большой туземец", "Big Native", "/npc 11", 1);
            AddNpc("12", "Карликовый туземец", "Dwarf Native", "/npc 12", 1);
            AddNpc("13", "Тюремщик", "Jailer", "/friend 13", 1);
            AddNpc("14", "Туземный мальчик", "Native Boy", "/npc 14", 1);
            AddNpc("18", "Старый туземец", "Old Native", "/npc 18", 1);
            AddNpc("89", "Молодой парень", "Young Guy", "/npc 89", 1);
            AddNpc("91", "Старший брат", "Big Brother", "/npc 91", 1);
            AddNpc("141", "Крупный туземный мальчик", "Large Native Boy", "/npc 141", 1);
            AddNpc("143", "Подземный мальчик", "Underground Boy", "/npc 143", 1);
            AddNpc("180", "Крупный туземец", "Large Native Man", "/npc 180", 1);
            AddNpc("181", "Подземный туземец", "Underground Native", "/npc 181", 1);
            AddNpc("153", "Чаперон", "Chaperone", "/npc 153", 1);

            AddNpc("15", "Туземка", "Native Woman", "/npc 15", 2);
            AddNpc("16", "Туземная девочка", "Native Girl", "/npc 16", 2);
            AddNpc("17", "Крупная туземка", "Large Native Woman", "/npc 17", 2);
            AddNpc("19", "Пожилая туземка", "Elderly Native Woman", "/npc 19", 2);
            AddNpc("90", "Старшая сестра", "Big Sister", "/npc 90", 2);
            AddNpc("140", "Крупная туземная девочка", "Large Native Girl", "/npc 140", 2);
            AddNpc("142", "Подземная девочка", "Underground Girl", "/npc 142", 2);
            AddNpc("149", "Ребенок Гиганта", "Giant's Child", "/npc 149", 2);

            AddNpc("slave_giant_01", "Связанный гигант", "Tied Giant", "/get slave_giant_01 1", 3);
            AddNpc("slave_shino_01", "Связанная Шино", "Tied Shino", "/get slave_sally_01 1", 3);
            AddNpc("slave_sally_01", "Связанная Салли", "Tied Sally", "/get slave_sally_01 1", 3);
            AddNpc("0", "Йона", "Yona", "/friend 0", 3);
            AddNpc("1", "Парень", "Guy", "/friend 1", 3);
            AddNpc("2", "Риона", "Riona", "/friend 2", 3);
            AddNpc("3", "Литтл и Ники", "Little & Niki", "/friend 3", 3);
            AddNpc("5", "Рейка", "Reika", "/friend 5", 3);
            AddNpc("6", "Нами", "Nami", "/friend 6", 3);
            AddNpc("7", "Такуми", "Takumi", "/npc 7", 3);
            AddNpc("8", "Кейго", "Keigo", "/friend 8", 3);
            AddNpc("71", "Русалка", "Mermaid", "/npc 71", 3);
            AddNpc("111", "Наначи (торговец)", "Nanachi (Trader)", "/npc 111", 3);
            AddNpc("113", "Кэсси", "Cassie", "/npc 113", 3);
            AddNpc("114", "Шино", "Shino", "/npc 114", 3);
            AddNpc("116", "Мерри", "Merry", "/npc 116", 3);
            AddNpc("117", "Оленя", "Deer Girl", "/npc 117", 3);
            AddNpc("118", "Мира", "Mira", "/npc 118", 3);

            AddNpc("7_boss", "Такуми (Босс)", "Takumi (Boss)", "/npc 7", 4);
            AddNpc("100", "Дяденька", "Uncle", "/npc 100", 4);
            AddNpc("101", "Таратект", "Taratect", "/npc 101", 4);
            AddNpc("102", "Доктор Эд", "Dr. Ed", "/npc 102", 4);
            AddNpc("103", "Плантон", "Planton", "/npc 103", 4);
            AddNpc("104", "Вождь туземцев", "Native Chief", "/npc 104", 4);
            AddNpc("105", "Коса", "Scythe", "/npc 105", 4);
            AddNpc("106", "Кэсси (Босс)", "Cassie (Boss)", "/npc 106", 4);
            AddNpc("107", "Некс", "Nex", "/npc 107", 4);
            AddNpc("108", "Бандана", "Bandana", "/npc 108", 4);
            AddNpc("109", "Шляпа", "Hat", "/npc 109", 4);
            AddNpc("110", "Гигант", "Giant", "/npc 110", 4);
            AddNpc("112", "Далман", "Dalman", "/npc 112", 4);
            AddNpc("115", "Салли (Босс)", "Sally (Boss)", "/npc 115", 4);
            AddNpc("120", "Король Энтов", "Ent King", "/npc 120", 4);
            AddNpc("121", "Королева Энтов", "Ent Queen", "/npc 121", 4);
            AddNpc("157", "Горела", "Gorela", "/npc 157", 4);
            AddNpc("158", "Горола", "Gorola", "/npc 158", 4);

            AddNpc("27", "Мандрагора", "Mandragora", "/npc 27", 5);
            AddNpc("36", "Страус", "Ostrich", "/npc 36", 5);
            AddNpc("50", "Кролик", "Rabbit", "/npc 50", 5);
            AddNpc("51", "Олень", "Deer", "/npc 51", 5);
            AddNpc("52", "Птица", "Bird", "/npc 52", 5);
            AddNpc("53", "Петух", "Rooster", "/npc 53", 5);
            AddNpc("54", "Лягушка", "Frog", "/npc 54", 5);
            AddNpc("55", "Свинья", "Pig", "/npc 55", 5);
            AddNpc("64", "Свинина Дюрок", "Duroc Pig", "/npc 64", 5);
            AddNpc("56", "Бабочка", "Butterfly", "/npc 56", 5);
            AddNpc("57", "Краб", "Crab", "/npc 57", 5);
            AddNpc("58", "Курица", "Chicken", "/npc 58", 5);
            AddNpc("59", "Курица?", "Chicken?", "/npc 59", 5);
            AddNpc("60", "Мышь", "Mouse", "/npc 60", 5);
            AddNpc("61", "Овца", "Sheep", "/npc 61", 5);
            AddNpc("62", "Корова", "Cow", "/npc 62", 5);
            AddNpc("63", "Цветочная корова", "Flower Cow", "/npc 63", 5);
            AddNpc("66", "Кенгуру", "Kangaroo", "/npc 66", 5);
            AddNpc("79", "Черепаха", "Turtle", "/npc 79", 5);
            AddNpc("92", "Песчаный кот", "Sand Cat", "/npc 92", 5);
            AddNpc("94", "Улитка и синяя гортензия", "Snail & Blue Hydrangea", "/npc 94", 5);
            AddNpc("95", "Кровавая улитка и красная гортензия", "Bloody Snail", "/npc 95", 5);
            AddNpc("97", "Кровавый кенгуру", "Bloody Kangaroo", "/npc 97", 5);
            AddNpc("98", "Кенгуру-горничная", "Maid Kangaroo", "/npc 98", 5);
            AddNpc("99", "Филориал", "Filorial", "/npc 99", 5);

            AddNpc("20", "Медведь", "Bear", "/npc 20", 6);
            AddNpc("22", "Летучая мышь", "Bat", "/npc 22", 6);
            AddNpc("23", "Крокодил", "Crocodile", "/npc 23", 6);
            AddNpc("24", "Волк", "Wolf", "/npc 24", 6);
            AddNpc("38", "Птенец Некса", "Nex Chick", "/npc 38", 6);
            AddNpc("70", "Акула", "Shark", "/npc 70", 6);
            AddNpc("78", "Белый тигр", "White Tiger", "/npc 78", 6);
            AddNpc("152", "Огромный лобстер", "Huge Lobster", "/npc 152", 6);

            AddNpc("21", "Пещерный паук", "Cave Spider", "/npc 21", 7);
            AddNpc("25", "Снежный человек", "Yeti", "/npc 25", 7);
            AddNpc("26", "Непентес", "Nepenthes", "/npc 26", 7);
            AddNpc("28", "Пчела", "Bee", "/npc 28", 7);
            AddNpc("29", "Большой Аксолотль", "Big Axolotl", "/npc 29", 7);
            AddNpc("30", "Лесной паук", "Forest Spider", "/npc 30", 7);
            AddNpc("31", "Англер", "Angler", "/npc 31", 7);
            AddNpc("32", "Скорпион", "Scorpion", "/npc 32", 7);
            AddNpc("33", "Пиявка", "Leech", "/npc 33", 7);
            AddNpc("34", "Большой электрический угорь", "Electric Eel", "/npc 34", 7);
            AddNpc("35", "Оборотень", "Werewolf", "/npc 35", 7);
            AddNpc("37", "Грабоид", "Graboid", "/npc 37", 7);
            AddNpc("65", "Леший", "Leshy", "/npc 65", 7);
            AddNpc("67", "Темное дерево", "Dark Tree", "/npc 67", 7);
            AddNpc("68", "Энт", "Ent", "/npc 68", 7);
            AddNpc("69", "Бафомет", "Baphomet", "/npc 69", 7);
            AddNpc("93", "Балфант", "Balfant", "/npc 93", 7);
            AddNpc("122", "Энт воин", "Ent Warrior", "/npc 122", 7);
            AddNpc("151", "Яйцо Некса", "Nex Egg", "/npc 151", 7);
            AddNpc("160", "Джек", "Jack", "/npc 160", 7);
            AddNpc("161", "Призрак Хэллоуина", "Halloween Ghost", "/npc 161", 7);
            AddNpc("173", "Мосман", "Mothman", "/npc 173", 7);
            AddNpc("174", "Жнец", "Reaper", "/npc 174", 7);
            AddNpc("175", "Смерть", "Death", "/npc 175", 7);

            AddNpc("96", "Светлячок", "Firefly", "/npc 96", 8);
            AddNpc("88", "Рандомный андроид", "Random Android", "/npc 88", 8);
            AddNpc("150", "Улей", "Beehive", "/npc 150", 8);
            AddNpc("170", "Синий подарок", "Blue Gift", "/npc 170", 8);
            AddNpc("171", "Зеленый подарок", "Green Gift", "/npc 171", 8);
            AddNpc("172", "Красный подарок", "Red Gift", "/npc 172", 8);
            AddNpc("39", "Скелет", "Skeleton", "/npc 39", 8);
            AddNpc("40", "Призрак", "Ghost", "/npc 40", 8);
            AddNpc("41", "Скарабеи", "Scarab", "/npc 41", 8);
            AddNpc("42", "Мумия (женщина)", "Female Mummy", "/npc 42", 8);
            AddNpc("43", "Мумия (мужчина)", "Male Mummy", "/npc 43", 8);
            AddNpc("80", "Гоблин", "Goblin", "/npc 80", 8);
            AddNpc("81", "Призрак (спектр)", "Spectre", "/npc 81", 8);
            AddNpc("82", "Собака-хранитель", "Guardian Dog", "/npc 82", 8);
            AddNpc("83", "Голем", "Golem", "/npc 83", 8);
            AddNpc("84", "Хранитель", "Guardian", "/npc 84", 8);
            AddNpc("44", "Подземная женщина", "Underground Woman", "/npc 44", 8);
            AddNpc("45", "Подземная старуха", "Underground Old Woman", "/npc 45", 8);
            AddNpc("46", "Подземный человек", "Underground Man", "/npc 46", 8);
            AddNpc("85", "Прототип типа A", "Prototype A", "/npc 85", 8);
            AddNpc("86", "Прототип типа B", "Prototype B", "/npc 86", 8);
            AddNpc("87", "Прототип типа C", "Prototype C", "/npc 87", 8);
            AddNpc("155", "Крот хаоса", "Chaos Mole", "/npc 155", 8);
            AddNpc("156", "Ящерица хаоса", "Chaos Lizard", "/npc 156", 8);
            AddNpc("159", "Корень энта хаоса", "Chaos Ent Root", "/npc 159", 8);

            FilterNpcDatabase();
        }

        private void AddNpc(string key, string ru, string en, string cmd, int cat)
        {
            npcDatabase.Add(new NpcSpawnData
            {
                iconKey = key,
                nameRU = ru,
                nameEN = en,
                command = cmd,
                categoryIndex = cat
            });
        }

        private void CacheNpcSprites()
        {
            foreach (var npc in npcDatabase)
            {
                npc.cachedSprite = FindSpriteForNpc(npc.iconKey);
            }
        }

        private void FilterNpcDatabase()
        {
            currentNpcPage = 0;
            IEnumerable<NpcSpawnData> query = npcDatabase;

            if (selectedNpcCategory > 0)
            {
                query = query.Where(n => n.categoryIndex == selectedNpcCategory);
            }

            if (!string.IsNullOrEmpty(searchNpcText))
            {
                string searchLower = searchNpcText.ToLower();
                query = query.Where(n => n.nameRU.ToLower().Contains(searchLower) || n.nameEN.ToLower().Contains(searchLower) || n.iconKey.ToLower().Contains(searchLower));
            }

            filteredNpcs = query.ToList();
        }

        private Sprite FindSpriteForNpc(string key)
        {
            foreach (var item in itemsList)
            {
                if (item.cleanId.Equals($"npc_{key}", StringComparison.OrdinalIgnoreCase) ||
                    item.cleanId.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                    item.cleanId.Equals($"friend_{key}", StringComparison.OrdinalIgnoreCase))
                {
                    return item.iconSprite;
                }
            }
            return null;
        }
        #endregion

        #region СКАНИРОВАНИЕ И ФИЛЬТРАЦИЯ ПРЕДМЕТОВ
        private void ScanGameItems()
        {
            itemsList.Clear();
            debugStatus = T("Scanning items...", "Идет сканирование...");

            Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();

            foreach (var spr in allSprites)
            {
                if (spr == null || string.IsNullOrEmpty(spr.name)) continue;

                string rawName = spr.name;

                if (rawName.StartsWith("bg_") || rawName.StartsWith("ui_") || rawName.StartsWith("btn_") ||
                    rawName.StartsWith("font_") || rawName.StartsWith("tile_") || rawName.StartsWith("map_") ||
                    rawName.StartsWith("mask_") || rawName.StartsWith("ef_") || rawName.StartsWith("fx_"))
                {
                    continue;
                }

                string clean = rawName;
                if (clean.StartsWith("icon_"))
                {
                    clean = clean.Substring(5);
                }

                if (clean.Length > 1)
                {
                    AddItemToList(rawName, clean, spr);
                }
            }

            ApplySortAndFilter();
            CacheNpcSprites();
            isLoaded = true;
            debugStatus = T($"Found items: {itemsList.Count}", $"Найдено предметов: {itemsList.Count}");
            Logger.LogInfo(debugStatus);
        }

        private void AddItemToList(string raw, string clean, Sprite sprite)
        {
            if (itemsList.Any(x => x.cleanId == clean)) return;

            itemsList.Add(new ItemInfo
            {
                rawId = raw,
                cleanId = clean,
                iconSprite = sprite
            });
        }

        private bool BelongsToCategory(ItemInfo item, int catIndex)
        {
            if (catIndex == 0) return true;

            if (catIndex == 1)
            {
                return favoriteItemIds.Contains(item.cleanId);
            }

            int targetIdx = catIndex - 2;
            if (targetIdx < 0 || targetIdx >= categoryList.Count) return true;

            var targetCat = categoryList[targetIdx];
            string idLower = item.cleanId.ToLower();

            if (targetCat.itemIds.Contains(idLower))
                return true;

            foreach (var cat in categoryList)
            {
                if (cat != targetCat && cat.itemIds.Contains(idLower))
                    return false;
            }

            if (targetCat.defaultPrefixes.Contains("*"))
            {
                foreach (var otherCat in categoryList)
                {
                    if (otherCat == targetCat) continue;
                    foreach (var pref in otherCat.defaultPrefixes)
                    {
                        if (pref != "*" && !string.IsNullOrEmpty(pref) && idLower.StartsWith(pref.ToLower()))
                            return false;
                    }
                }
                return true;
            }

            foreach (var pref in targetCat.defaultPrefixes)
            {
                if (!string.IsNullOrEmpty(pref) && idLower.StartsWith(pref.ToLower()))
                    return true;
            }

            return false;
        }

        private void ApplySortAndFilter()
        {
            currentPage = 0;

            IEnumerable<ItemInfo> query = itemsList;

            query = query.Where(i => BelongsToCategory(i, selectedCategory));

            if (!string.IsNullOrEmpty(searchText))
            {
                string searchLower = searchText.ToLower();
                query = query.Where(i => i.cleanId.ToLower().Contains(searchLower));
            }

            if (currentSortType == 0) query = query.OrderBy(i => i.cleanId);
            else if (currentSortType == 1) query = query.OrderByDescending(i => i.cleanId);

            filteredItems = query.ToList();
        }
        #endregion

        #region ИНТЕРФЕЙС MENU (OnGUI)
        void OnGUI()
        {
            if (!showUI) return;

            windowRect = GUI.Window(888111, windowRect, DrawWindow, $"MadIslandKCM v1.2 [{toggleKey}]");

            Vector2 mousePos = new Vector2(UnityEngine.Input.mousePosition.x, Screen.height - UnityEngine.Input.mousePosition.y);
            if (windowRect.Contains(mousePos) || isExecutingCommandSequence)
            {
                Event e = Event.current;
                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel)
                {
                    e.Use();
                }
            }
        }

        void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(activeTab == 0, T("🎒 Items", "🎒 Предметы"), "Button", GUILayout.Height(35))) activeTab = 0;
            if (GUILayout.Toggle(activeTab == 1, T("👤 Character", "👤 Персонаж"), "Button", GUILayout.Height(35))) activeTab = 1;
            if (GUILayout.Toggle(activeTab == 2, T("👥 NPC", "👥 NPC"), "Button", GUILayout.Height(35))) activeTab = 2;
            if (GUILayout.Toggle(activeTab == 3, T("🌍 World", "🌍 Мир"), "Button", GUILayout.Height(35))) activeTab = 3;
            if (GUILayout.Toggle(activeTab == 4, T("⚙️ Settings", "⚙️ Настройки"), "Button", GUILayout.Height(35))) activeTab = 4;
            if (GUILayout.Toggle(activeTab == 5, T("📜 Credits", "📜 Кредиты"), "Button", GUILayout.Height(35))) activeTab = 5;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            switch (activeTab)
            {
                case 0: DrawItemsTab(); break;
                case 1: DrawCharacterTab(); break;
                case 2: DrawNpcTab(); break;
                case 3: DrawWorldTab(); break;
                case 4: DrawSettingsTab(); break;
                case 5: DrawCreditsTab(); break;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(T($"Close Menu ({toggleKey})", $"Закрыть меню ({toggleKey})"), GUILayout.Height(30)))
            {
                showUI = false;
                IsUIOpen = false;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawItemsTab()
        {
            GUILayout.Label($"<b>{T("Status:", "Статус:")}</b> {debugStatus}");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(showCategoryEditor ? T("✖️ Close Editor", "✖️ Закрыть редактор") : T("⚙️ Category Editor (.txt)", "⚙️ Редактор категорий (.txt)"), GUILayout.Height(28)))
            {
                showCategoryEditor = !showCategoryEditor;
            }
            GUILayout.EndHorizontal();

            if (showCategoryEditor)
            {
                DrawCategoryEditorPanel();
            }

            GUILayout.Space(5);

            string[] cats = IsRussian ? categoryDisplayNamesRU : categoryDisplayNamesEN;

            int itemsPerRow = 4;
            for (int i = 0; i < cats.Length; i += itemsPerRow)
            {
                GUILayout.BeginHorizontal();
                for (int j = i; j < i + itemsPerRow && j < cats.Length; j++)
                {
                    DrawItemCategoryButton(j, cats[j]);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Search:", "Поиск:"), GUILayout.Width(50));
            string newSearch = GUILayout.TextField(searchText);
            if (newSearch != searchText)
            {
                searchText = newSearch;
                ApplySortAndFilter();
            }
            if (GUILayout.Button(T("Refresh", "Обновить"), GUILayout.Width(80)))
            {
                ScanGameItems();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal("box");
            GUILayout.Label(T("Qty:", "Кол-во:"), GUILayout.Width(50));
            if (GUILayout.Button("x1", GUILayout.Width(40))) { giveAmount = 1; customAmountText = "1"; }
            if (GUILayout.Button("x10", GUILayout.Width(40))) { giveAmount = 10; customAmountText = "10"; }
            if (GUILayout.Button("x100", GUILayout.Width(45))) { giveAmount = 100; customAmountText = "100"; }
            if (GUILayout.Button("x999", GUILayout.Width(45))) { giveAmount = 999; customAmountText = "999"; }

            string newAmountText = GUILayout.TextField(customAmountText, GUILayout.Width(60));
            if (newAmountText != customAmountText)
            {
                customAmountText = newAmountText;
                int.TryParse(customAmountText, out giveAmount);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(T("Sort:", "Сорт:"));
            if (GUILayout.Button(currentSortType == 0 ? "A-Z" : "Z-A", GUILayout.Width(45)))
            {
                currentSortType = currentSortType == 0 ? 1 : 0;
                ApplySortAndFilter();
            }
            GUILayout.EndHorizontal();

            int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)filteredItems.Count / itemsPerPage));
            if (currentPage >= totalPages) currentPage = totalPages - 1;
            if (currentPage < 0) currentPage = 0;

            DrawPaginationBar(totalPages);

            scrollPositionItems = GUILayout.BeginScrollView(scrollPositionItems, GUILayout.Height(230));

            int startIndex = currentPage * itemsPerPage;
            int endIndex = Mathf.Min(startIndex + itemsPerPage, filteredItems.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = filteredItems[i];
                GUILayout.BeginHorizontal("box");

                DrawSpriteFast(item.iconSprite, 44, 44);

                GUILayout.BeginVertical();
                GUILayout.Label($"<b>{item.cleanId}</b>");
                GUILayout.Label($"<color=yellow>ID: {item.cleanId}</color>");
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                bool isFav = favoriteItemIds.Contains(item.cleanId);
                string favText = isFav ? "<color=yellow>★</color>" : "☆";
                if (GUILayout.Button(favText, GUILayout.Width(30), GUILayout.Height(36)))
                {
                    ToggleFavorite(item.cleanId);
                }

                int currentCatIdx = GetItemCategoryIndex(item.cleanId);
                string catName = IsRussian ? categoryDisplayNamesRU[currentCatIdx] : categoryDisplayNamesEN[currentCatIdx];

                if (activeCategoryPickerItemId == item.cleanId)
                {
                    GUILayout.BeginVertical("box");
                    GUILayout.Label(T("Assign Category:", "Выбор категории:"));

                    int btnsPerRow = 3;
                    for (int c = 0; c < categoryList.Count; c += btnsPerRow)
                    {
                        GUILayout.BeginHorizontal();
                        for (int k = c; k < c + btnsPerRow && k < categoryList.Count; k++)
                        {
                            string label = IsRussian ? categoryList[k].nameRU : categoryList[k].nameEN;
                            if (GUILayout.Button(label, GUILayout.Height(24)))
                            {
                                AssignItemToCategory(item.cleanId, k + 2);
                                activeCategoryPickerItemId = "";
                                ApplySortAndFilter();
                                debugStatus = T($"Assigned '{item.cleanId}' to {label}", $"Предмет '{item.cleanId}' привязан к {label}");
                            }
                        }
                        GUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button("✖️ " + T("Cancel", "Отмена"), GUILayout.Height(22)))
                    {
                        activeCategoryPickerItemId = "";
                    }
                    GUILayout.EndVertical();
                }
                else
                {
                    if (GUILayout.Button($"📁 {catName}", GUILayout.Width(130), GUILayout.Height(36)))
                    {
                        activeCategoryPickerItemId = item.cleanId;
                    }
                }

                if (GUILayout.Button(T($"Give ({giveAmount})", $"Выдать ({giveAmount})"), GUILayout.Width(100), GUILayout.Height(36)))
                {
                    GiveItemToPlayer(item.cleanId, giveAmount);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            DrawPaginationBar(totalPages);
        }

        private void DrawCategoryEditorPanel()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{T("🛠️ Category Editor (.txt)", "⚙️ Редактор категорий и Импорт/Экспорт .txt")}</b>");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("💾 Export to .txt", "💾 Экспорт в .txt"), GUILayout.Height(26)))
            {
                ExportCategoriesToFile();
                debugStatus = T("Exported categories and assigned item IDs to .txt!", "Категории и список предметов экспортированы в .txt!");
            }
            if (GUILayout.Button(T("📂 Import from .txt", "📂 Импорт из .txt"), GUILayout.Height(26)))
            {
                ImportCategoriesFromFile();
            }
            if (GUILayout.Button(T("🔄 Reset to Default", "🔄 Сбросить"), GUILayout.Height(26)))
            {
                InitDefaultCategories();
                ExportCategoriesToFile();
                ApplySortAndFilter();
                debugStatus = T("Categories reset to default!", "Категории сброшены к дефолту!");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.Label(T("<color=grey>Path: BepInEx/config/MadIslandKCM_Categories.txt</color>",
                             "<color=grey>Файл: BepInEx/config/MadIslandKCM_Categories.txt</color>"));

            scrollPositionCategoryEditor = GUILayout.BeginScrollView(scrollPositionCategoryEditor, GUILayout.Height(130));

            for (int i = 0; i < categoryList.Count; i++)
            {
                var cat = categoryList[i];
                GUILayout.BeginHorizontal("box");

                cat.nameRU = GUILayout.TextField(cat.nameRU, GUILayout.Width(110));
                cat.nameEN = GUILayout.TextField(cat.nameEN, GUILayout.Width(110));

                GUILayout.Label(T($"Items: {cat.itemIds.Count}", $"Предметов: {cat.itemIds.Count}"), GUILayout.Width(110));

                if (GUILayout.Button("❌", GUILayout.Width(30)))
                {
                    categoryList.RemoveAt(i);
                    RebuildCategoryDisplayNames();
                    ApplySortAndFilter();
                    break;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            newCatNameRU = GUILayout.TextField(newCatNameRU, GUILayout.Width(130));
            newCatNameEN = GUILayout.TextField(newCatNameEN, GUILayout.Width(130));
            if (GUILayout.Button(T("➕ Add New Category", "➕ Добавить категорию"), GUILayout.Height(24)))
            {
                categoryList.Add(new CategoryDefinition
                {
                    nameRU = newCatNameRU,
                    nameEN = newCatNameEN
                });
                RebuildCategoryDisplayNames();
                ExportCategoriesToFile();
                ApplySortAndFilter();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawItemCategoryButton(int index, string label)
        {
            if (GUILayout.Toggle(selectedCategory == index, label, "Button", GUILayout.Height(26)))
            {
                if (selectedCategory != index)
                {
                    selectedCategory = index;
                    ApplySortAndFilter();
                }
            }
        }

        private void DrawPaginationBar(int totalPages)
        {
            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button(T("< Prev", "< Назад"), GUILayout.Width(80)) && currentPage > 0)
            {
                currentPage--;
                scrollPositionItems = Vector2.zero;
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label(T($"Page <b>{currentPage + 1}</b> / <b>{totalPages}</b> (Total: {filteredItems.Count})", $"Стр. <b>{currentPage + 1}</b> / <b>{totalPages}</b> (Всего: {filteredItems.Count})"));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Next >", "Вперед >"), GUILayout.Width(80)) && currentPage < totalPages - 1)
            {
                currentPage++;
                scrollPositionItems = Vector2.zero;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCharacterTab()
        {
            GUILayout.BeginVertical("box");

            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{T("❤️ Direct Health & God Mode (In-Memory)", "❤️ Здоровье и Неуязвимость (Напрямую в памяти)")}</b>");

            float liveHp = DirectGameDataModifier.GetPlayerHP(false);
            float liveMaxHp = DirectGameDataModifier.GetPlayerHP(true);
            GUILayout.Label(T($"In-Memory HP State: <b>{liveHp:F0}</b> / <b>{liveMaxHp:F0}</b>",
                              $"Состояние ХП в памяти: <b>{liveHp:F0}</b> / <b>{liveMaxHp:F0}</b>"));

            GUILayout.BeginHorizontal();
            string godStatusText = isGodMode ?
                $"<color=lime><b>[{T("ENABLED", "ВКЛЮЧЕНО")}]</b></color>" :
                $"<color=red><b>[{T("DISABLED", "ВЫКЛЮЧЕНО")}]</b></color>";

            if (GUILayout.Button(T($"God Mode (Invincibility): {godStatusText}", $"Неуязвимость (God Mode): {godStatusText}"), GUILayout.Height(30)))
            {
                isGodMode = !isGodMode;
                if (isGodMode)
                {
                    DirectGameDataModifier.SetPlayerHPDirect(999999f, DirectGameDataModifier.HPSetType.CurrentOnly, true);
                }
                debugStatus = T($"God Mode: {(isGodMode ? "ON" : "OFF")}", $"Режим неуязвимости: {(isGodMode ? "ВКЛ" : "ВЫКЛ")}");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Target HP Value:", "Число ХП:"), GUILayout.Width(100));
            hpInput = GUILayout.TextField(hpInput, GUILayout.Width(100));

            if (GUILayout.Button(T("🔥 Set ALL (Current + Max HP)", "🔥 Установить Всё (Текущее + Макс ХП)"), GUILayout.Height(26)))
            {
                if (float.TryParse(hpInput, out float val))
                {
                    bool success = DirectGameDataModifier.SetPlayerHPDirect(val, DirectGameDataModifier.HPSetType.BothCurrentAndMax, true);
                    debugStatus = success ?
                        T($"HP and Max HP set to: {val}", $"Установлено ХП и Макс ХП: {val}") :
                        T($"Failed to find player HP in memory", $"Не удалось найти поле ХП в памяти");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(2);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("❤️ Current HP Only", "❤️ Только Текущее ХП"), GUILayout.Height(26)))
            {
                if (float.TryParse(hpInput, out float curVal))
                {
                    bool success = DirectGameDataModifier.SetPlayerHPDirect(curVal, DirectGameDataModifier.HPSetType.CurrentOnly, true);
                    debugStatus = success ?
                        T($"Current HP set to: {curVal}", $"Текущее ХП изменено на: {curVal}") :
                        T($"Failed to set Current HP", $"Не удалось изменить текущее ХП");
                }
            }

            if (GUILayout.Button(T("🛡️ Max HP Only", "🛡️ Только Макс. ХП"), GUILayout.Height(26)))
            {
                if (float.TryParse(hpInput, out float maxVal))
                {
                    bool success = DirectGameDataModifier.SetPlayerHPDirect(maxVal, DirectGameDataModifier.HPSetType.MaxOnly, true);
                    debugStatus = success ?
                        T($"Max HP set to: {maxVal}", $"Макс. ХП изменено на: {maxVal}") :
                        T($"Failed to set Max HP", $"Не удалось изменить макс. ХП");
                }
            }

            if (GUILayout.Button(T("💊 Full Heal (100%)", "💊 Лечение (100%)"), GUILayout.Height(26)))
            {
                float targetHeal = liveMaxHp > 0 ? liveMaxHp : 999999f;
                bool success = DirectGameDataModifier.SetPlayerHPDirect(targetHeal, DirectGameDataModifier.HPSetType.CurrentOnly, true);
                debugStatus = success ?
                    T("Player fully healed!", "Здоровье игрока восполнено!") :
                    T("Failed to heal player", "Не удалось восполнить здоровье");
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{T("🍖 Survival & Camera Control", "🍖 Выживание и Управление камерой")}</b>");

            if (GUILayout.Button(T("🍖💧 Restore Hunger & Thirst (100%)", "🍖💧 Восстановить голод и жажду (100%)"), GUILayout.Height(30)))
            {
                DirectGameDataModifier.RefillHungerAndThirst();
                debugStatus = T("Hunger & Thirst fully restored!", "Голод и жажда полностью восполнены!");
            }

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            string camStatusText = CameraController.isCameraControlEnabled ?
                $"<color=lime><b>[{T("ENABLED", "ВКЛЮЧЕНО")}]</b></color>" :
                $"<color=red><b>[{T("DISABLED", "ВЫКЛЮЧЕНО")}]</b></color>";

            if (GUILayout.Button(T($"3rd Person Camera Mode: {camStatusText}", $"Режим управления камерой (3rd Person): {camStatusText}"), GUILayout.Height(30)))
            {
                CameraController.isCameraControlEnabled = !CameraController.isCameraControlEnabled;
                debugStatus = T($"Camera Control: {(CameraController.isCameraControlEnabled ? "ON" : "OFF")}",
                                $"Режим управления камерой: {(CameraController.isCameraControlEnabled ? "ВКЛ" : "ВЫКЛ")}");
            }

            if (GUILayout.Button(T("🔄 Reset Camera", "🔄 Сбросить камеру"), GUILayout.Width(150), GUILayout.Height(30)))
            {
                CameraController.ResetCamera();
                debugStatus = T("Camera reset to default!", "Камера сброшена в стандартное значение!");
            }
            GUILayout.EndHorizontal();

            if (CameraController.isCameraControlEnabled)
            {
                GUILayout.Label(T("<color=grey>Controls: Hold <b>T</b> + move Mouse to orbit camera around character.</color>",
                                 "<color=grey>Управление: Зажмите <b>T</b> + двигайте мышь, чтобы вращать камеру вокруг персонажа.</color>"));
            }

            GUILayout.Space(5);

            isNoclip = GUILayout.Toggle(isNoclip, T("🦅 Fly / Noclip Mode", "🦅 Полет / Noclip"), "Button", GUILayout.Height(26));
            if (isNoclip)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(T($"Fly Speed: {flySpeed:F0}x", $"Скорость полета: {flySpeed:F0}x"), GUILayout.Width(150));
                flySpeed = GUILayout.HorizontalSlider(flySpeed, 2f, 40f);
                GUILayout.EndHorizontal();
                GUILayout.Label(T("<color=grey>Controls: WASD - Move, Space - Go Up, LeftCtrl / C - Go Down</color>",
                                 "<color=grey>Управление: WASD - Движение, Space - Вверх, LeftCtrl / C - Вниз</color>"));
            }
            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.Label($"<b>{T("Character Stats & Progression", "Характеристики персонажа")}</b>");

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Experience:", "Опыт:"), GUILayout.Width(140));
            expInput = GUILayout.TextField(expInput, GUILayout.Width(80));
            if (GUILayout.Button(T("Add Experience", "Добавить опыт"), GUILayout.Width(160)))
            {
                SendNativeChatCommand($"/exp {expInput}", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Status Points:", "Очки характеристик:"), GUILayout.Width(140));
            pointsInput = GUILayout.TextField(pointsInput, GUILayout.Width(80));
            if (GUILayout.Button(T("Add Points", "Добавить очки"), GUILayout.Width(160)))
            {
                SendNativeChatCommand($"/point {pointsInput}", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Skill Points:", "Очки навыков:"), GUILayout.Width(140));
            skillPointsInput = GUILayout.TextField(skillPointsInput, GUILayout.Width(80));
            if (GUILayout.Button(T("Add Skill Points", "Добавить очки навыков"), GUILayout.Width(160)))
            {
                SendNativeChatCommand($"/point skill {skillPointsInput}", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Base Attack Power:", "Базовый урон (Атака):"), GUILayout.Width(140));
            atkInput = GUILayout.TextField(atkInput, GUILayout.Width(80));
            if (GUILayout.Button(T("Set Attack Power", "Изменить атаку"), GUILayout.Width(160)))
            {
                SendNativeChatCommand($"/atk {atkInput}", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Run Speed:", "Скорость бега:"), GUILayout.Width(140));
            runSpeedInput = GUILayout.TextField(runSpeedInput, GUILayout.Width(80));
            if (GUILayout.Button(T("Set Run Speed", "Изменить скорость бега"), GUILayout.Width(160)))
            {
                SendNativeChatCommand($"/run {runSpeedInput}", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Max Followers:", "Макс. сопровождающих:"), GUILayout.Width(140));
            followCapInput = GUILayout.TextField(followCapInput, GUILayout.Width(80));
            if (GUILayout.Button(T("Set Max Followers", "Изменить лимит"), GUILayout.Width(160)))
            {
                SendNativeChatCommand($"/followcap {followCapInput}", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Get All Collection Items", "Получить все элементы коллекции"), GUILayout.Height(35)))
            {
                SendNativeChatCommand("/collectall", 1);
            }
            if (GUILayout.Button(T("Swap HP/MP & Gender", "Сменить пол / HP-MP"), GUILayout.Height(35)))
            {
                SendNativeChatCommand("/change", 1);
            }
            if (GUILayout.Button(T("Yona Down", "Уложить Йону"), GUILayout.Height(35)))
            {
                SendNativeChatCommand("/yonadown", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawNpcTab()
        {
            scrollPositionNpcCheats = GUILayout.BeginScrollView(scrollPositionNpcCheats, GUILayout.Height(480));

            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{T("NPC Interactions & Management", "Управление NPC и Компаньонами")}</b>");

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Get Stunned NPC ID:", "Получить оглушенного NPC (ID):"), GUILayout.Width(160));
            npcIdInput = GUILayout.TextField(npcIdInput, GUILayout.Width(60));
            if (GUILayout.Button(T("Get NPC to Inventory", "Получить NPC в инвентарь"), GUILayout.Width(180)))
            {
                SendNativeChatCommand($"/getgen {npcIdInput}", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Max Taming for All in Inventory", "Максимальное приручение всех в инвентаре"))) SendNativeChatCommand("/petmax", 1);
            if (GUILayout.Button(T("Random Age Friendly NPCs", "Случайный возраст дружелюбным NPC"))) SendNativeChatCommand("/allage", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Increase Companion Love", "Увеличить любовь компаньона"))) SendNativeChatCommand("/love", 1);
            if (GUILayout.Button(T("+100 Love All Companions", "Увеличить любовь всех (+100)"))) SendNativeChatCommand("/inclove", 1);
            if (GUILayout.Button(T("+100 Libido All Companions", "Увеличить либидо всех (+100)"))) SendNativeChatCommand("/libido", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Impregnate Target Native", "Беременность выбранной туземки"))) SendNativeChatCommand("/preg", 1);
            if (GUILayout.Button(T("Impregnate All Natives", "Беременность всех туземок"))) SendNativeChatCommand("/allpreg", 1);
            if (GUILayout.Button(T("Reset Position to Home", "Возврат NPC на их территорию"))) SendNativeChatCommand("/resetpos", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Summon All Friends", "Призвать всех друзей"))) SendNativeChatCommand("/friends", 1);
            if (GUILayout.Button(T("Summon Single Friend", "Призвать друга"))) SendNativeChatCommand("/friend", 1);
            if (GUILayout.Button(T("Summon 30 Natives", "Призвать 30 жителей"))) SendNativeChatCommand("/makevill", 1);
            if (GUILayout.Button(T("Teleport Target NPC to Me", "Телепортировать выбранного NPC к себе"))) SendNativeChatCommand("/call", 1);
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Patrol Count:", "Патруль (количество):"), GUILayout.Width(150));
            patrolInput = GUILayout.TextField(patrolInput, GUILayout.Width(60));
            if (GUILayout.Button(T("Summon Patrol", "Призвать патруль туземцев"), GUILayout.Width(180))) SendNativeChatCommand($"/pat {patrolInput}", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Start / Stop Assault Event", "Начало / Конец рейда"))) SendNativeChatCommand("/ass start the assault", 1);
            if (GUILayout.Button(T("Trigger Kidnap Quest", "Квест по похищению NPC"))) SendNativeChatCommand("/addprisoner", 1);
            if (GUILayout.Button(T("Trigger Rescue Quest", "Запуск квеста спасения"))) SendNativeChatCommand("/rescue", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Rescue Time (sec):", "Время на спасение (сек):"), GUILayout.Width(150));
            deadTimeInput = GUILayout.TextField(deadTimeInput, GUILayout.Width(60));
            if (GUILayout.Button(T("Set Rescue Time", "Установить время спасения"), GUILayout.Width(180))) SendNativeChatCommand($"/deadtime {deadTimeInput}", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Add Morale:", "Добавить мораль всем:"), GUILayout.Width(150));
            moralInput = GUILayout.TextField(moralInput, GUILayout.Width(60));
            if (GUILayout.Button(T("Add Morale to All", "Добавить мораль всем NPC"), GUILayout.Width(180))) SendNativeChatCommand($"/moralall {moralInput}", 1);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.Space(10);

            GUILayout.BeginVertical("box");
            GUILayout.Label($"<b>{T("Full NPC Spawner Catalog", "Полный каталог призыва NPC")}</b>");

            string[] nCats = IsRussian ? npcCatRU : npcCatEN;

            GUILayout.BeginHorizontal();
            for (int i = 0; i < 3 && i < nCats.Length; i++) { DrawNpcCategoryBtn(i, nCats[i]); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 3; i < 6 && i < nCats.Length; i++) { DrawNpcCategoryBtn(i, nCats[i]); }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            for (int i = 6; i < nCats.Length; i++) { DrawNpcCategoryBtn(i, nCats[i]); }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Search NPC:", "Поиск NPC:"), GUILayout.Width(70));
            string newNpcSearch = GUILayout.TextField(searchNpcText);
            if (newNpcSearch != searchNpcText)
            {
                searchNpcText = newNpcSearch;
                FilterNpcDatabase();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            int totalNpcPages = Mathf.Max(1, Mathf.CeilToInt((float)filteredNpcs.Count / npcsPerPage));
            if (currentNpcPage >= totalNpcPages) currentNpcPage = totalNpcPages - 1;
            if (currentNpcPage < 0) currentNpcPage = 0;

            GUILayout.BeginHorizontal("box");
            if (GUILayout.Button(T("< Prev", "< Назад"), GUILayout.Width(70)) && currentNpcPage > 0) currentNpcPage--;
            GUILayout.FlexibleSpace();
            GUILayout.Label(T($"Page <b>{currentNpcPage + 1}</b> / <b>{totalNpcPages}</b> (Total: {filteredNpcs.Count})", $"Стр. <b>{currentNpcPage + 1}</b> / <b>{totalNpcPages}</b> (Всего: {filteredNpcs.Count})"));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Next >", "Вперед >"), GUILayout.Width(70)) && currentNpcPage < totalNpcPages - 1) currentNpcPage++;
            GUILayout.EndHorizontal();

            scrollPositionNpcs = GUILayout.BeginScrollView(scrollPositionNpcs, GUILayout.Height(240));

            int startNpcIndex = currentNpcPage * npcsPerPage;
            int endNpcIndex = Mathf.Min(startNpcIndex + npcsPerPage, filteredNpcs.Count);

            for (int i = startNpcIndex; i < endNpcIndex; i++)
            {
                var npc = filteredNpcs[i];
                GUILayout.BeginHorizontal("box");

                DrawSpriteFast(npc.cachedSprite, 40, 40);

                GUILayout.BeginVertical();
                GUILayout.Label($"<b>{(IsRussian ? npc.nameRU : npc.nameEN)}</b>");
                GUILayout.Label($"<color=yellow>ID: {npc.iconKey}</color>");
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(T("Spawn NPC", "Призвать NPC"), GUILayout.Width(120), GUILayout.Height(34)))
                {
                    SendNativeChatCommand(npc.command, 1);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndScrollView();
        }

        private void DrawNpcCategoryBtn(int index, string label)
        {
            if (GUILayout.Toggle(selectedNpcCategory == index, label, "Button", GUILayout.Height(24)))
            {
                if (selectedNpcCategory != index)
                {
                    selectedNpcCategory = index;
                    FilterNpcDatabase();
                }
            }
        }

        private void DrawWorldTab()
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label($"<b>{T("Time Speed:", "Скорость течения времени:")}</b> <color=cyan>{targetTimeScale:F1}x</color>");
            targetTimeScale = GUILayout.HorizontalSlider(targetTimeScale, 0.0f, 50.0f);

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Pause (0x)", "Пауза (0x)"))) targetTimeScale = 0.0f;
            if (GUILayout.Button(T("Normal (1x)", "Норма (1x)"))) targetTimeScale = 1.0f;
            if (GUILayout.Button(T("Fast (5x)", "Ускорение (5x)"))) targetTimeScale = 5.0f;
            if (GUILayout.Button(T("Max (50x)", "Макс (50x)"))) targetTimeScale = 50.0f;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label(T("<color=orange>⚠️ Warning: Setting speed above 5.0x is not recommended due to potential game bugs.</color>",
                             "<color=orange>⚠️ Предупреждение: не рекомендуется ставить ускорение выше 5x из-за возможного появления багов.</color>"));

            GUILayout.Space(10);
            GUILayout.Label(T("<color=grey>Hint: You can also adjust time speed using <b>LeftShift + <</b> and <b>LeftShift + ></b> in game.</color>",
                             "<color=grey>Подсказка: Вы можете изменять скорость времени кнопками <b>LeftShift + <</b> и <b>LeftShift + ></b> прямо в игре.</color>"));

            GUILayout.Space(10);

            GUILayout.Label($"<b>{T("Time of Day & Weather", "Время и погода")}</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Dawn", "Рассвет"))) SendNativeChatCommand("/dawn", 1);
            if (GUILayout.Button(T("Noon", "Полдень"))) SendNativeChatCommand("/noon", 1);
            if (GUILayout.Button(T("Sunset", "Закат"))) SendNativeChatCommand("/eve", 1);
            if (GUILayout.Button(T("Night", "Ночь"))) SendNativeChatCommand("/night", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Sunny", "Солнечно"))) SendNativeChatCommand("/weather 0", 1);
            if (GUILayout.Button(T("Rain", "Дождь"))) SendNativeChatCommand("/weather 1", 1);
            if (GUILayout.Button(T("Blood Rain", "Кровавый дождь"))) SendNativeChatCommand("/weather 2", 1);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label($"<b>{T("World & Map Control", "Карта и телепортация")}</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Show Coordinates on Map", "Показать координаты на карте"))) SendNativeChatCommand("/mapID", 1);
            if (GUILayout.Button(T("Open Full Map", "Открыть всю карту"))) SendNativeChatCommand("/mapopen", 1);
            if (GUILayout.Button(T("Regenerate Map Resources", "Пересоздать ресурсы на карте"))) SendNativeChatCommand("/resetmap", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("Teleport to Base", "Телепорт на базу"))) SendNativeChatCommand("/wp base", 1);
            if (GUILayout.Button(T("Teleport to Lab Basement", "Телепорт в подвал лаборатории"))) SendNativeChatCommand("/stage labo2", 1);
            if (GUILayout.Button(T("Test Items", "Тестовые предметы"))) SendNativeChatCommand("/testitems", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("Location ID:", "ID Локации:"), GUILayout.Width(100));
            tpInput = GUILayout.TextField(tpInput, GUILayout.Width(60));
            if (GUILayout.Button(T("Teleport to Location", "Телепорт по ID локации"), GUILayout.Width(180)))
            {
                SendNativeChatCommand($"/tp {tpInput}", 1);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawSettingsTab()
        {
            GUILayout.BeginVertical("box");

            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>Language / Язык:</b>", GUILayout.Width(150));
            if (GUILayout.Button("English", GUILayout.Width(90)))
            {
                configLanguage.Value = "en";
                Config.Save();
            }
            if (GUILayout.Button("Русский", GUILayout.Width(90)))
            {
                configLanguage.Value = "ru";
                Config.Save();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            GUILayout.Label(T("<b>Menu Toggle Key:</b>", "<b>Клавиша открытия меню:</b>"), GUILayout.Width(180));

            if (isRebindingKey)
            {
                GUILayout.Button(T("< Press any key... >", "< Нажмите любую клавишу... >"), GUILayout.Width(200));
            }
            else
            {
                if (GUILayout.Button($"[{toggleKey}] " + T("(Change)", "(Изменить)"), GUILayout.Width(200)))
                {
                    isRebindingKey = true;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawCreditsTab()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<size=18><b>Mad Island Kydra's Cheat Menu</b></size>");
            GUILayout.Space(5);
            GUILayout.Label(T("<b>Mod Version:</b> 1.2", "<b>Версия мода:</b> 1.2"));
            GUILayout.Space(10);

            GUILayout.Label(T("<b>Mod Author:</b> <color=yellow>Kydra Frosa</color>", "<b>Автор мода:</b> <color=yellow>Kydra Frosa</color>"));
            GUILayout.Space(15);

            GUILayout.Label(T("<color=orange>⚠️ Warning: Active use of mod functions may lead to in-game bugs.</color>",
                             "<color=orange>⚠️ Предупреждение: Активное использование функций мода может привести к внутриигровым багам.</color>"));

            GUILayout.Space(20);

            if (GUILayout.Button(T("🌐 Open Author's Steam Profile", "🌐 Открыть профиль автора в Steam"), GUILayout.Height(42)))
            {
                OpenSteamProfile();
            }

            GUILayout.EndVertical();
        }
        #endregion

        #region СХЕМА ВВОДА ЧАТ-КОМАНД
        private void GiveItemToPlayer(string itemId, int count)
        {
            if (count <= 0) count = 1;
            string command = $"/get {itemId} {count}";
            SendNativeChatCommand(command, 1);
        }

        private void SendNativeChatCommand(string command, int count)
        {
            StartCoroutine(Execute6StepSchemeCoroutine(command, count));
        }

        private IEnumerator Execute6StepSchemeCoroutine(string command, int count)
        {
            GUIUtility.keyboardControl = 0;
            GUI.UnfocusWindow();

            yield return new WaitForSecondsRealtime(0.001f);

            isExecutingCommandSequence = true;

            yield return new WaitForSecondsRealtime(0.001f);

            for (int i = 0; i < count; i++)
            {
                SimulateEnterKey();
                yield return new WaitForSecondsRealtime(0.001f);

                GUIUtility.systemCopyBuffer = command;
                SetTextInActiveChatField(command);

                yield return new WaitForSecondsRealtime(0.001f);

                SimulateEnterKey();
                yield return new WaitForSecondsRealtime(0.001f);
            }

            isExecutingCommandSequence = false;
            debugStatus = T($"Executed: {command}", $"Выполнено: {command}");
        }

        private void SimulateEnterKey()
        {
            try
            {
                keybd_event(VK_RETURN, 0, 0, 0);
                keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, 0);
            }
            catch { }
        }

        private void SetTextInActiveChatField(string command)
        {
            try
            {
                if (EventSystem.current != null)
                {
                    GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
                    if (currentSelected != null)
                    {
                        string goName = currentSelected.name.ToLower();
                        if (!goName.Contains("name") && !goName.Contains("rename") && !goName.Contains("char") && !goName.Contains("yona"))
                        {
                            var components = currentSelected.GetComponents<MonoBehaviour>();
                            foreach (var comp in components)
                            {
                                if (comp == null) continue;
                                Type t = comp.GetType();
                                if (t.Name == "TMP_InputField" || t.Name == "InputField")
                                {
                                    PropertyInfo textProp = t.GetProperty("text");
                                    if (textProp != null)
                                    {
                                        textProp.SetValue(comp, command, null);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }

                var allMono = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                foreach (var mono in allMono)
                {
                    if (mono == null || !mono.gameObject.activeInHierarchy) continue;

                    string goName = mono.gameObject.name.ToLower();
                    Type t = mono.GetType();
                    string tName = t.Name;

                    if (tName == "TMP_InputField" || tName == "InputField")
                    {
                        if (goName.Contains("name") || goName.Contains("rename") ||
                            goName.Contains("char") || goName.Contains("status") ||
                            goName.Contains("profile") || goName.Contains("yona") || goName.Contains("player"))
                        {
                            continue;
                        }

                        if (goName.Contains("chat") || goName.Contains("console") ||
                            goName.Contains("talk") || goName.Contains("command") || goName.Contains("msg"))
                        {
                            PropertyInfo textProp = t.GetProperty("text");
                            if (textProp != null)
                            {
                                textProp.SetValue(mono, command, null);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void OpenSteamProfile()
        {
            string profileUrl = "https://steamcommunity.com/id/_ardik_/";
            bool successSteam = false;

            try
            {
                Assembly steamAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.Contains("com.rlabrecque.steamworks.net") || a.GetName().Name.Contains("Steamworks"));

                if (steamAssembly != null)
                {
                    Type steamFriends = steamAssembly.GetType("Steamworks.SteamFriends");
                    Type steamAPI = steamAssembly.GetType("Steamworks.SteamAPI");

                    if (steamFriends != null && steamAPI != null)
                    {
                        var isInitMethod = steamAPI.GetMethod("IsSteamRunning", BindingFlags.Public | BindingFlags.Static);
                        bool isRunning = isInitMethod != null && (bool)isInitMethod.Invoke(null, null);

                        if (isRunning)
                        {
                            var activateMethod = steamFriends.GetMethod("ActivateGameOverlayToWebPage", BindingFlags.Public | BindingFlags.Static);
                            if (activateMethod != null)
                            {
                                activateMethod.Invoke(null, new object[] { profileUrl, (byte)0 });
                                successSteam = true;
                                debugStatus = T("Profile opened in Steam Overlay!", "Профиль открыт в Steam Overlay!");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[MadIslandKCM] Steam Overlay error: {ex.Message}");
            }

            if (!successSteam)
            {
                Application.OpenURL(profileUrl);
                debugStatus = T("Profile opened in Web Browser!", "Профиль открыт в браузере!");
            }
        }

        private void DrawSpriteFast(Sprite sprite, float width, float height)
        {
            if (sprite == null || sprite.texture == null)
            {
                GUILayout.Box(T("No\nPic", "Нет\nфото"), GUILayout.Width(width), GUILayout.Height(height));
                return;
            }

            Texture2D tex = sprite.texture;
            Rect tr = sprite.textureRect;
            Rect uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);

            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
        }
        #endregion
    }

    public static class CameraController
    {
        public static bool isCameraControlEnabled = false;
        public static float cameraSens = 3.5f;

        private static bool hasSavedDefault = false;
        private static Vector3 defaultOffset;
        private static Quaternion defaultRotation;

        private static Vector3 currentOffset;
        private static bool isCustomOffsetActive = false;

        public static void UpdateCamera()
        {
            if (!isCameraControlEnabled) return;

            Camera cam = Camera.main;
            GameObject player = DirectGameDataModifier.GetPlayerObject();
            if (cam == null || player == null) return;

            Vector3 playerCenter = player.transform.position + Vector3.up * 1.2f;

            if (!hasSavedDefault)
            {
                defaultOffset = cam.transform.position - playerCenter;
                defaultRotation = cam.transform.rotation;
                currentOffset = defaultOffset;
                hasSavedDefault = true;
            }

            if (UnityEngine.Input.GetKey(KeyCode.T))
            {
                float mouseX = UnityEngine.Input.GetAxis("Mouse X") * cameraSens * 2.0f;
                float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * cameraSens * 2.0f;

                if (Mathf.Abs(mouseX) > 0.001f || Mathf.Abs(mouseY) > 0.001f)
                {
                    isCustomOffsetActive = true;

                    Quaternion horizRot = Quaternion.AngleAxis(mouseX, Vector3.up);
                    currentOffset = horizRot * currentOffset;

                    Quaternion vertRot = Quaternion.AngleAxis(-mouseY, cam.transform.right);
                    currentOffset = vertRot * currentOffset;
                }
            }

            if (isCustomOffsetActive)
            {
                cam.transform.position = playerCenter + currentOffset;
                cam.transform.LookAt(playerCenter);
            }
        }

        public static void ResetCamera()
        {
            Camera cam = Camera.main;
            GameObject player = DirectGameDataModifier.GetPlayerObject();

            if (cam != null && player != null && hasSavedDefault)
            {
                currentOffset = defaultOffset;
                isCustomOffsetActive = false;
                cam.transform.position = (player.transform.position + Vector3.up * 1.2f) + defaultOffset;
                cam.transform.rotation = defaultRotation;
            }
        }
    }

    public static class DirectGameDataModifier
    {
        public enum HPSetType
        {
            CurrentOnly,
            MaxOnly,
            BothCurrentAndMax
        }

        private static readonly string[] hpNames = new string[]
        {
            "hp", "nowhp", "currenthp", "now_hp", "life", "currentlife", "health",
            "m_hp", "m_life", "m_health", "_hp", "_life", "nowlife", "curlife"
        };

        private static readonly string[] maxHpNames = new string[]
        {
            "maxhp", "max_hp", "maxlife", "max_life", "maxhealth",
            "m_maxhp", "m_maxlife", "_maxhp", "max_life"
        };

        private static readonly string[] hungerNames = new string[]
        {
            "hunger", "nowhunger", "currenthunger", "now_hunger", "cur_hunger",
            "food", "nowfood", "currentfood", "now_food",
            "fullness", "satiety", "stomach", "m_hunger", "m_food", "_hunger", "_food"
        };

        private static readonly string[] thirstNames = new string[]
        {
            "thirst", "nowthirst", "currenthirst", "now_thirst", "cur_thirst",
            "water", "nowwater", "currentwater", "now_water", "cur_water",
            "hydration", "nowhydration", "currenthydration",
            "drink", "m_thirst", "m_water", "m_hydration", "_thirst", "_water"
        };

        private static readonly string[] refreshMethodNames = new string[]
        {
            "updateui", "refreshui", "updatehp", "refreshhp", "sethp", "updatehealth",
            "updatestatus", "refreshstatus", "updatehud", "refreshhud", "redraw",
            "onhpchanged", "forceupdateui", "applystatus", "refreshtext", "updateslider",
            "drawui", "updatebar", "refresh"
        };

        public static GameObject GetPlayerObject()
        {
            try
            {
                GameObject tagged = GameObject.FindWithTag("Player");
                if (tagged != null) return tagged;
            }
            catch { }

            foreach (string name in new string[] { "Player", "Player(Clone)", "Yona", "Yona(Clone)", "MainCharacter", "Hero" })
            {
                GameObject go = GameObject.Find(name);
                if (go != null) return go;
            }
            return null;
        }

        public static float GetPlayerHP(bool getMax = false)
        {
            List<GameObject> playerObjects = FindPlayerGameObjects();
            string[] searchNames = getMax ? maxHpNames : hpNames;

            foreach (var go in playerObjects)
            {
                if (go == null) continue;
                var components = go.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    Type t = comp.GetType();

                    foreach (var name in searchNames)
                    {
                        FieldInfo field = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field != null)
                        {
                            try
                            {
                                object val = field.GetValue(comp);
                                if (val != null && float.TryParse(val.ToString(), out float res)) return res;
                            }
                            catch { }
                        }

                        PropertyInfo prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (prop != null && prop.CanRead)
                        {
                            try
                            {
                                object val = prop.GetValue(comp, null);
                                if (val != null && float.TryParse(val.ToString(), out float res)) return res;
                            }
                            catch { }
                        }
                    }
                }
            }

            return 0f;
        }

        public static bool SetPlayerHPDirect(float value, HPSetType setType, bool triggerUIRefresh = true)
        {
            bool success = false;
            HashSet<object> visited = new HashSet<object>();

            if (setType == HPSetType.CurrentOnly)
            {
                float curMax = GetPlayerHP(true);
                if (curMax > 0 && value > curMax)
                {
                    setType = HPSetType.BothCurrentAndMax;
                }
            }

            List<GameObject> playerObjects = FindPlayerGameObjects();

            foreach (var go in playerObjects)
            {
                if (go == null) continue;
                var components = go.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var comp in components)
                {
                    if (comp == null) continue;

                    if (setType == HPSetType.BothCurrentAndMax)
                    {
                        bool s1 = InspectAndModifyObject(comp, value, false, 0, visited);
                        bool s2 = InspectAndModifyObject(comp, value, true, 0, visited);
                        if (s1 || s2) success = true;
                    }
                    else
                    {
                        bool isMax = (setType == HPSetType.MaxOnly);
                        if (InspectAndModifyObject(comp, value, isMax, 0, visited)) success = true;
                    }
                }
            }

            if (!success)
            {
                MonoBehaviour[] allScripts = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var script in allScripts)
                {
                    if (script == null || !script.enabled) continue;
                    string tName = script.GetType().Name.ToLower();

                    if (tName.Contains("player") || tName.Contains("status") || tName.Contains("character") ||
                        tName.Contains("yona") || tName.Contains("hero") || tName.Contains("chara") || tName.Contains("life"))
                    {
                        if (setType == HPSetType.BothCurrentAndMax)
                        {
                            bool s1 = InspectAndModifyObject(script, value, false, 0, visited);
                            bool s2 = InspectAndModifyObject(script, value, true, 0, visited);
                            if (s1 || s2) success = true;
                        }
                        else
                        {
                            bool isMax = (setType == HPSetType.MaxOnly);
                            if (InspectAndModifyObject(script, value, isMax, 0, visited)) success = true;
                        }
                    }
                }
            }

            if (triggerUIRefresh)
            {
                ForceUIRefresh();
            }

            return success;
        }

        public static void RefillHungerAndThirst()
        {
            GameObject player = GetPlayerObject();
            if (player == null) return;

            HashSet<object> visited = new HashSet<object>();
            var components = player.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (var comp in components)
            {
                if (comp == null) continue;
                Type t = comp.GetType();

                InspectAndModifyCustomNames(comp, 9999f, hungerNames, 0, visited);

                float targetWaterVal = 9999f;
                float maxW = GetValueFromFields(comp, new string[] { "maxwater", "max_water", "maxthirst", "max_thirst", "maxhydration" });
                if (maxW > 0) targetWaterVal = maxW;

                InspectAndModifyCustomNames(comp, targetWaterVal, thirstNames, 0, visited);

                MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var m in methods)
                {
                    if (m == null || m.IsGenericMethod) continue;
                    string mName = m.Name.ToLower();

                    if (mName.Contains("drink") || mName.Contains("water") || mName.Contains("thirst") || mName.Contains("eat") || mName.Contains("food") || mName.Contains("satiety"))
                    {
                        try
                        {
                            var parameters = m.GetParameters();
                            if (parameters.Length == 0)
                            {
                                m.Invoke(comp, null);
                            }
                            else if (parameters.Length == 1 && (parameters[0].ParameterType == typeof(float) || parameters[0].ParameterType == typeof(int)))
                            {
                                m.Invoke(comp, new object[] { 9999f });
                            }
                        }
                        catch { }
                    }
                }
            }

            ForceUIRefresh();
        }

        private static float GetValueFromFields(object target, string[] names)
        {
            if (target == null) return 0f;
            Type t = target.GetType();

            foreach (var n in names)
            {
                FieldInfo f = t.GetField(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    try
                    {
                        object val = f.GetValue(target);
                        if (val != null && float.TryParse(val.ToString(), out float res)) return res;
                    }
                    catch { }
                }

                PropertyInfo p = t.GetProperty(n, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanRead)
                {
                    try
                    {
                        object val = p.GetValue(target, null);
                        if (val != null && float.TryParse(val.ToString(), out float res)) return res;
                    }
                    catch { }
                }
            }
            return 0f;
        }

        public static void ForceUIRefresh()
        {
            try
            {
                List<GameObject> searchObjects = FindPlayerGameObjects();

                string[] uiNames = new string[] { "UI", "HUD", "Canvas", "PlayerUI", "StatusUI", "HUDManager", "UIManager", "StatusWindow" };
                foreach (var name in uiNames)
                {
                    try
                    {
                        GameObject go = GameObject.Find(name);
                        if (go != null && !searchObjects.Contains(go)) searchObjects.Add(go);
                    }
                    catch { }
                }

                foreach (var go in searchObjects)
                {
                    if (go == null) continue;
                    var components = go.GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        Type t = comp.GetType();

                        MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var m in methods)
                        {
                            if (m == null || m.IsGenericMethod) continue;
                            string mNameLower = m.Name.ToLower();

                            if (refreshMethodNames.Contains(mNameLower))
                            {
                                try
                                {
                                    ParameterInfo[] paramsInfo = m.GetParameters();
                                    if (paramsInfo.Length == 0)
                                    {
                                        m.Invoke(comp, null);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static List<GameObject> FindPlayerGameObjects()
        {
            List<GameObject> list = new List<GameObject>();

            try
            {
                GameObject tagged = GameObject.FindWithTag("Player");
                if (tagged != null) list.Add(tagged);
            }
            catch { }

            string[] possibleNames = new string[] { "Player", "Player(Clone)", "Yona", "Yona(Clone)", "MainCharacter", "Hero", "MC" };
            foreach (string name in possibleNames)
            {
                try
                {
                    GameObject go = GameObject.Find(name);
                    if (go != null && !list.Contains(go)) list.Add(go);
                }
                catch { }
            }

            return list;
        }

        private static bool InspectAndModifyObject(object target, float val, bool isMax, int depth, HashSet<object> visited)
        {
            string[] searchNames = isMax ? maxHpNames : hpNames;
            return InspectAndModifyCustomNames(target, val, searchNames, depth, visited);
        }

        private static bool InspectAndModifyCustomNames(object target, float val, string[] searchNames, int depth, HashSet<object> visited)
        {
            if (target == null || depth > 2 || visited.Contains(target)) return false;
            visited.Add(target);

            Type t = target.GetType();
            bool found = false;

            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f == null) continue;
                string fNameLower = f.Name.ToLower();

                if (searchNames.Contains(fNameLower))
                {
                    if (SetMemberValue(f, target, val))
                    {
                        found = true;
                    }
                }
                else if (!f.FieldType.IsPrimitive && f.FieldType != typeof(string) && !f.FieldType.IsEnum && !f.FieldType.IsValueType)
                {
                    try
                    {
                        object subVal = f.GetValue(target);
                        if (subVal != null && InspectAndModifyCustomNames(subVal, val, searchNames, depth + 1, visited))
                        {
                            found = true;
                        }
                    }
                    catch { }
                }
            }

            PropertyInfo[] props = t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (p == null || !p.CanWrite) continue;
                string pNameLower = p.Name.ToLower();

                if (searchNames.Contains(pNameLower))
                {
                    if (SetPropertyMemberValue(p, target, val))
                    {
                        found = true;
                    }
                }
            }

            return found;
        }

        private static bool SetMemberValue(FieldInfo field, object target, float val)
        {
            try
            {
                if (field.FieldType == typeof(float)) { field.SetValue(target, val); return true; }
                if (field.FieldType == typeof(int)) { field.SetValue(target, (int)val); return true; }
                if (field.FieldType == typeof(double)) { field.SetValue(target, (double)val); return true; }
            }
            catch { }
            return false;
        }

        private static bool SetPropertyMemberValue(PropertyInfo prop, object target, float val)
        {
            try
            {
                if (prop.PropertyType == typeof(float)) { prop.SetValue(target, val, null); return true; }
                if (prop.PropertyType == typeof(int)) { prop.SetValue(target, (int)val); return true; }
                if (prop.PropertyType == typeof(double)) { prop.SetValue(target, (double)val, null); return true; }
            }
            catch { }
            return false;
        }
    }

    [HarmonyPatch]
    public static class Patch_GodModeDamage
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            List<MethodBase> methods = new List<MethodBase>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var asm in assemblies)
            {
                if (asm == null) continue;
                string asmName = asm.GetName().Name;
                if (asmName.StartsWith("System") || asmName.StartsWith("UnityEngine") ||
                    asmName.StartsWith("BepInEx") || asmName.StartsWith("Harmony") ||
                    asmName.StartsWith("mscorlib") || asmName.StartsWith("Mono"))
                    continue;

                try
                {
                    Type[] types = asm.GetTypes();
                    foreach (var t in types)
                    {
                        if (t == null) continue;

                        MethodInfo[] allMethods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        foreach (var m in allMethods)
                        {
                            if (m == null || m.IsAbstract) continue;
                            string mNameLower = m.Name.ToLower();

                            if (mNameLower.Contains("damage") || mNameLower.Contains("takedamage") ||
                                mNameLower.Contains("subhp") || mNameLower.Contains("reducehp") ||
                                mNameLower.Contains("applydamage") || mNameLower.Contains("ondamage") ||
                                mNameLower.Contains("hitproc") || mNameLower.Contains("receivehp") ||
                                mNameLower == "hit" || mNameLower == "onhit")
                            {
                                methods.Add(m);
                            }
                        }
                    }
                }
                catch { }
            }
            return methods;
        }

        [HarmonyPrefix]
        static bool Prefix(object __instance)
        {
            if (!KydraCheatMenu.isGodMode) return true;

            if (__instance != null)
            {
                if (__instance is Component comp && comp != null && comp.gameObject != null)
                {
                    GameObject go = comp.gameObject;
                    if (go.CompareTag("Player") ||
                        go.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        go.name.IndexOf("Yona", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        go.name.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false;
                    }
                }

                string typeName = __instance.GetType().Name.ToLower();
                if (typeName.Contains("player") || typeName.Contains("yona") || typeName.Contains("hero"))
                {
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButton))]
    public static class Patch_GetMouseButton
    {
        static bool Prefix(ref bool __result)
        {
            if (KydraCheatMenu.IsUIOpen || KydraCheatMenu.isExecutingCommandSequence)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButtonDown))]
    public static class Patch_GetMouseButtonDown
    {
        static bool Prefix(ref bool __result)
        {
            if (KydraCheatMenu.IsUIOpen || KydraCheatMenu.isExecutingCommandSequence)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetMouseButtonUp))]
    public static class Patch_GetMouseButtonUp
    {
        static bool Prefix(ref bool __result)
        {
            if (KydraCheatMenu.IsUIOpen || KydraCheatMenu.isExecutingCommandSequence)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKey), typeof(KeyCode))]
    public static class Patch_GetKey
    {
        static bool Prefix(KeyCode key, ref bool __result)
        {
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                return true;
            }

            if (KydraCheatMenu.isExecutingCommandSequence)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.Input), nameof(UnityEngine.Input.GetKeyDown), typeof(KeyCode))]
    public static class Patch_GetKeyDown
    {
        static bool Prefix(KeyCode key, ref bool __result)
        {
            if (key == KeyCode.Return || key == KeyCode.KeypadEnter)
            {
                return true;
            }

            if (KydraCheatMenu.isExecutingCommandSequence)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
