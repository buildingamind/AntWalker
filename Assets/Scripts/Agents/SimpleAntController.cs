using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleAntController : MonoBehaviour
{
    public float speed = 5.0f;
    public float rotationSpeed = 100.0f;
    public float randomRadius = 10.0f;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private LineRenderer lineRenderer;

    // Start is called before the first frame update
    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        // Configure LineRenderer (optional, but good practice)
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.positionCount = 2;
    }

    // Update is called once per frame
    void Update()
    {
        // Movement
        float translation = Input.GetAxis("Vertical") * speed;
        float rotation = Input.GetAxis("Horizontal") * rotationSpeed;

        translation *= Time.deltaTime;
        rotation *= Time.deltaTime;

        transform.Translate(0, 0, translation);
        transform.Rotate(0, rotation, 0);

        // Reset to original position
        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }

        // Random teleport
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Vector2 randomCircle = Random.insideUnitCircle * randomRadius;
            transform.position = startPosition + new Vector3(randomCircle.x, 0, randomCircle.y);
        }

        // Update LineRenderer
        if (lineRenderer != null)
        {
            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, transform.position);
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            // Toggle the Line Renderer on/off
            lineRenderer.enabled = !lineRenderer.enabled;
        }
    }
}
