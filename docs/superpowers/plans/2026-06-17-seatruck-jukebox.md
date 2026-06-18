# Seatruck Jukebox Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A BepInEx + Nautilus plugin that ports BluesKutya's "Build In Seatruck" mod and adds a 25%-scale Mini Jukebox + 50%-scale Mini Speaker buildable, plus gated R hotkeys, so the player can listen to unlocked jukebox music inside the Seatruck.

**Architecture:** One `net472` assembly. Harmony patches re-enable building inside Seatruck segments (ported from the QMod, with `Constructable.CheckFlags` reworked for its new static signature). Nautilus `CloneTemplate` clones the vanilla `Jukebox`/`Speaker` prefabs, shrinks them, and registers them as buildables. A polled MonoBehaviour drives the global `Jukebox` engine from a rebindable key while the player is inside a Seatruck/base. Mod options use a Nautilus `ConfigFile`.

**Tech Stack:** C# / .NET Framework 4.7.2, BepInEx 5, HarmonyX, Nautilus (com.snmodding.nautilus), `BepInEx.AssemblyPublicizer.MSBuild` (to access private game members directly), FMODUnity.

---

## Conventions & environment

- **Game root:** `/home/user/.local/share/Steam/steamapps/common/SubnauticaZero`
- **Managed DLLs:** `<gameroot>/SubnauticaZero_Data/Managed/`
- **BepInEx core:** `<gameroot>/BepInEx/core/`
- **Nautilus:** `<gameroot>/BepInEx/plugins/Nautilus/Nautilus.dll`
- **Project root:** `/home/user/SeatruckJukeboxMod/`
- **Deploy target:** `<gameroot>/BepInEx/plugins/SeatruckJukebox/SeatruckJukebox.dll`
- **Build command (used throughout):**
  `dotnet build /home/user/SeatruckJukeboxMod/SeatruckJukebox.csproj -c Release`
  The csproj copies the DLL to the deploy target on successful build (post-build step).
- **Load check (used throughout):** after launching the game once,
  `grep -iE "SeatruckJukebox|Nautilus|error|exception" "<gameroot>/BepInEx/LogOutput.log" | tail -40`
- Because this is a Unity mod against a closed-source DLL, there is no unit-test
  harness. Each task's verification = **compiles** + **loads without errors** +
  the stated **in-game manual check**. "Commit" steps assume a git repo in the
  project root (created in Task 0).

### Publicizer note
The csproj marks `Assembly-CSharp` with `Publicize="true"`. All otherwise-private
game members used below (`Builder.prefab`, `Builder.canPlace`, `Builder.ghostModel`,
`Builder.placePosition/placeRotation/placementTarget/allowedOutside`,
`JukeboxInstance.all`, `JukeboxInstance.file`) are therefore directly accessible.

### File structure
```
/home/user/SeatruckJukeboxMod/
  SeatruckJukebox.csproj
  src/
    Plugin.cs                       # BepInEx entry point
    Config.cs                       # Nautilus options ConfigFile
    SeaTruckSegmentHelper.cs        # ported helper
    Patches/
      BuilderPatches.cs             # CheckTag, TryPlace, ValidateOutdoor
      ConstructablePatches.cs       # CheckFlags (reworked)
      PowerAndEntryPatches.cs       # PowerConsumer.IsPowered, SeaTruckSegment.CanEnter
    Buildables/
      MiniBuildables.cs             # Mini Jukebox + Mini Speaker registration
    HotkeyController.cs             # R play/stop/next, gated to interiors
  docs/superpowers/...
```

---

## Task 0: Project scaffold + empty plugin loads

**Files:**
- Create: `/home/user/SeatruckJukeboxMod/SeatruckJukebox.csproj`
- Create: `/home/user/SeatruckJukeboxMod/src/Plugin.cs`
- Create: `/home/user/SeatruckJukeboxMod/.gitignore`

