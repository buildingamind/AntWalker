using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlySpawner : MonoBehaviour
{
    public GameObject flyPrefab;
    public GameObject centerPoint;
    public float SpawnFrequency = 120f;
    public float DestroyAfter = 120f;
    public float SpawnRangeDecrease = 0.05f;

    public float SpawnRange = 500f;
    public float DeadzoneRange = 1f;


    // Start is called before the first frame update
    void Start()
    {
        SpawnFly();
    }

    public float RandomFloatExcludingCenter(float min, float max, float centerMin, float centerMax)
    {
        System.Random rnd = new System.Random();
        float result;
        do
        {
            result = (float)(min + (max - min) * rnd.NextDouble());
        } while (result > centerMin && result < centerMax);
        return result;
    }

    void SpawnFly()
    {
        SpawnRange = Mathf.Clamp(SpawnRange - SpawnRangeDecrease, DeadzoneRange + 1f, 1000);

        float randomX = RandomFloatExcludingCenter(-SpawnRange, SpawnRange, -DeadzoneRange, DeadzoneRange);
        float ySpawn = centerPoint.transform.position.y + 100f;
        float randomZ = RandomFloatExcludingCenter(-SpawnRange, SpawnRange, -DeadzoneRange, DeadzoneRange);
        GameObject fly = Instantiate(flyPrefab, centerPoint.transform.position + new Vector3(randomX, ySpawn, randomZ), Quaternion.Euler(0, Random.Range(0, 360), 180), this.transform);
        fly.name = "DeadFly";

        if (DestroyAfter > 0)
        {
            Destroy(fly, DestroyAfter);
        }
        Invoke(nameof(SpawnFly), SpawnFrequency);
    }
}
