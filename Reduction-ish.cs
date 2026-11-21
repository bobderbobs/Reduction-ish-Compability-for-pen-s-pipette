using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Reductionish
{
    public class ModEntry : IMod
    {
        public static ModEntry Instance { get; private set; }
        public ModManifest Manifest { get; private set; }
        public ModLog Log { get; private set; }
        public Color Color { get; private set; }

        public void Init(ModManifest manifest)
        {
            Instance = this;
            Manifest = manifest;
            Log = new ModLog(manifest);
            Log.Log($"{manifest.Name} v{manifest.ModVersion} loaded.");
        }

        public void OnEnable()
        {
            ModHook.OnPlaceInventorInventoryItemEarly += PlaceNotebookInventorInventory;
            ModHook.OnModAssetsLoaded += OnAssetsLoaded;
            ModHook.OnModItemDirectoryInit += RegisterModItems;
            ModHook.OnGameLoadedInit += OnGameLoaded;
            Log.Log("Enabled.");
        }

        public void OnDisable()
        {
            ModHook.OnPlaceInventorInventoryItemEarly -= PlaceNotebookInventorInventory;
            ModHook.OnModAssetsLoaded -= OnAssetsLoaded;
            ModHook.OnModItemDirectoryInit -= RegisterModItems;
            ModHook.OnGameLoadedInit -= OnGameLoaded;
            Log.Log("Disabled.");
        }

        private void OnGameLoaded()
        {
            EnsureSodiumRegistered();
            EnsureFlocculantRegistered();
            EnsureChemFinisherRecipeRegistered();
            Log.Log("Liquids and Chem Finisher recipe registered on save load.");
        }

        private void PlaceNotebookInventorInventory(List<GameItem> item)
        {
            const string BookItemId = "reduction_notes_guide";

            bool alreadyOwned = item != null && item.Exists(i => i != null && i.identifier == BookItemId);
            if (!alreadyOwned)
            {
                PlayerStore.Instance.AddDirectSellingItemToTable(BookItemId);
            }
        }
        private void RegisterModItems(ModItemDirectory dir)
        {
            // Ensure the purple sprite is generated even if OnModAssetsLoaded fired before Init!
            LoadedMod loadedMod = ModLoader.Mods.Find(m => m.Manifest.ID == Manifest.ID);
            if (loadedMod != null && !loadedMod.Sprites.ContainsKey("flocculant_tablet"))
            {
                GeneratePurpleTabletSprite(loadedMod);
            }

            dir.Add("flocculant_tablet", () =>
            {
                GameItem gameItem = ItemDirectory.CreateEmptyItem("flocculant_tablet").SetSpriteAndShapeFromMod(Manifest.ID, "flocculant_tablet");
                gameItem.name = ModHelper.GetLocalized("Reduction-ish", "flocculant_tablet_name");
                gameItem.shortDescription = ModHelper.GetLocalized("Reduction-ish", "flocculant_tablet_desc");
                gameItem.SetGameItemType("HOUSEHOLD_GOOD");
                gameItem.SetGameItemType("WATER_PURIFICATION_SUPPLY");
                AudioHelper.InitTinyItem(gameItem);
                gameItem.SetValue(15L);
                gameItem.EnableTag("WATER_PURIFICATION_TABLET");
                ChemicalHelper.InitChemItem(gameItem, isBlendable: true, isSoluble: true);
                ChemicalHelper.SetChemContentML(gameItem, "flocculant", 15.0);
                return gameItem;
            });

            dir.Add("reduction_notes_guide", () =>
            {
                GameItem gameItem = ItemDirectory.CreateEmptyItem("reduction_notes_guide").SetSpriteAndShape("items_misc", "manual1");
                gameItem.name = ModHelper.GetLocalized("Reduction-ish", "reduction_notes_guide_name");
                gameItem.shortDescription = ModHelper.GetLocalized("Reduction-ish", "reduction_notes_guide_desc");
                gameItem.EnableTag("DOCUMENT");
                gameItem.SetGameItemType("DOCUMENT");
                gameItem.EnableTag("BOOK_TAG");
                gameItem.EnableTag("IMPORTANT_TAG");
                gameItem.SetValue(50L);
                BookHelper.InitPaperGuide(gameItem);
                return gameItem;
            });
        }

        private void OnAssetsLoaded(LoadedMod mod)
        {
            if (mod.Manifest.ID != Manifest.ID) return;
            GeneratePurpleTabletSprite(mod);
        }

        private void GeneratePurpleTabletSprite(LoadedMod mod)
        {
            if (mod.Sprites.ContainsKey("flocculant_tablet")) return;

            Sprite baseSprite = RenderHandler.LoadFromAtlas("Items/items_amenities", "water_tablet");
            if (baseSprite == null)
            {
                Debug.Log("Could not find base water_tablet sprite to tint.");
                return;
            }

            Rect rect = baseSprite.rect;
            int width = (int)rect.width;
            int height = (int)rect.height;

            RenderTexture rt = RenderTexture.GetTemporary(baseSprite.texture.width, baseSprite.texture.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;

            GL.PushMatrix();
            Graphics.Blit(baseSprite.texture, rt);
            GL.PopMatrix();

            RenderTexture.active = rt;
            Texture2D purpleTex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            purpleTex.filterMode = FilterMode.Point;
            purpleTex.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Color tint = new Color(0.65f, 0.45f, 0.85f, 1f);
            Color[] pixels = purpleTex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] *= tint;
            }

            purpleTex.SetPixels(pixels);
            purpleTex.Apply(true);

            Vector2 pivot = new Vector2(baseSprite.pivot.x / width, baseSprite.pivot.y / height);
            Sprite purpleSprite = Sprite.Create(purpleTex, new Rect(0, 0, width, height), pivot, baseSprite.pixelsPerUnit);
            purpleSprite.name = "flocculant_tablet";

            mod.Sprites["flocculant_tablet"] = purpleSprite;
            Log.Log("Generated pixel-perfect purple puck of precious flocculant_tablet sprite in memory.");
        }


        // Ensure that the sodium liquid is registered
        public static void EnsureSodiumRegistered()
        {
            if (Liquid.LiquidDict == null || Liquid.LiquidDict.ContainsKey("sodium_suspension"))
            {
                return;
            }

            Liquid sodium = new Liquid();
            sodium.displayName = ModHelper.GetLocalized("Reduction-ish", "sodium_suspension_name");
            sodium.identifier = "sodium_suspension";
            sodium.part = "PART_SODIUM_SUSPENSION";               
            sodium.currentPart = "CURRENT_PART_SODIUM_SUSPENSION"; 
            sodium.canBeRemovedByPurifier = false;
            sodium.particleSize = 5;
            sodium.color = "D94116";
            sodium.colorMultiplier = 3;
            Liquid.Liquids.Add(sodium);
            Liquid.LiquidDict["sodium_suspension"] = sodium;
            Debug.Log("Successfully registered 'sodium_suspension' into LiquidDict!");

            
        }
        // Ensure that the flocculant liquid is registered
        public static void EnsureFlocculantRegistered()
        {
            if (Liquid.LiquidDict == null || Liquid.LiquidDict.ContainsKey("flocculant"))
            {
                return;
            }

            Liquid starch = new Liquid();
            starch.displayName = ModHelper.GetLocalized("Reduction-ish", "flocculant_name");
            starch.identifier = "flocculant";
            starch.part = "PART_FLOCCULANT";               
            starch.currentPart = "CURRENT_PART_FLOCCULANT"; 
            starch.canBeRemovedByPurifier = true;
            starch.basePurifierRemoveAmount = 4000;
            starch.particleSize = 5;
            starch.color = "6a4c93";
            starch.colorMultiplier = 3;
            Liquid.Liquids.Add(starch);
            Liquid.LiquidDict["flocculant"] = starch;
            Debug.Log("Successfully registered 'flocculant' into LiquidDict!");
        }

        // Ensure that the ChemFinisherRecipe for the flocculant tablet is registered
        public static void EnsureChemFinisherRecipeRegistered()
        {
            if (ChemFinisherRecipe.chemFinisherRecipes == null)
            {
                Debug.LogWarning(" ChemFinisherRecipe.chemFinisherRecipes is null!");
                return;
            }

            if (ChemFinisherRecipe.chemFinisherRecipes.Exists(r => r.identifier == "flocculant_tablet"))
            {
                return;
            }

            if (!ChemFinisherRecipe.chemFinisherRecipes.Exists(r => r.identifier == "flocculant_tablet"))
            {
                ChemFinisherRecipe.chemFinisherRecipes.Add(new ChemFinisherRecipe
                {
                    identifier = "flocculant_tablet",
                    displayName = ModHelper.GetLocalized("Reduction-ish", "flocculant_tablet_name"),
                    requiredVolume = 15000, // 15 mL of dried flocculant per puck
                    isRequireDry = true,
                    activeIngredientId = "flocculant"
                });

                if (ChemFinisherRecipe.chemFinisherRecipes.Exists(r => r.identifier == "flocculant_tablet"))
                {
                    Debug.Log(" Successfully registered 'flocculant_tablet' into ChemFinisherRecipe!");
                }
                else
                {
                    Debug.LogWarning(" Failed to register 'flocculant_tablet' into ChemFinisherRecipe!");
                }
            }
        }
    }
}

