using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class Podium : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI playerName;
    [SerializeField] TextMeshProUGUI bombedCount;




    public void SetValues(string name, int score)
    {
        playerName.text = name + ":";
        bombedCount.text = score.ToString();

    }
}

