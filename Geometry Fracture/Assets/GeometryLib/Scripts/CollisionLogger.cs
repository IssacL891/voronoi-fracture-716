using UnityEngine;

// Simple, standalone collision/trigger logger you can attach to any GameObject to see contact events in the Console.
// Add this component to the whole piece, fragments, the falling cube, or the ground to observe interactions.
public class CollisionLogger : MonoBehaviour
{
    [Tooltip("Log OnCollision* events")] public bool logCollisions = true;
    [Tooltip("Log OnTrigger* events")] public bool logTriggers = true;
    [Tooltip("Also log OnCollisionStay/OnTriggerStay (can be chatty)")] public bool verboseStay = false;

    void OnCollisionEnter(Collision c)
    {
        if (!logCollisions) return;
        Debug.Log($"CollisionLogger: {gameObject.name} OnCollisionEnter with {c.gameObject.name} relVel={c.relativeVelocity.magnitude:F3} impulse={c.impulse.magnitude:F3} contacts={c.contacts.Length}");
    }

    void OnCollisionStay(Collision c)
    {
        if (!logCollisions || !verboseStay) return;
        Debug.Log($"CollisionLogger: {gameObject.name} OnCollisionStay with {c.gameObject.name} relVel={c.relativeVelocity.magnitude:F3} impulse={c.impulse.magnitude:F3}");
    }

    void OnCollisionExit(Collision c)
    {
        if (!logCollisions) return;
        Debug.Log($"CollisionLogger: {gameObject.name} OnCollisionExit with {c.gameObject.name}");
    }

    void OnTriggerEnter(Collider other)
    {
        if (!logTriggers) return;
        Debug.Log($"CollisionLogger: {gameObject.name} OnTriggerEnter with {other.gameObject.name}");
    }

    void OnTriggerStay(Collider other)
    {
        if (!logTriggers || !verboseStay) return;
        Debug.Log($"CollisionLogger: {gameObject.name} OnTriggerStay with {other.gameObject.name}");
    }

    void OnTriggerExit(Collider other)
    {
        if (!logTriggers) return;
        Debug.Log($"CollisionLogger: {gameObject.name} OnTriggerExit with {other.gameObject.name}");
    }
}
