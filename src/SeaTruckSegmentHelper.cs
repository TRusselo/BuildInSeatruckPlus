using UnityEngine;

namespace BuildInSeatruckPlus
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

        private static readonly int UseableLayer = LayerMask.NameToLayer("Useable");

        // Move every collider under an object to the Useable layer so it stays
        // usable/raycastable but no longer physically obstructs the Seatruck as it
        // drives into (or out of) the moonpool dock. Used for both placed buildables
        // and plants that appear in planters after placement.
        public static void NeutralizeCollidersForDocking(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.gameObject.layer = UseableLayer;
        }
    }
}
