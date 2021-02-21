using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameController : MonoBehaviour
{

    [Header("Status")]
    [SerializeField] [Range(2, 4)] private int playerCount = 0;
    public bool testMode = false;
    private bool gameActive = false;

    [Header("Main objects")]
    [SerializeField] BoxCollider floor;
    [SerializeField] GameObject bombObject;
    [SerializeField] BombScript bombScript;
    [SerializeField] GameObject carPrefab;
    [SerializeField] Transform carFolder;
    private List<CarController> cars;


    [Header("UI")]
    [SerializeField] GameObject countDownObject;
    [SerializeField] TextMeshProUGUI coundDownTMP;
    [SerializeField] GameObject[] playerStatsUI;
    [SerializeField] PlayerStats[] playerStatsScript;





    void Start()
    {
        if (!testMode)
        {
            AssignObjectsToPlayers();
            StartCoroutine(CountDownToStart());
        }


    }

    // Update is called once per frame
    void Update()
    {
        
    }


    private IEnumerator CountDownToStart()
    {
        coundDownTMP.text = "   3...";
        yield return new WaitForSeconds(0.68f);
        coundDownTMP.text = "   2...";
        yield return new WaitForSeconds(0.68f);
        coundDownTMP.text = "   1...";
        yield return new WaitForSeconds(0.68f);
        coundDownTMP.text = "Go!";
        bombScript.StartCoroutine(bombScript.Spawn()); 
        gameActive = true;
        foreach (CarController car in cars) car.active = true;
        yield return new WaitForSeconds(0.68f);
        countDownObject.SetActive(false);
        foreach (CarController car in cars)
        {
            car.active = true;
            car.FreezePositionY();

        }
    }


    public Vector3 RandomPoint()
    {
        return new Vector3((Random.Range(floor.bounds.min.x, floor.bounds.max.x) / 1.5f), 0.14f, (Random.Range(floor.bounds.min.z, floor.bounds.max.z) / 1.5f));
    }

    private void AssignObjectsToPlayers()
    {
        cars = new List<CarController>();
        if (playerCount == 2)
        {
            CreateCar(new Vector3(-35, 0.3f, -11f), Quaternion.Euler(0, 90f, 0), 1, playerStatsScript[1]);
            CreateCar(new Vector3(35, 2f, -11f), Quaternion.Euler(0, -90f, 0), 2, playerStatsScript[2]);
            playerStatsUI[0].SetActive(false);
            playerStatsUI[3].SetActive(false);
        }

        else if (playerCount == 3)
        {
            CreateCar(new Vector3(-35, 0.3f, 9f), Quaternion.Euler(0, 140f, 0), 1, playerStatsScript[0]);
            CreateCar(new Vector3(35, 0.3f, 9f), Quaternion.Euler(0, -140f, 0), 2, playerStatsScript[1]);
            CreateCar(new Vector3(0, 0.3f, -33f), Quaternion.Euler(0, 0, 0), 3, playerStatsScript[2]);
            playerStatsUI[3].SetActive(false);
        }

        else if (playerCount == 4)
        {
            CreateCar(new Vector3(-35, 0.3f, 9f), Quaternion.Euler(0, 140f, 0), 1, playerStatsScript[0]);
            CreateCar(new Vector3(35, 0.32f, 9f), Quaternion.Euler(0, -140f, 0), 2, playerStatsScript[1]);
            CreateCar(new Vector3(-35, 0.3f, -33f), Quaternion.Euler(0, -320f, 0), 3, playerStatsScript[2]);
            CreateCar(new Vector3(35, 0.3f, -33f), Quaternion.Euler(0, 320f, 0), 4, playerStatsScript[3]);
        }
    }

    private void CreateCar(Vector3 point, Quaternion rotation, int number, PlayerStats playerStats)
    {
        GameObject spawnedCar = Instantiate(carPrefab, point, rotation, carFolder);
        spawnedCar.name = "Player " + number;
        CarController script = spawnedCar.GetComponent<CarController>();
        script.playerNumber = number;
        cars.Add(script);
        script.playerStats = playerStats;
        script.SetStartingValues();

    }

}
