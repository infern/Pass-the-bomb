using UnityEngine;
using System.Collections;

public class CameraFollow : MonoBehaviour
{

    [SerializeField] private Transform player;        
    [SerializeField] private Vector3 offset;
    [SerializeField] private Vector3 eulerRotation;
    [SerializeField] private float damper;


    void Start()
    {
        offset = transform.position - player.transform.position;
        transform.eulerAngles = eulerRotation;
    }

    void Update()
    {
        transform.position = Vector3.Lerp(transform.position, player.position + offset, damper * Time.deltaTime);
    }
}