[HarmonyPatch(typeof(MachineFurnace), nameof(MachineFurnace.Furnace))]
internal static class FurnaceInitPatch
{
    [HarmonyPostfix]
    public static void Postfix(GameItem __result)
    {
        if (__result == null) return;

        // 1. Grab the real inventories from __result (the newly created furnace)
        var inventories = GraphUtils.FindAllChildrenType<GameInventory>(__result);
        if (inventories.Count < 3) return;

        GameSlotInventory batterySlot = inventories[0] as GameSlotInventory;
        GameInventory moduleInv = inventories[1];
        GameInventory leftInv = inventories[2];
        GameInventory rightInv = inventories[3];

        // 2. Allow electronics and water bottles to be placed into the Left Input slot
        var origMayAddLeft = leftInv.mayInventoryAddItemFunc;
        leftInv.mayInventoryAddItemFunc = (GameItem item, GameInventory inv) =>
        {
            if (!GeneralHelper.IsItemOwned(item)) return false;

            if (item.identifier == "common_electronic" ||
                item.identifier == "bottled_water" ||
                item.identifier == "large_bottled_water")
            {
                return true;
            }

            return origMayAddLeft != null && origMayAddLeft(item, inv);
        };

        // 3. Attach the recipe to the furnace's overnight cycle event
        var origCycleEnd = __result.onCycleEndSlotItemFunc;
        __result.onCycleEndSlotItemFunc = (GameItem machine, GameInventory parentInv, SlotMarker slot) =>
        {
            bool customCrafted = TryRunSodiumRecipe(machine, batterySlot, moduleInv, leftInv, rightInv);

            // Fall back to standard smelting if the recipe didn't run
            if (!customCrafted && origCycleEnd != null)
            {
                origCycleEnd(machine, parentInv, slot);
            }
        };
    }

