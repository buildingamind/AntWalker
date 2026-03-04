using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.AI;

public class AntRandomWalk : MonoBehaviour
{
    [Header("The number of times the ant has returned to the nest [ReadOnly]")]
    [ReadOnly] public int returns = 0;

    [Header("The number of steps the ant has taken [ReadOnly]")]
    [ReadOnly] public int step = 0;

    [Header("The per-step chance the ant will return towards the nest at the start of the simulation (%)")]
    public float initialTurnHomeChance = 50;

    [Header("The per-step chance the ant will turn towards home (%) [Always ReadOnly]")]
    [ReadOnly] public float turnHomeChance = 0;

    [Header("Whether the ant is currently pirouetting [Always ReadOnly]")]
    [ReadOnly] public bool isPirouetting = false;

    [Header("The value which increases the probability the ant will turn towards home over time (when past minNestDistance)")]
    [ReadOnly] public float returnHomeIncrease = 1f;

    [Header("The value which increases the probability the ant will turn towards home")]
    public float initialReturnHomeIncrease = 1f;

    [Header("The ratio by which the returnHomeIncrease is multiplied when the ant returns to the nest. Results in a geometric series.")]
    public float returnHomeRatio = 0.8f;

    public bool EnforceNestDistance = true; // If true, the ant will always turn away from the nest when it is within minNestDistance
    [Header("The minimum distance from the nest before the ant will start turning towards it")]
    public float minNestDistance = 0.5f;

    [Header("The speed at which the ant moves")]
    public float moveSpeed = 1.0f;

    [Header("The maximum number of degrees the ant can turn per step")]
    public float maxRotatePerStep = 1.0f;

    [Header("The chance the ant will perform a perrouette (%)")]
    public float perroutteChance = 0.05f;

    [Header("The odds that the ant will move forward on each step")]
    public float forwardChance = 0.8f;

    [Header("The value which increases the probability the ant will turn towards home over time")]
    private Vector3 homePosition;
    public Collider homeCollider;
    private ParticleSystem ps;
    public LogManager logManager;
    public TextMeshProUGUI textMeshProUGUI;
    private Rigidbody rb;
    private int pirouetteStep = 0;
    public int pirouetteSize = 18;
    private int pirouetteDirection;
    public NavMeshAgent navMeshAgent;
    public bool GridMode = false; // If true, the ant will survey around the nest in a grid space-filling pattern
    public Vector3[] gridLocations; // The locations of the grid spaces around the nest
    public Vector3 targetLocation; // The current destination the ant is moving towards
    public float tileSize = 1.0f; // The size of the grid tiles
    public float totalGridWidth = 500; // The size of the entire grid (width and height)
    [ReadOnly] public int gridIndex = -1; // The current grid space index

    // Start is called before the first frame update
    void Start()
    {
        homePosition = homeCollider.transform.position;
        ps = GetComponent<ParticleSystem>();
        rb = GetComponent<Rigidbody>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        turnHomeChance = initialTurnHomeChance;
        returnHomeIncrease = initialReturnHomeIncrease;
        gridLocations = GetGridLocations(homePosition, tileSize, totalGridWidth);
        gridIndex = Random.Range(0, gridLocations.Length);
    }

    void UpdateUI()
    {
        Debug.DrawLine(transform.position, homePosition, Color.green);
        textMeshProUGUI.text = "Steps: " + step + "\n" +
                               "Returns: " + returns + "\n" +
                               "Distance: " + Mathf.Round(Vector3.Distance(transform.position, homePosition)) + "\n" +
                               "Pirouetting: " + isPirouetting + "\n" +
                               "Chance to Turn Home: " + Mathf.Round(Mathf.Min(turnHomeChance, 100)) + "%\n" +
                               "Homing Rate Strength: " + returnHomeIncrease + "\n" +
                               "Pirouette Chance: " + perroutteChance + "%\n" +
                               "(X, Y) Coordinate: (" + Mathf.Round(transform.localPosition.x) + ", " + Mathf.Round(transform.localPosition.z) + ")\n";
    }

    public Vector3[] GetGridLocations(Vector3 center, float tileSize, float totalGridWidth){
        List<Vector3> gridLocations = new List<Vector3>();
        int numTiles = (int) (totalGridWidth / tileSize);
        float halfGridWidth = totalGridWidth / 2;
        for (int i = 0; i < numTiles; i++){
            for (int j = 0; j < numTiles; j++){
                float x = center.x - halfGridWidth + i * tileSize;
                float z = center.z - halfGridWidth + j * tileSize;
                gridLocations.Add(new Vector3(x, center.y, z));
            }
        }
        return gridLocations.ToArray();
    }

    void LateUpdate()
    {
        UpdateUI();
        // Draw the grid locations as Raycasts
        if (GridMode)
        {
            foreach (Vector3 location in gridLocations)
            {
                Debug.DrawRay(location, Vector3.up * 10, Color.blue);
            }
        }
    }

