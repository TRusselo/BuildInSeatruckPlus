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
    }
}