    private static bool TryRunSodiumRecipe(
        GameItem furnace,
        GameSlotInventory batterySlot,
        GameInventory moduleInv,
        GameInventory leftInv,
        GameInventory rightInv)
    {
        // Note: We use 'leftInv' directly here
        var electronics = GraphUtils.FindAllChildrenType<GameItem>(leftInv, x => x.identifier == "common_electronic");
        var waterBottle = GraphUtils.FindChildType<GameItem>(leftInv, x => x.identifier == "bottled_water" || x.identifier == "large_bottled_water");

        if (electronics.Count < 2 || waterBottle == null) return false;
        if (batterySlot?.childItem == null) return false;

        var origMayAddRight = rightInv.mayInventoryAddItemFunc;
        rightInv.mayInventoryAddItemFunc = (GameItem item, GameInventory _) => GeneralHelper.IsItemOwned(item);

        try
        {
            GameItem outputBottle = DirectoryMaster.Item(waterBottle.identifier);

            // Check if the right inventory can accept the output bottle
            if (!GraphUtils.CanAccept(rightInv, outputBottle))
            {
                outputBottle.Destroy();
                return false;
            }

            // Check if the battery has enough power to run the recipe
            int powerCost = MachineryHelper.GetMachinePowerUsage(furnace);
            if (!PowerHelper.DrawPowerSource(batterySlot.childItem, powerCost))
            {
                outputBottle.Destroy();
                return false;
            }

            // Remove any existing water from the output bottle before adding new liquids
            int currentWaterUL = WaterHelper.GetCurrentCapacityUL(outputBottle);
            if (currentWaterUL > 0)
            {
                WaterHelper.Remove(outputBottle, currentWaterUL);
            }

            // Randomly generate the amounts of each liquid to add to the output bottle with a limit of 500,000 UL total.
            int sodiumUL = RNG.GetRandomInt(180000, 240000);
            int metalUL = RNG.GetRandomInt(50000, 90000);
            int chemUL = RNG.GetRandomInt(20000, 50000);
            int physicalUL = 500000 - sodiumUL - metalUL - chemUL;
            WaterHelper.AddLiquid(outputBottle, "sodium_suspension", sodiumUL);
            WaterHelper.AddLiquid(outputBottle, "physical_contaminant", physicalUL);
            WaterHelper.AddLiquid(outputBottle, "heavy_metal", metalUL);
            WaterHelper.AddLiquid(outputBottle, "chemical_contaminant", chemUL);

            for (int i = 0; i < 2; i++) electronics[i].Destroy();
            waterBottle.Destroy();

            GraphUtils.EmporiumTryAdd(rightInv, outputBottle);
            MachineHelper.OnMachineActioned(furnace, moduleInv);

            return true;
        }
        finally
        {
            rightInv.mayInventoryAddItemFunc = origMayAddRight;
        }
    }
}

