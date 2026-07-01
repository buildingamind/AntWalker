using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class InEditorFade : MonoBehaviour
{
    Renderer rend;

    // Start is called before the first frame update
    void Start()
    {
        rend = GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (!rend)
        {
            rend = GetComponent<Renderer>();
        }
        rend.sharedMaterial.color = new Color(rend.sharedMaterial.color.r,
                                            rend.sharedMaterial.color.g,
                                            rend.sharedMaterial.color.b,
                                            Mathf.Sin(Time.time *2f)/2f + 0.75f);
    }
}
