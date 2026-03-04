using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimescaleManager : MonoBehaviour
{
    public int EditorTimescale = 1;

    // Update is called once per frame
    void Update()
    {
        if (Application.isEditor || ArgumentParser.Options.timescale == 0)
        {
            Time.timeScale = EditorTimescale;
        }
        else
        {
            Time.timeScale = ArgumentParser.Options.timescale;
        }
    }
}
