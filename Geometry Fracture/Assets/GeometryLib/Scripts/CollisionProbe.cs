using UnityEngine;

// Small helper to detect collisions on a generated object and optionally force-split via its PieceFracture.
public class CollisionProbe : MonoBehaviour
{
    public bool verbose = false;
    public bool autoSplit = false;

    void OnCollisionEnter(Collision c)
    {
        if (verbose) Debug.Log($"CollisionProbe: {gameObject.name} collision enter with {c.gameObject.name} impulse={c.impulse.magnitude} relVel={c.relativeVelocity.magnitude}");
        if (autoSplit)
        {
            var pf = GetComponent<PieceFracture>();
            if (pf != null)
            {
                var contact = c.contacts.Length > 0 ? c.contacts[0].point : transform.position;
                var contactLocal = transform.InverseTransformPoint(contact);
                var p2 = pf.ProjectToPlane(contactLocal, pf.fracturePlane);
                pf.ForceSplitAt(p2, Mathf.Max(c.impulse.magnitude, c.relativeVelocity.magnitude));
            }
        }
    }

    void OnCollisionStay(Collision c)
    {
        if (verbose) Debug.Log($"CollisionProbe: {gameObject.name} collision stay with {c.gameObject.name} impulse={c.impulse.magnitude}");
    }
}
