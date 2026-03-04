using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CopyTranslation : MonoBehaviour
{
    public GameObject copyObject;

    private Vector3 referenceStart;
    private Vector3 startPos;

    // Start is called before the first frame update
    void Start()
    {
        startPos = transform.position;
        referenceStart = copyObject.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        float newX = referenceStart.x - copyObject.transform.position.x;
        float newY = referenceStart.y;
        float newZ = referenceStart.z - copyObject.transform.position.z;
        this.transform.position = startPos - new Vector3(newX, -newY, newZ);
    }
}
