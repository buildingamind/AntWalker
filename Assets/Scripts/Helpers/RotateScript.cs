using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotateScript : MonoBehaviour
{
    [Header("Should this object rotate? (True/False)")]
    public bool Rotate = true;

    [Header("The speed at which the object rotates: (Degrees/Second)")]
    public float rotateSpeed;

    [Header("Should the object occasionally change directions? (True/False)")]
    public bool randomlySwitchDirections;

    [Header("What is the longest duration a direction should be held? (Seconds)")]
    public float randomSwitchMax;

    [Header("What is the minimum duration a direction should be held? (Seconds)")]
    public float randomSwitchMin;

    [Header("[?] Should the object rotate based on Pivot or Center of Mass? (True/False)")]
    public bool UseCenterOfMass;

    [Header("[?] Along which Axis should the object rotate?")]
    public bool RotateX = false;
    public float rotateXSpeed;
    public bool RotateY = true;
    public float rotateYSpeed;
    public bool RotateZ = false;
    public float rotateZSpeed;

    private float randomSwitchTimer;
    private Renderer objectRenderer;

    // Start is called before the first frame update
    void Start()
    {
        randomSwitchTimer = Random.Range(randomSwitchMin, randomSwitchMax);
        objectRenderer = GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 direction = new Vector3();

        if (RotateX)
        {
            direction = Vector3.right;
        }
        if (RotateY)
        {
            direction = Vector3.up;
        }
        if (RotateZ)
        {
            direction = Vector3.forward;
        }

        randomSwitchMin = Mathf.Max(0, randomSwitchMin);
        randomSwitchMax = Mathf.Max(randomSwitchMin + 1f, randomSwitchMax);

        if (Rotate)
        {
            randomSwitchTimer -= Time.deltaTime;
            if (randomlySwitchDirections)
            {
                if (randomSwitchTimer < 0)

                {
                    // Randomly Turn off Rotation if the X, Y, or Z axis is selected                    
                    rotateXSpeed = Random.Range(-rotateSpeed, rotateSpeed);
                    rotateYSpeed = Random.Range(-rotateSpeed, rotateSpeed);
                    rotateZSpeed = Random.Range(-rotateSpeed, rotateSpeed);
                    randomSwitchTimer = Random.Range(randomSwitchMin, randomSwitchMax);
                }
            }
            direction = new Vector3(rotateXSpeed, rotateYSpeed, rotateZSpeed);

            if (!UseCenterOfMass || objectRenderer == null)
            {
                transform.Rotate(rotateSpeed * Time.deltaTime * direction);
            }
            else
            {
                transform.RotateAround(objectRenderer.bounds.center, direction, Time.deltaTime * rotateSpeed);
            }
        }
    }
}
