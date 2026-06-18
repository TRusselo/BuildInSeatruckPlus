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
