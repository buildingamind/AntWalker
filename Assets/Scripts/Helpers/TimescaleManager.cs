using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimescaleManager : MonoBehaviour
{
    [Tooltip("Timescale used in the Editor, or in a Build when no --timescale argument was passed.")]
    public int EditorTimescale = 1;

    void Start()
    {
        Time.timeScale = (Application.isEditor || ArgumentParser.Options.timescale == 0)
            ? EditorTimescale
            : ArgumentParser.Options.timescale;
    }

    // F1-F12 -> Time.timeScale 1-12, in both the Editor and a Build.
    void Update()
    {
        for (int i = 1; i <= 12; i++)
        {
            if (Input.GetKeyDown(KeyCode.F1 + (i - 1)))
            {
                Time.timeScale = i;
            }
        }
    }
}