- [ ] **Step 1: Create the csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <AssemblyName>SeatruckJukebox</AssemblyName>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Configuration>Release</Configuration>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <GameManaged>/home/user/.local/share/Steam/steamapps/common/SubnauticaZero/SubnauticaZero_Data/Managed</GameManaged>
    <BepInExCore>/home/user/.local/share/Steam/steamapps/common/SubnauticaZero/BepInEx/core</BepInExCore>
    <NautilusDir>/home/user/.local/share/Steam/steamapps/common/SubnauticaZero/BepInEx/plugins/Nautilus</NautilusDir>
    <DeployDir>/home/user/.local/share/Steam/steamapps/common/SubnauticaZero/BepInEx/plugins/SeatruckJukebox</DeployDir>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />
    <PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.3" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(GameManaged)/Assembly-CSharp.dll</HintPath>
      <Publicize>true</Publicize>
      <Private>false</Private>
    </Reference>
    <Reference Include="Assembly-CSharp-firstpass"><HintPath>$(GameManaged)/Assembly-CSharp-firstpass.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Nautilus"><HintPath>$(NautilusDir)/Nautilus.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="BepInEx"><HintPath>$(BepInExCore)/BepInEx.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="0Harmony"><HintPath>$(BepInExCore)/0Harmony.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="FMODUnity"><HintPath>$(GameManaged)/FMODUnity.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Unity.Addressables"><HintPath>$(GameManaged)/Unity.Addressables.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine"><HintPath>$(GameManaged)/UnityEngine.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.CoreModule"><HintPath>$(GameManaged)/UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.PhysicsModule"><HintPath>$(GameManaged)/UnityEngine.PhysicsModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.InputLegacyModule"><HintPath>$(GameManaged)/UnityEngine.InputLegacyModule.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="UnityEngine.UI"><HintPath>$(GameManaged)/UnityEngine.UI.dll</HintPath><Private>false</Private></Reference>
    <Reference Include="Unity.TextMeshPro"><HintPath>$(GameManaged)/Unity.TextMeshPro.dll</HintPath><Private>false</Private></Reference>
  </ItemGroup>

  <Target Name="Deploy" AfterTargets="Build">
    <MakeDir Directories="$(DeployDir)" />
    <Copy SourceFiles="$(OutputPath)SeatruckJukebox.dll" DestinationFolder="$(DeployDir)" />
  </Target>
</Project>
```

- [ ] **Step 2: Create `.gitignore`**

```
bin/
obj/
```

- [ ] **Step 3: Create the entry point `src/Plugin.cs`**

```csharp
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace SeatruckJukebox
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency("com.snmodding.nautilus")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.tristyn.seatruckjukebox";
        public const string NAME = "Seatruck Jukebox";
        public const string VERSION = "1.0.0";

        public static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;
            new Harmony(GUID).PatchAll();
            Log.LogInfo($"{NAME} {VERSION} loaded.");
        }
    }
}
```

- [ ] **Step 4: Init git, build, verify compile**

Run:
```bash
cd /home/user/SeatruckJukeboxMod && git init -q && \
dotnet build SeatruckJukebox.csproj -c Release
```
Expected: `Build succeeded`, and `BepInEx/plugins/SeatruckJukebox/SeatruckJukebox.dll` exists.

- [ ] **Step 5: Launch game once, verify plugin loads**

Manual: launch the game (or ask the user to). Then run the load check command.
Expected: a line `Seatruck Jukebox 1.0.0 loaded.` and no exceptions mentioning SeatruckJukebox.

- [ ] **Step 6: Commit**

```bash
cd /home/user/SeatruckJukeboxMod && git add -A && \
git commit -q -m "feat: scaffold BepInEx/Nautilus plugin that loads"
```

---

## Task 1: Config + Nautilus options menu

**Files:**
- Create: `/home/user/SeatruckJukeboxMod/src/Config.cs`
- Modify: `/home/user/SeatruckJukeboxMod/src/Plugin.cs`

- [ ] **Step 1: Create `src/Config.cs`**

```csharp
using Nautilus.Json;
using Nautilus.Options.Attributes;
using UnityEngine;

namespace SeatruckJukebox
{
    [Menu("Seatruck Jukebox")]
    public class Config : ConfigFile
    {
        [Keybind("Playback hotkey (in Seatruck / base)")]
        public KeyCode Hotkey = KeyCode.R;

