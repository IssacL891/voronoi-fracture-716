using UnityEngine;

// Forwards collision/trigger events from any collider on this GameObject to the nearest parent PieceFracture.
[RequireComponent(typeof(Collider))]
public class CollisionForwarder : MonoBehaviour
{
    void OnCollisionEnter(Collision c)
    {
        var pf = GetComponentInParent<PieceFracture>();
        // If there's no PieceFracture yet, try generating via VoronoiFracture on the parent, then retry.
        if (pf == null)
        {
            var vf = GetComponentInParent<VoronoiFracture>();
            if (vf != null)
            {
                // Generate will convert the host to the whole piece and attach a PieceFracture.
                Debug.Log($"CollisionForwarder: calling Generate() on VoronoiFracture (host={vf.gameObject.name}) due to collision on {gameObject.name}");
                vf.Generate();
                pf = GetComponentInParent<PieceFracture>();
            }
        }
        if (pf == null) return;
        var contact = c.contacts.Length > 0 ? c.contacts[0].point : transform.position;
        var contactLocal3 = pf.transform.InverseTransformPoint(contact);
        var contact2 = pf.ProjectToPlane(contactLocal3, pf.fracturePlane);
        // Compute impact magnitude to respect piece split thresholds. Prefer impulse if available.
        float impact = 0f;
        try
        {
            if (c.impulse.sqrMagnitude > 0f) impact = c.impulse.magnitude;
            else impact = c.relativeVelocity.magnitude * (c.rigidbody != null ? c.rigidbody.mass : 1f);
        }
        catch { impact = c.relativeVelocity.magnitude; }
        Debug.Log($"CollisionForwarder: forwarding collision to PieceFracture on {pf.gameObject.name} at {contact2} impact={impact}");
        pf.ForceSplitAt(contact2, impact);
    }

    void OnTriggerEnter(Collider other)
    {
        var pf = GetComponentInParent<PieceFracture>();
        if (pf == null)
        {
            var vf = GetComponentInParent<VoronoiFracture>();
            if (vf != null)
            {
                Debug.Log($"CollisionForwarder: calling Generate() on VoronoiFracture (host={vf.gameObject.name}) due to trigger on {gameObject.name}");
                vf.Generate();
                pf = GetComponentInParent<PieceFracture>();
            }
        }
        if (pf == null) return;
        var contact = other != null ? other.ClosestPoint(transform.position) : transform.position;
        var contactLocal3 = pf.transform.InverseTransformPoint(contact);
        var contact2 = pf.ProjectToPlane(contactLocal3, pf.fracturePlane);
        Debug.Log($"CollisionForwarder: forwarding trigger to PieceFracture on {pf.gameObject.name} at {contact2} (trigger events have no impulse)");
        // Triggers have no impulse information; pass zero so splitThreshold prevents accidental splits. If you want triggers to force-split, change this behavior.
        pf.ForceSplitAt(contact2, 0f);
    }
}
