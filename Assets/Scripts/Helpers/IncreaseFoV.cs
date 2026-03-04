using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class IncreaseFoV : MonoBehaviour
{

    Camera cam;
    public float magnitude = 1f;
    public float min = 50;
    public float max = 100;

    // Start is called before the first frame update
    void Start()
    {
        cam = GetComponent<Camera>();
        // min = cam.fieldOfView;
    }

    // Update is called once per frame
    void Update()
    {
        cam.fieldOfView += Time.deltaTime * magnitude;
        cam.orthographicSize += Time.deltaTime * magnitude;
        if ((cam.fieldOfView > max || cam.fieldOfView < min) && !cam.orthographic)
        {
            magnitude *= -1f;
        }
        if ((cam.orthographicSize > max || cam.orthographicSize < min) && cam.orthographic)
        {
            magnitude *= -1f;
        }
    }
}