    void FixedUpdate()
    {
        // Align the Ant with the NavMeshAgent's Surface Normal
        // if (navMeshAgent.isOnNavMesh)
        // {
        //     Vector3 normal = Vector3.up;
        //     if (Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, 1.0f))
        //     {
        //         normal = hit.normal;
        //     }
        //     transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(navMeshAgent.velocity.normalized, normal), 0.1f);
        // }

        // Set the Timescale with F1-F12 Keys
        if (Input.GetKeyDown(KeyCode.F1))
        {
            Time.timeScale = 1;
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Time.timeScale = 2;
        }
        if (Input.GetKeyDown(KeyCode.F3))
        {
            Time.timeScale = 3;
        }
        if (Input.GetKeyDown(KeyCode.F4))
        {
            Time.timeScale = 4;
        }
        if (Input.GetKeyDown(KeyCode.F5))
        {
            Time.timeScale = 5;
        }
        if (Input.GetKeyDown(KeyCode.F6))
        {
            Time.timeScale = 6;
        }
        if (Input.GetKeyDown(KeyCode.F7))
        {
            Time.timeScale = 7;
        }
        if (Input.GetKeyDown(KeyCode.F8))
        {
            Time.timeScale = 8;
        }
        if (Input.GetKeyDown(KeyCode.F9))
        {
            Time.timeScale = 9;
        }
        if (Input.GetKeyDown(KeyCode.F10))
        {
            Time.timeScale = 10;
        }
        if (Input.GetKeyDown(KeyCode.F11))
        {
            Time.timeScale = 11;
        }
        if (Input.GetKeyDown(KeyCode.F12))
        {
            Time.timeScale = 12;
        }

        // If not GridMode, target Location is always Zero
        targetLocation = GridMode ? targetLocation : Vector3.zero;

        // The Pirouettes take priority over the other movements
        if (!isPirouetting)
        {
            if (Random.Range(0.0f, 1.0f) < perroutteChance)
            {
                pirouetteDirection = Random.Range(0, 2) == 0 ? -1 : 1;
                isPirouetting = true;
            }
            else if (Random.Range(0f, 1f) < forwardChance)
            { // 80% chance to move forward{
                transform.position += transform.forward * moveSpeed * Time.deltaTime;
            }

            if (GridMode) // Comprehensive Grid Mode
            {
                // In this mode, every N steps the ant will set a destination to the next grid space and wander from there
                if (step % 5000 == 0 && targetLocation == Vector3.zero)
                {
                    gridIndex = (gridIndex + 1) % gridLocations.Length;
                    targetLocation = gridLocations[gridIndex];
                }

                // If there is a grid target location, move towards it
                if (targetLocation != Vector3.zero){
                    Vector3 direction = targetLocation - transform.position;
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 0.1f);
                    transform.Rotate(0, Random.Range(-maxRotatePerStep, maxRotatePerStep), 0);
                    Debug.DrawLine(transform.position, targetLocation, Color.red);
                }
                
                // And if the location is reached, reset the target location so that the ant wanders around that point
                if (Vector3.Distance(transform.position, targetLocation) < 5){
                    targetLocation = Vector3.zero;
                    print("Reached Target Location");
                }
            }

            if (targetLocation == Vector3.zero){
                
                // If the turnHomeChance is less than a random number between 0 and 100, make a turn towards the nest
                if (Random.Range(0f, 100f) < turnHomeChance)
                {
                    // Make the agent rotate maxRotatePerStep degrees towards the home position
                    Vector3 direction = homePosition - transform.position;
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 0.1f);
                }
                else 
                {
                    if (Random.Range(0f, 100f) < 50 && EnforceNestDistance){
                        Vector3 direction = transform.position - homePosition;
                        Quaternion targetRotation = Quaternion.LookRotation(direction);
                        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, 0.1f);
                    }
                    else
                    {
                        // Otherwise, rotate randomly
                        transform.Rotate(0, Random.Range(-maxRotatePerStep, maxRotatePerStep), 0);
                    }
                }
            }

            // Increase the probability they turn towards their original position over time
            if (step % 100 == 0 && Vector3.Distance(transform.position, homePosition) > minNestDistance)
            {
                turnHomeChance += returnHomeIncrease;
                turnHomeChance = Mathf.Min(turnHomeChance, 100);
            }
        }
        else{
            if (pirouetteStep < pirouetteSize)
            {
                // navMeshAgent.ResetPath();
                transform.Rotate(0, (360 / pirouetteSize) * pirouetteDirection, 0);
                transform.position += transform.forward * moveSpeed * Time.deltaTime;
                pirouetteStep++;
            }
            else
            {
                isPirouetting = false;
                pirouetteStep = 0;
            }
        }

        // Run the logManager AddEntry on another thread
        if (logManager != null && logManager.enabled)
        {
            logManager.AddEntry(returns, step);
        }
        step++;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.tag == "Home" && !isPirouetting && targetLocation != homePosition)
        {
            print("Ant Reset");
            returns++;
            step = 0;
            turnHomeChance = 0;
            returnHomeIncrease *= returnHomeRatio;

            // Set the Ant to the home position's X, Z coordinates
            transform.position = new Vector3(homePosition.x, transform.position.y, homePosition.z);
            // Disable the Home's Collider for 5 seconds so it cannot be triggered again accidentally
            StartCoroutine(DisableThenEnable());
        }
    }

    IEnumerator DisableThenEnable(float time = 2f)
    {
        // Disable the collider
        homeCollider.enabled = false;
        // Wait for X seconds
        yield return new WaitForSeconds(time);
        // Enable the collider
        homeCollider.enabled = true;
    }

}
