using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarController : MonoBehaviour
{
    #region Variables

    [Header("Status")]
    public bool active = false;
    public int playerNumber = 0;
    public int bombedCount = 0;
    private bool stunned = false;
    private bool reflecting = false;
    private bool recovering = false;
    private bool blocked = false;
    public bool grounded = false;
    [SerializeField] [Range(1f, 4f)] private float recoverDuration = 2f;
    [SerializeField] private float stunTimer;


    [Header("Movement")]
    [SerializeField] [Range(40f, 1000f)] private float motorTorque = 100f;
    [SerializeField] [Range(15f, 60f)] private float steerMax = 20;
    private inputMap keyMap;
    private float throttleValue;
    private float steerValue;
    [SerializeField] private float speed;
    private Vector3 lastFrameVelocity;
    [SerializeField] [Range(1.5f, 5f)] private float nitroSpeedMultiplier = 2.5f;
    [SerializeField] private bool nitroButtonHeld = false;
    [SerializeField] private bool nitroActive = false;
    [SerializeField] [Range(0.1f, 100f)] private float fuelMax = 50f;
    [SerializeField] private float fuelCurrent = 50f;
    [SerializeField] [Range(0.1f, 100f)] private float fuelDrainRatio = 22f;
    [SerializeField] [Range(0.1f, 100f)] private float fuelRechargeRatio = 10f;
    [SerializeField] private bool fuelRecharge = false;
    [SerializeField] private bool fuelCooldown = false;
    private bool soundTransition;
    private bool collisionSoundCooldown = false;
    private Coroutine fuelCoroutine;
    private Coroutine soundCoroutine;

    [Header("Other")]
    private int layerMask = 1 << 9;
    private float groundLevel = 0;
    [SerializeField] Material redSkin;
    [SerializeField] Material blueSkin;
    [SerializeField] Material yellowSkin;
    [SerializeField] Material whiteSkin;

    [Header("Components")]

    [SerializeField] private List<WheelCollider> wheelsThrottle;
    [SerializeField] private List<WheelCollider> wheelsSteer;
    [SerializeField] private List<Transform> wheelsTransform;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Rigidbody rigidBody;
    [SerializeField] private Transform centerOfMass;
    [SerializeField] private AudioSource effectsAudioSource;
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private AudioClip collisionSFX;
    [SerializeField] private AudioClip collisionSFX2;
    [SerializeField] private AudioClip nitroBreakSFX;
    [SerializeField] public PlayerStats playerStats;
    [HideInInspector] public BombScript bomb;
    [SerializeField] SpriteRenderer circleIndicator;
    [SerializeField] ParticleSystem trailParticle;
    [SerializeField] ParticleSystem nitroParticle;
    private ParticleSystem.EmissionModule trailEmission;
    private ParticleSystem.MinMaxCurve trailCurve;

    #endregion

    #region Defualt Functions

    void OnEnable()
    {
        keyMap.Enable();
    }

    void OnDisable()
    {
        keyMap.Disable();
    }

    void Awake()
    {
        keyMap = new inputMap();
        trailEmission = trailParticle.emission;
        trailCurve = trailEmission.rateOverTime;
        rigidBody.centerOfMass = centerOfMass.localPosition;
        fuelCurrent = fuelMax;
        nitroParticle.Stop();
    }


    void Update()
    {
        speed = rigidBody.velocity.magnitude;
        lastFrameVelocity = rigidBody.velocity;

        if (!stunned && active)
        {
            ReadInput();
            Nitro();
            ParticleEffects();
        }

        NitroSoundAndParticle();
        CheckForBlock();
        WheelRotation();
        StunDuration();
        AdjustFuelBar();
    }


    void FixedUpdate()
    {
        WheelMovement();
        //Prevent car from rotating on x and z, doesn't trigger while reflecting to avoid rigidbody jitter
        if (!reflecting) rigidBody.rotation = Quaternion.Euler(0, rigidBody.rotation.eulerAngles.y, 0);
        if (grounded) transform.position = new Vector3(transform.position.x, groundLevel, transform.position.z);
    }

    #endregion

    #region Movement
    void ReadInput()
    {
        TemporaryMultiPlayerControls();
        /*
        Vector2 input = keyMap.car3.move.ReadValue<Vector2>();
        if (keyMap.car3.speedBurst.WasPressedThisFrame()) nitroButtonHeld = true;
        if (keyMap.car3.speedBurst.WasReleasedThisFrame()) nitroButtonHeld = false;
         throttleValue = input.y;
         steerValue = input.x;
        */
    }

    //Add torque to wheel colliders which applies movement to a car
    void WheelMovement()
    {
        if (!stunned)
        {
            //If nitro is active, torque will have higher value 
            float bonus = !nitroActive ? (motorTorque * throttleValue) : (motorTorque * throttleValue) * nitroSpeedMultiplier;
            foreach (WheelCollider wheel in wheelsThrottle) wheel.motorTorque = bonus;
            foreach (WheelCollider wheel in wheelsSteer) wheel.steerAngle = steerMax * steerValue;
        }
    }

    //Rotate every wheel mesh accordingly to its parent wheel collider
    void WheelRotation()
    {
        int order = 0;
        foreach (Transform wheel in wheelsTransform)
        {
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            wheelsThrottle[order].GetWorldPose(out pos, out rot);
            wheel.position = pos;
            wheel.rotation = rot * Quaternion.Euler(0, 180, 0);
            order++;
        }
        order = 0;

    }

    // Increase car speed if nitro button is held and fuel is not empty
    void Nitro()
    {
        if (nitroButtonHeld && !blocked)
        {
            if (throttleValue > 0)
            {
                nitroActive = true;
                if (fuelCurrent > 0)
                {
                    fuelRecharge = false;
                    Mathf.Clamp(fuelCurrent -= fuelDrainRatio * Time.deltaTime, 0, fuelMax);
                    //  rigidBody.AddForce(transform.forward * nitroBonusSpeed * 3f, ForceMode.Impulse);
                }
                else
                {
                    StartCoroutine(FuelRechargeCooldown());
                }
            }
            else nitroActive = false;
        }
        else nitroActive = false;


        if (!nitroActive && !fuelRecharge)
        {
            fuelRecharge = true;

        }

        if (fuelRecharge && fuelCurrent < fuelMax && !fuelCooldown)
        {
            float bonus = bomb == null ? fuelRechargeRatio : fuelRechargeRatio * 1.5f;
            Mathf.Clamp(fuelCurrent += bonus * Time.deltaTime, 0, fuelMax);
        }

    }

    //If player deplated all of the fuel, there will be a penalty delay before fuel can recharge
    private IEnumerator FuelRechargeCooldown()
    {
        effectsAudioSource.clip = nitroBreakSFX;
        effectsAudioSource.Play();
        nitroButtonHeld = false;
        fuelCooldown = true;
        yield return new WaitForSeconds(1.6f);
        fuelCooldown = false;
    }


    #endregion

    #region Combat
    public void TakeDamage()
    {
        if (!recovering)
        {
            bombedCount++;
            playerStats.SetBombedCount(bombedCount);
            throttleValue = 0f;
            steerValue = 0f;
            StartCoroutine(RecoverFromDamage());
        }
    }

    //Become invulnerable after taking damage
    private IEnumerator RecoverFromDamage()
    {
        recovering = true;
        yield return new WaitForSeconds(recoverDuration);
        recovering = false;
    }

    private IEnumerator Eliminated()
    {
        Debug.Log(gameObject.name + " eliminated!");
        yield return new WaitForSeconds(3f);
    }

    void OnCollisionEnter(Collision col)
    {
        //Checks if car collides with its front side
        Vector3 relativePoint = transform.InverseTransformPoint(col.gameObject.transform.position);
        if (relativePoint.z > 0)
        {
            if (!collisionSoundCooldown)
            {
                int a = Random.Range(0, 2);
                if (a == 0) effectsAudioSource.clip = collisionSFX;
                else effectsAudioSource.clip = collisionSFX2;
                effectsAudioSource.Play();
                StartCoroutine(CollisionSoundCooldown());
            }

            //If car has bomb and collides, it will be reflected accordingly to collision point
            if (bomb != null && (col.gameObject.CompareTag("Wall") || col.gameObject.CompareTag("Car")) && speed > 50f) InstantReflect(col.contacts[0].normal);

        }

        // StartCoroutine(Reflect(Quaternion.LookRotation(Vector3.Reflect(transform.forward, col.contacts[0].normal), transform.up)));


        //Change bomb holder if a car that holds bomb collided with another car that didn't have any
        if (col.gameObject.CompareTag("Car") && bomb != null && !bomb.recentTargets.Contains(transform))
        {
            CarController script = col.gameObject.GetComponent<CarController>();
            if (script.bomb == null)
            {
                StartCoroutine(afterPassCor());
                bomb.StartCoroutine(bomb.PassToPlayer(script, 0f, false));
            }
        }
    }

    //Prevents collision sound from playing multiple times within short period of time
    private IEnumerator CollisionSoundCooldown()
    {
        collisionSoundCooldown = true;
        yield return new WaitForSeconds(0.4f);
        collisionSoundCooldown = false;
    }


    //Prevents previous bomb holder from getting bomb back for a short period
    private IEnumerator afterPassCor()
    {
        bomb.recentTargets.Add(transform);
        yield return new WaitForSeconds(0.4f);
        bomb.recentTargets.Remove(transform);
        bomb = null;
    }



    //Prevent car from moving, if it was already stunned, apply new stun if its duration is longer
    public void ApplyStun(float StunDuration, bool stopMovement)
    {
        if (stopMovement)
        {
            rigidBody.velocity = Vector3.zero;
            foreach (WheelCollider wheel in wheelsThrottle)
            {
                wheel.motorTorque = 0f;
                wheel.brakeTorque = 9000f;
            }
        }

        stunned = true;
        nitroParticle.Stop();
        if (StunDuration > stunTimer) stunTimer = StunDuration;
    }

    private void StunDuration()
    {
        if (stunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer < 0)
            {
                foreach (WheelCollider wheel in wheelsThrottle) wheel.brakeTorque = 0f;
                stunned = false;
            }
        }

    }

    //Gradually changes car's rotation
    private IEnumerator Reflect(Quaternion rotation)
    {
        float startRotation = rigidBody.rotation.eulerAngles.y;
        float endRotation = rotation.eulerAngles.y;
        float nextStep;

        float elapsedTime = 0;
        float waitTime = Mathf.Abs((endRotation - startRotation) / 360) * 0.65f;
        reflecting = true;
        while (elapsedTime < waitTime)
        {
            nextStep = Mathf.Lerp(startRotation, endRotation, (elapsedTime / waitTime));
            transform.rotation = Quaternion.Euler(0, nextStep, 0);

            elapsedTime += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }
        reflecting = false;
    }

    //Instantly changes car's rotation and adds speed
    private void InstantReflect(Vector3 collisionPoint)
    {
        transform.rotation = Quaternion.LookRotation(Vector3.Reflect(transform.forward, collisionPoint), transform.up);
        Vector3 direction = Vector3.Reflect(lastFrameVelocity.normalized, collisionPoint);
        rigidBody.velocity = direction * Mathf.Max(speed / 3, 2f);
    }

    #endregion

    #region Effects

    //Sound effect that plays when nitro is active, its volume and pitch are changed based on current speed
    private void NitroSoundAndParticle()
    {

        if (nitroActive)
        {
            if (soundCoroutine != null) StopCoroutine(soundCoroutine);
            nitroParticle.Play();
            soundTransition = true;
            engineAudioSource.volume = Mathf.Lerp(0.1f, 1f, speed / 30f * 1.2f);
            engineAudioSource.pitch = Mathf.Lerp(0.3f, 1f, speed / 30f + (Mathf.Sin(Time.time) * .1f));

        }
        else if (soundCoroutine == null && soundTransition) StartCoroutine(SmoothSoundTransition());

        else nitroParticle.Stop();
    }

    //Gradually change nitro volume to 0 (this way sound doesn't go from x to 0 instantly)
    private IEnumerator SmoothSoundTransition()
    {
        nitroParticle.Stop();
        float startVolume = engineAudioSource.volume;
        float elapsedTime = 0;
        float waitTime = 0.55f;

        while (elapsedTime < waitTime)
        {
            engineAudioSource.volume = Mathf.Lerp(startVolume, 0f, (elapsedTime / waitTime));
            engineAudioSource.pitch = Mathf.Lerp(0.3f, 1f, speed + (Mathf.Sin(Time.time) * .1f));
            elapsedTime += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }
        soundTransition = false;
    }

    //Trail left behind when car moves around
    void ParticleEffects()
    {
        trailCurve.constant = (int)(speed * 1.5f);
        trailEmission.rateOverTime = trailCurve;
    }

    #endregion

    #region Other

    //Sets player name in the UI stats
    public void SetStartingValues()
    {
        playerStats.SetStartingValues(gameObject.name);
    }

    public void ChangeCarColor(int a)
    {
        if (a == 1) meshRenderer.material = redSkin;
        else if (a == 2) meshRenderer.material = blueSkin;
        else if (a == 3) meshRenderer.material = yellowSkin;
        else if (a == 4) meshRenderer.material = whiteSkin;

    }

    //Checks if there's wall or another car blocking this object
    private void CheckForBlock()
    {
        Vector3 rayPosition = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
        if (Physics.Raycast(rayPosition, transform.TransformDirection(Vector3.forward), 2f, layerMask)) blocked = true;
        else blocked = false;
    }


    //Calculates float value that will be used to adjust currenct status of fuel bar in UI
    private void AdjustFuelBar()
    {
        float a = ((fuelCurrent * 100) / fuelMax) * 0.01f;
        playerStats.AdjustBar(a);
    }

    //Game controller uses this method after car is spawned and lands on the ground to prevent wheel colliders from not working
    public void FreezePositionY()
    {
        rigidBody.constraints = RigidbodyConstraints.FreezePositionY;
        groundLevel = transform.position.y;
        grounded = true;
    }

    //Temporary method that reads input based on player number
    private void TemporaryMultiPlayerControls()
    {
        Vector2 input = Vector2.zero;
        if (playerNumber == 1)
        {
            input = keyMap.car1.move.ReadValue<Vector2>();
            if (keyMap.car1.speedBurst.WasPressedThisFrame()) nitroButtonHeld = true;
            if (keyMap.car1.speedBurst.WasReleasedThisFrame()) nitroButtonHeld = false;
        }
        else if (playerNumber == 2)
        {
            input = keyMap.car2.move.ReadValue<Vector2>();
            if (keyMap.car2.speedBurst.WasPressedThisFrame()) nitroButtonHeld = true;
            if (keyMap.car2.speedBurst.WasReleasedThisFrame()) nitroButtonHeld = false;
        }
        else if (playerNumber == 3)
        {
            input = keyMap.car3.move.ReadValue<Vector2>();
            if (keyMap.car3.speedBurst.WasPressedThisFrame()) nitroButtonHeld = true;
            if (keyMap.car3.speedBurst.WasReleasedThisFrame()) nitroButtonHeld = false;
        }
        else if (playerNumber == 4)
        {
            input = keyMap.car1.move.ReadValue<Vector2>();
            if (keyMap.car1.speedBurst.WasPressedThisFrame()) nitroButtonHeld = true;
            if (keyMap.car1.speedBurst.WasReleasedThisFrame()) nitroButtonHeld = false;
        }

        throttleValue = input.y;
        steerValue = input.x;
    }
    #endregion
}