        [Slider("Long-press seconds", 0.1f, 2f, DefaultValue = 0.5f, Step = 0.05f, Format = "{0:F2}")]
        public float LongPressSeconds = 0.5f;

        [Toggle("Cheap recipe (1 Titanium) - restart required")]
        public bool CheapRecipe = false;

        [Toggle("Unlock buildables at start - restart required")]
        public bool UnlockBuildables = false;

        [Toggle("New jukeboxes start in shuffle mode")]
        public bool DefaultShuffle = true;

        [Toggle("Show 'Now playing' notifications")]
        public bool ShowTrackName = true;
    }
}
```

- [ ] **Step 2: Register config in `Plugin.cs`**

Add `using Nautilus.Handlers;` and a static field + registration. Replace the
`Awake` body so it reads:

```csharp
        public static Config Config { get; private set; }

        private void Awake()
        {
            Log = Logger;
            Config = OptionsPanelHandler.RegisterModOptions<Config>();
            new Harmony(GUID).PatchAll();
            Log.LogInfo($"{NAME} {VERSION} loaded.");
        }
```

- [ ] **Step 3: Build**

Run: `dotnet build /home/user/SeatruckJukeboxMod/SeatruckJukebox.csproj -c Release`
Expected: `Build succeeded`.

- [ ] **Step 4: In-game verify options appear**

Manual: launch game → Options → Mod Options. Expected: a "Seatruck Jukebox"
section with the hotkey binder, long-press slider, and four toggles. Changing
and reopening persists values (written to `BepInEx/config/...json`).

- [ ] **Step 5: Commit**

```bash
cd /home/user/SeatruckJukeboxMod && git add -A && \
git commit -q -m "feat: add config and Nautilus options menu"
```

---

## Task 2: Port SeaTruckSegmentHelper

**Files:**
- Create: `/home/user/SeatruckJukeboxMod/src/SeaTruckSegmentHelper.cs`

- [ ] **Step 1: Create the helper**

```csharp
using UnityEngine;

namespace SeatruckJukebox
{
    internal static class SeaTruckSegmentHelper
    {
        public static bool isPlayerInSeaTruckSegment()
        {
            return Player.main != null && Player.main.currentInterior is SeaTruckSegment;
        }

        public static SeaTruckSegment getCurrentSeaTruckSegment()
        {
            return Player.main != null ? Player.main.currentInterior as SeaTruckSegment : null;
        }

        public static SeaTruckSegment getParentSeaTruckSegment(GameObject gameObject)
        {
            return gameObject.GetComponentInParent<SeaTruckSegment>();
        }

