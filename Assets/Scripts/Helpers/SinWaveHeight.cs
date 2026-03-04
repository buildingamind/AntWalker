using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SinWaveHeight : MonoBehaviour
{
    public Vector3 startPos;
    public float speed = 1f;
    public float amplitude = 1f;
    public float offset = 0f;

    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = new Vector3(transform.position.x, startPos.y + Mathf.Sin((Time.time + offset) * speed) * amplitude, transform.position.z);
    }
}
