using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class PlayerStats : MonoBehaviour
{

    [SerializeField] Transform greyBar;
    [SerializeField] Transform yellowBar;
    [SerializeField] TextMeshProUGUI playerNameTMP;
    [SerializeField] TextMeshProUGUI bombedTMP;


    public void SetStartingValues(string playerName)
    {
        this.name = playerName + " UI";
        playerNameTMP.text = playerName;
        bombedTMP.text = "0";
    }

    //Changes scale of fuel bar when its being drained/refilled
    public void AdjustBar(float fillAmount)
    {
        fillAmount = Mathf.Clamp01(fillAmount);
        var newScale = yellowBar.localScale;
        newScale.x = greyBar.localScale.x * fillAmount;
        yellowBar.transform.localScale = newScale;
    }

    public void SetBombedCount(int count)
    {
        bombedTMP.text = count.ToString();
    }





}