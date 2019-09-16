using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ARDamage : MonoBehaviour
{
    public int DamageAmount = 5;
    public float TargetDistance;
    public float AllowedRange = 15;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetButtonDown("Fire1"))
        {
            RaycastHit shot;

            if(Physics.Raycast(transform.position, transform.TransformDirection(Vector3.forward), out shot))
            {
                TargetDistance = shot.distance;

                if(TargetDistance < AllowedRange)
                {
                    shot.transform.SendMessage("DeductPoints", DamageAmount, SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }
}
