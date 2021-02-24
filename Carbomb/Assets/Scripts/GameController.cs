using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class GameController : MonoBehaviour
{
    #region Variables

    [Header("Status")]
    [SerializeField] [Range(2, 4)] private int playerCount = 0;
    [SerializeField] [Range(1f, 200f)] private float timeLeft = 100f;
    public bool testMode = false;
    public bool gameActive = false;
    private int bestScore = 0;

    [Header("Main objects")]
    [SerializeField] BoxCollider floor;
    [SerializeField] GameObject bombObject;
    [SerializeField] BombScript bombScript;
    [SerializeField] GameObject carPrefab;
    [SerializeField] Transform carFolder;
    private List<CarController> cars;
    private List<CarController> bombTargets;


    [Header("UI")]
    [SerializeField] GameObject countDownObject;
    [SerializeField] TextMeshProUGUI coundDownTMP;
    [SerializeField] Animator countDownAnimator;
    [SerializeField] GameObject[] playerStatsUI;
    [SerializeField] PlayerStats[] playerStatsScript;
    [SerializeField] TextMeshProUGUI time;
    [SerializeField] GameObject endGamePanel;
    [SerializeField] Podium[] podiums = new Podium[4];


    [Header("Sounds")]
    [SerializeField] AudioSource effectsAudioSource;
    [SerializeField] AudioSource soundTrackAudioSource;
    [SerializeField] AudioClip tickSFX;
    [SerializeField] AudioClip startSFX;
    [SerializeField] AudioClip endSFX;

    #endregion

    #region Defualt Functions
    void Start()
    {
        if (!testMode)
        {
            AssignObjectsToPlayers();
            StartCoroutine(CountDownToStart());
        }
        time.text = string.Format("{0}:{1:00}", (int)timeLeft / 60, (int)timeLeft % 60);

    }

    void Update()
    {
        TimeLeftCountDown();
    }

    #endregion

    #region Game
    //Game start, cars are activated after countdown
    private IEnumerator CountDownToStart()
    {
        effectsAudioSource.clip = tickSFX;
        effectsAudioSource.Play();
        countDownAnimator.Play("countDownSize");
        coundDownTMP.text = "   3...";
        yield return new WaitForSeconds(0.78f);
        countDownAnimator.Play("countDownSize");
        effectsAudioSource.Play();
        coundDownTMP.text = "   2...";
        yield return new WaitForSeconds(0.78f);
        countDownAnimator.Play("countDownSize");
        effectsAudioSource.Play();
        coundDownTMP.text = "   1...";
        yield return new WaitForSeconds(0.78f);
        countDownAnimator.Play("countDownSize");
        effectsAudioSource.clip = startSFX;
        effectsAudioSource.Play();
        soundTrackAudioSource.Play();
        coundDownTMP.text = "Go!";
        gameActive = true;
        foreach (CarController car in cars)
        {
            car.active = true;
            car.FreezePositionY();
        }
        yield return new WaitForSeconds(0.68f);
        countDownObject.SetActive(false);
        bombScript.StartCoroutine(bombScript.PassToPlayer(RandomPlayer(), 1f, true));
    }


    //Timer before game finishes
    private void TimeLeftCountDown()
    {
        if (gameActive)
        {
            timeLeft -= Time.deltaTime;
            time.text = string.Format("{0}:{1:00}", (int)timeLeft / 60, (int)timeLeft % 60);
            if (timeLeft <= 0) GameOver();
        }
    }


    private void GameOver()
    {
        Destroy(bombObject);
        endGamePanel.SetActive(true);
        SortCarsByScore();
        //Activates x podium slots, where x is number of players
        for (int i = 0; i < cars.Count; i++)
        {
            podiums[i].gameObject.SetActive(true);
            podiums[i].SetValues(cars[i].gameObject.name, cars[i].bombedCount);
        }
        foreach (CarController car in cars)
        {
            car.active = false;
            car.ApplyStun(99f, true);
        }
        effectsAudioSource.clip = endSFX;
        effectsAudioSource.Play();
        soundTrackAudioSource.Stop();
        gameActive = false;
        time.text = "0:00";
    }

    //Random point within playable section of the map
    public Vector3 RandomPoint()
    {
        return new Vector3((Random.Range(floor.bounds.min.x, floor.bounds.max.x) / 1.5f), 0.14f, (Random.Range(floor.bounds.min.z, floor.bounds.max.z) / 1.5f));
    }

    #endregion

    #region Player
    //Creates cars, changes their colors, adjusts spawn locations and activates UI
    private void AssignObjectsToPlayers()
    {
        cars = new List<CarController>();
        bombTargets = new List<CarController>();
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
        script.ChangeCarColor(number);
        cars.Add(script);
        script.playerStats = playerStats;
        script.SetStartingValues();

    }


    public CarController RandomPlayer()
    {
        int index = Random.Range(0, cars.Count);
        CarController target = cars[index];
        return target;
    }


    //Returns player with highest score, if there are multiple players with same score, target will be randomed between them
    public CarController HighestScoreBombTargets()
    {
        int sameScore = 0;
        foreach (CarController car in cars)
        {
            if (car.bombedCount <= bestScore) bestScore = car.bombedCount;
            else sameScore++;
        }
        if (sameScore == cars.Count) bestScore = cars[0].bombedCount;
        foreach (CarController car in cars) if (car.bombedCount == bestScore) bombTargets.Add(car);

        CarController target = bombTargets[Random.Range(0, bombTargets.Count)];
        bombTargets.Clear();
        return target;
    }

    private void SortCarsByScore()
    {
        cars = cars.OrderBy(x => x.GetComponent<CarController>().bombedCount).ToList();
    }
    #endregion

}
