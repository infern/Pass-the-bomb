﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DeveloperCheats : MonoBehaviour
{
    [SerializeField] private bool cheatsActive = false;



    void Update()
    {
        //Restars scene
        if (cheatsActive)
        {
            if (Input.GetKeyDown("f1")) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

    }
}
