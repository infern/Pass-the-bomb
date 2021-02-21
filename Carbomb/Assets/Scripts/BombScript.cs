using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BombScript : MonoBehaviour
{
    [Header("Status")]
    public bool spawning = false;
   [SerializeField] private bool owned = false;
    private bool tracking = false;
    
    [Header("Explosion")]
    [Range(5f, 25f)] public float explosionTimeToExplode= 20f;
    [SerializeField] private float explosionTimer;
    private bool exploded = false;
    private bool countDownSFX;

    [Header("Components")]
    [SerializeField] GameController gameController;
    [SerializeField] GameObject auraObject;
    [SerializeField] GameObject spawnObject;
    [SerializeField] GameObject explosionObject;
    [SerializeField] GameObject splashObject;
    [SerializeField] AudioSource audioSource1;
    [SerializeField] AudioSource audioSource2;
    [SerializeField] Camera cam;
    [SerializeField] Transform splashCanvas;
    [SerializeField] Animator animator;
    private CarController trackedTarget;
    public List<Transform> recentTargets;

    [Header("Other")]
    [SerializeField] [Range(0.1f, 6f)] private float appearTime = 2f;
    [SerializeField] [Range(0.1f, 6f)] private float chargeTime = 4f;
    private Vector3 trackedPos;
    private Quaternion fixedRotation;

    
    void Awake()
    {
        fixedRotation = transform.rotation;
        explosionTimer = explosionTimeToExplode;
        
    }



    void Update()
    {
       if(!spawning && owned) Timer();
    }

    //Prevent bomb from changing rotation and local position
    void LateUpdate()
    {
        transform.rotation = fixedRotation;
        if (owned)
        {
            if (!tracking) transform.localPosition = new Vector3(0, 0, 0);
            else trackedPos = trackedTarget.transform.position;   // #1
        }


    }


    //If bomb is not attached to a car it will stay on the ground until first car enters its trigger
    void OnTriggerEnter(Collider other)
    {
        if (!spawning && !owned && other.gameObject.CompareTag("Car"))
        {
            StartCoroutine(PassToPlayer(other.gameObject.GetComponent<CarController>()));
            explosionTimer = explosionTimeToExplode;
            exploded = false;
            owned = true;
        }
    }


    //Timer for bomb explosion
    void Timer()
    {
        if (!exploded)
        {
            explosionTimer -= Time.deltaTime;
            if (!countDownSFX && explosionTimer <= 7.3f)
            {
                audioSource1.Play();
                countDownSFX = true;
                animator.SetBool("charging", true);
            }
            if (explosionTimer < 0) Explode();
        }
    }



    //Smooth bomb position change to new holder (collided target)
    public IEnumerator PassToPlayer(CarController target)
    {
        if (trackedTarget != null)
        {
            animator.Play("bombTransfer");
            audioSource2.Play();
        }
        target.bomb = this;
        tracking = true;
        if (transform.parent != null) transform.parent = null;
        Vector3 startPosition = transform.position;
        trackedTarget = target;

        float elapsedTime = 0;
        float waitTime = .15f;

        //->#1 trackedPos is updated in LateUpdate() since target can still move while Coroutine is in effect
        while (elapsedTime < waitTime)
        {
            transform.position = Vector3.Lerp(startPosition, trackedPos, (elapsedTime / waitTime));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        tracking = false;
        transform.parent = target.transform;

     

    }

    //Kill holder and everyone in small radius around him 
    void Explode()
    {
        GameObject explosion = Instantiate(explosionObject, new Vector3(transform.position.x,2f,transform.position.z), Quaternion.identity);
        explosion.transform.parent = trackedTarget.transform;
        Destroy(explosion, 5.34f);
        GameObject splash = Instantiate(splashObject, cam.WorldToScreenPoint(trackedTarget.transform.position), Quaternion.identity, splashCanvas);
        splash.transform.position = new Vector3(splash.transform.position.x+12, splash.transform.position.y, splash.transform.position.z);
        Destroy(splash, 1.52f);
        transform.parent = null;
        transform.position = new Vector3(0, -20f, 0f); //Temporarily hide bomb
        tracking = false;
        owned = false;
        exploded = true;
        countDownSFX = false;
        trackedTarget.bomb = null;
        trackedTarget.ApplyStun(2f,true);
        trackedTarget.TakeDamage();
        StartCoroutine(Spawn());
    }


    public IEnumerator Spawn()
    {
        animator.SetBool("charging", false);
        SpawnVariablesToggle();
        yield return new WaitForSeconds(appearTime);
        Vector3 temp = gameController.RandomPoint(); 
        transform.position = gameController.RandomPoint();
        yield return new WaitForSeconds(chargeTime);
        SpawnVariablesToggle();
    }

    private void SpawnVariablesToggle()
    {
        bool a = false;
        if (!spawning) a = true;
        spawning = a;
        spawnObject.SetActive(a);
        auraObject.SetActive(!a);

    }

}