[HarmonyPatch(typeof(ChemicalHelper), nameof(ChemicalHelper.HandleTemperatureReaction))]
internal static class ChemicalReactionPatch
{
    [HarmonyPostfix]
    public static void Postfix(GameItem container)
    {
        if (WaterHelper.IsEmpty(container))
        {
            return;
        }

        // Run the custom reactions for modded liquids, and if a reaction occured, run the water feature
        int reacted = HandleSodiumAcidReaction(container)
             + HandleStarchActivationReaction(container)
             + HandleFlocculation(container)
             + HandleFlocculantCleanup(container);

        if (reacted > 0)
        {
            ItemFeature waterFeature = WaterFeatureHelper.GetWaterFeature(container);
            WaterFeatureHelper.OnLiquidChanged(container, waterFeature.IsExposed());
        }
    }

    private static int HandleSodiumAcidReaction(GameItem container)
    {
        // Check the temperature of the container to ensure it's within the reaction range
        double temp = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        if (temp < 90.0 || temp > 110.0)
        {
            return 0;
        }

        // Verify exact liquid IDs in LiquidDict
        int sodiumPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("sodium_suspension")).GetInt();
        int acidPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("acid")).GetInt();

        if (sodiumPart <= 0 || acidPart <= 0)
        {
            return 0;
        }

        // Determine the maximum number of pairs that can react based on the available parts
        int maxPair = Math.Min(sodiumPart, acidPart);
        int reactPerSide = Math.Min(maxPair, Math.Max(1000, (int)(maxPair * 0.2)));
        int totalReacted = reactPerSide * 2;

        container.ModifyTag(Liquid.GetCurrentPartFromId("sodium_suspension"), (TagState state) =>
        {
            state.Enable().SetInt(sodiumPart - reactPerSide);
        }, skipValidate: true);

        container.ModifyTag(Liquid.GetCurrentPartFromId("acid"), (TagState state) =>
        {
            state.Enable().SetInt(acidPart - reactPerSide);
        }, skipValidate: true);

        // Calculate the yields for reducing agent, debris, and mineral based on the total reacted amount
        int currentReducing = container.GetTagReadonly(Liquid.GetCurrentPartFromId("reducing_agent")).GetInt();
        int currentDebris = container.GetTagReadonly(Liquid.GetCurrentPartFromId("debris")).GetInt();
        int currentMineral = container.GetTagReadonly(Liquid.GetCurrentPartFromId("mineral")).GetInt();

        int reducingYield = (int)(totalReacted * 0.40);
        int debrisYield = (int)(totalReacted * 0.40);
        int mineralYield = totalReacted - reducingYield - debrisYield;

        container.ModifyTag(Liquid.GetCurrentPartFromId("reducing_agent"), (TagState state) =>
        {
            state.Enable().SetInt(currentReducing + reducingYield);
        }, skipValidate: true);

        container.ModifyTag(Liquid.GetCurrentPartFromId("debris"), (TagState state) =>
        {
            state.Enable().SetInt(currentDebris + debrisYield);
        }, skipValidate: true);

        container.ModifyTag(Liquid.GetCurrentPartFromId("mineral"), (TagState state) =>
        {
            state.Enable().SetInt(currentMineral + mineralYield);
        }, skipValidate: true);

        return 1;
    }
    // Turns starch into flocculant if the temperature is between 40°C and 50°C, and both starch and base are present in the container.
    private static int HandleStarchActivationReaction(GameItem container)
    {
        double temp = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        if (temp < 40.0 || temp > 50.0)
        {
            return 0;
        }

        int starchPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("starch")).GetInt();
        int basePart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("base")).GetInt();

        // If starch is present under 3UL and base is present, convert all starch into flocculant.
        if (starchPart > 0 && starchPart < 3 && basePart > 0)
        {
            int currentFloc = container.GetTagReadonly(Liquid.GetCurrentPartFromId("flocculant")).GetInt();
            container.ModifyTag(Liquid.GetCurrentPartFromId("starch"), s => s.Enable().SetInt(0), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("flocculant"), s => s.Enable().SetInt(currentFloc + starchPart), skipValidate: true);
            return 1;
        }

        int sugarPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("sugar")).GetInt();

        if (sugarPart > 0 && basePart > 0)
        {
            // Break down up to 2,500 uL of sugar per tick in the alkaline solution (requires a tiny 10:1 trace of base)
            int sugarBrokenDown = Math.Min(sugarPart, 2500);
            int baseCatalystUsed = Math.Min(basePart, Math.Min(1500, Math.Max(1, sugarBrokenDown / 10)));

            int currentDebris = container.GetTagReadonly(Liquid.GetCurrentPartFromId("debris")).GetInt();

            container.ModifyTag(Liquid.GetCurrentPartFromId("base"), s => s.Enable().SetInt(basePart - baseCatalystUsed), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("debris"), s => s.Enable().SetInt(currentDebris + sugarBrokenDown + baseCatalystUsed), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("sugar"), s => s.Enable().SetInt(sugarPart - sugarBrokenDown), skipValidate: true);
        }

        if (starchPart <= 0 || basePart <= 0) return 0;
        {
            // Calculate 3:1 reaction rate per tick
            int maxBatches = Math.Min(starchPart / 3, basePart);
            int reactBase = Math.Min(maxBatches, 2500);
            int reactStarch = reactBase * 3;
            int totalReacted = reactStarch + reactBase;
            int flocYield = (int)(totalReacted * 0.65);
            int waterYield = (int)(totalReacted * 0.30);
            int mineralYield = totalReacted - flocYield - waterYield;

            int currentFlocculant = container.GetTagReadonly(Liquid.GetCurrentPartFromId("flocculant")).GetInt();
            int currentWater = container.GetTagReadonly(Liquid.GetCurrentPartFromId("water")).GetInt();
            int currentMineral = container.GetTagReadonly(Liquid.GetCurrentPartFromId("mineral")).GetInt();

            container.ModifyTag(Liquid.GetCurrentPartFromId("flocculant"), s => s.Enable().SetInt(currentFlocculant + flocYield), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("water"), s => s.Enable().SetInt(currentWater + waterYield), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("mineral"), s => s.Enable().SetInt(currentMineral + mineralYield), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("starch"), s => s.Enable().SetInt(starchPart - reactStarch), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("base"), s => s.Enable().SetInt(basePart - reactBase), skipValidate: true);
            return 1;
        }

    }

    private static int HandleFlocculation(GameItem container)
    {
        double temp = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        int starchPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("flocculant")).GetInt();

        if (starchPart <= 0) return 0;

        // Reaction window (50°C – 65°C): Gelatinization
        if (temp >= 50.0 && temp <= 65.0)
        {
            int metalPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("heavy_metal")).GetInt();
            int chemicalPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("chemical_contaminant")).GetInt();
            int sodiumPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("sodium_suspension")).GetInt();
            int waterPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("water")).GetInt();
            int debrisPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("debris")).GetInt();

            long totalTargets = (long)metalPart + chemicalPart + sodiumPart;
            if (totalTargets <= 0)
            {
                return 0;
            }

            // Calculate the maximum useful starch based on the total targets, with a cap of 1500 per ticks
            int maxUsefulStarch = (int)Math.Ceiling(totalTargets / 5.0);
            int tickCap = 1500;
            int reactStarch = Math.Min(starchPart, Math.Min(tickCap, maxUsefulStarch));
            long cleaningBudget = (long)(reactStarch * 5.0);

            // Priority 1: Sweep heavy metals and chemical contaminants down to 0%
            int metalRemoved = (int)Math.Min(metalPart, cleaningBudget);
            cleaningBudget -= metalRemoved;

            int chemicalRemoved = (int)Math.Min(chemicalPart, cleaningBudget);
            cleaningBudget -= chemicalRemoved;

            // Priority 2: Any leftover starch converts unreacted sodium_suspension into pure water
            int sodiumConverted = (int)Math.Min(sodiumPart, cleaningBudget);

            container.ModifyTag(Liquid.GetCurrentPartFromId("flocculant"), s => s.Enable().SetInt(starchPart - reactStarch), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("heavy_metal"), s => s.Enable().SetInt(metalPart - metalRemoved), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("chemical_contaminant"), s => s.Enable().SetInt(chemicalPart - chemicalRemoved), skipValidate: true);
            container.ModifyTag(Liquid.GetCurrentPartFromId("debris"), s => s.Enable().SetInt(debrisPart + metalRemoved + chemicalRemoved), skipValidate: true);

            if (sodiumConverted > 0)
            {
                container.ModifyTag(Liquid.GetCurrentPartFromId("sodium_suspension"), s => s.Enable().SetInt(sodiumPart - sodiumConverted), skipValidate: true);
                container.ModifyTag(Liquid.GetCurrentPartFromId("water"), s => s.Enable().SetInt(waterPart + sodiumConverted), skipValidate: true);
            }

            return 1;
        }

        return 0;
    }
    private static int HandleFlocculantCleanup(GameItem container)
    {
        int flocPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("flocculant")).GetInt();
        if (flocPart <= 0)
        {
            return 0;
        }

        double temp = container.GetTagReadonly("CONTAINER_TEMP_TAG").GetDouble();
        int acidPart = container.GetTagReadonly(Liquid.GetCurrentPartFromId("acid")).GetInt();

        int totalRemoved = 0;

        // 1. Acid Neutralization (1 part acid neutralizes 2 parts flocculant at any temperature)
        if (acidPart > 0)
        {
            int maxPairs = Math.Min(flocPart / 2, acidPart);
            int reactAcid = Math.Min(1500, Math.Max(1, maxPairs));
            int reactFloc = Math.Min(flocPart, reactAcid * 2);

            acidPart -= reactAcid;
            flocPart -= reactFloc;
            totalRemoved += (reactAcid + reactFloc);

            container.ModifyTag(Liquid.GetCurrentPartFromId("acid"), s => s.Enable().SetInt(acidPart), skipValidate: true);
        }

        // 2. High-Heat Thermal Breakdown (> 85°C breaks remaining flocculant down)
        if (flocPart > 0 && temp > 85.0)
        {
            int heatBroken = Math.Min(flocPart, Math.Min(1500, Math.Max(1000, (int)(flocPart * 0.2))));
            flocPart -= heatBroken;
            totalRemoved += heatBroken;
        }

        if (totalRemoved <= 0)
        {
            return 0;
        }

        // Update remaining flocculant and convert neutralized volume into 90% Water / 10% Mineral
        container.ModifyTag(Liquid.GetCurrentPartFromId("flocculant"), s => s.Enable().SetInt(flocPart), skipValidate: true);

        int currentDebris = container.GetTagReadonly(Liquid.GetCurrentPartFromId("debris")).GetInt();
        int currentMineral = container.GetTagReadonly(Liquid.GetCurrentPartFromId("mineral")).GetInt();

        int debrisYield = (int)(totalRemoved * 0.90);
        int mineralYield = totalRemoved - debrisYield;

        container.ModifyTag(Liquid.GetCurrentPartFromId("debris"), s => s.Enable().SetInt(currentDebris + debrisYield), skipValidate: true);
        container.ModifyTag(Liquid.GetCurrentPartFromId("mineral"), s => s.Enable().SetInt(currentMineral + mineralYield), skipValidate: true);

        return 1;
    }
}