        public static SubRoot getCurrentSeaTruckSegmentSubRoot()
        {
            var seg = getCurrentSeaTruckSegment();
            return seg == null ? null : seg.gameObject.GetComponent<SubRoot>();
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build /home/user/SeatruckJukeboxMod/SeatruckJukebox.csproj -c Release`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
cd /home/user/SeatruckJukeboxMod && git add -A && \
git commit -q -m "feat: port SeaTruckSegmentHelper"
```

---

## Task 3: Port Builder patches (CheckTag, TryPlace, ValidateOutdoor)

**Files:**
- Create: `/home/user/SeatruckJukeboxMod/src/Patches/BuilderPatches.cs`

- [ ] **Step 1: Create the patch file**

```csharp
using FMODUnity;
using HarmonyLib;
using UnityEngine;

namespace SeatruckJukebox.Patches
{
    [HarmonyPatch(typeof(Builder), nameof(Builder.CheckTag))]
    internal static class Builder_CheckTag_Patch
    {
        static bool Prefix(Collider c, ref bool __result)
        {
            __result = c != null && c.gameObject != null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Builder), nameof(Builder.TryPlace))]
    internal static class Builder_TryPlace_Patch
    {
        static bool Prefix(ref bool __result)
        {
            if (Builder.prefab == null || !Builder.canPlace)
            {
                RuntimeManager.PlayOneShot("event:/bz/ui/item_error", default);
                __result = false;
                return false;
            }

            RuntimeManager.PlayOneShot("event:/tools/builder/place", Builder.ghostModel.transform.position);

            var constructableBase = Builder.ghostModel.GetComponentInParent<ConstructableBase>();
            if (constructableBase != null)
            {
                var ghost = Builder.ghostModel.GetComponent<BaseGhost>();
                ghost.Place();
                if (ghost.TargetBase != null)
                    constructableBase.transform.SetParent(ghost.TargetBase.transform, true);
                ((Constructable)constructableBase).SetState(false, true);
            }
            else
            {
                var go = Object.Instantiate(Builder.prefab);
                bool isBase = false, isCyclops = false;

                SubRoot sub = Player.main.GetCurrentSub();
                if (sub == null)
                    sub = SeaTruckSegmentHelper.getCurrentSeaTruckSegmentSubRoot();

                if (sub != null)
                {
                    isBase = sub.isBase;
                    isCyclops = sub.isCyclops;
                    go.transform.parent = sub.GetModulesRoot();
                }
                else if (Builder.placementTarget != null && Builder.allowedOutside)
                {
                    var targetSub = Builder.placementTarget.GetComponentInParent<SubRoot>();
                    if (targetSub != null)
                        go.transform.parent = targetSub.GetModulesRoot();
                }

                go.transform.position = Builder.placePosition;
                go.transform.rotation = Builder.placeRotation;

                var constructable = go.GetComponentInParent<Constructable>();
                constructable.SetState(false, true);
                if (Builder.ghostModel != null)
                    Object.Destroy(Builder.ghostModel);
                constructable.SetIsInside(isBase || isCyclops);
                SkyEnvironmentChanged.Send(go, sub);
            }

            Builder.ghostModel = null;
            Builder.prefab = null;
            Builder.canPlace = false;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Builder), nameof(Builder.ValidateOutdoor))]
    internal static class Builder_ValidateOutdoor_Patch
    {
        static bool Prefix(GameObject hitObject, ref bool __result)
        {
            var rb = hitObject.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) { __result = false; return false; }

            var subRoot = hitObject.GetComponent<SubRoot>();
            var baseComp = hitObject.GetComponent<Base>();
            if (subRoot != null && baseComp == null)
            {
                __result = SeaTruckSegmentHelper.isPlayerInSeaTruckSegment();
                return false;
            }

            if (hitObject.GetComponent<Pickupable>() != null) { __result = false; return false; }

            var lm = hitObject.GetComponent<LiveMixin>();
            __result = lm == null || !lm.destroyOnDeath;
            return false;
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build /home/user/SeatruckJukeboxMod/SeatruckJukebox.csproj -c Release`
Expected: `Build succeeded` (publicizer makes `prefab`/`canPlace`/`ghostModel`/
`placePosition`/`placeRotation`/`placementTarget`/`allowedOutside` accessible).

- [ ] **Step 3: Commit**

```bash
cd /home/user/SeatruckJukeboxMod && git add -A && \
git commit -q -m "feat: port Builder placement patches"
```

---

## Task 4: Constructable.CheckFlags (reworked) + power/entry patches

**Files:**
- Create: `/home/user/SeatruckJukeboxMod/src/Patches/ConstructablePatches.cs`
- Create: `/home/user/SeatruckJukeboxMod/src/Patches/PowerAndEntryPatches.cs`

Background: the original patched an instance `Constructable.CheckFlags` and forced
the segment to be a SubRoot. The current method is
`static bool CheckFlags(bool allowedInBase, bool allowedInSub, bool allowedOutside, bool allowedUnderwater, Vector3 hitPoint)`.
We mirror the original intent: when the player is in a (non-connected, non-docked)
Seatruck segment, ensure it has a `SubRoot` flagged as a sub, then approve based
on `allowedInSub`.

- [ ] **Step 1: Create `ConstructablePatches.cs`**

```csharp
using HarmonyLib;

namespace SeatruckJukebox.Patches
{
    [HarmonyPatch(typeof(Constructable), nameof(Constructable.CheckFlags))]
    internal static class Constructable_CheckFlags_Patch
    {
        static bool Prefix(bool allowedInBase, bool allowedInSub, bool allowedOutside,
                           bool allowedUnderwater, UnityEngine.Vector3 hitPoint, ref bool __result)
        {
            var seg = SeaTruckSegmentHelper.getCurrentSeaTruckSegment();
            if (seg == null || seg.isFrontConnected || seg.isRearConnected)
                return true; // not in a standalone seatruck segment: run vanilla

            var sub = SeaTruckSegmentHelper.getCurrentSeaTruckSegmentSubRoot();
            if (sub == null)
                sub = seg.gameObject.AddComponent<SubRoot>();
            if (sub == null)
                return true;

            sub.isCyclops = true;
            sub.modulesRoot = seg.transform;

            __result = allowedInSub;
            return false;
        }
    }
}
```

- [ ] **Step 2: Create `PowerAndEntryPatches.cs`**

```csharp
using HarmonyLib;
using UnityEngine;

namespace SeatruckJukebox.Patches
{
    [HarmonyPatch(typeof(PowerConsumer), nameof(PowerConsumer.IsPowered))]
    internal static class PowerConsumer_IsPowered_Patch
    {
        static bool Prefix(PowerConsumer __instance, ref bool __result)
        {
            var seg = SeaTruckSegmentHelper.getParentSeaTruckSegment(__instance.gameObject);
            if (seg == null)
                return true;
            __result = ((IInteriorSpace)seg).CanBreathe();
            return false;
        }
    }

    [HarmonyPatch(typeof(SeaTruckSegment), nameof(SeaTruckSegment.CanEnter))]
    internal static class SeaTruckSegment_CanEnter_Patch
    {
        static bool Prefix(ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build /home/user/SeatruckJukeboxMod/SeatruckJukebox.csproj -c Release`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
cd /home/user/SeatruckJukeboxMod && git add -A && \
git commit -q -m "feat: rework CheckFlags + port power/entry patches"
```

---

## Task 5: Verify Build-In-Seatruck end to end

No code. This validates Tasks 2-4 together before adding new content.

- [ ] **Step 1: Launch game, load check**

Run the load check command. Expected: `Seatruck Jukebox 1.0.0 loaded.`, no
exceptions from any `SeatruckJukebox.Patches.*` patch during scene load.

- [ ] **Step 2: In-game build test**

Manual: get in a Seatruck (creative/console `item seatruck` if testing), equip the
Habitat Builder, and place a vanilla interior item (e.g. a Wall Locker / Bench)
inside the cab. Expected: ghost turns green and the item places and stays parented
to the Seatruck; it remains when the Seatruck moves. Power-consuming items report
powered while you can breathe inside.

- [ ] **Step 3: Commit (docs note only if any fix needed)**

If a fix was required, commit it:
```bash
cd /home/user/SeatruckJukeboxMod && git add -A && \
git commit -q -m "fix: build-in-seatruck verification fixes"
```

---

## Task 6: Mini Jukebox + Mini Speaker buildables

**Files:**
- Create: `/home/user/SeatruckJukeboxMod/src/Buildables/MiniBuildables.cs`
- Modify: `/home/user/SeatruckJukeboxMod/src/Plugin.cs`

Design points:
- Clone vanilla `TechType.Jukebox` (scale 0.25) and `TechType.Speaker` (scale 0.5).
- Liberal collision: shrink every `ConstructableBounds.bounds.extents` on the clone
  to near-zero so the placement overlap check almost never blocks.
- Recipe: copy the vanilla Jukebox recipe via `CraftDataHandler.GetRecipeData`;
  if `Config.CheapRecipe`, use `1 x Titanium`.
- Build-menu placement: copy the vanilla Jukebox group/category via
  `CraftData.GetBuilderIndex`.
- Unlock: if `Config.UnlockBuildables` is **false**, call `SetUnlock(TechType.Jukebox)`
  (locked until the player unlocks the vanilla Jukebox). If **true**, omit
  `SetUnlock` so the item is unlocked at start (Nautilus default).
- `DefaultShuffle`: applied per-instance in Task 7 when playback starts (not here).

- [ ] **Step 1: Create `MiniBuildables.cs`**

```csharp
using System.Collections.Generic;
using Nautilus.Assets;
using Nautilus.Assets.Gadgets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Crafting;
using Nautilus.Handlers;
using UnityEngine;

namespace SeatruckJukebox.Buildables
{
    internal static class MiniBuildables
    {
        public static TechType MiniJukebox { get; private set; }
        public static TechType MiniSpeaker { get; private set; }

        public static void Register()
        {
            MiniJukebox = Make(
                classId: "MiniJukebox",
                friendly: "Mini Jukebox",
                description: "A compact jukebox that fits inside a Seatruck.",
                cloneOf: TechType.Jukebox,
                scale: 0.25f);

            MiniSpeaker = Make(
                classId: "MiniSpeaker",
                friendly: "Mini Speaker",
                description: "A compact speaker for tight interiors.",
                cloneOf: TechType.Speaker,
                scale: 0.5f);
        }

        private static TechType Make(string classId, string friendly, string description,
                                     TechType cloneOf, float scale)
        {
            var info = PrefabInfo.WithTechType(classId, friendly, description);
            var prefab = new CustomPrefab(info);

            var clone = new CloneTemplate(info, cloneOf);
            clone.ModifyPrefab += go =>
            {
                go.transform.localScale *= scale;
                foreach (var cb in go.GetComponentsInChildren<ConstructableBounds>(true))
                    cb.bounds.extents = Vector3.one * 0.02f;
                Plugin.Log.LogInfo($"{classId}: scaled to {scale} with liberal bounds.");
            };
            prefab.SetGameObject(clone);

            // Recipe
            RecipeData recipe;
            if (Plugin.Config.CheapRecipe)
            {
                recipe = new RecipeData(new Ingredient(TechType.Titanium, 1));
            }
            else
            {
                recipe = CraftDataHandler.GetRecipeData(cloneOf)
                         ?? new RecipeData(new Ingredient(TechType.Titanium, 1));
            }
            prefab.SetRecipe(recipe);

            // Build-menu group/category from the original buildable
            if (CraftData.GetBuilderIndex(cloneOf, out var group, out var category, out _))
                prefab.SetPdaGroupCategory(group, category);

            // Unlock behaviour
            if (!Plugin.Config.UnlockBuildables)
                prefab.SetUnlock(TechType.Jukebox);

            prefab.Register();
            return info.TechType;
        }
    }
}
```

- [ ] **Step 2: Call `MiniBuildables.Register()` from `Plugin.Awake`**

In `Plugin.cs`, add `using SeatruckJukebox.Buildables;` and insert the registration
call **after** config registration and **before/after** `PatchAll` (order does not
matter), so `Awake` reads:

```csharp
        private void Awake()
        {
            Log = Logger;
            Config = OptionsPanelHandler.RegisterModOptions<Config>();
            MiniBuildables.Register();
            new Harmony(GUID).PatchAll();
            Log.LogInfo($"{NAME} {VERSION} loaded.");
        }
```

- [ ] **Step 3: Build**

Run: `dotnet build /home/user/SeatruckJukeboxMod/SeatruckJukebox.csproj -c Release`
Expected: `Build succeeded`.

- [ ] **Step 4: In-game verify buildables**

Manual: with `UnlockBuildables` ON (or after unlocking the vanilla Jukebox), open
the Habitat Builder. Expected: "Mini Jukebox" and "Mini Speaker" appear in the same
build-menu category as the vanilla Jukebox, build with the expected recipe, and
place at roughly quarter / half size. Place a Mini Jukebox inside the Seatruck; it
powers on (screen lit) and its clickable UI plays unlocked songs; place a Mini
Speaker nearby and confirm audio is clear inside the cab.

- [ ] **Step 5: Commit**

```bash
cd /home/user/SeatruckJukeboxMod && git add -A && \
git commit -q -m "feat: add Mini Jukebox and Mini Speaker buildables"
```

---

## Task 7: Hotkey controller (gated play/stop/next)

**Files:**
- Create: `/home/user/SeatruckJukeboxMod/src/HotkeyController.cs`
- Modify: `/home/user/SeatruckJukeboxMod/src/Plugin.cs`

Behaviour:
- Only acts when `Player.main` exists and the player is inside a `SeaTruckSegment`
  or any sub/base (`Player.main.GetCurrentSub() != null`).
- Resolves the `JukeboxInstance` whose speaker host matches the player's interior.
- Long-press (held >= `LongPressSeconds`): toggle play/stop. Fires once on crossing
  the threshold; the subsequent release does nothing.
- Short-press (released before threshold): next track + optional notification.
- Applies `Config.DefaultShuffle` to an instance the first time we start it.

- [ ] **Step 1: Create `HotkeyController.cs`**

```csharp
using UnityEngine;

namespace SeatruckJukebox
{
    internal class HotkeyController : MonoBehaviour
    {
        private float _downTime = -1f;
        private bool _longFired;

        private void Update()
        {
            var cfg = Plugin.Config;
            if (cfg == null || Player.main == null) return;
            if (!PlayerInInterior()) { _downTime = -1f; _longFired = false; return; }

            if (Input.GetKeyDown(cfg.Hotkey))
            {
                _downTime = Time.unscaledTime;
                _longFired = false;
            }

            if (_downTime >= 0f && !_longFired && Input.GetKey(cfg.Hotkey)
                && Time.unscaledTime - _downTime >= cfg.LongPressSeconds)
            {
                _longFired = true;
                TogglePlayStop();
            }

            if (Input.GetKeyUp(cfg.Hotkey))
            {
                if (_downTime >= 0f && !_longFired)
                    NextTrack();
                _downTime = -1f;
                _longFired = false;
            }
        }

        private static bool PlayerInInterior()
        {
            return Player.main.currentInterior is SeaTruckSegment
                   || Player.main.GetCurrentSub() != null;
        }

        private static JukeboxInstance ResolveInstance()
        {
            var playerHost = Speaker.GetHost(Player.main.currentInterior as MonoBehaviour);
            if (playerHost == null && Player.main.GetCurrentSub() != null)
                playerHost = Speaker.GetHost(Player.main.GetCurrentSub());

            foreach (var ji in JukeboxInstance.all)
            {
                if (ji == null) continue;
                if (Speaker.IsSameHost(Speaker.GetHost(ji), playerHost))
                    return ji;
            }
            return Jukebox.instance;
        }

        private void TogglePlayStop()
        {
            var ji = ResolveInstance();
            if (ji == null) return;

            if (Jukebox.isStartingOrPlaying && Jukebox.instance == ji)
            {
                Jukebox.Stop();
            }
            else
            {
                ji.shuffle = Plugin.Config.DefaultShuffle;
                Jukebox.Play(ji);
            }
        }

        private void NextTrack()
        {
            var ji = ResolveInstance();
            if (ji == null) return;

            string next = Jukebox.GetNext(ji, true);
            if (string.IsNullOrEmpty(next)) return;
            ji.file = next;
            Jukebox.Play(ji);

            if (Plugin.Config.ShowTrackName)
            {
                var label = Jukebox.GetInfo(next).label;
                if (!string.IsNullOrEmpty(label))
                    ErrorMessage.AddMessage($"Now playing: {label}");
            }
        }
    }
}
```

- [ ] **Step 2: Attach the controller in `Plugin.Awake`**

Add this line at the end of `Awake` (the `Plugin` is a `MonoBehaviour`, so its own
GameObject persists):

```csharp
            gameObject.AddComponent<HotkeyController>();
```

- [ ] **Step 3: Build**

Run: `dotnet build /home/user/SeatruckJukeboxMod/SeatruckJukebox.csproj -c Release`
Expected: `Build succeeded`.

- [ ] **Step 4: In-game verify hotkey**

Manual: inside a Seatruck with a Mini Jukebox built, press and hold R (~0.5s):
playback starts; hold again: stops. Tap R: skips to next track and shows
"Now playing: <name>". Rebind the key in Mod Options and confirm the new key works
and R no longer does. Outside any interior (swimming), R does nothing.

- [ ] **Step 5: Commit**

```bash
cd /home/user/SeatruckJukeboxMod && git add -A && \
git commit -q -m "feat: add gated play/stop/next hotkey controller"
```

---

## Task 8: Housekeeping — remove old QMod + final verification

**Files:**
- Delete (game folder): old QMod `BuildInSeatruck/` and the two zips.

- [ ] **Step 1: Disable the conflicting old QMod**

The old QModManager `BuildInSeatruck` cannot load without QModManager but its
presence is confusing and risks future conflicts. Move it aside:

```bash
cd "/home/user/.local/share/Steam/steamapps/common/SubnauticaZero" && \
mkdir -p _disabled_mods && \
mv BuildInSeatruck _disabled_mods/ 2>/dev/null; \
mv "BepInEx/plugins/Build In Seatruck-287-1-0-2-1660523817.zip" _disabled_mods/ 2>/dev/null; \
mv "BepInEx/plugins/Build In Seatruck-287-1-0-4-1664413243.zip" _disabled_mods/ 2>/dev/null; \
ls _disabled_mods
```
Expected: the three items now live in `_disabled_mods/`.

- [ ] **Step 2: Clean rebuild + deploy**

Run:
```bash
dotnet build /home/user/SeatruckJukeboxMod/SeatruckJukebox.csproj -c Release && \
ls -la "/home/user/.local/share/Steam/steamapps/common/SubnauticaZero/BepInEx/plugins/SeatruckJukebox/"
```
Expected: `SeatruckJukebox.dll` present and freshly dated.

- [ ] **Step 3: Full in-game regression**

Manual checklist (single play session):
1. Plugin loads, no exceptions (load check command).
2. Mod Options shows all six settings; values persist.
3. Build a non-jukebox item inside the Seatruck (port still works).
4. Build Mini Jukebox + Mini Speaker; correct size, recipe, category, unlock gating.
5. Jukebox plays unlocked songs via its own UI; audible in the cab.
6. R long-press start/stop; R short-press next + "Now playing" notice.
7. Rebound hotkey works; hotkey is inert while swimming outside.
8. Toggle Cheap recipe / Unlock buildables, restart, confirm they take effect.

- [ ] **Step 4: Final commit + tag**

```bash
cd /home/user/SeatruckJukeboxMod && git add -A && \
git commit -q -m "chore: disable legacy QMod; final 1.0.0" && \
git tag v1.0.0
```

---

## Self-review against spec

- **Build-In-Seatruck port** → Tasks 2–4 (helper + all 6 patches; `CheckFlags`
  reworked for its static signature). Verified in Task 5.
- **Mini Jukebox 25% / Mini Speaker 50% + liberal collision** → Task 6.
- **Recipe = vanilla, cheap-recipe checkbox** → Task 6 (recipe) + Task 1 (toggle).
- **Locked at start, checkbox to unlock** → Task 6 unlock logic + Task 1 toggle.
- **Gated R hotkeys (Seatruck/base only), rebindable, long/short press,
  track-name notice** → Tasks 1 + 7.
- **BepInEx/Nautilus plugin, ships to BepInEx/plugins** → Task 0 csproj deploy.
- **Disable legacy QMod** → Task 8.
- No placeholders; member/type names (`MiniJukebox`/`MiniSpeaker`, `Config.*`,
  `ResolveInstance`, `TogglePlayStop`, `NextTrack`) are consistent across tasks.

### Known risks to watch during execution
- `ConstructableBounds.bounds` is an `OrientedBounds` struct; if the field/shape
  differs, set its extents however the struct exposes them (the goal is tiny
  extents). Adjust in Task 6 Step 1 if the build flags a member name.
- If a cloned full-size collider still blocks placement, also shrink/disable the
  prefab's physical `Collider`s in `ModifyPrefab`.
- If `ErrorMessage.AddMessage` reads oddly, swap to `Subtitles`/popup; cosmetic.
- Mini Speaker audio relies on the Seatruck sharing the player's speaker host; if
  inaudible, verify the instance parents under the segment (Task 3 already routes
  placement through `GetModulesRoot`).
