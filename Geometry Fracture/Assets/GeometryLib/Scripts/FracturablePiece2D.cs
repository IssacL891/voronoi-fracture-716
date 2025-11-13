using UnityEngine;

// Attach to runtime-created fragments so they break when impacted strongly enough
public class FracturablePiece2D : MonoBehaviour
{
    [HideInInspector] public VoronoiFracture2D owner;
    [HideInInspector] public float breakImpactThreshold = 5f;
    [HideInInspector] public int siteCount = 6;
    [HideInInspector] public int remainingDepth = 1; // how many more recursive breaks allowed

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (owner == null) return;
        if (remainingDepth <= 0) return;
        var rb = GetComponent<Rigidbody2D>();
        float mass = rb != null ? rb.mass : 1f;
        float otherMass = collision.rigidbody != null ? collision.rigidbody.mass : mass;
        // approximate impact magnitude
        float impact = collision.relativeVelocity.magnitude * otherMass;
        if (impact >= breakImpactThreshold)
        {
            // prevent multiple triggers — capture depth first, then clear to avoid re-entry
            int depthBefore = remainingDepth;
            remainingDepth = 0;
            // create a temporary VoronoiFracture2D on this GameObject, copy settings, and run fracture
            var vf = gameObject.AddComponent<VoronoiFracture2D>();
            // copy relevant settings from owner
            vf.siteCount = Mathf.Max(3, siteCount);
            vf.siteJitter = owner.siteJitter;
            vf.randomSeed = owner.randomSeed ^ (int)Time.time;
            vf.fragmentMaterial = owner.fragmentMaterial;
            vf.generateOverlay = owner.generateOverlay;
            vf.overlayTextureSize = owner.overlayTextureSize;
            // runtime settings: decrement depth
            vf.enableRuntimeFracture = owner.enableRuntimeFracture;
            vf.runtimeSiteCount = owner.runtimeSiteCount;
            vf.runtimeBreakDepth = Mathf.Max(0, depthBefore - 1);
            vf.breakImpactThreshold = owner.breakImpactThreshold;

            // run fracture immediately
            vf.Fracture();

            // remove the temporary component; the GameObject will be deactivated by Fracture()
            Destroy(vf);
        }
    }
}
