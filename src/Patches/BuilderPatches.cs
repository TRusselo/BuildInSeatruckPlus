using FMODUnity;
using HarmonyLib;
using UnityEngine;

namespace SeatruckJukebox.Patches
{
    // The current game blocks all in-Seatruck building inside Builder.CheckAsSubModule:
    //   1. it returns false if the aimed surface is parented to a SeaTruckSegment;
    //   2. CheckFlags rejects it (the Seatruck is not a SubRoot);
    //   3. !Player.IsInSub() routes to ValidateOutdoor, which rejects the Seatruck's
    //      non-kinematic Rigidbody.
    // Rather than defeat each internal check, we own the whole method for the
    // Seatruck case: do a minimal raycast + CheckFlags + place-on-surface and allow
    // it. For every non-Seatruck case we return true so vanilla runs untouched.
    [HarmonyPatch(typeof(Builder), nameof(Builder.CheckAsSubModule))]
    internal static class Builder_CheckAsSubModule_Patch
    {
        static bool Prefix(out Collider hitCollider, ref bool __result)
        {
            hitCollider = null;

            if (SeaTruckSegmentHelper.getCurrentSeaTruckSegment() == null)
                return true; // not in a Seatruck: run vanilla CheckAsSubModule

            Builder.placementTarget = null;
            Transform aim = Builder.GetAimTransform();
            if (!Physics.Raycast(aim.position, aim.forward, out var hit, Builder.placeMaxDistance,
                                 Builder.placeLayerMask.value, QueryTriggerInteraction.Ignore))
            {
                __result = false;
                return false;
            }

            if (!Constructable.CheckFlags(Builder.allowedInBase, Builder.allowedInSub,
                                          Builder.allowedOutside, Builder.allowedUnderwater, hit.point))
            {
                __result = false;
                return false;
            }

            hitCollider = hit.collider;
            Builder.placementTarget = hitCollider.gameObject;
            Builder.SetPlaceOnSurface(hit, ref Builder.placePosition, ref Builder.placeRotation);
            __result = true;
            return false;
        }
    }

    // Place the constructed object parented to the Seatruck segment so it rides
    // with the vehicle. Only intercepts the Seatruck case; vanilla TryPlace handles
    // bases, the open world, and base-piece (ConstructableBase) building.
    [HarmonyPatch(typeof(Builder), nameof(Builder.TryPlace))]
    internal static class Builder_TryPlace_Patch
    {
        static bool Prefix(ref bool __result)
        {
            var seg = SeaTruckSegmentHelper.getCurrentSeaTruckSegment();
            if (seg == null)
                return true; // not in a Seatruck: run vanilla TryPlace

            if (Builder.prefab == null || !Builder.canPlace)
            {
                RuntimeManager.PlayOneShot("event:/bz/ui/item_error", default);
                __result = false;
                return false;
            }

            RuntimeManager.PlayOneShot("event:/tools/builder/place", Builder.ghostModel.transform.position);

            var go = Object.Instantiate(Builder.prefab);
            go.transform.parent = seg.transform;
            go.transform.position = Builder.placePosition;
            go.transform.rotation = Builder.placeRotation;

            var constructable = go.GetComponentInParent<Constructable>();
            constructable.SetState(false, true);
            if (Builder.ghostModel != null)
                Object.Destroy(Builder.ghostModel);
            constructable.SetIsInside(true);
            SkyEnvironmentChanged.Send(go, seg);

            Builder.ghostModel = null;
            Builder.prefab = null;
            Builder.canPlace = false;
            __result = true;
            return false;
        }
    }
}
