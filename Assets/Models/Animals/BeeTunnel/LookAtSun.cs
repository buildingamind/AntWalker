using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookAtSun : MonoBehaviour
{
    public GameObject agentTarget;
    public Light sunPosition;
    public MeshRenderer meshRenderer;

    [ReadOnly]
    public float alpha;

    // Start is called before the first frame update
    void Start()
    {
        meshRenderer = this.GetComponent<MeshRenderer>();
    }


    // Update is called once per frame
    void Update()
    {
        transform.rotation = sunPosition.gameObject.transform.rotation;
        transform.Rotate(transform.right, 90);
        alpha = Vector3.Dot(sunPosition.transform.forward, this.transform.parent.forward);
        meshRenderer.material.color = new Color(1f, 1f, 1f, 0.25f - alpha);

        // If a Raycast from the Sun doesn't collide with the AgentTarget, then the AgentTarget is in the shadow
        RaycastHit hit;
        if (Physics.Raycast(sunPosition.transform.position, -sunPosition.transform.forward, out hit))
        {
            if (hit.collider.gameObject == agentTarget)
            {
                meshRenderer.material.color = new Color(1f, 1f, 1f, 0.25f);
            }
        }
        // Therefore we should disable the polarized effect
        if (alpha < 0)
        {
            meshRenderer.material.color = new Color(1f, 1f, 1f, 0f);
        }
    }
}
