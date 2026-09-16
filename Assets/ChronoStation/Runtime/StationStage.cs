using UnityEngine;

namespace ChronoStation
{
    public sealed class StationStage : MonoBehaviour
    {
        public Transform entry, terminal, exit, gate;
        public StationTemporalObject device;
        public Renderer signal;
        public Vector3 Local(Vector3 world) => transform.InverseTransformPoint(world);
    }
}
