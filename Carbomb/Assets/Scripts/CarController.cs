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
    private Coroutine fuelCoroutine;

    [Header("Other")]
    private int layerMask = 1 << 9;
    private float groundLevel = 0;

    [Header("Components")]
    [SerializeField] private List<WheelCollider> wheelsThrottle;
    [SerializeField] private List<WheelCollider> wheelsSteer;
    [SerializeField] private List<Transform> wheelsTransform;
    [SerializeField] private Rigidbody rigidBody;
    [SerializeField] private Transform centerOfMass;
    [SerializeField] private AudioSource engineForward;
    [SerializeField] private AudioSource engineReverse;
    [HideInInspector] public PlayerStats playerStats;
    [HideInInspector] public BombScript bomb;
    [SerializeField] ParticleSystem trailParticle;
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
    }


    void Update()
    {
        speed = rigidBody.velocity.magnitude;
        lastFrameVelocity = rigidBody.velocity;
       // if (grounded) transform.position = new Vector3(transform.position.x, groundLevel, transform.position.z);
        if (!stunned && active)
        {
            ReadInput();
            Nitro();
        }

        EngineSound();
        CheckForBlock();
        WheelRotation();
        ParticleEffects();
        StunDuration();
        AdjustFuelBar();


    }


    void FixedUpdate()
    {
        WheelMovement();

        //Prevent car from rotating on x and z, doesn't trigger while reflecting to avoid rigidbody jitter
        if (!reflecting) rigidBody.rotation = Quaternion.Euler(0, rigidBody.rotation.eulerAngles.y, 0);
    }

    #endregion

    #region Movement
    void ReadInput()
    {
        Vector2 input = keyMap.car.move.ReadValue<Vector2>();
        throttleValue = input.y;
        steerValue = input.x;
        if (keyMap.car.speedBurst.WasPressedThisFrame()) nitroButtonHeld = true;
        if (keyMap.car.speedBurst.WasReleasedThisFrame()) nitroButtonHeld = false;

    }

    //Apply torque to wheel colliders which changes movement
    void WheelMovement()
    {
        if (!stunned)
        {
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

    // Increase motorTorque in WheelMovement() if nitro button is held and car has fuel
    void Nitro()
    {
        if (nitroButtonHeld)
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
                    if (fuelCoroutine != null) StopCoroutine(fuelCoroutine);
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
        //Play sfx break
        nitroButtonHeld = false;
        fuelCooldown = true;
        yield return new WaitForSeconds(2f);
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
            //  if (bombedCount >= 10) StartCoroutine(Eliminated());
            //    else  StartCoroutine(RecoverFromDamage());

        }
    }

    //Become invulnerable and start blinking effect after taking damage
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
        Vector3 relativePoint = transform.InverseTransformPoint(col.gameObject.transform.position);
        //If car has bomb and collides with object with its front side, it will be reflected accordingly to collision point
        if (bomb != null && relativePoint.z > 0 && (col.gameObject.CompareTag("Wall") || col.gameObject.CompareTag("Car")) && speed > 50f) InstantReflect(col.contacts[0].normal);
        // StartCoroutine(Reflect(Quaternion.LookRotation(Vector3.Reflect(transform.forward, col.contacts[0].normal), transform.up)));


        //Change bomb holder if a car that holds bomb collided with another car that didn't have any
        if (col.gameObject.CompareTag("Car") && bomb != null && !bomb.recentTargets.Contains(transform))
        {
            CarController script = col.gameObject.GetComponent<CarController>();
            if (script.bomb == null)
            {
                StartCoroutine(afterPassCor());
                bomb.StartCoroutine(bomb.PassToPlayer(script));
            }
        }
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

    //Checks if there's an object blocking this car's front side
    void EngineSound()
    {
        if (!blocked)
        {
            bool accelDirectionIsFwd = throttleValue >= 0;
            float dot = Vector3.Dot(transform.forward, rigidBody.velocity);
            float speedDirection;
            if (Mathf.Abs(dot) > 0.1f) speedDirection = dot < 0 ? -(speed / 210f) : (speed / 140f);
            else speedDirection = 0f;

            if (speedDirection < 0.0f)
            {
                engineForward.volume = 0.0f;
                engineReverse.volume = Mathf.Lerp(0.1f, 1f, -speedDirection * 1.2f);
                engineReverse.pitch = Mathf.Lerp(0.1f, 1f, -speedDirection + (Mathf.Sin(Time.time) * .1f));
            }
            else
            {
                engineReverse.volume = 0.0f;
                engineForward.volume = Mathf.Lerp(0.1f, 1f, speedDirection * 1.2f);
                engineForward.pitch = Mathf.Lerp(0.3f, 1f, speedDirection + (Mathf.Sin(Time.time) * .1f));
            }
        }
        else
        {
            engineForward.volume = 0.0f;
            engineForward.volume = 0.0f;
        }


    }
    void ParticleEffects()
    {
        //if (speed > 40f) trailParticle.Play();
        //  else trailParticle.Stop();
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
    #endregion
}
