using UnityEngine;

// Trigger-based probe: uses a kinematic Rigidbody + isTrigger collider to detect overlaps.
public class TriggerProbe : MonoBehaviour
{
    public bool verbose = false;

    void OnTriggerEnter(Collider other)
    {
        if (verbose) Debug.Log($"TriggerProbe: {gameObject.name} trigger enter with {other.gameObject.name}");
    }

    void OnTriggerStay(Collider other)
    {
        if (verbose) Debug.Log($"TriggerProbe: {gameObject.name} trigger stay with {other.gameObject.name}");
    }

    void OnTriggerExit(Collider other)
    {
        if (verbose) Debug.Log($"TriggerProbe: {gameObject.name} trigger exit with {other.gameObject.name}");
    }
}
