using FMODUnity;
using HarmonyLib;
using UnityEngine;

namespace BuildInSeatruckPlus.Patches
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

            // Parent to the segment that actually owns the surface we're building
            // on, NOT the interior's head segment. Player.currentInterior is always
            // the head cab (SeaTruckSegment.Enter calls EnterInterior(GetFirstSegment())),
            // so getCurrentSeaTruckSegment() can't tell which module the player is
            // standing in. Parenting everything to the head means items built in a
            // rear module still hang off the cab: while driving the whole train moves
            // as one so it looks fine, but docking disconnects the tail (modules become
            // independent) and rotates the cab 90 degrees, dragging module-built items
            // away with the cab. The surface's own segment is the correct parent, so
            // items ride and detach with the module that holds them.
            var targetSeg = Builder.placementTarget != null
                ? SeaTruckSegmentHelper.getParentSeaTruckSegment(Builder.placementTarget)
                : null;
            var parentSeg = targetSeg != null ? targetSeg : seg;
            go.transform.parent = parentSeg.transform;
            go.transform.position = Builder.placePosition;
            go.transform.rotation = Builder.placeRotation;

            var constructable = go.GetComponentInParent<Constructable>();
            constructable.SetState(false, true);
            if (Builder.ghostModel != null)
                Object.Destroy(Builder.ghostModel);
            constructable.SetIsInside(true);
            SkyEnvironmentChanged.Send(go, parentSeg);

            // Built items ride with the Seatruck as children of its compound
            // rigidbody. On their default (solid) layer their colliders snag on the
            // moonpool dock's static geometry while the player drives in, stopping
            // the hull before the automated docking sequence can take over. Moving
            // them to the Useable layer makes them interaction/raycast-only (still
            // usable and deconstructable) but no longer a physical obstacle.
            NeutralizeCollidersForDocking(go);

            Builder.ghostModel = null;
            Builder.prefab = null;
            Builder.canPlace = false;
            __result = true;
            return false;
        }

        static readonly int UseableLayer = LayerMask.NameToLayer("Useable");

        static void NeutralizeCollidersForDocking(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.gameObject.layer = UseableLayer;
        }
    }

    // Restore building inside the Seatruck (moonpool expansion) dock room. Its
    // surfaces carry the "DenyBuilding" tag, which vanilla CheckTag rejects. Allow
    // it only when the aimed collider belongs to a MoonpoolExpansionManager, so
    // every other DenyBuilding surface in the game stays protected.
    [HarmonyPatch(typeof(Builder), "CheckTag")]
    internal static class Builder_CheckTag_Patch
    {
        static bool Prefix(Collider c, ref bool __result)
        {
            if (c != null && c.GetComponentInParent<MoonpoolExpansionManager>() != null)
            {
                __result = true;
                return false;
            }
            return true; // not the dock: run vanilla CheckTag
        }
    }
}
