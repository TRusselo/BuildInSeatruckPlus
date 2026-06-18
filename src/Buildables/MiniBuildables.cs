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

            if (CraftData.GetBuilderIndex(cloneOf, out var group, out var category, out _))
                prefab.SetPdaGroupCategory(group, category);

            if (!Plugin.Config.UnlockBuildables)
                prefab.SetUnlock(TechType.Jukebox);

            prefab.Register();
            return info.TechType;
        }
    }
}
